namespace SimplePLC.Application.Logic.Compilation;

/// <summary>
/// Metadata mô tả nguồn gốc phát sinh của một Candidate Rule được hạ cấp từ Authoring Macro (ví dụ TON, TOF, TP).
/// Metadata này chỉ tồn tại trong RAM của Compiler, Diagnostics và Simulator; hoàn toàn KHÔNG serialize xuống Modbus wire.
/// </summary>
public sealed record GeneratedRuleOrigin(
    string SourceNodeId,
    string MacroInstanceId,
    string MacroType,
    int ExpansionIndex,
    string DisplayLabel = "")
{
    public override string ToString() => $"{MacroType}[{MacroInstanceId}]:{ExpansionIndex} ({DisplayLabel})";
}
