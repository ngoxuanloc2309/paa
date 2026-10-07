using BenchmarkDotNet.Attributes;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class RuleMappingBenchmarks
{
    private ProductDefinition _product = null!;
    private RuleTable _domainTable = null!;
    private RuleRecordDto[] _dtos = null!;

    [GlobalSetup]
    public void Setup()
    {
        _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        _domainTable = new RuleTable();

        for (int i = 0; i < ModbusRegisterMap.MaxRules; i++)
        {
            var di = _product.FindTagByIndex((ushort)(i % 8))!;
            var doTag = _product.FindTagByIndex((ushort)(8 + (i % 8)))!;
            var grdTag = _product.FindTagByIndex((ushort)(20 + (i % 16)))!;

            var trigger = new TriggerModel(di, TriggerKind.OnRise)
            {
                CompareOp = CompareOperator.GreaterThan,
                ThresholdLo = i * 10,
                ThresholdHi = i * 20,
                ForMs = (uint)(i * 50)
            };

            var action = new ActionModel(doTag, ActionKind.SetTag, i);
            var guard = new GuardModel(grdTag, negated: i % 2 == 1);

            _domainTable.AddRule(new Rule(i, $"Rule_{i}", trigger, action, guard));
        }

        _dtos = RuleMapper.ToDtoArray(_domainTable);
    }

    [Benchmark(Description = "Map 100 Domain Rules to DTOs")]
    public RuleRecordDto[] DomainToDto_100Rules()
    {
        return RuleMapper.ToDtoArray(_domainTable);
    }

    [Benchmark(Description = "Map 100 DTOs to Domain RuleTable")]
    public RuleTable DtoToDomain_100Rules()
    {
        return RuleMapper.ToDomainTable(_dtos, _product);
    }
}
