namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

public sealed class RuleCompiler : IRuleCompiler
{
    public const string ErrActionNoIncoming = "SPLC-COMP-001";
    public const string ErrGuardNoTrigger = "SPLC-COMP-002";
    public const string ErrTriggerNoInput = "SPLC-COMP-003";
    public const string ErrGuardNotBoolean = "SPLC-COMP-004";
    public const string ErrCapacityExceeded = "SPLC-COMP-005";
    public const string ErrTagNotFound = "SPLC-COMP-006";
    public const string ErrScaleNoInput = "SPLC-COMP-SCALE-IN";
    public const string ErrScaleNoOutput = "SPLC-COMP-SCALE-OUT";
    public const string WarnNoActions = "SPLC-COMP-W01";
    public const string ErrCircularDependency = RuleDependencyGraph.ErrCircularDependency;
    public const string ErrConflictingWriters = WriteConflictValidator.ErrConflictingWriters;
    public const string ErrUnsupportedWriterCombination = WriteConflictValidator.ErrUnsupportedWriterCombination;
    public const string ErrIndeterminateOrdering = "SPLC-COMP-DEP-004";

    public CompileResult Compile(LogicGraph graph, ProductDefinition product, string? programId = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(product);

        var diagnostics = new List<CompileDiagnostic>();

        // Step 1: Structural Grammar Validation & Cycle Detection
        var grammarErrors = GraphGrammarV1.ValidateStructure(graph);
        diagnostics.AddRange(grammarErrors);
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        var nodeMap = graph.Nodes.ToDictionary(n => n.Id);
        var actionNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.Action).ToList();
        var timerNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.Timer).ToList();
        var counterNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.Counter).ToList();
        var scaleNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.Scale).ToList();

        if (actionNodes.Count == 0 && timerNodes.Count == 0 && counterNodes.Count == 0 && scaleNodes.Count == 0)
        {
            diagnostics.Add(new CompileDiagnostic(
                WarnNoActions,
                DiagnosticSeverity.Warning,
                "Graph does not contain any Action, Timer, Counter or Scale nodes. 0 rules produced."));

            var emptyProgram = new CompiledProgram(
                programId ?? Guid.NewGuid().ToString("N"),
                Array.Empty<Rule>(),
                DateTimeOffset.UtcNow);

            return CompileResult.Success(emptyProgram, diagnostics);
        }

        var candidateRules = new List<(LogicNode ActionNode, TriggerModel Trigger, ActionModel Action, GuardModel Guard, string RuleName, GeneratedRuleOrigin? Origin)>();

        // Step 2: Path enumeration from each Action terminal
        foreach (var actionNode in actionNodes)
        {
            var actionIncoming = graph.GetIncomingEdges(actionNode.Id).FirstOrDefault();
            if (actionIncoming == null || !nodeMap.TryGetValue(actionIncoming.SourceNodeId, out var directSource))
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionNode.Id}' is not connected to a Trigger or Guard.",
                    NodeId: actionNode.Id));
                continue;
            }

            if (directSource.Kind is LogicNodeKind.Counter or LogicNodeKind.Timer or LogicNodeKind.Scale)
            {
                // Action node connects from Counter, Timer or Scale port as visual destination.
                // The rules are fully expanded in Step 2b / 2c / 2d (Macro Expansion).
                continue;
            }

            LogicNode? triggerNode = null;
            LogicNode? guardNode = null;

            if (directSource.Kind == LogicNodeKind.Trigger)
            {
                triggerNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Guard)
            {
                guardNode = directSource;
                var guardIncoming = graph.GetIncomingEdges(guardNode.Id).FirstOrDefault();
                if (guardIncoming == null || !nodeMap.TryGetValue(guardIncoming.SourceNodeId, out var guardSource) || guardSource.Kind != LogicNodeKind.Trigger)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrGuardNoTrigger,
                        DiagnosticSeverity.Error,
                        $"Guard node '{guardNode.Id}' is not connected to an upstream Trigger.",
                        NodeId: guardNode.Id));
                    continue;
                }
                triggerNode = guardSource;
            }
            else
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionNode.Id}' must be fed by Trigger or Guard, but got {directSource.Kind}.",
                    NodeId: actionNode.Id));
                continue;
            }

            // Trace Trigger upstream input
            LogicNode? inputNode = null;
            var triggerIncoming = graph.GetIncomingEdges(triggerNode.Id).FirstOrDefault();
            if (triggerIncoming != null && nodeMap.TryGetValue(triggerIncoming.SourceNodeId, out var inpCandidate) && inpCandidate.Kind == LogicNodeKind.Input)
            {
                inputNode = inpCandidate;
            }

            var triggerData = triggerNode.TriggerData ?? new TriggerNodeData();
            bool requiresInput = triggerData.Type is not (TriggerKind.Interval or TriggerKind.TimeWindow);

            if (requiresInput && inputNode == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTriggerNoInput,
                    DiagnosticSeverity.Error,
                    $"Trigger node '{triggerNode.Id}' requires an upstream Input Tag.",
                    NodeId: triggerNode.Id,
                    FieldName: "InputTag"));
                continue;
            }

            // Resolve Trigger Tag
            ushort triggerTagIndex = inputNode?.InputData?.TagIndex ?? 0;
            var triggerTag = product.FindTagByIndex(triggerTagIndex);
            if (triggerTag == null)
            {
                if (requiresInput)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrTagNotFound,
                        DiagnosticSeverity.Error,
                        $"Trigger Tag index {triggerTagIndex} not found in device profile.",
                        NodeId: inputNode?.Id ?? triggerNode.Id));
                    continue;
                }
                // Fallback virtual/placeholder for interval/timewindow
                triggerTag = new TagDefinition(0, "NONE", TagKind.None, TagDataType.Boolean, isReadOnly: true);
            }

            var triggerModel = new TriggerModel(triggerTag, triggerData.Type)
            {
                CompareOp = triggerData.CompareOp,
                ThresholdLo = triggerData.ThresholdLo,
                ThresholdHi = triggerData.ThresholdHi,
                ForMs = triggerData.ForMs
            };

            // Resolve Guard Tag (if guard exists)
            GuardModel guardModel = GuardModel.Empty;
            if (guardNode != null)
            {
                var guardData = guardNode.GuardData ?? new GuardNodeData();
                var guardTag = product.FindTagByIndex(guardData.TagIndex);
                if (guardTag == null)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrTagNotFound,
                        DiagnosticSeverity.Error,
                        $"Guard Tag index {guardData.TagIndex} not found in device profile.",
                        NodeId: guardNode.Id));
                    continue;
                }

                if (guardTag.DataType != TagDataType.Boolean)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrGuardNotBoolean,
                        DiagnosticSeverity.Error,
                        $"Guard Tag '{guardTag.Name}' must be Boolean, but found {guardTag.DataType}.",
                        NodeId: guardNode.Id));
                    continue;
                }

                guardModel = new GuardModel(guardTag, guardData.Negated);
            }

            // Resolve Action Tag
            var actionData = actionNode.ActionData ?? new ActionNodeData();
            var actionTag = product.FindTagByIndex(actionData.TargetTagIndex);
            if (actionTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Action Target Tag index {actionData.TargetTagIndex} not found in device profile.",
                    NodeId: actionNode.Id));
                continue;
            }

            var actionModel = new ActionModel(actionTag, actionData.Type, actionData.Parameter);

            string ruleName = !string.IsNullOrWhiteSpace(actionNode.Label) ? actionNode.Label : string.Empty;
            candidateRules.Add((actionNode, triggerModel, actionModel, guardModel, ruleName, null));
        }

        var compiledFbTimers = new List<FbTimerRecordDto>();
        var compiledFbCounters = new List<FbCounterRecordDto>();

        if (product.SupportsDedicatedFunctionBlocks)
        {
            if (timerNodes.Count > ModbusRegisterMap.FbMaxTimers)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrCapacityExceeded,
                    DiagnosticSeverity.Error,
                    $"Timer count ({timerNodes.Count}) exceeds maximum hardware limit ({ModbusRegisterMap.FbMaxTimers}).",
                    NodeId: timerNodes[ModbusRegisterMap.FbMaxTimers].Id));
            }

            if (counterNodes.Count > ModbusRegisterMap.FbMaxCounters)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrCapacityExceeded,
                    DiagnosticSeverity.Error,
                    $"Counter count ({counterNodes.Count}) exceeds maximum hardware limit ({ModbusRegisterMap.FbMaxCounters}).",
                    NodeId: counterNodes[ModbusRegisterMap.FbMaxCounters].Id));
            }

            foreach (var timerNode in timerNodes)
            {
                var tData = timerNode.TimerData ?? new TimerNodeData();
                var mode = tData.Type switch
                {
                    TimerMacroType.Tof => SPLC_TimerMode.TOF,
                    TimerMacroType.Tp => SPLC_TimerMode.TP,
                    _ => SPLC_TimerMode.TON
                };

                compiledFbTimers.Add(new FbTimerRecordDto
                {
                    Mode = mode,
                    PresetMs = tData.PresetMs,
                    ElapsedMs = 0,
                    StatusBits = 0
                });
            }

            foreach (var counterNode in counterNodes)
            {
                var cData = counterNode.CounterData ?? new CounterNodeData();
                var mode = cData.Type == CounterMacroType.Ctd ? SPLC_CounterMode.CTD : SPLC_CounterMode.CTU;
                ushort retainTagIndex = (cData.CvTagIndex >= ModbusRegisterMap.VregRetainBaseIndex &&
                                         cData.CvTagIndex < ModbusRegisterMap.VregRetainBaseIndex + ModbusRegisterMap.MaxRetentiveRegisters)
                    ? cData.CvTagIndex
                    : ModbusRegisterMap.FbCounterRetainNone;

                compiledFbCounters.Add(new FbCounterRecordDto
                {
                    Mode = mode,
                    PresetValue = cData.PresetValue,
                    CurrentValue = 0,
                    RetainTagIndex = retainTagIndex,
                    StatusBits = 0
                });
            }
        }
        else
        {
            // Step 2b: Macro Expansion for Timer nodes (TON, TOF, TP)
            foreach (var timerNode in timerNodes)
            {
                var tData = timerNode.TimerData ?? new TimerNodeData();
                ushort resolvedInTagIndex = tData.InTagIndex;
            ushort resolvedQTagIndex = tData.QTagIndex;

            // Nếu Timer node được nối dây từ một Input node upstream -> lấy InTagIndex từ Input node
            var inEdge = graph.GetIncomingEdges(timerNode.Id).FirstOrDefault();
            if (inEdge != null && nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode) && srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
            {
                resolvedInTagIndex = srcNode.InputData.TagIndex;
            }

            // Nếu Timer node được nối dây sang Action node downstream -> lấy QTagIndex từ Action node
            var outEdge = graph.GetOutgoingEdges(timerNode.Id).FirstOrDefault();
            if (outEdge != null && nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
            {
                resolvedQTagIndex = tgtNode.ActionData.TargetTagIndex;
            }

            var inTag = product.FindTagByIndex(resolvedInTagIndex);
            var qTag = product.FindTagByIndex(resolvedQTagIndex);

            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerNode.Id}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            if (qTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerNode.Id}' references unknown Output Tag {tData.QTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            string macroId = timerNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(timerNode.Label) ? timerNode.Label : timerNode.Id;

            switch (tData.Type)
            {
                case TimerMacroType.Ton:
                {
                    // R0: IN ON_RISE for PT -> SET Q = 1
                    var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = tData.PresetMs };
                    var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                    var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 0, timerNode.Label);
                    candidateRules.Add((timerNode, trig0, act0, GuardModel.Empty, $"{labelPrefix}_TON_ON", origin0));

                    // R1: IN ON_FALL for 0 -> SET Q = 0
                    var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = 0 };
                    var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                    var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 1, timerNode.Label);
                    candidateRules.Add((timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TON_OFF", origin1));
                    break;
                }

                case TimerMacroType.Tof:
                {
                    // R0: IN ON_RISE for 0 -> SET Q = 1
                    var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = 0 };
                    var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                    var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 0, timerNode.Label);
                    candidateRules.Add((timerNode, trig0, act0, GuardModel.Empty, $"{labelPrefix}_TOF_ON", origin0));

                    // R1: IN ON_FALL for PT, Guard Q == 1 -> SET Q = 0
                    var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = tData.PresetMs };
                    var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                    var guard1 = new GuardModel(qTag, negated: false);
                    var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 1, timerNode.Label);
                    candidateRules.Add((timerNode, trig1, act1, guard1, $"{labelPrefix}_TOF_OFF", origin1));
                    break;
                }

                case TimerMacroType.Tp:
                {
                    // R0: IN ON_RISE for 0, Guard Q == 0 -> SET Q = 1
                    var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = 0 };
                    var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                    var guard0 = new GuardModel(qTag, negated: true);
                    var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 0, timerNode.Label);
                    candidateRules.Add((timerNode, trig0, act0, guard0, $"{labelPrefix}_TP_START", origin0));

                    // R1: Q ON_RISE for PT -> SET Q = 0
                    var trig1 = new TriggerModel(qTag, TriggerKind.OnRise) { ForMs = tData.PresetMs };
                    var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                    var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 1, timerNode.Label);
                    candidateRules.Add((timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TP_EXPIRE", origin1));
                    break;
                }
            }
        }

        // Step 2c: Macro Expansion for Counter nodes (CTU, CTD)
        foreach (var counterNode in counterNodes)
        {
            var cData = counterNode.CounterData ?? new CounterNodeData();
            ushort resolvedCuTagIndex = cData.CuTagIndex;
            ushort? resolvedResetTagIndex = cData.ResetTagIndex;
            ushort resolvedQTagIndex = cData.QTagIndex;

            // Phân giải kết nối dây từ graph:
            // 1. Incoming edges (CU/CD hoặc R)
            foreach (var inEdge in graph.GetIncomingEdges(counterNode.Id))
            {
                if (nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode) && srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    if (string.Equals(inEdge.TargetPort, "R", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(inEdge.TargetPort, "Reset", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedResetTagIndex = srcNode.InputData.TagIndex;
                    }
                    else
                    {
                        resolvedCuTagIndex = srcNode.InputData.TagIndex;
                    }
                }
            }

            // 2. Outgoing edges (Counter Q -> Action)
            foreach (var outEdge in graph.GetOutgoingEdges(counterNode.Id))
            {
                if (nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
                {
                    resolvedQTagIndex = tgtNode.ActionData.TargetTagIndex;
                }
            }

            var cuTag = product.FindTagByIndex(resolvedCuTagIndex);
            var cvTag = product.FindTagByIndex(cData.CvTagIndex);
            var qTag = product.FindTagByIndex(resolvedQTagIndex);
            TagDefinition? resetTag = resolvedResetTagIndex.HasValue ? product.FindTagByIndex(resolvedResetTagIndex.Value) : null;

            if (cuTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterNode.Id}' references unknown Count Input Tag {resolvedCuTagIndex}.",
                    NodeId: counterNode.Id));
                continue;
            }

            if (cvTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterNode.Id}' references unknown CV Register Tag {cData.CvTagIndex}.",
                    NodeId: counterNode.Id));
                continue;
            }

            if (qTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterNode.Id}' references unknown Output Tag {cData.QTagIndex}.",
                    NodeId: counterNode.Id));
                continue;
            }

            string macroId = counterNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(counterNode.Label) ? counterNode.Label : counterNode.Id;
            string macroType = cData.Type == CounterMacroType.Ctd ? "CTD" : "CTU";

            int ruleExpIndex = 0;

            // R0: Count Pulse
            // CTU: CU ON_RISE -> CV += 1
            // CTD: CD ON_RISE -> CV += -1 (decrement)
            int incStep = cData.Type == CounterMacroType.Ctd ? -1 : 1;
            var trigCount = new TriggerModel(cuTag, TriggerKind.OnRise) { ForMs = 0 };
            var actCount = new ActionModel(cvTag, ActionKind.IncrementCounter, incStep);
            var guardCount = resetTag != null ? new GuardModel(resetTag, negated: true) : GuardModel.Empty;
            var originCount = new GeneratedRuleOrigin(counterNode.Id, macroId, macroType, ruleExpIndex++, counterNode.Label);
            candidateRules.Add((counterNode, trigCount, actCount, guardCount, $"{labelPrefix}_{macroType}_COUNT", originCount));

            // R1: Reset (nếu có resetTag)
            // CTU: Reset ON_RISE -> SET CV = 0
            // CTD: Load/Reset ON_RISE -> SET CV = PresetValue
            if (resetTag != null)
            {
                var trigReset = new TriggerModel(resetTag, TriggerKind.OnRise) { ForMs = 0 };
                int resetTargetVal = cData.Type == CounterMacroType.Ctd ? cData.PresetValue : 0;
                var actReset = new ActionModel(cvTag, ActionKind.SetTag, resetTargetVal);
                var originReset = new GeneratedRuleOrigin(counterNode.Id, macroId, macroType, ruleExpIndex++, counterNode.Label);
                candidateRules.Add((counterNode, trigReset, actReset, GuardModel.Empty, $"{labelPrefix}_{macroType}_RESET", originReset));
            }

            // R2: Threshold Trip (Khi đạt ngưỡng -> SET Q = 1)
            // CTU: CV >= PV -> SET Q = 1
            // CTD: CV <= 0 -> SET Q = 1
            var tripOp = cData.Type == CounterMacroType.Ctd ? CompareOperator.LessThanOrEqual : CompareOperator.GreaterThanOrEqual;
            int tripThreshold = cData.Type == CounterMacroType.Ctd ? 0 : cData.PresetValue;
            var trigTrip = new TriggerModel(cvTag, TriggerKind.OnChange) { CompareOp = tripOp, ThresholdLo = tripThreshold, ForMs = 0 };
            var actTrip = new ActionModel(qTag, ActionKind.SetTag, 1);
            var originTrip = new GeneratedRuleOrigin(counterNode.Id, macroId, macroType, ruleExpIndex++, counterNode.Label);
            candidateRules.Add((counterNode, trigTrip, actTrip, GuardModel.Empty, $"{labelPrefix}_{macroType}_TRIP", originTrip));

            // R3: Threshold Clear (Khi chưa đạt / sau reset -> SET Q = 0)
            // CTU: CV < PV -> SET Q = 0
            // CTD: CV > 0 -> SET Q = 0
            var clearOp = cData.Type == CounterMacroType.Ctd ? CompareOperator.GreaterThan : CompareOperator.LessThan;
            int clearThreshold = cData.Type == CounterMacroType.Ctd ? 0 : cData.PresetValue;
            var trigClear = new TriggerModel(cvTag, TriggerKind.OnChange) { CompareOp = clearOp, ThresholdLo = clearThreshold, ForMs = 0 };
            var actClear = new ActionModel(qTag, ActionKind.SetTag, 0);
            var originClear = new GeneratedRuleOrigin(counterNode.Id, macroId, macroType, ruleExpIndex++, counterNode.Label);
            candidateRules.Add((counterNode, trigClear, actClear, GuardModel.Empty, $"{labelPrefix}_{macroType}_CLEAR", originClear));
        }
    }

        // Step 2d: Macro Expansion for Scale nodes (Linear Scaler)
        foreach (var scaleNode in scaleNodes)
        {
            var sData = scaleNode.ScaleData ?? new ScaleNodeData();
            ushort resolvedInTagIndex = sData.InTagIndex;
            ushort? resolvedOutTagIndex = sData.OutTagIndex;

            // 1. Resolve upstream Input connection
            foreach (var inEdge in graph.GetIncomingEdges(scaleNode.Id))
            {
                if (nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode) && srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    resolvedInTagIndex = srcNode.InputData.TagIndex;
                }
            }

            // 2. Resolve downstream Output connection (Scale OUT -> Action)
            foreach (var outEdge in graph.GetOutgoingEdges(scaleNode.Id))
            {
                if (nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
                {
                    resolvedOutTagIndex = tgtNode.ActionData.TargetTagIndex;
                }
            }

            var inTag = product.FindTagByIndex(resolvedInTagIndex);
            var outTag = resolvedOutTagIndex.HasValue ? product.FindTagByIndex(resolvedOutTagIndex.Value) : null;

            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrScaleNoInput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleNode.Id}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: scaleNode.Id));
                continue;
            }

            if (outTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrScaleNoOutput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleNode.Id}' requires an Output Tag (connect OUT to an Action node).",
                    NodeId: scaleNode.Id));
                continue;
            }

            string macroId = scaleNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(scaleNode.Label) ? scaleNode.Label : scaleNode.Id;

            int scaleParam = (int)Math.Round(sData.Gain * 1000.0);
            if (scaleParam == 0 && Math.Abs(sData.Gain) > 1e-6)
            {
                scaleParam = sData.Gain > 0 ? 1 : -1;
            }

            var trigScale = new TriggerModel(inTag, TriggerKind.OnChange)
            {
                ThresholdHi = (int)Math.Round(sData.Offset),
                ForMs = 0
            };
            var actScale = new ActionModel(outTag, ActionKind.ScaleTag, scaleParam);
            var originScale = new GeneratedRuleOrigin(scaleNode.Id, macroId, "SCALE", 0, scaleNode.Label);

            candidateRules.Add((scaleNode, trigScale, actScale, GuardModel.Empty, $"{labelPrefix}_SCALE", originScale));
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        // Step 3: Access Analysis, Conflict Validation & Stable Topological Ordering
        // 3a. Phân tích quyền truy cập đọc/ghi cho từng candidate rule
        var analyzedCandidates = candidateRules
            .Select(c => RuleAccessAnalyzer.Analyze(c.ActionNode, c.Trigger, c.Action, c.Guard, c.RuleName, c.Origin))
            .ToList();

        // 3b. Thẩm định xung đột ghi (Write Conflict Validation)
        var conflictErrors = WriteConflictValidator.Validate(analyzedCandidates, product);
        diagnostics.AddRange(conflictErrors);
        if (conflictErrors.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        // 3c. Xây dựng đồ thị phụ thuộc dữ liệu và phát hiện chu trình (Data Dependency Cycle Detection)
        var depGraph = new RuleDependencyGraph(analyzedCandidates);
        var cycleError = depGraph.DetectCycle();
        if (cycleError != null)
        {
            diagnostics.Add(cycleError);
            return CompileResult.Failure(diagnostics);
        }

        // 3d. Sắp xếp Topo ổn định (Stable Topological Sort)
        var sortedCandidates = StableTopologicalSorter.Sort(depGraph);

        // 3e. Gán RuleIndex tuần tự theo thứ tự thực thi đã được tối ưu
        var rules = new List<Rule>(sortedCandidates.Count);
        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            var item = sortedCandidates[i];
            string finalName = string.IsNullOrWhiteSpace(item.RuleName) ? $"Rule_{i + 1}" : item.RuleName;
            var rule = new Rule(
                ruleIndex: i,
                name: finalName,
                trigger: item.Trigger,
                action: item.Action,
                guard: item.Guard,
                enabled: true)
            {
                ActionNodeId = item.ActionNode.Id
            };
            rules.Add(rule);
        }

        // Step 4: Capacity and Domain Invariants Validation
        if (rules.Count > product.MaxRules)
        {
            diagnostics.Add(new CompileDiagnostic(
                ErrCapacityExceeded,
                DiagnosticSeverity.Error,
                $"Compiled rule count ({rules.Count}) exceeds device limit ({product.MaxRules})."));
        }

        var domainTable = new RuleTable();
        foreach (var r in rules)
        {
            domainTable.AddRule(r);
        }

        var domainValidation = RuleTableValidator.Validate(domainTable);
        if (!domainValidation.IsValid)
        {
            foreach (var err in domainValidation.Errors)
            {
                string? associatedNodeId = null;
                if (err.RuleIndex.HasValue && err.RuleIndex.Value < sortedCandidates.Count)
                {
                    associatedNodeId = sortedCandidates[err.RuleIndex.Value].ActionNode.Id;
                }
                diagnostics.Add(new CompileDiagnostic(
                    err.Code,
                    DiagnosticSeverity.Error,
                    err.Message,
                    NodeId: associatedNodeId));
            }
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        var sourceMap = new Dictionary<int, GeneratedRuleOrigin>();
        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            if (sortedCandidates[i].Origin != null)
            {
                sourceMap[i] = sortedCandidates[i].Origin!;
            }
        }

        var program = new CompiledProgram(
            programId ?? Guid.NewGuid().ToString("N"),
            rules,
            DateTimeOffset.UtcNow,
            sourceMap,
            compiledFbTimers,
            compiledFbCounters);

        return CompileResult.Success(program, diagnostics);
    }
}
