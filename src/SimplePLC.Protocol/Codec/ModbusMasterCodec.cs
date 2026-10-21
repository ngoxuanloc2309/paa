using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec đóng gói và giải mã cấu trúc cấu hình Modbus Master RTU theo đặc tả SPLC-SPEC-MBM-001.
/// Mỗi block chiếm đúng 64 thanh ghi (128 bytes):
/// - Offset +0..+15: Header cấu hình khối (16 thanh ghi = 32 bytes)
/// - Offset +16..+63: 4 Khối Pin Bindings (4 pins * 12 thanh ghi = 48 thanh ghi = 96 bytes)
/// </summary>
public static class ModbusMasterCodec
{
    /// <summary>
    /// Mã hóa ModbusMasterReadConfigDto thành 64 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeBlock(ModbusMasterReadConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.ModbusMasterRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.ModbusMasterRegistersPerBlock} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.ModbusMasterRegistersPerBlock).Fill(0);

        // 1. Header (Offset +0..+15)
        // Offset +0: CONTROL_FLAGS (Bit 0 = Enabled, Bit 4..7 = PortIndex)
        ushort control = config.Enabled ? (ushort)0x0001 : (ushort)0x0000;
        control |= (ushort)((config.PortIndex & 0x0F) << ModbusRegisterMap.ModbusMasterControlPortShift);
        destination[0] = control;

        // Offset +1: FUNCTION_CODE (High Byte) | SLAVE_ID (Low Byte)
        byte fc = (byte)config.FunctionCode;
        byte slaveId = config.SlaveId;
        destination[1] = (ushort)((fc << 8) | slaveId);

        // Offset +2: START_ADDRESS
        destination[2] = config.StartAddress;

        // Offset +3: REG_COUNT
        destination[3] = config.RegCount;

        // Offset +4..+5: POLL_INTERVAL_MS (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.PollIntervalMs, destination.Slice(4, 2));

        // Offset +6..+7: TIMEOUT_MS (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.TimeoutMs, destination.Slice(6, 2));

        // Offset +8: VALID_TAG_INDEX
        destination[8] = config.ValidTagIndex ?? 0xFFFF;

        // Offset +9: ERROR_TAG_INDEX
        destination[9] = config.ErrorTagIndex ?? 0xFFFF;

        // Offset +10: ERR_CODE_TAG_INDEX
        destination[10] = config.ErrCodeTagIndex ?? 0xFFFF;

        // Offset +11: PIN_COUNT (0..4)
        var pins = config.Pins ?? new List<ModbusMasterPinBindingDto>();
        ushort pinCount = (ushort)Math.Clamp(pins.Count, 0, ModbusRegisterMap.ModbusMasterMaxPinsPerBlock);
        destination[11] = pinCount;

        // Offset +12: READ_MODE (0 = Cyclic, 1 = OnTriggerRisingEdge)
        destination[12] = (ushort)config.ReadMode;

        // Offset +13: TRIG_TAG_INDEX
        destination[13] = config.TrigTagIndex ?? 0xFFFF;

        // Offset +14: PORT_INDEX (Cổng Modbus Master 0..3)
        destination[14] = config.PortIndex;

        // Offset +15: RESERVED
        destination[15] = 0;

