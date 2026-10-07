using BenchmarkDotNet.Attributes;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class RuleCodecBenchmarks
{
    private RuleRecordDto[] _dtos = null!;
    private ushort[] _registers = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dtos = new RuleRecordDto[ModbusRegisterMap.MaxRules];
        for (int i = 0; i < ModbusRegisterMap.MaxRules; i++)
        {
            _dtos[i] = new RuleRecordDto
            {
                ThresholdLo = i * 10,
                ThresholdHi = i * 20 + 50,
                ForMs = (uint)(i * 100),
                ActionParam = i,
                TriggerTag = (ushort)(i % 20),
                ActionTag = (ushort)(8 + (i % 20)),
                GuardTag = (ushort)((i % 16) | (i % 2 == 0 ? ModbusRegisterMap.GuardTagNegateMask : 0)),
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_RISE,
                CompareOp = SPLC_CompareOp.GT,
                ActionType = SPLC_ActionType.SET_TAG
            };
        }

        _registers = new ushort[ModbusRegisterMap.MaxRules * ModbusRegisterMap.RegistersPerRule];
        RegisterCodec.EncodeRuleRecords(_dtos, _registers.AsSpan());
    }

    [Benchmark(Description = "Encode 100 RuleRecords to Registers")]
    public void Encode100Rules()
    {
        RegisterCodec.EncodeRuleRecords(_dtos, _registers.AsSpan());
    }

    [Benchmark(Description = "Decode 100 RuleRecords from Registers")]
    public RuleRecordDto[] Decode100Rules()
    {
        return RegisterCodec.DecodeRuleRecords(_registers.AsSpan(), ModbusRegisterMap.MaxRules);
    }
}
