using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec đóng gói và giải mã các khối MQTT Subscribe theo đặc tả SPLC-SPEC-MQTT-002.
/// Mỗi khối chiếm đúng 64 thanh ghi (128 bytes):
/// - Offset +0..+7: Cờ điều khiển, Timeout FailSafe, Số chân Pin (8 thanh ghi)
/// - Offset +8..+23: Chuỗi MQTT Topic (16 thanh ghi = 32 bytes ASCII)
/// - Offset +24..+63: 4 Khối Pin Bindings (4 pins * 10 thanh ghi = 40 thanh ghi)
/// </summary>
public static class MqttSubscriptionCodec
{
    /// <summary>
    /// Mã hóa một MqttSubscriptionConfigDto thành 64 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeBlock(MqttSubscriptionConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.MqttSubscribeRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.MqttSubscribeRegistersPerBlock} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.MqttSubscribeRegistersPerBlock).Fill(0);

        // 1. Đóng gói CONTROL_FLAGS (Offset +0)
        ushort control = ModbusRegisterMap.MqttSubControlBitEnabled;
        control |= (ushort)(((config.Qos & 0x03) << ModbusRegisterMap.MqttSubControlQosShift) & ModbusRegisterMap.MqttSubControlQosMask);

        if (config.QueueMode == MqttQueueMode.Fifo)
        {
            control |= ModbusRegisterMap.MqttSubControlBitQueueFifo;
        }

        if (config.IgnoreRetainedOnBoot)
        {
            control |= ModbusRegisterMap.MqttSubControlBitIgnoreRetain;
        }

        int failSafeVal = config.FailSafeAction switch
        {
            MqttFailSafeAction.ResetToDefault => 2,
            _ => 0
        };
        control |= (ushort)((failSafeVal << ModbusRegisterMap.MqttSubControlFailSafeShift) & ModbusRegisterMap.MqttSubControlFailSafeMask);

        destination[0] = control;

        // Offset +1: RESERVED
        destination[1] = 0x0000;

        // Offset +2..+3: FAILSAFE_TIMEOUT_MS (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.TimeoutMs, destination.Slice(2, 2));

        // Offset +4: PIN_COUNT (0..4)
        var pins = config.Pins ?? new List<MqttPinBindingDto>();
        ushort pinCount = (ushort)Math.Clamp(pins.Count, 0, ModbusRegisterMap.MqttSubscribeMaxPinsPerBlock);
        destination[4] = pinCount;

        // Offset +5..+7: RESERVED_PINS
        destination[5] = 0;
        destination[6] = 0;
        destination[7] = 0;

        // Offset +8..+23: TOPIC (32 bytes ASCII = 16 thanh ghi)
        EncodePackedAscii(config.Topic, destination.Slice(8, 16), ModbusRegisterMap.MqttTopicMaxChars);

