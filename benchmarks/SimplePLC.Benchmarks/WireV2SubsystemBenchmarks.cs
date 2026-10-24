using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class WireV2SubsystemBenchmarks
{
    private byte[] _tagDataTypes = null!;
    private ushort[] _tagRegistryRegisters = null!;

    private DirectWriteConfigDto[] _directWriteConfigs = null!;
    private ushort[] _directWriteRegisters = null!;

    private ModbusMasterReadConfigDto[] _modbusReadConfigs = null!;
    private ushort[] _modbusReadRegisters = null!;

    private MqttTelemetryConfigDto[] _mqttTelemetryConfigs = null!;
    private ushort[] _mqttTelemetryRegisters = null!;

    private MqttSubscriptionConfigDto[] _mqttSubConfigs = null!;
    private ushort[] _mqttSubRegisters = null!;

    private float[] _floatSampleValues = null!;
    private ushort[] _floatRegisters = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1. Tag Registry Setup (128 Tags: INT32, REAL, UINT32, BOOL)
        _tagDataTypes = new byte[ModbusRegisterMap.MaxRuntimeTags];
        for (int i = 0; i < ModbusRegisterMap.MaxRuntimeTags; i++)
        {
            _tagDataTypes[i] = (byte)(i % 4); // 0=INT32, 1=REAL, 2=UINT32, 3=BOOL
        }
        _tagRegistryRegisters = new ushort[ModbusRegisterMap.TagDataTypesLength]; // 64 registers (0x0080..0x00BF)
        RegisterCodec.EncodeTagDataTypes(_tagDataTypes, _tagRegistryRegisters);

        // 2. DirectWrite / Modbus Master Write (16 blocks)
        _directWriteConfigs = new DirectWriteConfigDto[16];
        _directWriteRegisters = new ushort[16 * ModbusRegisterMap.DirectWriteRegistersPerBlock];
        for (int i = 0; i < 16; i++)
        {
            _directWriteConfigs[i] = new DirectWriteConfigDto
            {
                Id = $"DW_{i}",
                Mode = DirectWriteMode.PulseTrain,
                TargetPin = (DirectWriteTargetPin)(i % 8),
                ChannelCount = 1,
                TriggerMode = DirectWriteTriggerMode.Continuous,
                PulseWidthUs = (ushort)(100 + i * 50),
                PwmFrequencyHz = (uint)(1000 + i * 100),
                ChannelTagIndex = (ushort)i,
                ParamTagIndex = (ushort)(16 + i)
            };
            DirectWriteCodec.EncodeBlock(_directWriteConfigs[i], _directWriteRegisters.AsSpan(i * ModbusRegisterMap.DirectWriteRegistersPerBlock, ModbusRegisterMap.DirectWriteRegistersPerBlock));
        }

        // 3. Modbus Master Read (16 blocks)
        _modbusReadConfigs = new ModbusMasterReadConfigDto[16];
        _modbusReadRegisters = new ushort[16 * ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        for (int i = 0; i < 16; i++)
        {
            var pins = new List<ModbusMasterPinBindingDto>
            {
                new() { Id = "P0", Label = "Volt_L1", RegOffset = 0, DataType = ModbusDataType.Real, TargetTagIndex = (ushort)(40 + i) },
                new() { Id = "P1", Label = "Current_L1", RegOffset = 2, DataType = ModbusDataType.Real, TargetTagIndex = (ushort)(41 + i) }
            };

            _modbusReadConfigs[i] = new ModbusMasterReadConfigDto
            {
                NodeId = $"MB_{i}",
                BlockIndex = i,
                PortIndex = (byte)(i % 2),
                SlaveId = (byte)(i + 1),
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = (ushort)(i * 10),
                RegCount = 4,
                PollIntervalMs = 500,
                TimeoutMs = 200,
                Pins = pins
            };
            ModbusMasterCodec.EncodeBlock(_modbusReadConfigs[i], _modbusReadRegisters.AsSpan(i * ModbusRegisterMap.ModbusMasterRegistersPerBlock, ModbusRegisterMap.ModbusMasterRegistersPerBlock));
        }

        // 4. MQTT Telemetry (16 blocks)
        _mqttTelemetryConfigs = new MqttTelemetryConfigDto[16];
        _mqttTelemetryRegisters = new ushort[16 * ModbusRegisterMap.MqttRegistersPerBlock];
        for (int i = 0; i < 16; i++)
        {
            _mqttTelemetryConfigs[i] = new MqttTelemetryConfigDto
            {
                NodeId = $"PUB_{i}",
                PublishMode = "Cyclic",
                PayloadMode = "JSON",
                Qos = 1,
                Retain = false,
                IntervalMs = 1000,
                Topic = $"factory/line1/sensor{i}",
                Fields = new List<MqttPayloadFieldDto>
                {
                    new() { Key = "v_avg", TagIndex = (ushort)i },
                    new() { Key = "temp", TagIndex = (ushort)(i + 1) }
                }
            };
            MqttTelemetryCodec.EncodeBlock(_mqttTelemetryConfigs[i], _mqttTelemetryRegisters.AsSpan(i * ModbusRegisterMap.MqttRegistersPerBlock, ModbusRegisterMap.MqttRegistersPerBlock));
        }

        // 5. MQTT Subscription (16 blocks)
        _mqttSubConfigs = new MqttSubscriptionConfigDto[16];
        _mqttSubRegisters = new ushort[16 * ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
        for (int i = 0; i < 16; i++)
        {
            _mqttSubConfigs[i] = new MqttSubscriptionConfigDto
            {
                NodeId = $"SUB_{i}",
                Qos = 1,
                QueueMode = MqttQueueMode.Fifo,
                FailSafeAction = MqttFailSafeAction.KeepLastValue,
                TimeoutMs = 5000,
                Topic = $"factory/command/setpoint{i}",
                Pins = new List<MqttPinBindingDto>
                {
                    new() { Id = "P0", Label = "temp", JsonPath = "temp", DataType = MqttDataType.Float, TargetTagIndex = (ushort)(50 + i) }
                }
            };
            MqttSubscriptionCodec.EncodeBlock(_mqttSubConfigs[i], _mqttSubRegisters.AsSpan(i * ModbusRegisterMap.MqttSubscribeRegistersPerBlock, ModbusRegisterMap.MqttSubscribeRegistersPerBlock));
        }

        // 6. Float32 Samples
        _floatSampleValues = new float[1000];
        _floatRegisters = new ushort[2000];
        for (int i = 0; i < 1000; i++)
        {
            _floatSampleValues[i] = (float)(i * 3.14159 / 180.0);
            RegisterCodec.EncodeFloat(_floatSampleValues[i], _floatRegisters.AsSpan(i * 2, 2));
        }
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 64 Tag Registry Types (0x0080)")]
    public byte[] TagRegistry_EncodeDecode_64Tags()
    {
        RegisterCodec.EncodeTagDataTypes(_tagDataTypes, _tagRegistryRegisters);
        return RegisterCodec.DecodeTagDataTypes(_tagRegistryRegisters);
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 16 DirectWrite / Modbus Master Write Blocks")]
    public int DirectWrite_16Blocks_EncodeDecode()
    {
        for (int i = 0; i < _directWriteConfigs.Length; i++)
        {
            var slice = _directWriteRegisters.AsSpan(i * ModbusRegisterMap.DirectWriteRegistersPerBlock, ModbusRegisterMap.DirectWriteRegistersPerBlock);
            DirectWriteCodec.EncodeBlock(_directWriteConfigs[i], slice);
            _ = DirectWriteCodec.DecodeBlock(slice);
        }
        return _directWriteRegisters[0];
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 16 Modbus Master Read Blocks")]
    public int ModbusMasterRead_16Blocks_EncodeDecode()
    {
        for (int i = 0; i < _modbusReadConfigs.Length; i++)
        {
            var slice = _modbusReadRegisters.AsSpan(i * ModbusRegisterMap.ModbusMasterRegistersPerBlock, ModbusRegisterMap.ModbusMasterRegistersPerBlock);
            ModbusMasterCodec.EncodeBlock(_modbusReadConfigs[i], slice);
            _ = ModbusMasterCodec.DecodeBlock(slice, i);
        }
        return _modbusReadRegisters[0];
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 16 MQTT Telemetry Blocks")]
    public int MqttTelemetry_16Blocks_EncodeDecode()
    {
        for (int i = 0; i < _mqttTelemetryConfigs.Length; i++)
        {
            var slice = _mqttTelemetryRegisters.AsSpan(i * ModbusRegisterMap.MqttRegistersPerBlock, ModbusRegisterMap.MqttRegistersPerBlock);
            MqttTelemetryCodec.EncodeBlock(_mqttTelemetryConfigs[i], slice);
            _ = MqttTelemetryCodec.DecodeBlock(slice);
        }
        return _mqttTelemetryRegisters[0];
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 16 MQTT Subscription Blocks")]
    public int MqttSubscription_16Blocks_EncodeDecode()
    {
        for (int i = 0; i < _mqttSubConfigs.Length; i++)
        {
            var slice = _mqttSubRegisters.AsSpan(i * ModbusRegisterMap.MqttSubscribeRegistersPerBlock, ModbusRegisterMap.MqttSubscribeRegistersPerBlock);
            MqttSubscriptionCodec.EncodeBlock(_mqttSubConfigs[i], slice);
            _ = MqttSubscriptionCodec.DecodeBlock(slice);
        }
        return _mqttSubRegisters[0];
    }

    [Benchmark(Description = "Wire V2: Encode & Decode 1000 Float32 Values (Bit Cast)")]
    public float Float32_1000Conversions()
    {
        float sum = 0f;
        for (int i = 0; i < 1000; i++)
        {
            var slice = _floatRegisters.AsSpan(i * 2, 2);
            RegisterCodec.EncodeFloat(_floatSampleValues[i], slice);
            sum += RegisterCodec.DecodeFloat(slice);
        }
        return sum;
    }
}
