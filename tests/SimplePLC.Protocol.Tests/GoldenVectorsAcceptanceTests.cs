using System;
using System.Collections.Generic;
using System.Text;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;
using Xunit.Abstractions;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Bộ kiểm thử sinh và xác minh Golden Vectors (Hex Dumps) theo chuẩn Hợp đồng Giao thức V2.0.
/// Cung cấp các chuỗi hex chuẩn tham chiếu cho team Firmware (board_dev_gw):
/// 1. MB_READ (Modbus Master Read Block, 64 thanh ghi = 128 bytes tại 0x1000)
/// 2. MB_WRITE (Modbus Master Write Block, 64 thanh ghi = 128 bytes tại 0x1100 - Direct & ChannelIndexed)
/// 3. MQTT SUB (MQTT Subscription Block, 64 thanh ghi = 128 bytes tại 0x0D80)
/// 4. DIRECT_WRITE / PULSE_OUT (Direct Actuator Block, 16 thanh ghi = 32 bytes tại 0x1200)
/// </summary>
public sealed class GoldenVectorsAcceptanceTests
{
    private readonly ITestOutputHelper _output;

    public GoldenVectorsAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateAndVerify_MbReadGoldenVector_WithRealPin()
    {
        // Arrange: Khối MB_READ đọc 2 thanh ghi từ Slave ID 2, Function 0x03, Địa chỉ 0x0100
        // Chu kỳ 500ms, Timeout 1000ms. Pin 0: kiểu Real (Float32), Offset 0, Multiplier 1.0, nhãn "Temp"
        var config = new ModbusMasterReadConfigDto
        {
            Enabled = true,
            SlaveId = 2,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = 0x0100,
            RegCount = 2,
            PollIntervalMs = 500,
            TimeoutMs = 1000,
            ValidTagIndex = 0x0010, // Tag 16 nhận cờ Valid
            ErrorTagIndex = 0x0011, // Tag 17 nhận cờ Error
            ErrCodeTagIndex = 0x0012, // Tag 18 nhận ErrCode
            Pins = new List<ModbusMasterPinBindingDto>
            {
                new()
                {
                    Id = "Pin_0",
                    Label = "Temp",
                    RegOffset = 0,
                    DataType = ModbusDataType.Real,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 1.0f,
                    TargetTagIndex = 0x0020 // Tag 32 nhận giá trị
                }
            }
        };

        var registers = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeBlock(config, registers);

        // Assert: Xác thực cấu trúc Header
        Assert.Equal(0x0001, registers[0]); // Control: Enabled = 1
        Assert.Equal(0x0302, registers[1]); // FC 0x03, Slave ID 2
        Assert.Equal(0x0100, registers[2]); // StartAddress = 0x0100
        Assert.Equal(0x0002, registers[3]); // RegCount = 2
        Assert.Equal(0x0000, registers[4]); // PollInterval High Word (500 = 0x000001F4)
        Assert.Equal(0x01F4, registers[5]); // PollInterval Low Word
        Assert.Equal(0x0000, registers[6]); // Timeout High Word (1000 = 0x000003E8)
        Assert.Equal(0x03E8, registers[7]); // Timeout Low Word
        Assert.Equal(0x0010, registers[8]); // Valid tag = 16
        Assert.Equal(0x0011, registers[9]); // Error tag = 17
        Assert.Equal(0x0012, registers[10]); // ErrCode tag = 18
        Assert.Equal(0x0001, registers[11]); // Pin count = 1
        Assert.Equal(0x0000, registers[12]); // ReadMode = 0 (Cyclic)
        Assert.Equal(0xFFFF, registers[13]); // TrigTagIndex = 0xFFFF (None)
        Assert.Equal(0x0000, registers[14]); // Reserved
        Assert.Equal(0x0000, registers[15]); // Reserved

        // Assert: Pin 0 tại offset +16
        Assert.Equal(0x0020, registers[16]); // TargetTag = 32
        Assert.Equal(0x0000, registers[17]); // RegOffset = 0
        Assert.Equal(0x0006, registers[18]); // DataType = 6 (Real)
        Assert.Equal(0x0000, registers[19]); // Endianness = 0 (Big-Endian ABCD)
        Assert.Equal(0x3F80, registers[20]); // Multiplier 1.0f IEEE-754 High Word (0x3F80)
        Assert.Equal(0x0000, registers[21]); // Multiplier 1.0f IEEE-754 Low Word (0x0000)

        string hexDump = FormatRegistersHex(registers);
        _output.WriteLine("=== GOLDEN VECTOR: MB_READ (128 BYTES / 64 REGISTERS) ===");
        _output.WriteLine(hexDump);
    }

