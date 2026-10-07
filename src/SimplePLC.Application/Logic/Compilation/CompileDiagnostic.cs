namespace SimplePLC.Application.Logic.Compilation;

public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record CompileDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? NodeId = null,
    string? EdgeId = null,
    string? FieldName = null,
    IReadOnlyList<string>? RelatedNodeIds = null);
