namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

public static class WriteConflictValidator
{
    public const string ErrConflictingWriters = "SPLC-COMP-DEP-002";
    public const string ErrUnsupportedWriterCombination = "SPLC-COMP-DEP-003";

    /// <summary>
    /// Thẩm định xung đột ghi giữa các Candidate Rules theo Ma trận Write Conflict V1.
    /// Cho phép INC_COUNTER + INC_COUNTER cùng target (giao hoán, độc lập thứ tự).
    /// Từ chối SET + SET, SET + TOGGLE, SET + INC, TOGGLE + TOGGLE, ADD + ADD, SCALE + any.
    /// </summary>
    public static IReadOnlyList<CompileDiagnostic> Validate(
        IReadOnlyList<RuleAccessInfo> rules,
        ProductDefinition product)
    {
        var diagnostics = new List<CompileDiagnostic>();

        // Nhóm các Rule theo từng Tag bị ghi
        var writesByTag = new Dictionary<ushort, List<RuleAccessInfo>>();
        foreach (var rule in rules)
        {
            foreach (var tagIdx in rule.Writes)
            {
                if (!writesByTag.TryGetValue(tagIdx, out var list))
                {
                    list = new List<RuleAccessInfo>();
                    writesByTag[tagIdx] = list;
                }
                list.Add(rule);
            }
        }

        foreach (var (tagIdx, writers) in writesByTag)
        {
            if (writers.Count <= 1)
                continue;

            var tagName = product.FindTagByIndex(tagIdx)?.Name ?? $"TAG_{tagIdx}";
            var nodeIds = writers.Select(w => w.ActionNodeId).Distinct().ToList();

            // 1. Kiểm tra Controlled Multi-Writer Group (Authoring Macros như TON, TOF, TP)
            bool anyFromMacro = writers.Any(w => w.Origin != null);
            if (anyFromMacro)
            {
                var macroIds = writers.Select(w => w.Origin?.MacroInstanceId).Distinct().ToList();
                // Nếu tất cả writers đều cùng sinh từ một Macro Instance duy nhất
                if (macroIds.Count == 1 && macroIds[0] != null)
                {
                    string macroId = macroIds[0]!;
                    var macroTypes = writers.Select(w => w.Origin?.MacroType).Distinct().ToList();
                    string macroType = macroTypes.FirstOrDefault() ?? "TIMER";

                    // Thẩm định tính toàn vẹn chặt chẽ của Controlled Multi-Writer Group:
                    // 1. Nhóm Timer Macro (ghi vào ngõ ra Q: 2 quy tắc SetTag với tham số 1 và 0)
                    bool isWellFormedTimerPair =
                        (macroType is "TON" or "TOF" or "TP" or "TIMER") &&
                        writers.Count == 2 &&
                        writers.All(w => w.Action.Type == ActionKind.SetTag) &&
                        writers.Any(w => w.Origin?.ExpansionIndex == 0 && w.Action.Parameter == 1) &&
                        writers.Any(w => w.Origin?.ExpansionIndex == 1 && w.Action.Parameter == 0);

                    if (isWellFormedTimerPair)
                    {
                        continue;
                    }

                    // 2. Nhóm Counter Macro (CTU, CTD):
                    //    - Trên thanh ghi CV: 1 quy tắc IncrementCounter + 1 quy tắc SetTag (Reset)
                    //    - Trên ngõ ra Q: 2 quy tắc SetTag (Trip=1, Clear=0)
                    bool isCounterMacro = macroType is "CTU" or "CTD" or "COUNTER";
                    if (isCounterMacro)
                    {
                        bool isWellFormedCounterCv =
                            writers.Count == 2 &&
                            writers.Any(w => w.Action.Type == ActionKind.IncrementCounter) &&
                            writers.Any(w => w.Action.Type == ActionKind.SetTag);

                        bool isWellFormedCounterQ =
                            writers.Count == 2 &&
                            writers.All(w => w.Action.Type == ActionKind.SetTag) &&
                            writers.Any(w => w.Action.Parameter == 1) &&
                            writers.Any(w => w.Action.Parameter == 0);

                        if (isWellFormedCounterCv || isWellFormedCounterQ)
                        {
                            continue;
                        }
                    }

                    // Nếu nhóm writer sinh từ macro không đúng cấu trúc verified -> Reject
                    diagnostics.Add(new CompileDiagnostic(
                        ErrConflictingWriters,
                        DiagnosticSeverity.Error,
                        $"Phát hiện nhóm quy tắc sinh từ Macro '{macroId}' ({macroType}) không đúng định dạng chuẩn.",
                        NodeId: nodeIds.FirstOrDefault(),
                        RelatedNodeIds: nodeIds));
                    continue;
                }

                // Nếu có sự tranh chấp giữa nhiều Macro khác nhau, hoặc giữa Macro và User Rule ngoài -> Báo lỗi xung đột
                diagnostics.Add(new CompileDiagnostic(
                    ErrConflictingWriters,
                    DiagnosticSeverity.Error,
                    $"Phát hiện xung đột ghi vào Tag '{tagName}': Khối Timer yêu cầu quyền điều khiển độc quyền ngõ ra Q, không thể kết hợp với quy tắc ghi nào khác.",
                    NodeId: nodeIds.FirstOrDefault(),
                    RelatedNodeIds: nodeIds));
                continue;
            }

            // Nếu tất cả các writer đều là INC_COUNTER -> Hợp lệ (Giao hoán)
            bool allIncrement = writers.All(w => w.Action.Type == ActionKind.IncrementCounter);
            if (allIncrement)
            {
                continue;
            }

            // Nếu tất cả các writer đều là SET_TAG với các giá trị khác nhau -> Hợp lệ (Mô hình Set / Reset)
            bool allSet = writers.All(w => w.Action.Type == ActionKind.SetTag);
            if (allSet)
            {
                var distinctValues = writers.Select(w => w.Action.Parameter).Distinct().ToList();
                if (distinctValues.Count > 1)
                {
                    continue;
                }
            }

            // Nếu có chứa ADD_TAG hoặc SCALE_TAG -> Chưa hỗ trợ trong V1 (ErrUnsupportedWriterCombination)
            bool containsAddOrScale = writers.Any(w => w.Action.Type is ActionKind.AddTag or ActionKind.ScaleTag);
            if (containsAddOrScale)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrUnsupportedWriterCombination,
                    DiagnosticSeverity.Error,
                    $"Phát hiện nhiều quy tắc cùng ghi Tag '{tagName}' chứa thao tác chưa hỗ trợ đồng thời (ADD/SCALE) trong V1.",
                    NodeId: nodeIds.FirstOrDefault(),
                    RelatedNodeIds: nodeIds));
                continue;
            }

            // Xung đột giữa SET / TOGGLE / INC -> ErrConflictingWriters
            var actionTypesStr = string.Join(", ", writers.Select(w => w.Action.Type.ToString()).Distinct());
            diagnostics.Add(new CompileDiagnostic(
                ErrConflictingWriters,
                DiagnosticSeverity.Error,
                $"Phát hiện nhiều quy tắc xung đột cùng ghi vào Tag '{tagName}' ({actionTypesStr}). Cần dùng trạng thái trung gian (VFLAG) để giải quyết ưu tiên.",
                NodeId: nodeIds.FirstOrDefault(),
                RelatedNodeIds: nodeIds));
        }

        return diagnostics;
    }
}
