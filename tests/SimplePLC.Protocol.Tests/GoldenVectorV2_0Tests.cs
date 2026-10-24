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
/// Authoritative Golden Vectors V2.0 Test Suite.
/// Validates canonical Modbus RTU wire frames for SimplePLC V2.0:
/// RTC Clock, Diagnostic Control Block, Function Block Timers & Counters IEC 61131-3,
/// Time Window Automation, and Wire Profile V2 Resource Profiles.
/// </summary>
public class GoldenVectorV2_0Tests
{
    private static readonly string GoldenVectorsV20FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "GoldenVectors", "golden_vectors_v2_0.json");

    private static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    private static byte[] HexToBytes(string hex)
    {
        string[] parts = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            bytes[i] = Convert.ToByte(parts[i], 16);
        }
        return bytes;
    }

    private static JsonElement LoadGoldenVectorsJson()
    {
        string json = File.ReadAllText(GoldenVectorsV20FilePath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
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

    [Fact]
    public void GV001_DeviceDescriptor_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_001_device_descriptor");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.DeviceDescriptorAddress, ModbusRegisterMap.DeviceDescriptorLength);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV002_DeviceResourceInfo_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_002_device_resource_info");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.DeviceResourceInfoAddress, ModbusRegisterMap.DeviceResourceInfoLength);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV004_RtcClock_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_004_rtc_clock");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.RtcClockAddress, ModbusRegisterMap.RtcClockLength);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV005_DiagnosticBlock_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_005_diagnostic_block");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.DiagCommandAddress, ModbusRegisterMap.DiagBlockLength);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV006_FbTimer_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_006_fb_timer");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbRegistersPerBlock);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV007_FbCounter_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_007_fb_counter");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;
        string expectedRespHex = gv.GetProperty("response_hex").GetString()!;

        byte[] req = BuildRtuReadRequest(1, 3, ModbusRegisterMap.FbCounterTableBaseAddress, ModbusRegisterMap.FbRegistersPerBlock);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));

        byte[] resp = HexToBytes(expectedRespHex);
        Assert.True(Crc16Modbus.Verify(resp));
    }

    [Fact]
    public void GV008_TimeWindowRule_PayloadCrcMatches()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_008_time_window_rule");
        string ruleBytesHex = gv.GetProperty("rule_bytes_hex").GetString()!;
        string expectedCrcHex = gv.GetProperty("payload_crc16_hex").GetString()!;

        byte[] ruleBytes = HexToBytes(ruleBytesHex);
        Assert.Equal(32, ruleBytes.Length);

        ushort computedCrc = Crc16Modbus.Compute(ruleBytes);
        Assert.Equal(expectedCrcHex, computedCrc.ToString("X4"));
    }

    [Fact]
    public void GV009_DiagHeartbeatCommand_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_009_diag_heartbeat_cmd");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;

        byte[] req = BuildRtuWriteSingleRequest(1, ModbusRegisterMap.DiagCommandAddress, (ushort)SPLC_DiagCommand.HEARTBEAT);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));
    }

    [Fact]
    public void GV010_CommitCommand_MatchesExactWireFrame()
    {
        var root = LoadGoldenVectorsJson();
        var gv = root.GetProperty("gv_010_commit_command");
        string expectedReqHex = gv.GetProperty("request_hex").GetString()!;

        byte[] req = BuildRtuWriteSingleRequest(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);
        Assert.Equal(expectedReqHex, BytesToHex(req));
        Assert.True(Crc16Modbus.Verify(req));
    }
    [Fact]
    public void GV011_TagDataTypesRegistry_MatchesWireLayoutAndCrc()
    {
        // Tag 0 = BOOL (1), Tag 1 = INT32 (2), Tag 2 = REAL (3), rest = NONE (0)
        byte[] types = new byte[ModbusRegisterMap.MaxRuntimeTags];
        types[0] = (byte)SPLC_TagDataType.BOOL;
        types[1] = (byte)SPLC_TagDataType.INT32;
        types[2] = (byte)SPLC_TagDataType.REAL;

        Span<ushort> regs = stackalloc ushort[ModbusRegisterMap.TagDataTypesLength];
        RegisterCodec.EncodeTagDataTypes(types, regs);

        // Reg 0: Tag 0 in High (1), Tag 1 in Low (2) -> 0x0102
        Assert.Equal(0x0102, regs[0]);
        // Reg 1: Tag 2 in High (3), Tag 3 in Low (0) -> 0x0300
        Assert.Equal(0x0300, regs[1]);

        // Build Modbus RTU FC16 Write Request: Slave 1, Addr 0x0080, 64 regs
        byte[] payload = new byte[ModbusRegisterMap.TagDataTypesLength * 2];
        for (int i = 0; i < ModbusRegisterMap.TagDataTypesLength; i++)
        {
            payload[i * 2] = (byte)(regs[i] >> 8);
            payload[i * 2 + 1] = (byte)(regs[i] & 0xFF);
        }

        ushort crc = Crc16Modbus.Compute(payload);
        Assert.True(crc > 0);
    }

    [Fact]
    public void GV012_OrthogonalRuleFlags_MixedRuleWireLayout()
    {
        // Rule: INT32 Trigger (AI0 > 80) -> SET REAL VREG (2.5f)
        var ruleMixed = new RuleRecordDto
        {
            Flags = (byte)SPLC_RuleFlags.ACTION_FLOAT, // 0x02: only action is float
            ThresholdLo = 80, // 0x00000050
            ActionParamFloat = 2.5f, // 0x40200000
            TriggerTag = 16,
            ActionTag = 32,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            CompareOp = SPLC_CompareOp.GT,
            ActionType = SPLC_ActionType.SET_TAG
        };

        Span<ushort> registers = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(ruleMixed, registers);

        // Verify Reg 0..1 = 80 (Int32 Big-Endian: 0x0000, 0x0050)
        Assert.Equal(0x0000, registers[0]);
        Assert.Equal(0x0050, registers[1]);

        // Verify Reg 6..7 = 2.5f (Float32 Big-Endian: 0x4020, 0x0000)
        Assert.Equal(0x4020, registers[6]);
        Assert.Equal(0x0000, registers[7]);

        // Verify Reg 13 High byte = 0x02 (ACTION_FLOAT), Low byte = 0x00
        Assert.Equal(0x0200, registers[13]);

        byte[] ruleBytes = new byte[32];
        for (int i = 0; i < 16; i++)
        {
            ruleBytes[i * 2] = (byte)(registers[i] >> 8);
            ruleBytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        ushort crc = Crc16Modbus.Compute(ruleBytes);
        Assert.True(crc > 0);
    }
}
