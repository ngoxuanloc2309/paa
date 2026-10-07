namespace SimplePLC.Application.Logic.Runtime;

public interface ISimulatedClock
{
    long NowMs { get; }
    void Reset();
    void Advance(long milliseconds);
}

public sealed class SimulatedClock : ISimulatedClock
{
    public long NowMs { get; private set; }
    public void Reset() => NowMs = 0;
    public void Advance(long milliseconds) => NowMs += Math.Max(0, milliseconds);
}

public enum RuleEvaluationStatus
{
    Pass,
    Skip,
    BlockedByGuard,
    WaitingDwell,
    Disabled
}

public sealed record ActionExecutionDelta(
    int TargetTagIndex,
    string TargetTagName,
    SimplePLC.Domain.Enums.ActionKind ActionType,
    int BeforeValue,
    int AfterValue,
    string FormattedDelta);

public sealed record RuleEvaluationRecord(
    int RuleIndex,
    string RuleName,
    RuleEvaluationStatus Status,
    string TriggerSummary,
    string? GuardSummary,
    long DwellElapsedMs,
    long DwellRequiredMs,
    ActionExecutionDelta? ActionDelta,
    bool IsChainedInSameScan,
    string SummaryLine);

public sealed record RuntimeEvent(
    long TickMs,
    int RuleIndex,
    string Message,
    SimplePLC.Application.Logic.Compilation.DiagnosticSeverity Severity = SimplePLC.Application.Logic.Compilation.DiagnosticSeverity.Info);

public sealed record RuntimeSnapshot(
    long TickMs,
    IReadOnlyDictionary<int, int> Values,
    IReadOnlyList<RuntimeEvent> Events,
    IReadOnlyDictionary<int, long> PendingDwellMs,
    long ScanNumber = 0,
    IReadOnlyList<RuleEvaluationRecord>? Evaluations = null,
    IReadOnlyList<string>? FormattedTrace = null)
{
    public IReadOnlyList<RuleEvaluationRecord> Evaluations { get; init; } = Evaluations ?? Array.Empty<RuleEvaluationRecord>();
    public IReadOnlyList<string> FormattedTrace { get; init; } = FormattedTrace ?? Array.Empty<string>();
}