        // 2. Pin Bindings (Offset +16..+63, mỗi pin 12 thanh ghi = 24 bytes)
        for (int i = 0; i < ModbusRegisterMap.ModbusMasterMaxPinsPerBlock; i++)
        {
            int pinOffset = 16 + (i * 12);
            var pinSpan = destination.Slice(pinOffset, 12);

            if (i < pins.Count)
            {
                var pin = pins[i];

                // +0: TARGET_TAG_INDEX
                pinSpan[0] = pin.TargetTagIndex ?? 0xFFFF;

                // +1: REG_OFFSET
                pinSpan[1] = pin.RegOffset;

                // +2: DATA_TYPE
                pinSpan[2] = (ushort)pin.DataType;

                // +3: ENDIANNESS
                pinSpan[3] = (ushort)pin.Endianness;

                // +4..+5: MULTIPLIER (float 32-bit IEEE 754)
                EncodeFloat(pin.Multiplier, pinSpan.Slice(4, 2));

                // +6..+11: PIN_LABEL (12 bytes ASCII = 6 thanh ghi)
                EncodePackedAscii(pin.Label ?? string.Empty, pinSpan.Slice(6, 6), ModbusRegisterMap.ModbusMasterPinLabelMaxChars);
            }
            else
            {
                pinSpan.Fill(0);
                pinSpan[0] = 0xFFFF; // Unmapped sentinel
            }
        }
    }

    /// <summary>
    /// Giải mã 64 thanh ghi Modbus thành ModbusMasterReadConfigDto.
    /// </summary>
    public static ModbusMasterReadConfigDto DecodeBlock(ReadOnlySpan<ushort> source, int blockIndex)
    {
        if (source.Length < ModbusRegisterMap.ModbusMasterRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.ModbusMasterRegistersPerBlock} registers.", nameof(source));

        byte portIdx = (byte)((source[0] >> ModbusRegisterMap.ModbusMasterControlPortShift) & 0x0F);
        if (portIdx == 0 && source[14] > 0 && source[14] < ModbusRegisterMap.ModbusPortConfigMaxPorts)
        {
            portIdx = (byte)source[14];
        }

        var dto = new ModbusMasterReadConfigDto
        {
            BlockIndex = blockIndex,
            PortIndex = portIdx,
            Enabled = (source[0] & 0x0001) != 0,
            SlaveId = (byte)(source[1] & 0xFF),
            FunctionCode = (ModbusMasterFunctionCode)(source[1] >> 8),
            StartAddress = source[2],
            RegCount = source[3],
            PollIntervalMs = RegisterCodec.DecodeUInt32(source.Slice(4, 2)),
            TimeoutMs = RegisterCodec.DecodeUInt32(source.Slice(6, 2)),
            ValidTagIndex = source[8] == 0xFFFF ? null : source[8],
            ErrorTagIndex = source[9] == 0xFFFF ? null : source[9],
            ErrCodeTagIndex = source[10] == 0xFFFF ? null : source[10],
            ReadMode = Enum.IsDefined(typeof(ModbusReadMode), (ModbusReadMode)source[12]) ? (ModbusReadMode)source[12] : ModbusReadMode.Cyclic,
            TrigTagIndex = source[13] == 0xFFFF ? null : source[13]
        };

        if (dto.FunctionCode < ModbusMasterFunctionCode.ReadCoils || dto.FunctionCode > ModbusMasterFunctionCode.ReadInputRegisters)
        {
            dto.FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters;
        }

        ushort pinCount = Math.Min(source[11], (ushort)ModbusRegisterMap.ModbusMasterMaxPinsPerBlock);
        for (int i = 0; i < pinCount; i++)
        {
            int pinOffset = 16 + (i * 12);
            var pinSpan = source.Slice(pinOffset, 12);

            ushort targetTag = pinSpan[0];
            ushort regOffset = pinSpan[1];
            var dt = (ModbusDataType)pinSpan[2];
            var endianness = (ModbusEndianness)pinSpan[3];
            float multiplier = DecodeFloat(pinSpan.Slice(4, 2));
            string label = DecodePackedAscii(pinSpan.Slice(6, 6)).Trim();

            dto.Pins.Add(new ModbusMasterPinBindingDto
            {
                Id = $"Pin_{i}",
                Label = label,
                RegOffset = regOffset,
                DataType = Enum.IsDefined(typeof(ModbusDataType), dt) ? dt : ModbusDataType.Int16,
                Endianness = Enum.IsDefined(typeof(ModbusEndianness), endianness) ? endianness : ModbusEndianness.BigEndian_ABCD,
                Multiplier = multiplier == 0.0f ? 1.0f : multiplier,
                TargetTagIndex = targetTag == 0xFFFF ? null : targetTag
            });
        }

        return dto;
    }

    /// <summary>
    /// Mã hóa ModbusMasterWriteConfigDto thành 64 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeWriteBlock(ModbusMasterWriteConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.ModbusMasterRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.ModbusMasterRegistersPerBlock} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.ModbusMasterRegistersPerBlock).Fill(0);

        // 1. Header (Offset +0..+15)
        // Offset +0: CONTROL_FLAGS (Bit 0 = Enabled, Bit 1 = IsExclusiveChannel, Bit 2..3 = WriteMode)
        ushort control = 0;
        if (config.Enabled) control |= 0x0001;
        if (config.IsExclusiveChannel) control |= 0x0002;
        control |= (ushort)(((byte)config.WriteMode & 0x03) << 2);
        control |= (ushort)((config.PortIndex & 0x0F) << ModbusRegisterMap.ModbusMasterControlPortShift);
        destination[0] = control;

        // Offset +1: FUNCTION_CODE (High Byte) | SLAVE_ID (Low Byte)
        byte fc = (byte)config.FunctionCode;
        byte slaveId = config.SlaveId;
        destination[1] = (ushort)((fc << 8) | slaveId);

        // Offset +2: START_ADDRESS / BASE_ADDRESS_COMMAND
        destination[2] = config.WriteMode == ModbusWriteMode.ChannelIndexed 
            ? config.BaseAddressCommand 
            : config.StartAddress;

        // Offset +3: POINT_COUNT / CHANNEL_COUNT
        destination[3] = config.WriteMode == ModbusWriteMode.ChannelIndexed 
            ? config.ChannelCount 
            : config.PointCount;

        // Offset +4..+5: BASE_ADDRESS_PARAM (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.BaseAddressParam, destination.Slice(4, 2));

        // Offset +6..+7: CYCLIC_INTERVAL_MS (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.CyclicIntervalMs, destination.Slice(6, 2));

        // Offset +8..+9: TIMEOUT_MS (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.TimeoutMs, destination.Slice(8, 2));

        // Offset +10: TRIGGER_MODE
        destination[10] = (ushort)config.TriggerMode;

        // Offset +11: TRIG_TAG_INDEX
        destination[11] = config.TrigTagIndex ?? 0xFFFF;

        // Offset +12: DONE_TAG_INDEX
        destination[12] = config.DoneTagIndex ?? 0xFFFF;

        // Offset +13: BUSY_TAG_INDEX
        destination[13] = config.BusyTagIndex ?? 0xFFFF;

        // Offset +14: ERROR_TAG_INDEX
        destination[14] = config.ErrorTagIndex ?? 0xFFFF;

        // Offset +15: ERR_CODE_TAG_INDEX
        destination[15] = config.ErrCodeTagIndex ?? 0xFFFF;

        // 2. Data payload / Bindings (Offset +16..+63)
        if (config.WriteMode == ModbusWriteMode.ChannelIndexed)
        {
            destination[16] = config.ChannelTagIndex ?? 0xFFFF;
            destination[17] = config.CommandTagIndex ?? 0xFFFF;
            destination[18] = config.ParamTagIndex ?? 0xFFFF;
        }
        else
        {
            var pins = config.Pins ?? new List<ModbusMasterPinBindingDto>();
            for (int i = 0; i < ModbusRegisterMap.ModbusMasterMaxPinsPerBlock; i++)
            {
                int pinOffset = 16 + (i * 12);
                var pinSpan = destination.Slice(pinOffset, 12);

                if (i < pins.Count)
                {
                    var pin = pins[i];
                    pinSpan[0] = pin.TargetTagIndex ?? 0xFFFF;
                    pinSpan[1] = pin.RegOffset;
                    pinSpan[2] = (ushort)pin.DataType;
                    pinSpan[3] = (ushort)pin.Endianness;
                    EncodeFloat(pin.Multiplier, pinSpan.Slice(4, 2));
                    EncodePackedAscii(pin.Label ?? string.Empty, pinSpan.Slice(6, 6), ModbusRegisterMap.ModbusMasterPinLabelMaxChars);
                }
                else
                {
                    pinSpan[0] = 0xFFFF;
                }
            }
        }
    }

    /// <summary>
    /// Giải mã 64 thanh ghi Modbus thành ModbusMasterWriteConfigDto.
    /// </summary>
    public static ModbusMasterWriteConfigDto DecodeWriteBlock(ReadOnlySpan<ushort> source, int blockIndex = 0)
    {
        if (source.Length < ModbusRegisterMap.ModbusMasterRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.ModbusMasterRegistersPerBlock} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & 0x0001) != 0;
        bool isExclusive = (control & 0x0002) != 0;
        var writeMode = (ModbusWriteMode)((control >> 2) & 0x03);
        byte writePortIdx = (byte)((control >> ModbusRegisterMap.ModbusMasterControlPortShift) & 0x0F);

        ushort fcAndSlave = source[1];
        byte fc = (byte)(fcAndSlave >> 8);
        byte slaveId = (byte)(fcAndSlave & 0xFF);

        ushort startOrBaseCmd = source[2];
        ushort countOrChannels = source[3];
        ushort baseParam = (ushort)RegisterCodec.DecodeUInt32(source.Slice(4, 2));
        uint cyclicInterval = RegisterCodec.DecodeUInt32(source.Slice(6, 2));
        uint timeoutMs = RegisterCodec.DecodeUInt32(source.Slice(8, 2));
        var triggerMode = (ModbusWriteTriggerMode)source[10];

        ushort trigTag = source[11];
        ushort doneTag = source[12];
        ushort busyTag = source[13];
        ushort errorTag = source[14];
        ushort errCodeTag = source[15];

        var dto = new ModbusMasterWriteConfigDto
        {
            BlockIndex = blockIndex,
            PortIndex = writePortIdx,
            Enabled = enabled,
            IsExclusiveChannel = isExclusive,
            WriteMode = writeMode,
            SlaveId = slaveId == 0 ? (byte)1 : slaveId,
            FunctionCode = Enum.IsDefined(typeof(ModbusMasterWriteFunctionCode), fc) 
                ? (ModbusMasterWriteFunctionCode)fc 
                : ModbusMasterWriteFunctionCode.WriteSingleRegister,
            StartAddress = writeMode == ModbusWriteMode.DirectRegisters ? startOrBaseCmd : (ushort)0,
            PointCount = writeMode == ModbusWriteMode.DirectRegisters ? countOrChannels : (ushort)1,
            BaseAddressCommand = writeMode == ModbusWriteMode.ChannelIndexed ? startOrBaseCmd : (ushort)0x0100,
            ChannelCount = writeMode == ModbusWriteMode.ChannelIndexed ? countOrChannels : (ushort)9,
            BaseAddressParam = baseParam,
            CyclicIntervalMs = cyclicInterval == 0 ? 500 : cyclicInterval,
            TimeoutMs = timeoutMs == 0 ? 300 : timeoutMs,
            TriggerMode = triggerMode,
            TrigTagIndex = trigTag == 0xFFFF ? null : trigTag,
            DoneTagIndex = doneTag == 0xFFFF ? null : doneTag,
            BusyTagIndex = busyTag == 0xFFFF ? null : busyTag,
            ErrorTagIndex = errorTag == 0xFFFF ? null : errorTag,
            ErrCodeTagIndex = errCodeTag == 0xFFFF ? null : errCodeTag
        };

        if (writeMode == ModbusWriteMode.ChannelIndexed)
        {
            ushort chTag = source[16];
            ushort cmdTag = source[17];
            ushort paramTag = source[18];
            dto.ChannelTagIndex = chTag == 0xFFFF ? null : chTag;
            dto.CommandTagIndex = cmdTag == 0xFFFF ? null : cmdTag;
            dto.ParamTagIndex = paramTag == 0xFFFF ? null : paramTag;
        }
        else
        {
            for (int i = 0; i < ModbusRegisterMap.ModbusMasterMaxPinsPerBlock; i++)
            {
                int pinOffset = 16 + (i * 12);
                var pinSpan = source.Slice(pinOffset, 12);

                ushort targetTag = pinSpan[0];
                ushort regOffset = pinSpan[1];
                var dt = (ModbusDataType)pinSpan[2];
                var endianness = (ModbusEndianness)pinSpan[3];
                float multiplier = DecodeFloat(pinSpan.Slice(4, 2));
                string label = DecodePackedAscii(pinSpan.Slice(6, 6)).Trim();

                if (string.IsNullOrWhiteSpace(label) || targetTag == 0xFFFF)
                    continue;

                dto.Pins.Add(new ModbusMasterPinBindingDto
                {
                    Id = $"Pin_{i}",
                    Label = label,
                    RegOffset = regOffset,
                    DataType = Enum.IsDefined(typeof(ModbusDataType), dt) ? dt : ModbusDataType.Int16,
                    Endianness = Enum.IsDefined(typeof(ModbusEndianness), endianness) ? endianness : ModbusEndianness.BigEndian_ABCD,
                    Multiplier = multiplier == 0.0f ? 1.0f : multiplier,
                    TargetTagIndex = targetTag == 0xFFFF ? null : targetTag
                });
            }
        }

        return dto;
    }

    private static void EncodePackedAscii(string text, Span<ushort> destination, int maxCapacity)
    {
        destination.Fill(0);
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
        int len = Math.Min(bytes.Length, maxCapacity);
        int regCount = destination.Length;

        for (int i = 0; i < len; i += 2)
        {
            int regIdx = i / 2;
            if (regIdx >= regCount) break;

            byte high = bytes[i];
            byte low = (i + 1 < len) ? bytes[i + 1] : (byte)0;
            destination[regIdx] = (ushort)((high << 8) | low);
        }
    }

    private static string DecodePackedAscii(ReadOnlySpan<ushort> source)
    {
        var bytes = new List<byte>(source.Length * 2);
        foreach (var reg in source)
        {
            byte high = (byte)(reg >> 8);
            byte low = (byte)(reg & 0xFF);
            if (high == 0) break;
            bytes.Add(high);
            if (low == 0) break;
            bytes.Add(low);
        }
        return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void EncodeFloat(float value, Span<ushort> destination)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        destination[0] = (ushort)(bits >> 16);
        destination[1] = (ushort)(bits & 0xFFFF);
    }

    private static float DecodeFloat(ReadOnlySpan<ushort> source)
    {
        uint bits = ((uint)source[0] << 16) | source[1];
        return BitConverter.UInt32BitsToSingle(bits);
    }
}
