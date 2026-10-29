namespace SimplePLC.Infrastructure.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

public class RuleTableWriterModbusSlaveFailSafeTests
{
    private static RuleRecordDto CreateTestRule()
    {
        return new RuleRecordDto
        {
            ThresholdLo = 10,
            ThresholdHi = 100,
            ForMs = 0,
            ActionParam = 1,
            TriggerTag = 0,
            ActionTag = 8,
            GuardTag = 0xFFFF,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.GT,
            ActionType = SPLC_ActionType.SET_TAG
        };
    }

    [Fact]
    public async Task DeployRulesAsync_WithSlavePort_AndValidCapability_DeploysSuccessfullyAndWrites0x1400And0x1500()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Simulator mặc định đã có SPLC_CAP_MODBUS_SLAVE (0x4000)
        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = 5 }
        };

        var slaveMapConfigs = new List<ModbusSlaveMapEntryDto>
        {
            new()
            {
                ModbusAddress = 0,
                TargetTagIndex = 0, // DI0
                AccessRight = ModbusSlaveAccessRight.ReadOnly,
                DataType = 1,
                PortMask = 0x02,
                Description = "E-Stop"
            },
            new()
            {
                ModbusAddress = 10,
                TargetTagIndex = 52, // VREG0
                AccessRight = ModbusSlaveAccessRight.ReadWrite,
                DataType = 2,
                PortMask = 0x02,
                Description = "Setpoint"
            }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: portConfigs,
            slaveMapConfigs: slaveMapConfigs,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}");
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);

        // Kiểm tra log xác thực ghi 0x1400 và đảm bảo ZERO-LEGACY: Không nạp bảng 0x1500
        Assert.Contains(logs, l => l.Contains("[RS-485 Ports] Writing 64 registers to 0x1400"));
        Assert.DoesNotContain(logs, l => l.Contains("0x1500"));

        // Kiểm tra dữ liệu được ghi vào simulator memory tại 0x1400
        ushort port1Control = client.Simulator.Memory.InternalRead(ModbusRegisterMap.ModbusPortConfigBaseAddress);
        Assert.Equal((ushort)0x0101, port1Control); // Port 1: Enabled (0x0100) | Port 1 (1)

        ushort port2Control = client.Simulator.Memory.InternalRead((ushort)(ModbusRegisterMap.ModbusPortConfigBaseAddress + 16));
        Assert.True((port2Control & ModbusRegisterMap.ModbusPortControlBitRoleSlave) != 0, "Port 2 must have ROLE_SLAVE bit in control word");

        ushort port2Role = client.Simulator.Memory.InternalRead((ushort)(ModbusRegisterMap.ModbusPortConfigBaseAddress + 16 + 14));
        Assert.Equal((ushort)ModbusPortRole.Slave, port2Role);

        ushort port2SlaveId = client.Simulator.Memory.InternalRead((ushort)(ModbusRegisterMap.ModbusPortConfigBaseAddress + 16 + 15));
        Assert.Equal((ushort)5, port2SlaveId);
    }

    [Fact]
    public async Task DeployRulesAsync_WithSlavePort_MissingSlaveCapability_AbortsImmediatelyWithUnsupported()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Giả lập MCU chỉ hỗ trợ BASE_LOGIC + MODBUS_MASTER (thiếu SPLC_CAP_MODBUS_SLAVE 0x4000)
        client.Simulator.SetHardwareCapabilities(
            SPLC_HardwareCapabilities.BASE_LOGIC |
            SPLC_HardwareCapabilities.RETENTIVE |
            SPLC_HardwareCapabilities.MODBUS_MASTER);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = 2 }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: portConfigs,
            slaveMapConfigs: null,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Fail Fast
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("missing SPLC_CAP_MODBUS_SLAVE at 0x0A08", result.ErrorMessage);
        Assert.Contains("Deployment aborted for safety", result.ErrorMessage);

        // Kiểm tra Staging không được commit
        Assert.Equal(0, client.Simulator.Memory.InternalRead(ModbusRegisterMap.ActiveRuleVersionAddress));
    }

    [Fact]
    public async Task DeployRulesAsync_WithoutSlavePort_MissingSlaveCapability_SucceedsNormally()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Giả lập MCU không có SPLC_CAP_MODBUS_SLAVE
        client.Simulator.SetHardwareCapabilities(
            SPLC_HardwareCapabilities.BASE_LOGIC |
            SPLC_HardwareCapabilities.RETENTIVE |
            SPLC_HardwareCapabilities.MODBUS_MASTER);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        // Chỉ có cổng Master, không có cổng Slave nào được bật
        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: portConfigs,
            slaveMapConfigs: null,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Backward compatible: nạp thành công bình thường
        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}");
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
    }

    [Fact]
    public async Task DeployRulesAsync_WithSlavePort_CapabilitiesTimeout_AbortsImmediatelyWithUnsupported()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Cấy lỗi: Timeout khi đọc địa chỉ 0x0A08
        client.Simulator.Control.Faults.TimeoutOnAddress = ModbusRegisterMap.HardwareCapabilitiesAddress;

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = 3 }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: portConfigs,
            slaveMapConfigs: null,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Fail Fast on communication error
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("0x0A08", result.ErrorMessage);
        Assert.Contains("Modbus deployment aborted for safety", result.ErrorMessage);
    }

    [Fact]
    public async Task ReadActiveModbusSlaveMapConfigsAsync_ReturnsEmpty_WithoutAccessing0x1500()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Cấy lỗi: Ném exception nếu có bất kỳ frame đọc nào chạm vào vùng 0x1500
        client.Simulator.Control.Faults.TimeoutOnAddress = ModbusRegisterMap.ModbusSlaveMapBaseAddress;

        var reader = new RuleTableReader(client);

        // Act
        var slaveMaps = await reader.ReadActiveModbusSlaveMapConfigsAsync(1);

        // Assert - Zero Legacy: không truy cập 0x1500, trả về mảng rỗng tức thì
        Assert.NotNull(slaveMaps);
        Assert.Empty(slaveMaps);
    }
}