        // Offset +24..+63: 4 PIN BINDINGS (mỗi pin 10 thanh ghi = 20 bytes)
        for (int i = 0; i < ModbusRegisterMap.MqttSubscribeMaxPinsPerBlock; i++)
        {
            int pinOffset = 24 + (i * 10);
            var pinSpan = destination.Slice(pinOffset, 10);

            if (i < pins.Count)
            {
                var pin = pins[i];

                // +0: TARGET_TAG_INDEX
                pinSpan[0] = pin.TargetTagIndex ?? 0xFFFF;

                // +1: DATA_TYPE (1=BOOL, 2=INT32, 3=REAL)
                ushort dt = pin.DataType switch
                {
                    MqttDataType.Bool => 1,
                    MqttDataType.Int16 or MqttDataType.Int32 => 2,
                    MqttDataType.Float => 3,
                    _ => 2
                };
                pinSpan[1] = dt;

                // +2..+3: DEFAULT_VALUE_RAW (32-bit)
                if (dt == 3)
                {
                    EncodeFloat((float)pin.DefaultValue, pinSpan.Slice(2, 2));
                }
                else
                {
                    uint intRaw = (uint)(int)pin.DefaultValue;
                    RegisterCodec.EncodeUInt32(intRaw, pinSpan.Slice(2, 2));
                }

                // +4..+9: JSON_KEY (12 bytes ASCII = 6 thanh ghi)
                string key = !string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.JsonPath : (!string.IsNullOrWhiteSpace(pin.Label) ? pin.Label : pin.Id);
                EncodePackedAscii(key, pinSpan.Slice(4, 6), ModbusRegisterMap.MqttSubscribeJsonKeyMaxChars);
            }
            else
            {
                // Slot chưa dùng: Target = 0xFFFF
                pinSpan[0] = 0xFFFF;
            }
        }
    }

    /// <summary>
    /// Giải mã 64 thanh ghi Modbus thành đối tượng MqttSubscriptionConfigDto.
    /// </summary>
    public static MqttSubscriptionConfigDto DecodeBlock(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.MqttSubscribeRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.MqttSubscribeRegistersPerBlock} registers.", nameof(source));

        ushort control = source[0];
        int qos = (control & ModbusRegisterMap.MqttSubControlQosMask) >> ModbusRegisterMap.MqttSubControlQosShift;
        bool isFifo = (control & ModbusRegisterMap.MqttSubControlBitQueueFifo) != 0;
        bool ignoreRetained = (control & ModbusRegisterMap.MqttSubControlBitIgnoreRetain) != 0;
        int failSafeCode = (control & ModbusRegisterMap.MqttSubControlFailSafeMask) >> ModbusRegisterMap.MqttSubControlFailSafeShift;

        var failSafeAction = failSafeCode == 2 ? MqttFailSafeAction.ResetToDefault : MqttFailSafeAction.KeepLastValue;

        uint timeoutMs = RegisterCodec.DecodeUInt32(source.Slice(2, 2));
        ushort declaredPinCount = source[4];

        string topic = DecodePackedAscii(source.Slice(8, 16));

        var pins = new List<MqttPinBindingDto>();
        int pinCount = Math.Clamp((int)declaredPinCount, 0, ModbusRegisterMap.MqttSubscribeMaxPinsPerBlock);

        for (int i = 0; i < pinCount; i++)
        {
            int pinOffset = 24 + (i * 10);
            var pinSpan = source.Slice(pinOffset, 10);

            ushort targetTag = pinSpan[0];
            if (targetTag == 0xFFFF && i >= declaredPinCount) break;

            ushort dtCode = pinSpan[1];
            var dt = dtCode switch
            {
                1 => MqttDataType.Bool,
                3 => MqttDataType.Float,
                _ => MqttDataType.Int32
            };

            double defVal;
            if (dt == MqttDataType.Float)
            {
                defVal = DecodeFloat(pinSpan.Slice(2, 2));
            }
            else
            {
                defVal = (int)RegisterCodec.DecodeUInt32(pinSpan.Slice(2, 2));
            }

            string jsonKey = DecodePackedAscii(pinSpan.Slice(4, 6));

            pins.Add(new MqttPinBindingDto
            {
                Id = !string.IsNullOrWhiteSpace(jsonKey) ? jsonKey : $"pin_{i}",
                Label = !string.IsNullOrWhiteSpace(jsonKey) ? jsonKey : $"Pin {i}",
                JsonPath = jsonKey,
                DataType = dt,
                TargetTagIndex = targetTag != 0xFFFF ? targetTag : null,
                DefaultValue = defVal
            });
        }

        return new MqttSubscriptionConfigDto
        {
            Topic = topic,
            Qos = qos,
            QueueMode = isFifo ? MqttQueueMode.Fifo : MqttQueueMode.LatestWins,
            IgnoreRetainedOnBoot = ignoreRetained,
            TimeoutMs = timeoutMs,
            FailSafeAction = failSafeAction,
            Pins = pins
        };
    }

    private static void EncodePackedAscii(string? text, Span<ushort> destination, int maxChars)
    {
        destination.Fill(0);
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
        int maxCapacity = Math.Min(maxChars, destination.Length * 2);
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
