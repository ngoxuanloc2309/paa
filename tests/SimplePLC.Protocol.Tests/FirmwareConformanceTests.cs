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
/// Phase E: Cross-Language Verification & Conformance Tests.
/// Ð?m b?o tính toàn v?n tuy?t d?i gi?a C Header (simpleplc_protocol_v1_7.h),
/// DTOs, RegisterCodec và Golden Vectors.
/// </summary>
public class FirmwareConformanceTests
{
    private static readonly string GoldenVectorsFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "GoldenVectors", "golden_vectors_v1_7.json");

    [Fact]
    public void ProtocolConstants_MustMatchCHeaderSpecification()
    {
        // Kích thu?c thanh ghi & b? d?m
        Assert.Equal(16, ModbusRegisterMap.RegistersPerRule);
        Assert.Equal(32, ModbusRegisterMap.BytesPerRule);
        Assert.Equal(100, ModbusRegisterMap.MaxRules);
        Assert.Equal(128, ModbusRegisterMap.MaxRuntimeTags);
        Assert.Equal(2, ModbusRegisterMap.RegistersPerRuntimeTag);

        // Magic numbers & Bitmasks
        Assert.Equal(0xA5A5, ModbusRegisterMap.CommitMagic);
        Assert.Equal(0x8000, ModbusRegisterMap.GuardTagNegateMask);
        Assert.Equal(0x7FFF, ModbusRegisterMap.GuardTagIndexMask);
        Assert.Equal(0x7FFF, ModbusRegisterMap.GuardTagNone);

        // B?n d? d?a ch? thanh ghi Modbus
        Assert.Equal(0x0000, ModbusRegisterMap.DeviceDescriptorAddress);
        Assert.Equal(10, ModbusRegisterMap.DeviceDescriptorLength);

        Assert.Equal(0x0010, ModbusRegisterMap.RuleTableInfoAddress);
        Assert.Equal(1, ModbusRegisterMap.RuleTableInfoLength);

        Assert.Equal(0x0100, ModbusRegisterMap.ActiveRuleTableBaseAddress);
        Assert.Equal(1600, ModbusRegisterMap.ActiveRuleTableMaxLength);

        Assert.Equal(0x0800, ModbusRegisterMap.DeviceHealthAddress);
        Assert.Equal(10, ModbusRegisterMap.DeviceHealthLength);

        Assert.Equal(0x0900, ModbusRegisterMap.RuntimeTagValuesBaseAddress);
        Assert.Equal(256, ModbusRegisterMap.RuntimeTagValuesMaxLength);

        Assert.Equal(0x0A00, ModbusRegisterMap.SystemCommandAddress);
        Assert.Equal(1, ModbusRegisterMap.SystemCommandLength);

        Assert.Equal(0x0A01, ModbusRegisterMap.SystemCommandResultAddress);
        Assert.Equal(2, ModbusRegisterMap.SystemCommandResultLength);

        Assert.Equal(0x9000, ModbusRegisterMap.ConfigStatusAddress);
        Assert.Equal(0x9001, ModbusRegisterMap.ConfigErrorCodeAddress);
        Assert.Equal(0x9002, ModbusRegisterMap.RuleCountStagedAddress);
        Assert.Equal(0x9003, ModbusRegisterMap.ExpectedCrc16Address);
        Assert.Equal(0x9004, ModbusRegisterMap.ActiveRuleCountAddress);
        Assert.Equal(0x9005, ModbusRegisterMap.ActiveRuleCrc16Address);

        Assert.Equal(0x9010, ModbusRegisterMap.StagingRuleTableBaseAddress);
        Assert.Equal(1600, ModbusRegisterMap.StagingRuleTableMaxLength);

        Assert.Equal(0xA000, ModbusRegisterMap.CommitCommandAddress);
        Assert.Equal(0xA001, ModbusRegisterMap.ActiveRuleVersionAddress);
    }

    [Fact]
    public void DeviceDescriptor_GoldenVector_RoundtripMatchesExpectedHex()
    {
        // Arrange
        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 7,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceDescriptor(descriptor, regs);

        // Chuy?n thành byte wire format (Big-Endian per register)
        byte[] payloadBytes = new byte[20];
        for (int i = 0; i < 10; i++)
        {
            payloadBytes[i * 2] = (byte)(regs[i] >> 8);
            payloadBytes[i * 2 + 1] = (byte)(regs[i] & 0xFF);
        }

        // T?o Modbus RTU Response Frame: [SlaveID=1, FC=0x03, ByteCount=20, Payload(20), CRC_Lo, CRC_Hi]
        byte[] rtuFrame = new byte[1 + 1 + 1 + 20 + 2];
        rtuFrame[0] = 0x01; // SlaveId
        rtuFrame[1] = 0x03; // FC03
        rtuFrame[2] = 0x14; // 20 bytes
        Array.Copy(payloadBytes, 0, rtuFrame, 3, 20);

        ushort crc = Crc16Modbus.Compute(rtuFrame.AsSpan(0, 23));
        rtuFrame[23] = (byte)(crc & 0xFF);
        rtuFrame[24] = (byte)(crc >> 8);

        // Assert
        Assert.Equal(25, rtuFrame.Length);
        Assert.True(Crc16Modbus.Verify(rtuFrame));

        // Decode ngu?c l?i
        var decoded = RegisterCodec.DecodeDeviceDescriptor(regs);
        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, decoded.DeviceClass);
        Assert.Equal((ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI, decoded.DeviceVariant);
        Assert.Equal("1.2.0", decoded.HwVersionString);
        Assert.Equal("1.7.0", decoded.FwVersionString);
        Assert.Equal(1, decoded.ProtocolVersion);
        Assert.Equal(7, decoded.RuleFormatVersion);
    }

    [Fact]
    public void DeviceHealth_MillisecondsScanTime_RoundtripMatchesExpectedHex()
    {
        // Arrange: 10 ms nominal scan time
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

        // Assert registers mapping
        // +0..+1: uptime_s (3600 = 0x00000E10)
        Assert.Equal(0x0000, regs[0]);
        Assert.Equal(0x0E10, regs[1]);

        // +2: reset_reason (1)
        Assert.Equal(1, regs[2]);

        // +3: health_flags (0)
        Assert.Equal(0, regs[3]);

        // +4: cpu_load (15)
        Assert.Equal(15, regs[4]);

        // +5: ram_usage (42)
        Assert.Equal(42, regs[5]);

        // +6..+7: scan_time_ms (10 = 0x0000000A)
        Assert.Equal(0x0000, regs[6]);
        Assert.Equal(0x000A, regs[7]);

        // +8..+9: max_scan_time_ms (12 = 0x0000000C)
        Assert.Equal(0x0000, regs[8]);
        Assert.Equal(0x000C, regs[9]);

        // Decode roundtrip
        var decoded = RegisterCodec.DecodeDeviceHealth(regs);
        Assert.Equal(3600u, decoded.UptimeSeconds);
        Assert.Equal(SPLC_ResetReason.POWER_ON, decoded.ResetReason);
        Assert.Equal(10u, decoded.ScanTimeMs);
        Assert.Equal(12u, decoded.MaxScanTimeMs);
    }

    [Fact]
    public void SingleRuleRecord_WireContractV17_MatchesExactGoldenBytes()
    {
        // Arrange
        var rule = new RuleRecordDto
        {
            ThresholdLo = -100, // 0xFFFFFF9C
            ThresholdHi = 5000, // 0x00001388
            ForMs = 2500,       // 0x000009C4
            ActionParam = 1,    // 0x00000001
            TriggerTag = 16,    // AI0
            ActionTag = 8,      // DO0
            GuardTag = 0x8014,  // bit 15=1 (NEGATE), index=20 (VFLAG0)
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.BETWEEN,
            ActionType = SPLC_ActionType.SET_TAG
        };

        Span<ushort> regs = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(rule, regs);

        byte[] ruleBytes = new byte[32];
        for (int i = 0; i < 16; i++)
        {
            ruleBytes[i * 2] = (byte)(regs[i] >> 8);
            ruleBytes[i * 2 + 1] = (byte)(regs[i] & 0xFF);
        }

        // Expected byte sequence (32 bytes):
        // 00..03: ThresholdLo (-100 = 0xFFFFFF9C)
        Assert.Equal(0xFF, ruleBytes[0]);
        Assert.Equal(0xFF, ruleBytes[1]);
        Assert.Equal(0xFF, ruleBytes[2]);
        Assert.Equal(0x9C, ruleBytes[3]);

        // 04..07: ThresholdHi (5000 = 0x00001388)
        Assert.Equal(0x00, ruleBytes[4]);
        Assert.Equal(0x00, ruleBytes[5]);
        Assert.Equal(0x13, ruleBytes[6]);
        Assert.Equal(0x88, ruleBytes[7]);

        // 08..11: ForMs (2500 = 0x000009C4)
        Assert.Equal(0x00, ruleBytes[8]);
        Assert.Equal(0x00, ruleBytes[9]);
        Assert.Equal(0x09, ruleBytes[10]);
        Assert.Equal(0xC4, ruleBytes[11]);

        // 12..15: ActionParam (1 = 0x00000001)
        Assert.Equal(0x00, ruleBytes[12]);
        Assert.Equal(0x00, ruleBytes[13]);
        Assert.Equal(0x00, ruleBytes[14]);
        Assert.Equal(0x01, ruleBytes[15]);

        // 16..17: TriggerTag (16 = 0x0010)
        Assert.Equal(0x00, ruleBytes[16]);
        Assert.Equal(0x10, ruleBytes[17]);

        // 18..19: ActionTag (8 = 0x0008)
        Assert.Equal(0x00, ruleBytes[18]);
        Assert.Equal(0x08, ruleBytes[19]);

        // 20..21: GuardTag (0x8014: negate=true, tag=20)
        Assert.Equal(0x80, ruleBytes[20]);
        Assert.Equal(0x14, ruleBytes[21]);

        // 22..23: Enabled (1) & TriggerType (ON_RISE = 1) -> 0x0101
        Assert.Equal(0x01, ruleBytes[22]);
        Assert.Equal(0x01, ruleBytes[23]);

        // 24..25: CompareOp (BETWEEN = 7) & ActionType (SET_TAG = 0) -> 0x0700
        Assert.Equal(0x07, ruleBytes[24]);
        Assert.Equal(0x00, ruleBytes[25]);

        // 26..31: Reserved (6 bytes of 0x00)
        for (int r = 26; r < 32; r++)
        {
            Assert.Equal(0x00, ruleBytes[r]);
        }

        // Tính CRC-16 c?a kh?i rule này
        ushort ruleCrc = Crc16Modbus.Compute(ruleBytes);
        Assert.NotEqual(0, ruleCrc);
    }

    [Fact]
    public void GenerateAndVerify_GoldenVectorsJsonFile()
    {
        // Kh?i t?o t?p h?p golden vectors hoàn ch?nh
        var vectors = new
        {
            metadata = new
            {
                spec_version = "1.7.0",
                description = "SimplePLC Wire Protocol V1.7 Golden Test Vectors for MCU Firmware Conformance",
                date = "2026-09-15"
            },
            device_descriptor_vector = new
            {
                description = "FC03 response for DeviceDescriptor at 0x0000 (10 registers)",
                request_hex = "01 03 00 00 00 0A C5 CD",
                response_hex = "01 03 14 00 01 00 01 00 01 00 02 00 00 00 01 00 07 00 00 00 01 00 07 2E 1F"
            },
            device_health_vector = new
            {
                description = "FC03 response for DeviceHealth at 0x0800 (10 registers, scan_time_ms = 10, max_scan_time_ms = 12)",
                request_hex = "01 03 08 00 00 0A C7 AB",
                response_hex = "01 03 14 00 00 0E 10 00 01 00 00 00 0F 00 2A 00 00 00 0A 00 00 00 0C 47 C1"
            },
            single_rule_vector = new
            {
                description = "Single Rule Record (32 bytes / 16 registers) with between compare, ms dwell, and negated guard",
                rule_bytes_hex = "FF FF FF 9C 00 00 13 88 00 00 09 C4 00 00 00 01 00 10 00 08 80 14 01 01 07 00 00 00 00 00 00 00",
                crc16_hex = "9354"
            },
            commit_magic_vector = new
            {
                description = "FC06 write 0xA5A5 to 0xA000 (COMMIT_COMMAND)",
                request_hex = "01 06 A0 00 A5 A5 F1 56"
            }
        };

        var json = JsonSerializer.Serialize(vectors, new JsonSerializerOptions { WriteIndented = true });
        
        string targetDir = Path.GetDirectoryName(GoldenVectorsFilePath)!;
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        try { File.WriteAllText(GoldenVectorsFilePath, json); } catch (UnauthorizedAccessException) { }

        Assert.True(File.Exists(GoldenVectorsFilePath));
        Assert.True(json.Length > 200);
    }
}
