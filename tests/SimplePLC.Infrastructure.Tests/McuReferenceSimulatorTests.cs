using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class McuReferenceSimulatorTests
{
    private static RuleRecordDto CreateDummyRule(int index) => new()
    {
        ThresholdLo = 100 * index,
        ThresholdHi = 200 * index,
        ForMs = 1000,
        ActionParam = 1,
        TriggerTag = (ushort)index,
        ActionTag = (ushort)(index + 10),
        Enabled = true,
        TriggerType = SPLC_TriggerType.ON_RISE,
        CompareOp = SPLC_CompareOp.GT,
        ActionType = SPLC_ActionType.SET_TAG
    };

    [Fact]
    public async Task RegisterMemory_UndefinedAddressRead_ThrowsIllegalAddressException()
    {
        // Arrange
        var sim = new McuReferenceSimulator();

        // Act & Assert: Address 0x0050 is not in defined contract map
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sim.ReadHoldingRegistersAsync(1, 0x0050, 5));
    }

    [Fact]
    public async Task RegisterMemory_ReadOnlyAddressWrite_ThrowsIllegalAddressException()
    {
        // Arrange
        var sim = new McuReferenceSimulator();

        // Act & Assert: Descriptor 0x0000 is strictly read-only
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sim.WriteSingleRegisterAsync(1, 0x0000, 0x1234));
    }

    [Fact]
    public async Task StagingPartialWrite_DoesNotModifyActiveTable()
    {
        // Arrange
        var sim = new McuReferenceSimulator();

        // Initialize staging for 2 rules (32 registers)
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 2, 0x1234 });

        // Write only rule 1 (16 registers)
        ushort[] partialChunk = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), partialChunk);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, partialChunk);

        // Assert: Active rule count remains 0
        var activeInfo = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RuleTableInfoAddress, 1);
        Assert.Equal(0, activeInfo[0]);

        var configStatus = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 1);
        Assert.Equal(1, configStatus[0]); // RECEIVING
    }

    [Fact]
    public async Task CommitWithoutCompletePayload_IsRejected()
    {
        // Arrange: declared 2 rules, but only wrote 1 rule
        var sim = new McuReferenceSimulator();
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 2, 0x1234 });

        ushort[] chunk1 = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), chunk1);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, chunk1);

        // Act: Commit magic
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Assert: CONFIG_STATUS = 4 (ERROR), CONFIG_ERROR_CODE = INVALID_PARAMETER (2)
        var status = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        Assert.Equal(4, status[0]); // ERROR
        Assert.Equal((ushort)SPLC_ErrorCode.INVALID_PARAMETER, status[1]);
    }

    [Fact]
    public async Task WriteBeyondStagedRuleCount_IsRejected()
    {
        // Arrange: declared 1 rule (16 registers)
        var sim = new McuReferenceSimulator();
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0x1234 });

        // Act & Assert: Writing 32 registers exceeds declared capacity of 16
        ushort[] chunkOver = new ushort[32];
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, chunkOver));
    }

    [Fact]
    public async Task FailedCommit_PreservesPreviousActiveTable()
    {
        // Arrange: valid staging payload of 1 rule
        var sim = new McuReferenceSimulator();
        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);

        // Give incorrect expected CRC
        ushort wrongCrc = (ushort)(actualCrc ^ 0x1234);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, wrongCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);

        // Act
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Assert: Status = ERROR, Code = CRC_MISMATCH, Active count remains 0, Version remains 1
        var status = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        Assert.Equal(4, status[0]);
        Assert.Equal((ushort)SPLC_ErrorCode.CRC_MISMATCH, status[1]);

        var activeCount = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
        Assert.Equal(0, activeCount[0]);
        var version = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, version[0]);
    }

    [Fact]
    public async Task SuccessfulCommit_IncrementsVersionExactlyOnce_AndPersistsToFlash()
    {
        // Arrange
        var sim = new McuReferenceSimulator();
        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);

        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, actualCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);

        // Act
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Assert
        var status = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        Assert.Equal(3, status[0]); // READY
        Assert.Equal((ushort)SPLC_ErrorCode.NONE, status[1]);

        var activeInfo = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 2);
        Assert.Equal(1, activeInfo[0]); // Active count = 1
        Assert.Equal(actualCrc, activeInfo[1]); // Active CRC = actualCrc

        var version = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, version[0]); // 0 -> 1

        // Check Flash directly
        Assert.True(sim.Control.Flash.HasCommittedData);
        Assert.Equal(1, sim.Control.Flash.StoredRuleCount);
        Assert.Equal(1, sim.Control.Flash.StoredVersion);
    }

    [Fact]
    public async Task CommitTwiceWithoutNewStaging_DoesNotIncrementVersionAgain()
    {
        // Arrange
        var sim = new McuReferenceSimulator();
        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);

        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, actualCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        var ver1 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver1[0]);

        // Act: Commit second time without new staging
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Assert: Version should NOT increment again
        var ver2 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver2[0]);
    }

    [Fact]
    public async Task NewStagingAfterError_RecoversFromErrorState()
    {
        // Arrange: Cause an error first
        var sim = new McuReferenceSimulator();
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 2, 0x1234 });
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        var errStatus = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 1);
        Assert.Equal(4, errStatus[0]); // ERROR

        // Act: App starts a new staging session by writing RULE_COUNT_STAGED
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0x9999 });

        // Assert: State returns to IDLE (0) and ErrorCode = NONE (0)
        var recovered = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        Assert.Equal(0, recovered[0]); // IDLE
        Assert.Equal((ushort)SPLC_ErrorCode.NONE, recovered[1]);
    }

    [Fact]
    public void ReservedBytes_NonZero_DoNotAffectDecode()
    {
        // Arrange: Encode a rule into 16 registers
        ushort[] buffer = new ushort[16];
        var dto = CreateDummyRule(3);
        RegisterCodec.EncodeRuleRecord(dto, buffer);

        // Simulate corrupted/non-zero reserved registers in wire stream
        buffer[13] = 0xAAAA;
        buffer[14] = 0xBBBB;
        buffer[15] = 0xCCCC;

        // Act: Decode
        var decoded = RegisterCodec.DecodeRuleRecord(buffer);

        // Assert: Decode succeeds without throwing and preserves functional fields
        Assert.Equal(dto.ThresholdLo, decoded.ThresholdLo);
        Assert.Equal(dto.TriggerTag, decoded.TriggerTag);
        Assert.Equal(dto.ActionTag, decoded.ActionTag);
    }

    [Fact]
    public async Task Reboot_DiscardsUncommittedStaging()
    {
        // Arrange: write partial staging
        var sim = new McuReferenceSimulator();
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0x1234 });
        ushort[] chunk = new ushort[16];
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, chunk);

        // Act: Reboot
        sim.Control.SoftwareReboot();

        // Assert: Staging is cleared and tracker is empty
        Assert.False(sim.Control.IsStagingRegisterWritten(0));
        var status = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 1);
        Assert.Equal(0, status[0]); // IDLE
    }

    [Fact]
    public async Task PowerCycle_RestoresCommittedFlash()
    {
        // Arrange: Commit 1 rule into Flash
        var sim = new McuReferenceSimulator();
        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);

        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, actualCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Act: Power Cycle (complete memory power down)
        sim.Control.PowerCycle();

        // Assert: Active rules and version are restored from Flash
        var activeCount = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
        Assert.Equal(1, activeCount[0]);
        var version = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, version[0]);

        // Verify active table registers match
        var restoredActive = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleTableBaseAddress, 16);
        Assert.Equal(staging, restoredActive);
    }

    [Fact]
    public async Task PowerCycle_SetsPowerOnResetReason()
    {
        var sim = new McuReferenceSimulator();
        sim.Control.PowerCycle();

        var health = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceHealthAddress, 4);
        // Reg 0..1 = Uptime (0 after reset)
        Assert.Equal(0, health[0]);
        Assert.Equal(0, health[1]);
        // Reg 2 = ResetReason (POWER_ON = 1)
        Assert.Equal((ushort)SPLC_ResetReason.POWER_ON, health[2]);
    }

    [Fact]
    public async Task SoftwareReboot_SetsSoftwareResetReason()
    {
        var sim = new McuReferenceSimulator();
        sim.Control.SoftwareReboot();

        var health = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceHealthAddress, 4);
        // Reg 2 = ResetReason (SOFTWARE = 2)
        Assert.Equal((ushort)SPLC_ResetReason.SOFTWARE, health[2]);
    }

    [Fact]
    public async Task FactoryReset_ClearsPersistentRules_PreservesDescriptor()
    {
        // Arrange: Commit 1 rule
        var sim = new McuReferenceSimulator();
        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, actualCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Act: Factory Reset
        sim.Control.FactoryReset();

        // Assert: Flash and Active rules are cleared
        Assert.False(sim.Control.Flash.HasCommittedData);
        var activeCount = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
        Assert.Equal(0, activeCount[0]);
        var version = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, version[0]);

        // Descriptor is 100% PRESERVED!
        var descriptor = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceDescriptorAddress, 10);
        Assert.Equal((ushort)SPLC_DeviceClass.REMOTE_IO, descriptor[0]);
        Assert.Equal((ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI, descriptor[1]);
        Assert.Equal(1, descriptor[2]); // HW Major
        Assert.Equal(1, descriptor[5]); // FW Major
        Assert.Equal(7, descriptor[6]); // FW Minor
    }

    [Fact]
    public async Task TimeoutOnWriteNumber_ThrowsTimeoutException()
    {
        // Arrange: Inject timeout at write transaction #2
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.TimeoutOnWriteNumber = 2;

        // Transaction 1: succeeds
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0x1234 });

        // Transaction 2: must throw TimeoutException!
        ushort[] chunk = new ushort[16];
        await Assert.ThrowsAsync<TimeoutException>(() =>
            sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, chunk));
    }

    [Fact]
    public async Task DeviceBusy_ReturnsBusyErrorCode()
    {
        // Arrange: Set device busy
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.DeviceBusy = true;

        ushort[] staging = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging);
        ushort actualCrc = Crc16Modbus.ComputeFromRegisters(staging);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, actualCrc });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging);

        // Act: Commit while MCU is busy
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // Assert: Status = ERROR (4), Code = BUSY (3)
        var status = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
        Assert.Equal(4, status[0]);
        Assert.Equal((ushort)SPLC_ErrorCode.BUSY, status[1]);
    }

    [Fact]
    public async Task Telemetry_DeterministicMode_ProducesPredictableValues()
    {
        // Arrange
        var sim = new McuReferenceSimulator();
        sim.Control.Clock.Mode = SimulatorMode.Deterministic;
        sim.Control.Clock.Reset(0);

        // Tick 0
        var h0 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceHealthAddress, 8);
        Assert.Equal(0, h0[0]); // Uptime high
        Assert.Equal(0, h0[1]); // Uptime low
        Assert.Equal(25, h0[4]); // CPU = 25%
        Assert.Equal(10, h0[7]); // Scan time = 10 ms

        // Advance 2 seconds
        sim.Control.AdvanceTicks(2);
        var h1 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceHealthAddress, 8);
        Assert.Equal(2, h1[1]); // Uptime = 2s
        Assert.Equal(27, h1[4]); // CPU = 25 + 2 = 27%
        Assert.Equal(10, h1[7]); // Scan time = 10 ms (nominal)
    }

    [Fact]
    public async Task PowerLossDuringStaging_PowerCycle_PreservesPreviousActiveTable()
    {
        // Arrange: 1. Deploy and commit Rule 1
        var sim = new McuReferenceSimulator();
        ushort[] staging1 = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(1), staging1);
        ushort crc1 = Crc16Modbus.ComputeFromRegisters(staging1);

        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, crc1 });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging1);
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        var ver1 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver1[0]);

        // 2. Start new staging for Rule 2, but power loss occurs before commit
        ushort[] staging2 = new ushort[16];
        RegisterCodec.EncodeRuleRecord(CreateDummyRule(2), staging2);
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0x9999 });
        await sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, staging2);

        // Act: Complete power loss during staging
        sim.Control.PowerCycle();

        // Assert: Previous committed Active Table (Rule 1) is 100% intact!
        // Verify all 4 fields simultaneously: Count, CRC16, Version, and Table Registers
        var count = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
        Assert.Equal(1, count[0]);

        var crc = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCrc16Address, 1);
        Assert.Equal(crc1, crc[0]);

        var ver2 = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver2[0]);

        var activeRegs = await sim.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleTableBaseAddress, 16);
        Assert.Equal(staging1, activeRegs);
    }
}
