namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Compilation.MacroLowering;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Trình biên dịch đồ thị FBD thành các bảng luật (Rules) và cấu hình nhị phân chuẩn MCU.
/// Đóng vai trò bộ điều phối đường ống (Pipeline Orchestrator) liên kết các bước phân tích,
/// trích xuất ngoại vi, mở rộng macro và sắp xếp Topo tối ưu.
/// </summary>
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
    public const string ErrModbusPortSlaveConflict = "SPLC-COMP-MB-SLAVE";
    public const string ErrActionReadOnlyTarget = "SPLC-COMP-ACT-RO";

    /// <summary>
    /// Biên dịch đồ thị FBD với cấu hình mặc định.
    /// </summary>
    public CompileResult Compile(LogicGraph graph, ProductDefinition product, string? programId = null) =>
        Compile(graph, product, programId, null, null);

    /// <summary>
    /// Biên dịch đồ thị FBD cùng cấu hình cổng Modbus Master/Slave.
    /// </summary>
    public CompileResult Compile(
        LogicGraph graph,
        ProductDefinition product,
        string? programId,
        IReadOnlyList<ModbusPortConfigDto>? modbusPorts,
        IReadOnlyList<ModbusSlaveMapEntryDto>? slaveMapConfigs)
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
        var mqttNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttPublish).ToList();
        var mqttSubNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttSubscribe).ToList();
        var modbusReadNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusRead).ToList();
        var modbusWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusWrite).ToList();
        var directWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.DirectWrite).ToList();
        var httpNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.HttpClient).ToList();
        var tcpNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.TcpClient).ToList();

        // Step 2: Peripheral Extraction & Modbus Port Safety Gate
        var peripheralResult = PeripheralConfigExtractor.Extract(graph, product, nodeMap, modbusPorts);
        diagnostics.AddRange(peripheralResult.Diagnostics);
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        // Kiểm tra đồ thị rỗng hoàn toàn
        if (actionNodes.Count == 0 && timerNodes.Count == 0 && counterNodes.Count == 0 && scaleNodes.Count == 0 &&
            mqttNodes.Count == 0 && mqttSubNodes.Count == 0 && modbusReadNodes.Count == 0 && modbusWriteNodes.Count == 0 &&
            directWriteNodes.Count == 0 && httpNodes.Count == 0 && tcpNodes.Count == 0)
        {
            diagnostics.Add(new CompileDiagnostic(
                WarnNoActions,
                DiagnosticSeverity.Warning,
                "Graph does not contain any Action, Timer, Counter, Scale, MQTT, Modbus, Direct Write, or DTU nodes. 0 rules produced."));

            var emptyProgram = new CompiledProgram(
                programId ?? Guid.NewGuid().ToString("N"),
                Array.Empty<Rule>(),
                DateTimeOffset.UtcNow);

            return CompileResult.Success(emptyProgram, diagnostics);
        }

        // Kiểm tra đồ thị chỉ thuần cấu hình telemetry (không có Action/Timer/Counter/Scale hay Modbus Status rules)
        bool hasModbusStatusRules = modbusReadNodes.Any(m => m.ModbusReadData?.ValidTagIndex != null || m.ModbusReadData?.ErrorTagIndex != null);
        if (actionNodes.Count == 0 && timerNodes.Count == 0 && counterNodes.Count == 0 && scaleNodes.Count == 0 && !hasModbusStatusRules)
        {
            var telemetryOnlyProgram = new CompiledProgram(
                programId ?? Guid.NewGuid().ToString("N"),
                Array.Empty<Rule>(),
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                peripheralResult.MqttConfigs,
                peripheralResult.MqttSubConfigs,
                peripheralResult.ModbusReadConfigs,
                peripheralResult.ModbusWriteConfigs,
                peripheralResult.DirectWriteConfigs,
                slaveMapConfigs,
                dtuHttpConfig: peripheralResult.DtuHttpConfig,
                dtuTcpConfig: peripheralResult.DtuTcpConfig,
                dtuConfig: peripheralResult.DtuConfig);

            return CompileResult.Success(telemetryOnlyProgram, diagnostics);
        }

        var candidateRules = new List<CandidateRule>();

        // Step 3: Path enumeration from each Action terminal
        ActionPathTracer.Trace(graph, product, nodeMap, actionNodes, candidateRules, diagnostics);

        // Step 4: Dedicated Function Blocks (FB) Generation (Timers & Counters)
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
                    TimerMacroType.Cycle => SPLC_TimerMode.CYCLE,
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

            for (int i = 0; i < counterNodes.Count; i++)
            {
                var counterNode = counterNodes[i];
                var cData = counterNode.CounterData ?? new CounterNodeData();
                var mode = cData.Type == CounterMacroType.Ctd ? SPLC_CounterMode.CTD : SPLC_CounterMode.CTU;

                // Tính retain_tag_index động từ DeviceResourceInfo làm con trỏ thanh ghi lưu trữ CV (Storage Register Binding)
                ushort retainTagIndex = ModbusRegisterMap.FbCounterRetainNone;
                ushort resolvedCvTagIndex = cData.CvTagIndex;
                var cvTagDef = product.FindTagByIndex(resolvedCvTagIndex);
                if (cvTagDef == null)
                {
                    // Quy ước: Counter thứ i dùng tag COUNTER[i] làm CV nếu cData.CvTagIndex chưa được cấu hình
                    var defaultCounterTag = product.FindTagByName($"COUNTER{i}");
                    if (defaultCounterTag != null)
                    {
                        cvTagDef = defaultCounterTag;
                        resolvedCvTagIndex = defaultCounterTag.TagIndex;
                        cData.CvTagIndex = resolvedCvTagIndex;
                    }
                }

                // Chấp nhận mọi thanh ghi lưu trữ hợp lệ (VREG, VREG_RETAIN, VFLAG, COUNTER...)
                if (cvTagDef != null && resolvedCvTagIndex != ModbusRegisterMap.FbCounterRetainNone)
                {
                    retainTagIndex = resolvedCvTagIndex;
                }
                else if (resolvedCvTagIndex < product.Resources.TotalTags && resolvedCvTagIndex != ModbusRegisterMap.FbCounterRetainNone)
                {
                    retainTagIndex = resolvedCvTagIndex;
                }

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

        // Step 5: Macro Expansion
        // 5a. Timer nodes (TON, TOF, TP, CYCLE)
        TimerMacroLowering.Lower(graph, product, nodeMap, timerNodes, candidateRules, diagnostics);

        // 5b. Counter nodes (CTU, CTD)
        CounterMacroLowering.Lower(graph, product, nodeMap, counterNodes, candidateRules, diagnostics);

        // 5c. Scale nodes (Linear Scaler)
        ScaleMacroLowering.Lower(graph, product, nodeMap, scaleNodes, candidateRules, diagnostics);

        // 5d. Modbus Master Status Indicators (Valid / Error)
        ModbusStatusMacroLowering.Lower(graph, product, modbusReadNodes, candidateRules);

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        // Step 6: Access Analysis, Conflict Validation & Stable Topological Ordering
        var analyzedCandidates = candidateRules
            .Select(c => RuleAccessAnalyzer.Analyze(c.ActionNode, c.Trigger, c.Action, c.Guard, c.RuleName, c.Origin))
            .ToList();

        var conflictErrors = WriteConflictValidator.Validate(analyzedCandidates, product);
        diagnostics.AddRange(conflictErrors);
        if (conflictErrors.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return CompileResult.Failure(diagnostics);
        }

        var depGraph = new RuleDependencyGraph(analyzedCandidates);
        var cycleError = depGraph.DetectCycle();
        if (cycleError != null)
        {
            diagnostics.Add(cycleError);
            return CompileResult.Failure(diagnostics);
        }

        var sortedCandidates = StableTopologicalSorter.Sort(depGraph);

        // Gán RuleIndex tuần tự theo thứ tự thực thi đã được tối ưu
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

        // Step 7: Capacity and Domain Invariants Validation
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

        // Step 8: Cập nhật RuleRef cho các Timer blocks dựa trên thứ tự cuối cùng của các rules
        for (int t = 0; t < timerNodes.Count && t < compiledFbTimers.Count; t++)
        {
            var tNode = timerNodes[t];
            var tData = tNode.TimerData ?? new TimerNodeData();
            int timingExpIndex = (tData.Type == TimerMacroType.Ton) ? 0 : 1;

            int ruleIdx = -1;
            for (int r = 0; r < sortedCandidates.Count; r++)
            {
                var cand = sortedCandidates[r];
                if (cand.Origin != null &&
                    cand.Origin.SourceNodeId == tNode.Id &&
                    cand.Origin.ExpansionIndex == timingExpIndex)
                {
                    ruleIdx = r;
                    break;
                }
            }

            if (ruleIdx >= 0)
            {
                compiledFbTimers[t].RuleRef = (ushort)(ruleIdx + 1);
            }
            else
            {
                compiledFbTimers[t].RuleRef = 0;
            }
        }

        var program = new CompiledProgram(
            programId ?? Guid.NewGuid().ToString("N"),
            rules,
            DateTimeOffset.UtcNow,
            sourceMap,
            compiledFbTimers,
            compiledFbCounters,
            peripheralResult.MqttConfigs,
            peripheralResult.MqttSubConfigs,
            peripheralResult.ModbusReadConfigs,
            peripheralResult.ModbusWriteConfigs,
            peripheralResult.DirectWriteConfigs,
            slaveMapConfigs,
            dtuHttpConfig: peripheralResult.DtuHttpConfig,
            dtuTcpConfig: peripheralResult.DtuTcpConfig,
            dtuConfig: peripheralResult.DtuConfig);

        return CompileResult.Success(program, diagnostics);
    }

    internal static CompareOperator InvertCompareOp(CompareOperator op) =>
        CompilationHelpers.InvertCompareOp(op);

    internal static bool IsFloatThreshold(double lo, double hi, TagDefinition tag) =>
        CompilationHelpers.IsFloatThreshold(lo, hi, tag);

    internal static string GetNodeLabel(LogicNode? node, string fallback = "Node") =>
        CompilationHelpers.GetNodeLabel(node, fallback);

    internal static ushort? ResolveSourcePortTagIndex(LogicNode srcNode, string sourcePort) =>
        CompilationHelpers.ResolveSourcePortTagIndex(srcNode, sourcePort);
}
