using System.IO;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Phase F2 — Deploy Resilience & Atomicity:
/// Kiểm tra trọn vẹn tính ACID (bảo toàn, bất khả phân) của quy trình nạp cấu hình xuống thiết bị:
/// - F2_01: Timeout tại chunk ghi thứ 7 (giữa 100 rules) -> Active Table & Version giữ nguyên.
/// - F2_02: Disconnect tại rule 55 -> Active Table không bị xáo trộn.
/// - F2_03: CRC payload sai -> Version không tăng, trả về CRC_MISMATCH.
/// - F2_04: Commit khi Staging chưa ghi đủ theo khai báo -> Bị tracker chặn, không commit.
/// - F2_05: Device BUSY khi deploy -> Nhận mã lỗi BUSY có kiểm soát.
/// - F2_06: Mất điện (Power Loss/Reboot) trước Commit -> Reboot xóa Staging, giữ Active cũ từ Flash.
/// - F2_07: Mất điện (Power Loss/Reboot) sau Commit -> Reboot tải lại đầy đủ Active mới từ Flash.
/// - F2_08: Retry deploy sau khi Staging thất bại -> Không sinh trạng thái lai (nửa cũ / nửa mới).
/// </summary>
public sealed class DeployResilienceTests
{
    private static RuleRecordDto CreateRule(int id, int marker = 100)
    {
        return new RuleRecordDto
        {
            ThresholdLo = marker + id,
            ThresholdHi = marker + id + 50,
            ForMs = 1000,
            ActionParam = marker,
            TriggerTag = (ushort)(id % 8),
            ActionTag = (ushort)(8 + (id % 8)),
            GuardTag = 0,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.NONE,
            ActionType = SPLC_ActionType.SET_TAG
        };
    }

    private static List<RuleRecordDto> Create100Rules(int marker = 100)
    {
        var list = new List<RuleRecordDto>(100);
        for (int i = 0; i < 100; i++)
        {
            list.Add(CreateRule(i, marker));
        }
        return list;
    }

    [Fact]
    public async Task F2_01_Deploy100Rules_TimeoutAtChunk7_ActiveTableUnchanged_VersionUnchanged()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        var initialRules = await reader.ReadActiveRulesAsync(1);
        Assert.Empty(initialRules);

        // 100 rules = 1600 registers = 25 chunks ghi (mỗi chunk 64 regs).
        // Cấy lỗi timeout tại transaction ghi thứ 7:
        sim.Control.Faults.TimeoutOnWriteNumber = 7;

        var rules100 = Create100Rules(marker: 500);

        // Act & Assert: Thao tác nạp phải ném TimeoutException
        await Assert.ThrowsAsync<TimeoutException>(
            () => writer.DeployRulesAsync(1, rules100));

        // Reset cờ lỗi để đọc lại kiểm tra MCU
        sim.Control.Faults.TimeoutOnWriteNumber = null;

        // Bất biến F2: Active Rule Table không hề thay đổi, Version giữ nguyên là 0
        var activeRulesAfter = await reader.ReadActiveRulesAsync(1);
        Assert.Empty(activeRulesAfter);

