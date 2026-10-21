using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Bộ kiểm thử độc lập cho quy trình nạp cấu hình Đa cổng RS-485 Modbus Master (Component 4).
/// Kiểm chứng ghi các thanh ghi 0x1400..0x143F xuống MCU Simulator.
/// </summary>
public sealed class RuleTableWriterModbusPortTests
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
    public async Task DeployRulesAsync_WithModbusPorts_WritesTo0x1400Successfully()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "PORT1", BaudRate = 115200, Parity = ModbusParity.Even, StopBits = 1, Enabled = true },
            new() { PortIndex = 1, PortName = "PORT2", BaudRate = 19200, Parity = ModbusParity.None, StopBits = 2, Enabled = true },
            new() { PortIndex = 2, PortName = "PORT3", BaudRate = 9600, Parity = ModbusParity.Odd, StopBits = 1, Enabled = false },
            new() { PortIndex = 3, PortName = "PORT4", BaudRate = 57600, Parity = ModbusParity.None, StopBits = 1, Enabled = false }
        };

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
            cancellationToken: default,
            progressLogger: logs.Add);

        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}");
        Assert.Contains(logs, l => l.Contains("[RS-485 Ports] Writing 64 registers to 0x1400"));

        // Đọc lại từ simulator memory và giải mã
        var readBackRegs = client.Simulator.Memory.ReadRegisters(
            ModbusRegisterMap.ModbusPortConfigBaseAddress,
            ModbusRegisterMap.ModbusPortConfigTableTotalLength);

        var decodedPorts = ModbusPortConfigCodec.DecodeAllPorts(readBackRegs);
        Assert.Equal(4, decodedPorts.Count);

        // Port 1
        Assert.True(decodedPorts[0].Enabled);
        Assert.Equal(115200u, decodedPorts[0].BaudRate);
        Assert.Equal(ModbusParity.Even, decodedPorts[0].Parity);
        Assert.Equal((byte)1, decodedPorts[0].StopBits);
        Assert.Equal("PORT1", decodedPorts[0].PortName);

        // Port 2
        Assert.True(decodedPorts[1].Enabled);
        Assert.Equal(19200u, decodedPorts[1].BaudRate);
        Assert.Equal(ModbusParity.None, decodedPorts[1].Parity);
        Assert.Equal((byte)2, decodedPorts[1].StopBits);
        Assert.Equal("PORT2", decodedPorts[1].PortName);

        // Port 3
        Assert.False(decodedPorts[2].Enabled);
        Assert.Equal(ModbusParity.Odd, decodedPorts[2].Parity);
    }
}
