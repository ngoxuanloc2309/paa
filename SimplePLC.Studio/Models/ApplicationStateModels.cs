namespace SimplePLC.Studio.Models;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Simulator
}

public enum CompileState
{
    NotCompiled,
    Stale,
    Valid,
    Invalid
}

public enum DeployState
{
    Idle,
    Preparing,
    Staging,
    ReadyToCommit,
    Applying,
    Success,
    Failed,
    Unknown
}

public enum RuntimeSource
{
    Simulator,
    LiveDevice
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Message,
    int? RuleIndex = null,
    string? NodeId = null,
    string? FieldName = null,
    IReadOnlyList<string>? RelatedNodeIds = null);

public sealed record DeviceSession(
    string Port,
    ConnectionState State,
    uint? FirmwareVersion = null,
    ushort? ProtocolVersion = null,
    ushort? RuleFormatVersion = null,
    ushort? DeviceProfile = null,
    uint? ActiveConfigVersion = null)
{
    public bool IsCompatible => State == ConnectionState.Connected
        && (ProtocolVersion == 1 || ProtocolVersion == 2)
        && (RuleFormatVersion == 1 || RuleFormatVersion == 7)
        && (DeviceProfile == 1 || DeviceProfile == 2);
}

public sealed record CompileResult(
    IReadOnlyList<RuleItemModel> Rules,
    IReadOnlyList<Diagnostic> Diagnostics,
    SimplePLC.Application.Logic.Compilation.CompiledProgram? Program = null)
{
    public bool IsValid => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);
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
    ActionType ActionType,
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
    DiagnosticSeverity Severity = DiagnosticSeverity.Info);

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