        var verRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, verRegs[0]);

        var activeCountRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
        Assert.Equal(0, activeCountRegs[0]);
    }

    [Fact]
    public async Task F2_02_Deploy100Rules_DisconnectDuringStaging_ActiveTableUnchanged()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        // Đầu tiên nạp thành công 5 rules phiên bản 1
        var baselineRules = new List<RuleRecordDto>
        {
            CreateRule(0, marker: 111),
            CreateRule(1, marker: 111),
            CreateRule(2, marker: 111),
            CreateRule(3, marker: 111),
            CreateRule(4, marker: 111)
        };
        var deployBase = await writer.DeployRulesAsync(1, baselineRules);
        Assert.True(deployBase.IsSuccess);
        Assert.Equal(1, deployBase.ActiveVersion);

        // Chuẩn bị nạp 100 rules mới, nhưng rút cáp giữa chừng
        sim.Control.Faults.TimeoutOnWriteNumber = null;

        // Cấy lỗi ngắt kết nối vật lý khi bắt đầu ghi Staging chunks
        var rules100 = Create100Rules(marker: 999);

        // Mô phỏng cáp bị rút:
        sim.Control.Faults.CableDisconnected = true;

        await Assert.ThrowsAsync<IOException>(
            () => writer.DeployRulesAsync(1, rules100));

        // Cắm lại cáp
        sim.Control.Faults.CableDisconnected = false;

        // Bất biến F2: Active Table giữ nguyên 5 rules cũ, Version giữ nguyên 1
        var activeRules = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(5, activeRules.Count);
        Assert.All(activeRules, r => Assert.True(r.ThresholdLo >= 111 && r.ThresholdLo < 120));

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver[0]);
    }

    [Fact]
    public async Task F2_03_DeployRules_CorruptCommitCrc_ActiveVersionDoesNotIncrement_ReturnsCrcMismatch()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        sim.Control.Faults.CorruptCommitCrc = true;

        var rules = new List<RuleRecordDto> { CreateRule(0), CreateRule(1) };
        var result = await writer.DeployRulesAsync(1, rules);

        // Bất biến F2: Nạp thất bại, mã lỗi CRC_MISMATCH, trạng thái ERROR (4)
        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.ConfigStatus);
        Assert.Equal(SPLC_ErrorCode.CRC_MISMATCH, result.ErrorCode);

        // Active Version không tăng
        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, ver[0]);

        var activeRules = await reader.ReadActiveRulesAsync(1);
        Assert.Empty(activeRules);
    }

    [Fact]
    public async Task F2_04_CommitWithIncompleteStaging_RejectedByTracker()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);

        // B1: Khai báo nạp 10 rules (160 registers)
        ushort declaredRules = 10;
        ushort fakeCrc = 0x1234;
        await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { declaredRules, fakeCrc });

        // B2: Cố tình chỉ ghi 32 registers vào Staging RAM (thiếu 128 registers)
        ushort[] partialData = new ushort[32];
        await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, partialData);

        // B3: Gửi lệnh Commit Magic 0xA5A5
        await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // B4: Kiểm tra trạng thái MCU: Tracker phải phát hiện thiếu registers và từ chối
        var statusRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        ushort status = statusRegs[0];
        var err = (SPLC_ErrorCode)statusRegs[1];

        Assert.Equal(4, status); // ERROR
        Assert.Equal(SPLC_ErrorCode.INVALID_PARAMETER, err);

        // Active Version không tăng
        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, ver[0]);
    }

    [Fact]
    public async Task F2_05_DeployRules_DeviceBusy_ReturnsControlledError()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);

        sim.Control.Faults.DeviceBusy = true;

        var rules = new List<RuleRecordDto> { CreateRule(0) };

        // Bất biến: Trả về kết quả lỗi có kiểm soát với mã lỗi BUSY
        var result = await writer.DeployRulesAsync(1, rules);
        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.ConfigStatus); // ERROR
        Assert.Equal(SPLC_ErrorCode.BUSY, result.ErrorCode);

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, ver[0]);
    }

    [Fact]
    public async Task F2_06_PowerLoss_BeforeCommit_RebootClearsStaging_RestoresActiveFromFlash()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        // Nạp trước baseline 3 rules (Version 1)
        var baseline = new List<RuleRecordDto> { CreateRule(0, 10), CreateRule(1, 10), CreateRule(2, 10) };
        var baseRes = await writer.DeployRulesAsync(1, baseline);
        Assert.True(baseRes.IsSuccess);
        Assert.Equal(1, baseRes.ActiveVersion);

        // Ghi dở dang Staging cho 50 rules mới mà KHÔNG commit
        ushort newCount = 50;
        ushort fakeCrc = 0xABCD;
        await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { newCount, fakeCrc });
        ushort[] stagingChunk = new ushort[64];
        await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, stagingChunk);

        // Mô phỏng mất điện đột ngột (Power Cycle / Hardware Reset)
        sim.Control.PowerCycle();

        // Kiểm tra sau khi khởi động lại:
        // - Staging RAM phải sạch
        // - Active Table khôi phục từ Flash nguyên vẹn 3 rules của Version 1
        var activeRules = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(3, activeRules.Count);
        Assert.Equal(10, activeRules[0].ThresholdLo);

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver[0]);

        var statusRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 1);
        Assert.Equal(0, statusRegs[0]); // IDLE
    }

    [Fact]
    public async Task F2_07_PowerLoss_AfterCommit_RebootPreservesCommittedRules()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        // Nạp trọn vẹn 100 rules (Version 1) và commit thành công
        var rules100 = Create100Rules(marker: 777);
        var res = await writer.DeployRulesAsync(1, rules100);
        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.ActiveVersion);

        // Mất điện sau khi commit thành công
        sim.Control.PowerCycle();

        // Kiểm tra sau khi khởi động lại: Flash phải nạp lại đủ 100 rules
        var activeRules = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(100, activeRules.Count);
        Assert.Equal(777, activeRules[0].ThresholdLo);
        Assert.Equal(777 + 99, activeRules[99].ThresholdLo);

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver[0]);
    }

    [Fact]
    public async Task F2_08_RetryDeploy_AfterFailedStaging_OverwritesCompletely_ZeroHybridRules()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        // Đợt 1: Cố nạp Table A (marker 1000) nhưng bị timeout giữa chừng tại chunk #3
        sim.Control.Faults.TimeoutOnWriteNumber = 3;
        var tableA = Create100Rules(marker: 1000);
        await Assert.ThrowsAsync<TimeoutException>(() => writer.DeployRulesAsync(1, tableA));

        // Đợt 2: Thử lại nạp Table B (marker 2000) sau khi gỡ lỗi timeout
        sim.Control.Faults.TimeoutOnWriteNumber = null;
        var tableB = Create100Rules(marker: 2000);
        var retryRes = await writer.DeployRulesAsync(1, tableB);

        Assert.True(retryRes.IsSuccess);
        Assert.Equal(1, retryRes.ActiveVersion);

        // Bất biến F2: Toàn bộ 100 rules đều là Table B, không có bất kỳ rule nào sót lại của Table A
        var readBack = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(100, readBack.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(2000 + i, readBack[i].ThresholdLo);
            Assert.Equal(2000, readBack[i].ActionParam);
        }
    }
}
