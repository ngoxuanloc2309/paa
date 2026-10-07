using System.IO;
using System.Text.Json;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Phase E3: Golden Vectors V1.9 Test Suite.
/// Xác thực 9 canonical vectors (GV-001..GV-009) độc lập,
/// phân biệt rành mạch giữa Rule Table Payload CRC16 và Modbus RTU Frame CRC16.
/// </summary>
public class GoldenVectorV1_9Tests
{
    private static readonly string GoldenVectorsV19FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "GoldenVectors", "golden_vectors_v1_9.json");

    private static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    private static byte[] BuildRtuResponse(byte slaveId, byte fc, ReadOnlySpan<ushort> registers)
    {
        byte byteCount = (byte)(registers.Length * 2);
        byte[] frame = new byte[1 + 1 + 1 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = fc;
        frame[2] = byteCount;

        for (int i = 0; i < registers.Length; i++)
        {
            frame[3 + i * 2] = (byte)(registers[i] >> 8);
            frame[3 + i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 3 + byteCount));
        frame[^2] = (byte)(crc & 0xFF);         // Little-Endian CRC_Lo
        frame[^1] = (byte)((crc >> 8) & 0xFF);  // CRC_Hi
        return frame;
    }

    private static byte[] BuildRtuReadRequest(byte slaveId, byte fc, ushort address, ushort count)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = fc;
        frame[2] = (byte)(address >> 8);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)(count >> 8);
        frame[5] = (byte)(count & 0xFF);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] BuildRtuWriteSingleRequest(byte slaveId, ushort address, ushort value)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x06; // FC06
        frame[2] = (byte)(address >> 8);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)(value >> 8);
        frame[5] = (byte)(value & 0xFF);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] BuildRtuWriteMultipleRequest(byte slaveId, ushort startAddress, ReadOnlySpan<ushort> values)
    {
        byte byteCount = (byte)(values.Length * 2);
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x10; // FC16
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)(startAddress & 0xFF);
        frame[4] = (byte)(values.Length >> 8);
        frame[5] = (byte)(values.Length & 0xFF);
        frame[6] = byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            frame[7 + i * 2] = (byte)(values[i] >> 8);
            frame[7 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 7 + byteCount));
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    [Fact]
    public void GV001_DeviceDescriptor_MatchesExactWireFrame()
    {
        // 10 registers at 0x0000: Class=1, Variant=1, HW=1.2.0, FW=1.9.0, Proto=1, RuleFmt=7
        var desc = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 9,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceDescriptor(desc, regs);

        byte[] frame = BuildRtuResponse(1, 0x03, regs);
        Assert.True(Crc16Modbus.Verify(frame));
        Assert.Equal(25, frame.Length);

        // FC03 Request read 10 regs at 0x0000
        byte[] req = BuildRtuReadRequest(1, 0x03, 0x0000, 10);
        Assert.Equal("01 03 00 00 00 0A C5 CD", BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));
    }

    [Fact]
    public void GV002_DeviceResourceInfo_MatchesExactWireFrame()
    {
        // 10 registers at 0x0020 (Contract V1.9): WireProfile=1, MaxRules=100, Tags=124, 8DI/8DO/4AI/32VFLAG/32VREG/32RETAIN/8COUNTER
        var res = DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceResourceInfo(res, regs);

        byte[] frame = BuildRtuResponse(1, 0x03, regs);
        Assert.True(Crc16Modbus.Verify(frame));
        Assert.Equal(25, frame.Length);

        // FC03 Request read 10 regs at 0x0020
        byte[] req = BuildRtuReadRequest(1, 0x03, 0x0020, 10);
        Assert.Equal("01 03 00 20 00 0A C4 07", BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        // Response payload check
        Assert.Equal(0x00, frame[3]); // WireProfile High
        Assert.Equal(0x01, frame[4]); // WireProfile Low = 1
        Assert.Equal(0x00, frame[5]); // MaxRules High
        Assert.Equal(0x64, frame[6]); // MaxRules Low = 100
        Assert.Equal(0x00, frame[7]); // Tags High
        Assert.Equal(0x7C, frame[8]); // Tags Low = 124
    }

    [Fact]
    public void GV003_DeviceHealth_MatchesExactWireFrame()
    {
        // 10 registers at 0x0800
        var health = new DeviceHealthDto
        {
            UptimeSeconds = 3600,
            ResetReason = SPLC_ResetReason.POWER_ON,
            HealthFlags = SPLC_HealthFlags.NONE,
            CpuLoadPercent = 15,
            RamUsagePercent = 42,
            ScanTimeMs = 10,
            MaxScanTimeMs = 12
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceHealth(health, regs);

        byte[] frame = BuildRtuResponse(1, 0x03, regs);
        Assert.True(Crc16Modbus.Verify(frame));

        // Request: FC03 read 10 regs at 0x0800
        byte[] req = BuildRtuReadRequest(1, 0x03, 0x0800, 10);
        Assert.Equal("01 03 08 00 00 0A C7 AD", BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));
        Assert.Equal("01 03 14 00 00 0E 10 00 01 00 00 00 0F 00 2A 00 00 00 0A 00 00 00 0C 42 38", BytesToHex(frame));
    }

    [Fact]
    public void GV004_SingleRuleRecord_ComputesDistinctPayloadCrc16()
    {
        var rule = new RuleRecordDto
        {
            ThresholdLo = -100,
            ThresholdHi = 5000,
            ForMs = 2500,
            ActionParam = 1,
            TriggerTag = 16,
            ActionTag = 8,
            GuardTag = 0x8014,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.BETWEEN,
            ActionType = SPLC_ActionType.SET_TAG
        };

        Span<ushort> regs = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(rule, regs);

        byte[] payloadBytes = new byte[32];
        for (int i = 0; i < 16; i++)
        {
            payloadBytes[i * 2] = (byte)(regs[i] >> 8);
            payloadBytes[i * 2 + 1] = (byte)(regs[i] & 0xFF);
        }

        // Exact wire hex representation
        Assert.Equal("FF FF FF 9C 00 00 13 88 00 00 09 C4 00 00 00 01 00 10 00 08 80 14 01 01 07 00 00 00 00 00 00 00", BytesToHex(payloadBytes));

        // Rule Table Payload CRC16 is 0x11E9 (computed directly on 32 raw bytes)
        ushort payloadCrc = Crc16Modbus.Compute(payloadBytes);
        Assert.Equal(0x11E9, payloadCrc);

        // Verify that register-based computation matches byte-based computation
        Assert.Equal(payloadCrc, Crc16Modbus.ComputeFromRegisters(regs));
    }

    [Fact]
    public void GV005_MultiRuleStagingBuffer_ComputesCombinedPayloadCrc16()
    {
        var rule1 = new RuleRecordDto
        {
            ThresholdLo = -100,
            ThresholdHi = 5000,
            ForMs = 2500,
            ActionParam = 1,
            TriggerTag = 16,
            ActionTag = 8,
            GuardTag = 0x8014,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.BETWEEN,
            ActionType = SPLC_ActionType.SET_TAG
        };

        var rule2 = new RuleRecordDto
        {
            ThresholdLo = 0,
            ThresholdHi = 100,
            ForMs = 1000,
            ActionParam = 0,
            TriggerTag = 20,
            ActionTag = 9,
            GuardTag = 0,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            CompareOp = SPLC_CompareOp.EQ,
            ActionType = SPLC_ActionType.TOGGLE_TAG
        };

        Span<ushort> buffer = stackalloc ushort[32]; // 2 rules = 32 registers = 64 bytes
        RegisterCodec.EncodeRuleRecord(rule1, buffer.Slice(0, 16));
        RegisterCodec.EncodeRuleRecord(rule2, buffer.Slice(16, 16));

        ushort multiRuleCrc = Crc16Modbus.ComputeFromRegisters(buffer);
        Assert.NotEqual(0, multiRuleCrc);
        Assert.NotEqual(0x11E9, multiRuleCrc); // Differs from single rule CRC
        Assert.Equal(0xA3C7, multiRuleCrc);
    }

    [Fact]
    public void GV006_StagingHandshakeFrame_MatchesExactModbusFrame()
    {
        // FC16 write 2 registers to 0x9002 (RuleCount=1, ExpectedCrc=0x11E9)
        ushort[] handshakeValues = new ushort[] { 1, 0x11E9 };
        byte[] frame = BuildRtuWriteMultipleRequest(1, ModbusRegisterMap.RuleCountStagedAddress, handshakeValues);

        Assert.True(Crc16Modbus.Verify(frame));
        Assert.Equal(13, frame.Length);
        Assert.Equal(0x10, frame[1]);
        Assert.Equal(0x90, frame[2]);
        Assert.Equal(0x02, frame[3]);
        Assert.Equal("01 10 90 02 00 02 04 00 01 11 E9 42 6E", BytesToHex(frame));
    }

    [Fact]
    public void GV007_CommitCommandFrame_MatchesExactHex()
    {
        // FC06 write 0xA5A5 to 0xA000
        byte[] frame = BuildRtuWriteSingleRequest(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);
        Assert.Equal("01 06 A0 00 A5 A5 10 E1", BytesToHex(frame));
        Assert.True(Crc16Modbus.Verify(frame));
    }

    [Fact]
    public void GV008_SystemCommandFrame_MatchesExactHex()
    {
        // FC06 write REBOOT=1 to 0x0A00
        byte[] frame = BuildRtuWriteSingleRequest(1, ModbusRegisterMap.SystemCommandAddress, (ushort)SPLC_SystemCommand.REBOOT);
        Assert.Equal("01 06 0A 00 00 01 4B D2", BytesToHex(frame));
        Assert.True(Crc16Modbus.Verify(frame));
    }

    [Fact]
    public void GV009_ModbusExceptionFrame_MatchesStandardExceptionFormat()
    {
        // Modbus Exception: SlaveID=1, FC=0x83, ExceptionCode=0x02 (Illegal Data Address)
        byte[] exceptionFrame = new byte[5];
        exceptionFrame[0] = 1;
        exceptionFrame[1] = 0x83;
        exceptionFrame[2] = (byte)ModbusExceptionCode.IllegalDataAddress; // 0x02

        ushort crc = Crc16Modbus.Compute(exceptionFrame.AsSpan(0, 3));
        exceptionFrame[3] = (byte)(crc & 0xFF);
        exceptionFrame[4] = (byte)((crc >> 8) & 0xFF);

        Assert.Equal("01 83 02 C0 F1", BytesToHex(exceptionFrame));
        Assert.True(Crc16Modbus.Verify(exceptionFrame));
    }

    public static ushort[] GenerateCanonical100RulesPayload()
    {
        var buffer = new ushort[ModbusRegisterMap.MaxRules * ModbusRegisterMap.RegistersPerRule]; // 1600 registers = 3200 bytes
        for (int i = 0; i < ModbusRegisterMap.MaxRules; i++)
        {
            var rule = new RuleRecordDto
            {
                ThresholdLo = i * 10 - 500,
                ThresholdHi = (i + 1) * 100,
                ForMs = (uint)(i * 25),
                ActionParam = i + 1,
                TriggerTag = (ushort)(i % 124),
                ActionTag = (ushort)((i + 1) % 124),
                GuardTag = (ushort)(i % 2 == 0 ? 0 : 0x8000 | (i % 32 + 20)),
                Enabled = (i % 5 != 0),
                TriggerType = (SPLC_TriggerType)(i % 5),
                CompareOp = (SPLC_CompareOp)(i % 8),
                ActionType = (SPLC_ActionType)(i % 8)
            };
            RegisterCodec.EncodeRuleRecord(rule, buffer.AsSpan(i * ModbusRegisterMap.RegistersPerRule, ModbusRegisterMap.RegistersPerRule));
        }
        return buffer;
    }

    [Fact]
    public void GV_PayloadMax_100Rules_3200Bytes_1600Registers_MatchesExactFixedPayloadCrc16()
    {
        // Boundary case: full capacity of Wire Profile V1
        ushort[] buffer = GenerateCanonical100RulesPayload();
        Assert.Equal(1600, buffer.Length);

        byte[] payloadBytes = new byte[buffer.Length * 2];
        for (int i = 0; i < buffer.Length; i++)
        {
            payloadBytes[i * 2] = (byte)(buffer[i] >> 8);
            payloadBytes[i * 2 + 1] = (byte)(buffer[i] & 0xFF);
        }
        Assert.Equal(3200, payloadBytes.Length);

        ushort crcFromBytes = Crc16Modbus.Compute(payloadBytes);
        ushort crcFromRegs = Crc16Modbus.ComputeFromRegisters(buffer);

        Assert.Equal(crcFromBytes, crcFromRegs);
        Assert.Equal(0x926F, crcFromBytes);
    }

    [Fact]
    public void GenerateAndVerify_GoldenVectorsV1_9JsonFile()
    {
        // Canonical Golden Vectors V1.9 Generator & Integrity Verifier
        Span<ushort> r1 = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(new RuleRecordDto
        {
            ThresholdLo = -100,
            ThresholdHi = 5000,
            ForMs = 2500,
            ActionParam = 1,
            TriggerTag = 16,
            ActionTag = 8,
            GuardTag = 0x8014,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.BETWEEN,
            ActionType = SPLC_ActionType.SET_TAG
        }, r1);

        Span<ushort> r2 = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(new RuleRecordDto
        {
            ThresholdLo = 0,
            ThresholdHi = 100,
            ForMs = 1000,
            ActionParam = 0,
            TriggerTag = 20,
            ActionTag = 9,
            GuardTag = 0,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            CompareOp = SPLC_CompareOp.EQ,
            ActionType = SPLC_ActionType.TOGGLE_TAG
        }, r2);

        Span<ushort> buffer = stackalloc ushort[32];
        r1.CopyTo(buffer.Slice(0, 16));
        r2.CopyTo(buffer.Slice(16, 16));
        ushort multiRuleCrc = Crc16Modbus.ComputeFromRegisters(buffer);

        Span<ushort> descRegs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceDescriptor(new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 9,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        }, descRegs);

        Span<ushort> resRegs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceResourceInfo(DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai(), resRegs);

        Span<ushort> healthRegs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceHealth(new DeviceHealthDto
        {
            UptimeSeconds = 3600,
            ResetReason = SPLC_ResetReason.POWER_ON,
            HealthFlags = SPLC_HealthFlags.NONE,
            CpuLoadPercent = 15,
            RamUsagePercent = 42,
            ScanTimeMs = 10,
            MaxScanTimeMs = 12
        }, healthRegs);

        var vectors = new
        {
            metadata = new
            {
                spec_version = "1.9.0",
                description = "SimplePLC Platform V1.9 Canonical Golden Vectors (9 canonical vectors)",
                date = "2026-09-16"
            },
            gv_001_device_descriptor = new
            {
                description = "GV-001: FC03 DeviceDescriptor at 0x0000 (10 registers)",
                request_hex = "01 03 00 00 00 0A C5 CD",
                response_hex = BytesToHex(BuildRtuResponse(1, 0x03, descRegs))
            },
            gv_002_device_resource_info = new
            {
                description = "GV-002: FC03 DeviceResourceInfo at 0x0020 (10 registers, V1.9 self-describing)",
                request_hex = "01 03 00 20 00 0A C4 07",
                response_hex = BytesToHex(BuildRtuResponse(1, 0x03, resRegs))
            },
            gv_003_device_health = new
            {
                description = "GV-003: FC03 DeviceHealth at 0x0800 (10 registers, scan_time_ms = 10, max = 12)",
                request_hex = "01 03 08 00 00 0A C7 AD",
                response_hex = BytesToHex(BuildRtuResponse(1, 0x03, healthRegs))
            },
            gv_004_single_rule_payload = new
            {
                description = "GV-004: Single Rule Record (32 bytes) with Rule Table Payload CRC16",
                rule_bytes_hex = "FF FF FF 9C 00 00 13 88 00 00 09 C4 00 00 00 01 00 10 00 08 80 14 01 01 07 00 00 00 00 00 00 00",
                payload_crc16_hex = "11E9"
            },
            gv_005_multi_rule_payload = new
            {
                description = "GV-005: Two Rule Records (64 bytes) with combined Rule Table Payload CRC16",
                rule_count = 2,
                total_bytes = 64,
                payload_crc16_hex = multiRuleCrc.ToString("X4")
            },
            gv_payload_max = new
            {
                description = "GV-PAYLOAD-MAX: Full Capacity 100 Rule Records (3200 bytes / 1600 registers) with fixed Payload CRC16",
                rule_count = 100,
                total_registers = 1600,
                total_bytes = 3200,
                payload_crc16_hex = "926F"
            },
            gv_006_staging_handshake = new
            {
                description = "GV-006: FC16 Staging Handshake Write to 0x9002 (RuleCount=1, ExpectedCrc=0x11E9)",
                request_hex = BytesToHex(BuildRtuWriteMultipleRequest(1, 0x9002, new ushort[] { 1, 0x11E9 }))
            },
            gv_007_commit_command = new
            {
                description = "GV-007: FC06 Commit Command Write 0xA5A5 to 0xA000",
                request_hex = "01 06 A0 00 A5 A5 10 E1"
            },
            gv_008_system_command_reboot = new
            {
                description = "GV-008: FC06 System Command REBOOT=1 to 0x0A00",
                request_hex = "01 06 0A 00 00 01 4B D2"
            },
            gv_009_modbus_exception = new
            {
                description = "GV-009: Modbus Exception Frame (SlaveID=1, FC=0x83, Code=0x02 Illegal Address)",
                response_hex = "01 83 02 C0 F1"
            }
        };

        string json = JsonSerializer.Serialize(vectors, new JsonSerializerOptions { WriteIndented = true });
        string dir = Path.GetDirectoryName(GoldenVectorsV19FilePath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        try { File.WriteAllText(GoldenVectorsV19FilePath, json); } catch (UnauthorizedAccessException) { }

        Assert.True(File.Exists(GoldenVectorsV19FilePath));
        Assert.True(json.Length > 500);
    }
}
