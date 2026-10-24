namespace SimplePLC.Application.UseCases;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Use Case đọc toàn bộ hình ảnh cấu hình thiết bị (Full Device Image) từ MCU:
/// Bao gồm Bảng Rule cơ sở (RuleTable), Timers, Counters, Modbus Master Reads/Writes, DirectWrites,
/// MQTT Telemetries/Subscriptions, MQTT Broker và cấu hình cổng RS-485.
/// </summary>
public sealed class LoadFullDeviceProjectUseCase
{
    private readonly IRuleTableReader _ruleTableReader;

    public LoadFullDeviceProjectUseCase(IRuleTableReader ruleTableReader)
    {
        _ruleTableReader = ruleTableReader ?? throw new ArgumentNullException(nameof(ruleTableReader));
    }

    public async Task<(RuleTable Rules, FullDeviceConfigurationDto FullConfig)> ExecuteAsync(
        ProductDefinition? product = null,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        var fullConfig = await _ruleTableReader.ReadFullDeviceConfigurationAsync(slaveId, ct).ConfigureAwait(false);
        var domainRules = RuleMapper.ToDomainTable(fullConfig.Rules, product);
        return (domainRules, fullConfig);
    }
}