    [Fact]
    public void GenerateAndVerify_MbReadGoldenVector_OnTriggerRisingEdge()
    {
        var config = new ModbusMasterReadConfigDto
        {
            Enabled = true,
            SlaveId = 1,
            FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters,
            StartAddress = 0x0020,
            RegCount = 1,
            ReadMode = ModbusReadMode.OnTriggerRisingEdge,
            TrigTagIndex = 0x0015, // Tag 21
            PollIntervalMs = 0,
            TimeoutMs = 500,
            ValidTagIndex = 0x0010,
            ErrorTagIndex = 0x0011,
            ErrCodeTagIndex = 0x0012,
            Pins = new List<ModbusMasterPinBindingDto>
            {
                new()
                {
                    Id = "Pin_0",
                    Label = "Pressure",
                    RegOffset = 0,
                    DataType = ModbusDataType.UInt16,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 0.5f,
                    TargetTagIndex = 0x0030
                }
            }
        };

        var registers = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeBlock(config, registers);

        // Control & Mode
        Assert.Equal(0x0001, registers[0]);
        Assert.Equal(0x0401, registers[1]); // FC 0x04, Slave ID 1
        Assert.Equal(0x0020, registers[2]); // StartAddress
        Assert.Equal(0x0001, registers[3]); // RegCount
        Assert.Equal(0x0000, registers[4]); // PollInterval High Word (0)
        Assert.Equal(0x0000, registers[5]); // PollInterval Low Word (0)
        Assert.Equal(0x0000, registers[6]); // Timeout High Word
        Assert.Equal(0x01F4, registers[7]); // Timeout Low Word (500)
        Assert.Equal(0x0010, registers[8]); // Valid
        Assert.Equal(0x0011, registers[9]); // Error
        Assert.Equal(0x0012, registers[10]); // ErrCode
        Assert.Equal(0x0001, registers[11]); // Pin count = 1
        Assert.Equal(0x0001, registers[12]); // ReadMode = 1 (OnTriggerRisingEdge)
        Assert.Equal(0x0015, registers[13]); // TrigTagIndex = 0x0015 (Tag 21)
        Assert.Equal(0x0000, registers[14]);
        Assert.Equal(0x0000, registers[15]);


        string hexDump = FormatRegistersHex(registers);
        _output.WriteLine("=== GOLDEN VECTOR: MB_READ ON_TRIGGER (128 BYTES) ===");
        _output.WriteLine(hexDump);
    }


