namespace SimplePLC.Application.Logic.Compilation.MacroLowering;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Hạ cấp (Macro Lowering) các khối Math (tính toán số học và đại số IEC 61131-3) thành tập luật logic cơ sở.
/// Hỗ trợ cả 2 chế độ toán hạng 2 (Tag vs Hằng số scalar) và đa ngõ vào động (2..8 chân).
/// Tự động cấp phát các thanh ghi tạm thời VREG an toàn và phân rã biểu thức phức tạp.
/// </summary>
public static class MathMacroLowering
{
    public const string ErrMathNoInput = "SPLC-COMP-MATH-IN";
    public const string ErrMathNoOutput = "SPLC-COMP-MATH-OUT";
    public const string ErrMathNoScratch = "SPLC-COMP-MATH-NO-SCRATCH";
    public const string ErrMathDivZero = "SPLC-COMP-MATH-DIV-ZERO";
    public const string ErrMathTypeMismatch = "SPLC-COMP-MATH-TYPE-MISMATCH";
    public const string ErrMathReadOnlyTarget = "SPLC-COMP-MATH-READONLY";
    public const string ErrMathUnsupported = "SPLC-COMP-MATH-UNSUPPORTED";

    /// <summary>
    /// Thực hiện mở rộng các Math nodes thành các CandidateRule 32-byte.
    /// </summary>
    public static void Lower(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        IReadOnlyList<LogicNode> mathNodes,
        List<CandidateRule> candidateRules,
        List<CompileDiagnostic> diagnostics)
    {
        if (mathNodes.Count == 0) return;

        // Kiểm tra năng lực phần cứng mục tiêu hỗ trợ MATH_ALU (Bit 16)
        if (!product.HardwareCapabilities.HasFlag(SPLC_HardwareCapabilities.MATH_ALU))
        {
            diagnostics.Add(new CompileDiagnostic(
                ErrMathUnsupported,
                DiagnosticSeverity.Error,
                $"Thiết bị '{product.ProductName}' không hỗ trợ tập lệnh tính toán số học MATH_ALU (HardwareCapabilities Bit 16)."));
            return;
        }

        // Tập hợp toàn bộ TagIndex đã được tham chiếu tĩnh trong toàn bộ đồ thị để tránh cấp phát trùng
        var usedTagIndices = CollectUsedTagIndices(graph);

        foreach (var mathNode in mathNodes)
        {
            var mData = mathNode.MathData ?? new MathNodeData();
            var mathLabel = CompilationHelpers.GetNodeLabel(mathNode, "Math");

            // 1. Phân giải các ngõ vào toán hạng (Pins)
            var resolvedPins = ResolveInputPins(graph, nodeMap, mathNode, mData);

            if (resolvedPins.Count < 2)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMathNoInput,
                    DiagnosticSeverity.Error,
                    $"Khối Math '{mathLabel}' yêu cầu tối thiểu 2 ngõ vào toán hạng để tính toán.",
                    NodeId: mathNode.Id));
                continue;
            }

            if ((mData.Operator == MathOperator.Divide || mData.Operator == MathOperator.Modulo) && resolvedPins.Count > 2)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMathNoInput,
                    DiagnosticSeverity.Error,
                    $"Toán tử '{mData.Operator}' của khối Math '{mathLabel}' là phép toán không giao hoán, chỉ hỗ trợ chính xác 2 ngõ vào.",
                    NodeId: mathNode.Id));
                continue;
            }

            if ((mData.Operator == MathOperator.Divide || mData.Operator == MathOperator.Modulo) &&
                resolvedPins.Count >= 2 &&
                resolvedPins[1].IsConstant &&
                Math.Abs(resolvedPins[1].ConstantValue) < 1e-9)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMathDivZero,
                    DiagnosticSeverity.Error,
                    $"Khối Math '{mathLabel}' có toán hạng chia là hằng số 0 không hợp lệ.",
                    NodeId: mathNode.Id));
                continue;
            }

            // 2. Phân giải Tag ngõ ra (Output Tag)
            ushort? resolvedOutTagIndex = mData.OutTagIndex;
            foreach (var outEdge in graph.GetOutgoingEdges(mathNode.Id))
            {
                if (nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode))
                {
                    if (tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
                    {
                        resolvedOutTagIndex = tgtNode.ActionData.TargetTagIndex;
                    }
                    else if (tgtNode.Kind == LogicNodeKind.Math && tgtNode.MathData != null && tgtNode.MathData.OutTagIndex.HasValue)
                    {
                        // Cho phép nối Math -> Math
                    }
                }
            }

            var outTag = resolvedOutTagIndex.HasValue ? product.FindTagByIndex(resolvedOutTagIndex.Value) : null;
            if (outTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMathNoOutput,
                    DiagnosticSeverity.Error,
                    $"Khối Math '{mathLabel}' yêu cầu có Tag ngõ ra (hãy chọn OutputTag trong Inspector hoặc nối chân OUT tới khối Action).",
                    NodeId: mathNode.Id));
                continue;
            }

            // Chốt chặn an toàn: Tag ngõ ra không được là Tag chỉ đọc (AI, DI, System Tag...)
            if (outTag.IsReadOnly)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMathReadOnlyTarget,
                    DiagnosticSeverity.Error,
                    $"Khối Math '{mathLabel}' không thể xuất kết quả ra Tag chỉ đọc '{outTag.Name}'. Hãy chọn VREG hoặc DO.",
                    NodeId: mathNode.Id));
                continue;
            }

            // 3. Phân tích chế độ kiểu dữ liệu (Data Type Promotion)
            bool isFloat = DetermineIsFloat(resolvedPins, product, mData.TypeMode, outTag);

            string macroId = mathNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(mathNode.Label) ? mathNode.Label : mathNode.Id;

            // 3.1. Automatic Type Promotion (Implicit Lowering per Option B)
            // Khi tính toán ở chế độ số thực (isFloat == true), toàn bộ các toán hạng là Tag số nguyên
            // (INT32 / AI) sẽ được tự động thăng hạng sang Float32 thông qua micro-rule SCALE ngầm vào VREG nháp.
            var effectivePins = new List<ResolvedMathPin>(resolvedPins.Count);
            if (isFloat)
            {
                var pinsNeedingPromo = resolvedPins
                    .Where(p => !p.IsConstant && p.TagIndex.HasValue && product.FindTagByIndex(p.TagIndex.Value)?.DataType != TagDataType.Float32)
                    .ToList();

                if (pinsNeedingPromo.Count > 0)
                {
                    var promoVregs = AllocateScratchVregs(product, usedTagIndices, pinsNeedingPromo.Count);
                    if (promoVregs.Count < pinsNeedingPromo.Count)
                    {
                        diagnostics.Add(new CompileDiagnostic(
                            ErrMathNoScratch,
                            DiagnosticSeverity.Error,
                            $"Khối Math '{mathLabel}' cần {pinsNeedingPromo.Count} biến trung gian để chuyển đổi kiểu INT32 sang REAL nhưng không đủ thanh ghi VREG trống trong RAM.",
                            NodeId: mathNode.Id));
                        continue;
                    }

                    // Đánh dấu đã sử dụng để không bị cấp trùng
                    foreach (var v in promoVregs)
                    {
                        usedTagIndices.Add(v.TagIndex);
                    }

                    int pIdx = 0;
                    foreach (var p in resolvedPins)
                    {
                        if (!p.IsConstant && p.TagIndex.HasValue)
                        {
                            var t = product.FindTagByIndex(p.TagIndex.Value);
                            if (t != null && t.DataType != TagDataType.Float32)
                            {
                                var vregPromo = promoVregs[pIdx];
                                var promoTrig = new TriggerModel(t, TriggerKind.OnChange)
                                {
                                    ThresholdHi = 0.0,
                                    IsFloat = false,
                                    ForMs = 0
                                };
                                var promoAct = new ActionModel(vregPromo, ActionKind.ScaleTag, parameter: 1.0, auxParameter: 0.0)
                                {
                                    IsFloat = true
                                };
                                var promoOrigin = new GeneratedRuleOrigin(mathNode.Id, macroId, "MATH_PROMO", pIdx, mathNode.Label);
                                candidateRules.Add(new CandidateRule(mathNode, promoTrig, promoAct, GuardModel.Empty, $"{labelPrefix}_PROMO_{pIdx + 1}", promoOrigin));

                                effectivePins.Add(new ResolvedMathPin(vregPromo.TagIndex, false, 0.0, p.Label));
                                pIdx++;
                                continue;
                            }
                        }
                        effectivePins.Add(p);
                    }
                }
                else
                {
                    effectivePins.AddRange(resolvedPins);
                }
            }
            else
            {
                effectivePins.AddRange(resolvedPins);
            }

            // 4. Nếu có đúng 2 chân ngõ vào: Biên dịch thành đúng 1 Rule 32-byte nguyên tử
            if (effectivePins.Count == 2)
            {
                var pin1 = effectivePins[0];
                var pin2 = effectivePins[1];

                var inTag1 = pin1.TagIndex.HasValue ? product.FindTagByIndex(pin1.TagIndex.Value) : null;
                if (inTag1 == null)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrMathNoInput,
                        DiagnosticSeverity.Error,
                        $"Khối Math '{mathLabel}' ngõ vào 1 chưa liên kết Tag hợp lệ.",
                        NodeId: mathNode.Id));
                    continue;
                }

                var trig = new TriggerModel(inTag1, TriggerKind.OnChange)
                {
                    CompareOp = (CompareOperator)mData.Operator,
                    IsFloat = isFloat,
                    ForMs = 0
                };

                GuardModel guard;
                ActionModel act;

                if (pin2.IsConstant)
                {
                    guard = GuardModel.Empty;
                    act = new ActionModel(outTag, ActionKind.MathOp, parameter: pin2.ConstantValue, auxParameter: pin2.ConstantValue)
                    {
                        IsFloat = isFloat
                    };
                }
                else
                {
                    var inTag2 = pin2.TagIndex.HasValue ? product.FindTagByIndex(pin2.TagIndex.Value) : null;
                    if (inTag2 == null)
                    {
                        diagnostics.Add(new CompileDiagnostic(
                            ErrMathNoInput,
                            DiagnosticSeverity.Error,
                            $"Khối Math '{mathLabel}' ngõ vào 2 (chế độ Tag) chưa liên kết Tag hợp lệ.",
                            NodeId: mathNode.Id));
                        continue;
                    }

                    guard = new GuardModel(inTag2, negated: false);
                    act = new ActionModel(outTag, ActionKind.MathOp, parameter: 0, auxParameter: 0)
                    {
                        IsFloat = isFloat
                    };
                }

                var origin = new GeneratedRuleOrigin(mathNode.Id, macroId, "MATH", 0, mathNode.Label);
                candidateRules.Add(new CandidateRule(mathNode, trig, act, guard, $"{labelPrefix}_MATH", origin));
            }
            else
            {
                // 5. Nếu có N > 2 chân ngõ vào: Cấp phát biến tạm VREG và phân rã thành chuỗi N-1 Micro-Rules
                int intermediateCount = effectivePins.Count - 2;
                var allocatedVregs = AllocateScratchVregs(product, usedTagIndices, intermediateCount);

                if (allocatedVregs.Count < intermediateCount)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrMathNoScratch,
                        DiagnosticSeverity.Error,
                        $"Khối Math '{mathLabel}' cần {intermediateCount} biến trung gian nhưng không đủ thanh ghi VREG trống trong RAM (đã dùng hết VREG).",
                        NodeId: mathNode.Id));
                    continue;
                }

                // Đánh dấu các VREG vừa cấp phát vào usedTagIndices để các khối Math sau không lấy trùng
                foreach (var vreg in allocatedVregs)
                {
                    usedTagIndices.Add(vreg.TagIndex);
                }

                TagDefinition currentOp1Tag;
                var pin0 = effectivePins[0];
                var tag0 = pin0.TagIndex.HasValue ? product.FindTagByIndex(pin0.TagIndex.Value) : null;
                if (tag0 == null)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrMathNoInput,
                        DiagnosticSeverity.Error,
                        $"Khối Math '{mathLabel}' ngõ vào 1 chưa liên kết Tag hợp lệ.",
                        NodeId: mathNode.Id));
                    continue;
                }
                currentOp1Tag = tag0;

                int stepCount = effectivePins.Count - 1;
                for (int step = 0; step < stepCount; step++)
                {
                    var nextPin = effectivePins[step + 1];
                    TagDefinition stepTargetTag = (step == stepCount - 1) ? outTag : allocatedVregs[step];

                    var trig = new TriggerModel(currentOp1Tag, TriggerKind.OnChange)
                    {
                        CompareOp = (CompareOperator)mData.Operator,
                        IsFloat = isFloat,
                        ForMs = 0
                    };

                    GuardModel guard;
                    ActionModel act;

                    if (nextPin.IsConstant)
                    {
                        guard = GuardModel.Empty;
                        act = new ActionModel(stepTargetTag, ActionKind.MathOp, parameter: nextPin.ConstantValue, auxParameter: nextPin.ConstantValue)
                        {
                            IsFloat = isFloat
                        };
                    }
                    else
                    {
                        var inTagNext = nextPin.TagIndex.HasValue ? product.FindTagByIndex(nextPin.TagIndex.Value) : null;
                        if (inTagNext == null)
                        {
                            diagnostics.Add(new CompileDiagnostic(
                                ErrMathNoInput,
                                DiagnosticSeverity.Error,
                                $"Khối Math '{mathLabel}' ngõ vào {step + 2} chưa liên kết Tag hợp lệ.",
                                NodeId: mathNode.Id));
                            break;
                        }

                        guard = new GuardModel(inTagNext, negated: false);
                        act = new ActionModel(stepTargetTag, ActionKind.MathOp, parameter: 0, auxParameter: 0)
                        {
                            IsFloat = isFloat
                        };
                    }

                    var origin = new GeneratedRuleOrigin(mathNode.Id, macroId, "MATH_CHAIN", step, $"{mathNode.Label}_Step{step + 1}");
                    candidateRules.Add(new CandidateRule(mathNode, trig, act, guard, $"{labelPrefix}_MATH_S{step + 1}", origin));

                    // Cho bước kế tiếp: kết quả của bước này trở thành Op1 của bước sau
                    currentOp1Tag = stepTargetTag;
                }
            }
        }
    }

    private static List<ResolvedMathPin> ResolveInputPins(
        LogicGraph graph,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        LogicNode mathNode,
        MathNodeData mData)
    {
        var result = new List<ResolvedMathPin>();

        // Lấy danh sách chân đã cấu hình từ MathData
        if (mData.Pins != null && mData.Pins.Count > 0)
        {
            foreach (var pin in mData.Pins)
            {
                result.Add(new ResolvedMathPin(pin.TagIndex, pin.IsConstant, pin.ConstantValue, pin.Label));
            }
        }

        // Kiểm tra các kết nối dây ngõ vào (Incoming Edges)
        var incomingEdges = graph.GetIncomingEdges(mathNode.Id).ToList();
        for (int i = 0; i < incomingEdges.Count; i++)
        {
            var edge = incomingEdges[i];
            if (nodeMap.TryGetValue(edge.SourceNodeId, out var srcNode))
            {
                ushort? srcTag = null;
                if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    srcTag = srcNode.InputData.TagIndex;
                }
                else if (srcNode.Kind == LogicNodeKind.Scale && srcNode.ScaleData?.OutTagIndex != null)
                {
                    srcTag = srcNode.ScaleData.OutTagIndex;
                }
                else if (srcNode.Kind == LogicNodeKind.Math && srcNode.MathData?.OutTagIndex != null)
                {
                    srcTag = srcNode.MathData.OutTagIndex;
                }

                if (srcTag.HasValue)
                {
                    if (i < result.Count)
                    {
                        // Ghi đè vào chân thứ i
                        result[i] = new ResolvedMathPin(srcTag.Value, false, 0, result[i].Label);
                    }
                    else if (result.Count < 8)
                    {
                        // Thêm chân mới từ dây nối
                        result.Add(new ResolvedMathPin(srcTag.Value, false, 0, $"IN{result.Count + 1}"));
                    }
                }
            }
        }

        return result;
    }

    private static bool DetermineIsFloat(
        IReadOnlyList<ResolvedMathPin> pins,
        ProductDefinition product,
        MathTypeMode typeMode,
        TagDefinition outTag)
    {
        if (typeMode == MathTypeMode.ForceReal) return true;
        if (typeMode == MathTypeMode.ForceInt32) return false;

        // Chế độ Auto: Nếu outTag là Float32, hoặc bất kỳ toán hạng nào là Float32 hoặc có số lẻ thập phân
        if (outTag.DataType == TagDataType.Float32) return true;

        foreach (var pin in pins)
        {
            if (pin.IsConstant)
            {
                if (Math.Abs(pin.ConstantValue - Math.Round(pin.ConstantValue)) > 1e-6)
                    return true;
            }
            else if (pin.TagIndex.HasValue)
            {
                var tag = product.FindTagByIndex(pin.TagIndex.Value);
                if (tag != null && tag.DataType == TagDataType.Float32)
                    return true;
            }
        }

        return false;
    }

    private static HashSet<ushort> CollectUsedTagIndices(LogicGraph graph)
    {
        var set = new HashSet<ushort>();
        foreach (var n in graph.Nodes)
        {
            if (n.InputData != null) set.Add(n.InputData.TagIndex);
            if (n.TriggerData?.InputTagIndex != null) set.Add(n.TriggerData.InputTagIndex.Value);
            if (n.GuardData != null) set.Add(n.GuardData.TagIndex);
            if (n.ActionData != null) set.Add(n.ActionData.TargetTagIndex);
            if (n.ScaleData != null)
            {
                set.Add(n.ScaleData.InTagIndex);
                if (n.ScaleData.OutTagIndex.HasValue) set.Add(n.ScaleData.OutTagIndex.Value);
            }
            if (n.TimerData != null)
            {
                set.Add(n.TimerData.InTagIndex);
                set.Add(n.TimerData.QTagIndex);
            }
            if (n.CounterData != null)
            {
                set.Add(n.CounterData.CuTagIndex);
                set.Add(n.CounterData.CvTagIndex);
                set.Add(n.CounterData.QTagIndex);
            }
            if (n.MathData != null)
            {
                if (n.MathData.OutTagIndex.HasValue) set.Add(n.MathData.OutTagIndex.Value);
                foreach (var p in n.MathData.Pins)
                {
                    if (p.TagIndex.HasValue) set.Add(p.TagIndex.Value);
                }
            }
        }
        return set;
    }

    private static List<TagDefinition> AllocateScratchVregs(
        ProductDefinition product,
        HashSet<ushort> usedTags,
        int count)
    {
        // Quét ngược từ VREG15 xuống VREG0 để ưu tiên dùng vùng nhớ cuối
        var availableVregs = product.Tags
            .Where(t => t.Kind == TagKind.VirtualRegister && !usedTags.Contains(t.TagIndex))
            .OrderByDescending(t => t.TagIndex)
            .Take(count)
            .ToList();

        return availableVregs;
    }

    private sealed record ResolvedMathPin(
        ushort? TagIndex,
        bool IsConstant,
        double ConstantValue,
        string Label);
}
