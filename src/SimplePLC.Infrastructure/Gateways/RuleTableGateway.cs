using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Gateways;

/// <summary>
/// Cổng giao tiếp toàn diện cho bảng quy tắc (Active Table & Staging Table).
/// Hợp nhất IRuleTableReader và IRuleTableWriter theo Clean Architecture.
/// </summary>
public sealed class RuleTableGateway : IRuleTableGateway
{
    private readonly IRuleTableReader _reader;
    private readonly IRuleTableWriter _writer;

    public RuleTableGateway(IRuleTableReader reader, IRuleTableWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public RuleTableGateway(IModbusClient client)
        : this(new RuleTableReader(client), new RuleTableWriter(client))
    {
    }

    public Task<IReadOnlyList<RuleRecordDto>> ReadActiveRulesAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveRulesAsync(slaveId, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(byte slaveId, IReadOnlyList<RuleRecordDto> rules, CancellationToken cancellationToken = default) =>
        _writer.DeployRulesAsync(slaveId, rules, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default) =>
        _writer.DeployRulesAsync(slaveId, rules, timers, counters, cancellationToken);
}