    [Fact]
    public void GenerateAndVerify_MbWriteGoldenVectors_DirectAndIndexed()
    {
        // 1. Chế độ Direct: Slave 3, FC 0x10, Addr 0x0050, 2 registers
        var directConfig = new ModbusMasterWriteConfigDto
        {
            Enabled = true,
            SlaveId = 3,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            WriteMode = ModbusWriteMode.DirectRegisters,
            IsExclusiveChannel = false,
            StartAddress = 0x0050,
            PointCount = 2,
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
            TrigTagIndex = 0x0005, // Tag 5 kích hoạt
            DoneTagIndex = null,   // Không bind -> ghi 0xFFFF
            BusyTagIndex = null,
            ErrorTagIndex = null,
            ErrCodeTagIndex = null,
            Pins = new List<ModbusMasterPinBindingDto>
            {
                new()
                {
                    Id = "Pin_0",
                    Label = "OutSp",
                    RegOffset = 0,
                    DataType = ModbusDataType.Int16,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 1.0f,
                    TargetTagIndex = 0x0015 // Nguồn dữ liệu từ Tag 21
                }
            }
        };

        var directRegs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeWriteBlock(directConfig, directRegs);

        Assert.Equal(0x0001, directRegs[0]); // Bit 0=1 (Enabled), Bit 2..3=0 (DirectRegisters)
        Assert.Equal(0x1003, directRegs[1]); // FC 0x10, Slave ID 3
        Assert.Equal(0x0050, directRegs[2]); // StartAddress = 0x0050
        Assert.Equal(0x0002, directRegs[3]); // PointCount = 2
        Assert.Equal(0x0000, directRegs[10]); // TriggerMode = 0 (OnTriggerRisingEdge)
        Assert.Equal(0x0005, directRegs[11]); // TrigTag = 5
        Assert.Equal(0xFFFF, directRegs[12]); // DoneTag = 0xFFFF
        Assert.Equal(0xFFFF, directRegs[13]); // BusyTag = 0xFFFF
        Assert.Equal(0xFFFF, directRegs[14]); // ErrorTag = 0xFFFF
        Assert.Equal(0xFFFF, directRegs[15]); // ErrCodeTag = 0xFFFF

        _output.WriteLine("=== GOLDEN VECTOR: MB_WRITE DIRECT MODE ===");
        _output.WriteLine(FormatRegistersHex(directRegs));

        // 2. Chế độ ChannelIndexed: WriteMode = 1 (ChannelIndexed)
        var indexedConfig = new ModbusMasterWriteConfigDto
        {
            Enabled = true,
            SlaveId = 1,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            IsExclusiveChannel = false,
            BaseAddressCommand = 0x0200,
            ChannelCount = 8,
            BaseAddressParam = 0x0120,
            TriggerMode = ModbusWriteTriggerMode.Cyclic,
            CyclicIntervalMs = 250,
            TimeoutMs = 1000,
            TrigTagIndex = null,
            DoneTagIndex = null,
            BusyTagIndex = null,
            ErrorTagIndex = null,
            ErrCodeTagIndex = null
        };

        var indexedRegs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeWriteBlock(indexedConfig, indexedRegs);

        Assert.Equal(0x0005, indexedRegs[0]); // Bit 0=1 (Enabled), Bit 2..3=1 (ChannelIndexed: 1 << 2 = 4 -> 1+4=5)
        Assert.Equal(0x1001, indexedRegs[1]); // FC 0x10, Slave 1
        Assert.Equal(0x0200, indexedRegs[2]); // BaseAddressCommand = 0x0200
        Assert.Equal(0x0008, indexedRegs[3]); // ChannelCount = 8
        Assert.Equal(0x0000, indexedRegs[4]); // BaseAddressParam High Word (0x0000)
        Assert.Equal(0x0120, indexedRegs[5]); // BaseAddressParam Low Word (0x0120)
        Assert.Equal(0x0000, indexedRegs[6]); // Cyclic High Word
        Assert.Equal(0x00FA, indexedRegs[7]); // Cyclic Low Word (250ms = 0x00FA)

        _output.WriteLine("=== GOLDEN VECTOR: MB_WRITE CHANNEL-INDEXED MODE ===");
        _output.WriteLine(FormatRegistersHex(indexedRegs));
    }

