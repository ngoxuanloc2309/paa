namespace SimplePLC.Application.UseCases;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Models;

public sealed class LoadRulesUseCase
{
    private readonly IRuleTableReader _ruleTableReader;

    public LoadRulesUseCase(IRuleTableReader ruleTableReader)
    {
        _ruleTableReader = ruleTableReader ?? throw new ArgumentNullException(nameof(ruleTableReader));
    }

    public async Task<RuleTable> ExecuteAsync(
        ProductDefinition? product = null,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        var dtos = await _ruleTableReader.ReadActiveRulesAsync(slaveId, ct).ConfigureAwait(false);
        return RuleMapper.ToDomainTable(dtos, product);
    }
}
