using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Kiểm thử độc lập cho tầng Hạ Tầng (Infrastructure): Đọc thanh ghi phần cứng 0x0A0A và kiểm tra an toàn triển khai.
/// </summary>
public sealed class HardwareAwareRs485InfrastructureTests
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
    public async Task DeviceDescriptorReader_Reads0x0A0A_ReturnsHardwareCount()
    {
        // TC-HW-MB-004: Đọc thành công số lượng cổng từ 0x0A0A
        var client = new FakeModbusClient();
        await client.ConnectAsync("SIMULATOR");

        // Giả lập MCU chỉ có 1 cổng RS-485 vật lý
        client.Simulator.SetPhysicalRs485PortCount(1);

        var reader = new DeviceDescriptorReader(client);
        ushort count = await reader.ReadPhysicalRs485PortCountAsync(1, hasModbusMasterCap: true);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeviceDescriptorReader_When0x0A0ATimeout_FallbackToCapabilities()
    {
        // TC-HW-MB-005: Giả lập lỗi timeout trên 0x0A0A (Firmware cũ), fallback về 2 cổng
        var client = new FakeModbusClient();
        await client.ConnectAsync("SIMULATOR");

        client.Simulator.Control.Faults.TimeoutOnAddress = ModbusRegisterMap.Rs485PhysicalPortCountAddress;

        var reader = new DeviceDescriptorReader(client);
        ushort count = await reader.ReadPhysicalRs485PortCountAsync(1, hasModbusMasterCap: true);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task DeviceDescriptorReader_WhenNoModbusMasterCap_ReturnsZeroWithoutReading()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("SIMULATOR");

        var reader = new DeviceDescriptorReader(client);
        ushort count = await reader.ReadPhysicalRs485PortCountAsync(1, hasModbusMasterCap: false);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RuleTableWriter_FailsDeploy_WhenTargetPortExceedsPhysicalCount()
    {
        // TC-HW-MB-006: MCU chỉ có 1 cổng RS-485 (PortIndex 0), cố nạp khối Modbus Read trỏ vào Cổng 2 (PortIndex 1) -> Bị chặn
        var client = new FakeModbusClient();
        await client.ConnectAsync("SIMULATOR");

        client.Simulator.SetPhysicalRs485PortCount(1);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var invalidMbReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                BlockIndex = 0,
                PortIndex = 1, // Vượt quá cổng vật lý (chỉ có PortIndex 0)
                SlaveId = 1,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                RegCount = 2,
                Enabled = true
            }
        };

        var result = await writer.DeployRulesAsync(
            1,
            rules,
            null,
            null,
            null,
            null,
            null,
            invalidMbReads,
            System.Threading.CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("exceeding device's physical RS-485 port count", result.ErrorMessage);
    }

    [Fact]
    public async Task RuleTableWriter_SucceedsDeploy_WhenTargetPortWithinPhysicalCount()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("SIMULATOR");

        client.Simulator.SetPhysicalRs485PortCount(2);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var validMbReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                BlockIndex = 0,
                PortIndex = 1, // Hợp lệ vì MCU có 2 cổng (PortIndex 0 và 1)
                SlaveId = 1,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                RegCount = 2,
                Enabled = true
            }
        };

        var result = await writer.DeployRulesAsync(
            1,
            rules,
            null,
            null,
            null,
            null,
            null,
            validMbReads,
            System.Threading.CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