    [Fact]
    public void GenerateAndVerify_MqttSubGoldenVector_WithFailSafeActions()
    {
        // Arrange: Khối MQTT SUB đăng ký topic "plc/commands", timeout 5000ms
        // FailSafe = ResetToDefault (Code 2), QoS = 1, 2 Pins
        var config = new MqttSubscriptionConfigDto
        {
            Topic = "plc/commands",
            Qos = 1,
            QueueMode = MqttQueueMode.LatestWins,
            IgnoreRetainedOnBoot = true,
            FailSafeAction = MqttFailSafeAction.ResetToDefault,
            TimeoutMs = 5000,
            Pins = new List<MqttPinBindingDto>
            {
                new()
                {
                    Id = "Pin_0",
                    Label = "StartCmd",
                    JsonPath = "start",
                    DataType = MqttDataType.Bool,
                    DefaultValue = 0,
                    TargetTagIndex = 0x0001 // Tag 1 (VFLAG1)
                },
                new()
                {
                    Id = "Pin_1",
                    Label = "SetSpeed",
                    JsonPath = "speed",
                    DataType = MqttDataType.Float,
                    DefaultValue = 1500.5f,
                    TargetTagIndex = 0x0025 // Tag 37 (VREG5)
                }
            }
        };

        var registers = new ushort[ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
        MqttSubscriptionCodec.EncodeBlock(config, registers);

        // Control flags check:
        // Bit 0: Enabled = 1
        // Bit 1..2: QoS = 1 (1 << 1 = 2)
        // Bit 4: IgnoreRetained = 1 (1 << 4 = 16)
        // Bit 5..6: FailSafe = 2 (ResetToDefault: 2 << 5 = 64)
        // Tổng: 1 + 2 + 16 + 64 = 83 = 0x0053
        Assert.Equal(0x0053, registers[0]);
        Assert.Equal(0x0000, registers[1]); // Reserved
        Assert.Equal(0x0000, registers[2]); // Timeout High Word (5000 = 0x00001388)
        Assert.Equal(0x1388, registers[3]); // Timeout Low Word
        Assert.Equal(0x0002, registers[4]); // PinCount = 2

        // Pin 0 (offset 24): Target = 1, Type = 1 (Bool), Default = 0
        Assert.Equal(0x0001, registers[24]);
        Assert.Equal(0x0001, registers[25]); // Bool
        Assert.Equal(0x0000, registers[26]);
        Assert.Equal(0x0000, registers[27]);

        // Pin 1 (offset 34): Target = 37, Type = 3 (Float), Default = 1500.5f (0x44BB9000)
        Assert.Equal(0x0025, registers[34]);
        Assert.Equal(0x0003, registers[35]); // Float
        Assert.Equal(0x44BB, registers[36]); // Default High Word
        Assert.Equal(0x9000, registers[37]); // Default Low Word

        _output.WriteLine("=== GOLDEN VECTOR: MQTT SUB BLOCK (128 BYTES / 64 REGISTERS) ===");
        _output.WriteLine(FormatRegistersHex(registers));
    }

    [Fact]
    public void GenerateAndVerify_DirectWritePulseOutGoldenVector_BigEndianWordOrder()
    {
        // Arrange: Chế độ PWM trên chân DO0: Tần số 1000 Hz, Duty 0..100%, Active-High, Channel Tag 20
        var config = new DirectWriteConfigDto
        {
            TargetPin = DirectWriteTargetPin.Do0,
            Mode = DirectWriteMode.PwmDuty,
            ChannelCount = 1,
            IsExclusiveChannel = false,
            TriggerMode = DirectWriteTriggerMode.Continuous,
            PulseWidthUs = 50,
            PwmFrequencyHz = 1000, // 0x000003E8
            ChannelTagIndex = 20,
            ParamTagIndex = null,
            DefaultColorRgb = 0x00FF8800
        };

        var registers = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
        DirectWriteCodec.EncodeBlock(config, registers);

        // Reg 0: (Mode << 8) | TargetPin -> (1 << 8) | 0 = 0x0100
        Assert.Equal(0x0100, registers[0]);
        // Reg 1: ChannelCount = 1
        Assert.Equal(0x0001, registers[1]);
        // Reg 2: (TriggerMode << 8) | Flags -> (1 << 8) | 0x01 (active-high) = 0x0101
        Assert.Equal(0x0101, registers[2]);
        // Reg 3: PulseWidth = 50
        Assert.Equal(0x0032, registers[3]);

        // Reg 4..5: pwm_freq_hz (Big-Endian Word Order)
        // 1000 = 0x000003E8 -> Reg 4 = High Word (0x0000), Reg 5 = Low Word (0x03E8)
        Assert.Equal(0x0000, registers[4]);
        Assert.Equal(0x03E8, registers[5]);

        // Reg 6: (duty_max << 8) | duty_min -> (100 << 8) | 0 = (0x64 << 8) | 0 = 0x6400
        Assert.Equal(0x6400, registers[6]);
        // Reg 7: channel_tag_index = 20
        Assert.Equal(0x0014, registers[7]);
        // Reg 8: param_tag_index = 0xFFFF
        Assert.Equal(0xFFFF, registers[8]);
        // Reg 9..10: default_color_rgb (0x00FF8800) -> High 0x00FF, Low 0x8800
        Assert.Equal(0x00FF, registers[9]);
        Assert.Equal(0x8800, registers[10]);

        _output.WriteLine("=== GOLDEN VECTOR: DIRECT_WRITE / PULSE_OUT (32 BYTES / 16 REGISTERS) ===");
        _output.WriteLine(FormatRegistersHex(registers));
    }

    private static string FormatRegistersHex(ReadOnlySpan<ushort> regs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < regs.Length; i++)
        {
            if (i % 8 == 0)
            {
                if (i > 0) sb.AppendLine();
                sb.Append($"[+{i:D2}..+{i + 7:D2}]: ");
            }
            sb.Append($"{regs[i]:X4} ");
        }
        return sb.ToString();
    }
}
