namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Domain.Models;

using SimplePLC.Protocol.Dto;

public sealed class CompiledProgram
{
    public string ProgramId { get; }
    public IReadOnlyList<Rule> Rules { get; }
    public DateTimeOffset CompiledAt { get; }
    public ushort RuleCount => (ushort)Rules.Count;
    public IReadOnlyDictionary<int, GeneratedRuleOrigin> SourceMap { get; }
    public IReadOnlyList<FbTimerRecordDto> FunctionBlockTimers { get; }
    public IReadOnlyList<FbCounterRecordDto> FunctionBlockCounters { get; }
    public IReadOnlyList<MqttTelemetryConfigDto> MqttTelemetryConfigs { get; }
    public IReadOnlyList<MqttSubscriptionConfigDto> MqttSubscriptionConfigs { get; }
    public IReadOnlyList<ModbusMasterReadConfigDto> ModbusMasterConfigs { get; }
    public IReadOnlyList<ModbusMasterWriteConfigDto> ModbusMasterWriteConfigs { get; }
    public IReadOnlyList<DirectWriteConfigDto> DirectWriteConfigs { get; }

    public CompiledProgram(
        string programId,
        IReadOnlyList<Rule> rules,
        DateTimeOffset compiledAt,
        IReadOnlyDictionary<int, GeneratedRuleOrigin>? sourceMap = null,
        IReadOnlyList<FbTimerRecordDto>? fbTimers = null,
        IReadOnlyList<FbCounterRecordDto>? fbCounters = null,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs = null,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs = null,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs = null,
        IReadOnlyList<ModbusMasterWriteConfigDto>? modbusMasterWriteConfigs = null,
        IReadOnlyList<DirectWriteConfigDto>? directWriteConfigs = null)
    {
        ProgramId = programId ?? Guid.NewGuid().ToString("N");
        Rules = rules ?? Array.Empty<Rule>();
        CompiledAt = compiledAt;
        SourceMap = sourceMap ?? new Dictionary<int, GeneratedRuleOrigin>();
        FunctionBlockTimers = fbTimers ?? Array.Empty<FbTimerRecordDto>();
        FunctionBlockCounters = fbCounters ?? Array.Empty<FbCounterRecordDto>();
        MqttTelemetryConfigs = mqttConfigs ?? Array.Empty<MqttTelemetryConfigDto>();
        MqttSubscriptionConfigs = mqttSubConfigs ?? Array.Empty<MqttSubscriptionConfigDto>();
        ModbusMasterConfigs = modbusMasterConfigs ?? Array.Empty<ModbusMasterReadConfigDto>();
        ModbusMasterWriteConfigs = modbusMasterWriteConfigs ?? Array.Empty<ModbusMasterWriteConfigDto>();
        DirectWriteConfigs = directWriteConfigs ?? Array.Empty<DirectWriteConfigDto>();
    }

    public RuleTable ToRuleTable()
    {
        var table = new RuleTable();
        foreach (var rule in Rules)
        {
            table.AddRule(rule);
        }
        return table;
    }
}

public sealed class CompileResult
{
    public CompiledProgram? Program { get; }
    public IReadOnlyList<CompileDiagnostic> Diagnostics { get; }

    public bool IsSuccess => Program != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public CompileResult(CompiledProgram? program, IEnumerable<CompileDiagnostic>? diagnostics = null)
    {
        Program = program;
        Diagnostics = diagnostics?.ToList().AsReadOnly() ?? (IReadOnlyList<CompileDiagnostic>)Array.Empty<CompileDiagnostic>();
    }

    public static CompileResult Success(CompiledProgram program, IEnumerable<CompileDiagnostic>? diagnostics = null) =>
        new(program, diagnostics);

    public static CompileResult Failure(IEnumerable<CompileDiagnostic> diagnostics) =>
        new(null, diagnostics);
}
