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
        var mqttNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttPublish).ToList();
        var mqttSubNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttSubscribe).ToList();
        var modbusReadNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusRead).ToList();
        var modbusWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusWrite).ToList();
        var directWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.DirectWrite).ToList();

        // Extract MQTT Telemetry Configs
        var compiledMqttConfigs = new List<MqttTelemetryConfigDto>();
        foreach (var mqttNode in mqttNodes)
        {
            var mData = mqttNode.MqttPublishData ?? new MqttPublishNodeData();
            var inEdges = graph.GetIncomingEdges(mqttNode.Id).ToList();
            var telemetryFields = new List<MqttPayloadFieldDto>();

            ushort? enTag = mData.EnTagIndex;
            foreach (var inEdge in inEdges)
            {
                if (nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode))
                {
                    ushort tagIdx = 0;
                    string fieldKey = !string.IsNullOrEmpty(inEdge.TargetPort) ? inEdge.TargetPort : "val";
                    string unit = "";

                    if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                    {
                        tagIdx = srcNode.InputData.TagIndex;
                        if (string.Equals(fieldKey, "val", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = product.FindTagByIndex(tagIdx);
                            if (t != null)
                            {
                                fieldKey = t.Name.ToLowerInvariant();
                            }
                        }
                    }
                    else if (srcNode.Kind == LogicNodeKind.Scale && srcNode.ScaleData != null)
                    {
                        tagIdx = srcNode.ScaleData.InTagIndex;
                        var t = product.FindTagByIndex(tagIdx);
                        if (t != null && string.Equals(fieldKey, "val", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldKey = $"{t.Name.ToLowerInvariant()}_scaled";
                        }
                        unit = srcNode.ScaleData.Unit;
                    }
                    else if (srcNode.Kind == LogicNodeKind.Timer && srcNode.TimerData != null)
                    {
                        tagIdx = srcNode.TimerData.QTagIndex;
                        if (string.Equals(fieldKey, "val", StringComparison.OrdinalIgnoreCase)) fieldKey = "timer_q";
                    }
                    else if (srcNode.Kind == LogicNodeKind.Counter && srcNode.CounterData != null)
                    {
                        tagIdx = srcNode.CounterData.CvTagIndex;
                        if (string.Equals(fieldKey, "val", StringComparison.OrdinalIgnoreCase)) fieldKey = "counter_cv";
                    }

                    if (string.Equals(inEdge.TargetPort, "Trig", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(inEdge.TargetPort, "EN", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(mData.PublishMode, "Event", StringComparison.OrdinalIgnoreCase))
                        {
                            enTag = tagIdx;
                        }
                        continue;
                    }

                    telemetryFields.Add(new MqttPayloadFieldDto
                    {
                        Key = fieldKey,
                        TagIndex = tagIdx,
                        Unit = unit
                    });
                }
            }

            if (telemetryFields.Count == 0 && mData.Fields.Count > 0)
            {
                telemetryFields.AddRange(mData.Fields);
            }

            compiledMqttConfigs.Add(new MqttTelemetryConfigDto
            {
                NodeId = mqttNode.Id,
                Topic = mData.Topic,
                PublishMode = mData.PublishMode,
                Deadband = mData.Deadband,
                IntervalMs = mData.IntervalMs,
                Qos = mData.Qos,
                Retain = mData.Retain,
                PayloadMode = mData.PayloadMode,
                EnTagIndex = enTag,
                DataTagIndex = mData.DataTagIndex,
                CustomTemplate = mData.CustomTemplate,
                IncludeDevice = mData.IncludeDevice,
                IncludeTimestamp = mData.IncludeTimestamp,
                Fields = telemetryFields
            });
        }

        // Extract MQTT Subscription Configs
        var compiledMqttSubConfigs = new List<MqttSubscriptionConfigDto>();
        foreach (var subNode in mqttSubNodes)
        {
            var sData = subNode.MqttSubscribeData ?? new MqttSubscribeNodeData();
            compiledMqttSubConfigs.Add(new MqttSubscriptionConfigDto
            {
                NodeId = subNode.Id,
                Topic = sData.Topic,
                Qos = sData.Qos,
                QueueMode = sData.QueueMode,
                QueueCapacity = sData.QueueCapacity,
                IgnoreRetainedOnBoot = sData.IgnoreRetained,
                TimeoutMs = sData.TimeoutMs,
                FailSafeAction = sData.FailSafeAction,
                Pins = new List<MqttPinBindingDto>(sData.Pins)
            });
        }

        // Extract Modbus Master Read Configs
        var compiledModbusConfigs = new List<ModbusMasterReadConfigDto>();
        for (int i = 0; i < modbusReadNodes.Count; i++)
        {
            var mbNode = modbusReadNodes[i];
            var mbData = mbNode.ModbusReadData ?? new ModbusReadNodeData();
            compiledModbusConfigs.Add(new ModbusMasterReadConfigDto
            {
                NodeId = mbNode.Id,
                BlockIndex = i,
                PortIndex = mbData.PortIndex,
                Enabled = true,
                SlaveId = mbData.SlaveId,
                FunctionCode = mbData.FunctionCode,
                StartAddress = mbData.StartAddress,
                RegCount = mbData.RegCount,
                PollIntervalMs = mbData.PollIntervalMs,
                TimeoutMs = mbData.TimeoutMs,
                ReadMode = mbData.ReadMode,
                EnTagIndex = mbData.EnTagIndex,
                TrigTagIndex = mbData.TrigTagIndex,
                Pins = new List<ModbusMasterPinBindingDto>(mbData.Pins)
            });
        }

        // Extract Modbus Master Write Configs
        var compiledModbusWriteConfigs = new List<ModbusMasterWriteConfigDto>();
        for (int i = 0; i < modbusWriteNodes.Count; i++)
        {
            var mbNode = modbusWriteNodes[i];
            var mbData = mbNode.ModbusWriteData ?? new ModbusWriteNodeData();
            compiledModbusWriteConfigs.Add(new ModbusMasterWriteConfigDto
            {
                NodeId = mbNode.Id,
                BlockIndex = i,
                PortIndex = mbData.PortIndex,
                Enabled = true,
                SlaveId = mbData.SlaveId,
                WriteMode = mbData.WriteMode,
                FunctionCode = mbData.FunctionCode,
                StartAddress = mbData.StartAddress,
                PointCount = mbData.PointCount,
                BaseAddressCommand = mbData.BaseAddressCommand,
                BaseAddressParam = mbData.BaseAddressParam,
                ChannelCount = mbData.ChannelCount,
                IsExclusiveChannel = mbData.IsExclusiveChannel,
                TriggerMode = mbData.TriggerMode,
                CyclicIntervalMs = mbData.CyclicIntervalMs,
                TimeoutMs = mbData.TimeoutMs,
                TrigTagIndex = mbData.TrigTagIndex,
                ChannelTagIndex = mbData.ChannelTagIndex,
                CommandTagIndex = mbData.CommandTagIndex,
                ParamTagIndex = mbData.ParamTagIndex,
                Pins = new List<ModbusMasterPinBindingDto>(mbData.Pins)
            });
        }

        // Extract Direct Write Configs (Direct Physical Output Actuators)
        var compiledDirectWriteConfigs = new List<DirectWriteConfigDto>();
        for (int i = 0; i < directWriteNodes.Count; i++)
        {
            var dwNode = directWriteNodes[i];
            var dwData = dwNode.DirectWriteData ?? new DirectWriteNodeData();
            compiledDirectWriteConfigs.Add(new DirectWriteConfigDto
            {
                Id = dwNode.Id,
                TargetPin = dwData.TargetPin,
                Mode = dwData.Mode,
                ChannelCount = dwData.ChannelCount,
                IsExclusiveChannel = dwData.IsExclusiveChannel,
                PulseWidthUs = dwData.PulseWidthUs,
                PwmFrequencyHz = dwData.PwmFrequencyHz,
                TriggerMode = dwData.TriggerMode,
                TimeoutMs = dwData.TimeoutMs,
                ChannelTagIndex = dwData.ChannelTagIndex,
                ParamTagIndex = dwData.ParamTagIndex,
                DefaultColorRgb = dwData.DefaultColorRgb
            });
        }

        if (actionNodes.Count == 0 && timerNodes.Count == 0 && counterNodes.Count == 0 && scaleNodes.Count == 0 && mqttNodes.Count == 0 && mqttSubNodes.Count == 0 && modbusReadNodes.Count == 0 && modbusWriteNodes.Count == 0 && directWriteNodes.Count == 0)
        {
            diagnostics.Add(new CompileDiagnostic(
                WarnNoActions,
                DiagnosticSeverity.Warning,
                "Graph does not contain any Action, Timer, Counter, Scale, MQTT, Modbus, or Direct Write nodes. 0 rules produced."));

            var emptyProgram = new CompiledProgram(
                programId ?? Guid.NewGuid().ToString("N"),
                Array.Empty<Rule>(),
                DateTimeOffset.UtcNow);

            return CompileResult.Success(emptyProgram, diagnostics);
        }

        if (actionNodes.Count == 0 && timerNodes.Count == 0 && counterNodes.Count == 0 && scaleNodes.Count == 0)
        {
            var telemetryOnlyProgram = new CompiledProgram(
                programId ?? Guid.NewGuid().ToString("N"),
                Array.Empty<Rule>(),
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                compiledMqttConfigs,
                compiledMqttSubConfigs,
                compiledModbusConfigs,
                compiledModbusWriteConfigs,
                compiledDirectWriteConfigs);

            return CompileResult.Success(telemetryOnlyProgram, diagnostics);
        }

        var candidateRules = new List<(LogicNode ActionNode, TriggerModel Trigger, ActionModel Action, GuardModel Guard, string RuleName, GeneratedRuleOrigin? Origin)>();

        // Step 2: Path enumeration from each Action terminal
        foreach (var actionNode in actionNodes)
        {
            var actionIncoming = graph.GetIncomingEdges(actionNode.Id).FirstOrDefault();
            if (actionIncoming == null || !nodeMap.TryGetValue(actionIncoming.SourceNodeId, out var directSource))
            {
                var actionLabel = GetNodeLabel(actionNode, "Action");
                diagnostics.Add(new CompileDiagnostic(
                    ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionLabel}' is not connected to a Trigger or Guard.",
                    NodeId: actionNode.Id));
                continue;
            }

            if (directSource.Kind is LogicNodeKind.Counter or LogicNodeKind.Timer or LogicNodeKind.Scale or LogicNodeKind.MqttPublish or LogicNodeKind.MqttSubscribe or LogicNodeKind.ModbusRead or LogicNodeKind.ModbusWrite or LogicNodeKind.DirectWrite)
            {
                // Action node connects from Counter, Timer, Scale, MQTT, Modbus, or Direct Write port as visual destination.
                // The rules are fully expanded in Step 2b / 2c / 2d (Macro Expansion) or handled by communication subsystems.
                continue;
            }

            LogicNode? triggerNode = null;
            LogicNode? guardNode = null;
            LogicNode? directComparatorNode = null;

            if (directSource.Kind == LogicNodeKind.Trigger)
            {
                triggerNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Comparator)
            {
                directComparatorNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Guard)
            {
                guardNode = directSource;
                var guardIncoming = graph.GetIncomingEdges(guardNode.Id).FirstOrDefault();
                if (guardIncoming == null || !nodeMap.TryGetValue(guardIncoming.SourceNodeId, out var guardSource))
                {
                    var guardLabel = GetNodeLabel(guardNode, "Guard");
                    diagnostics.Add(new CompileDiagnostic(
                        ErrGuardNoTrigger,
                        DiagnosticSeverity.Error,
                        $"Guard node '{guardLabel}' is not connected to an upstream Trigger or Comparator.",
                        NodeId: guardNode.Id));
                    continue;
                }

                if (guardSource.Kind == LogicNodeKind.Trigger)
                {
                    triggerNode = guardSource;
                }
                else if (guardSource.Kind == LogicNodeKind.Comparator)
                {
                    directComparatorNode = guardSource;
                }
                else
                {
                    var guardLabel2 = GetNodeLabel(guardNode, "Guard");
                    diagnostics.Add(new CompileDiagnostic(
                        ErrGuardNoTrigger,
                        DiagnosticSeverity.Error,
                        $"Guard node '{guardLabel2}' must be connected to an upstream Trigger or Comparator.",
                        NodeId: guardNode.Id));
                    continue;
                }
            }
            else
            {
                var actionLabel2 = GetNodeLabel(actionNode, "Action");
                diagnostics.Add(new CompileDiagnostic(
                    ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionLabel2}' must be fed by Trigger, Comparator, or Guard, but got {directSource.Kind}.",
                    NodeId: actionNode.Id));
                continue;
            }

            // Trace Trigger upstream input (or via Comparator)
            LogicNode? inputNode = null;
            LogicNode? comparatorNode = directComparatorNode;

            if (triggerNode != null)
            {
                var triggerIncoming = graph.GetIncomingEdges(triggerNode.Id).FirstOrDefault();
                if (triggerIncoming != null && nodeMap.TryGetValue(triggerIncoming.SourceNodeId, out var candidateSrc))
                {
                    if (candidateSrc.Kind == LogicNodeKind.Input)
                    {
                        inputNode = candidateSrc;
                    }
                    else if (candidateSrc.Kind == LogicNodeKind.Comparator)
                    {
                        comparatorNode = candidateSrc;
                        var compIncoming = graph.GetIncomingEdges(comparatorNode.Id).FirstOrDefault();
                        if (compIncoming != null && nodeMap.TryGetValue(compIncoming.SourceNodeId, out var compSrc) && compSrc.Kind == LogicNodeKind.Input)
                        {
                            inputNode = compSrc;
                        }
                    }
                }
            }
            else if (directComparatorNode != null)
            {
                var compIncoming = graph.GetIncomingEdges(directComparatorNode.Id).FirstOrDefault();
                if (compIncoming != null && nodeMap.TryGetValue(compIncoming.SourceNodeId, out var compSrc) && compSrc.Kind == LogicNodeKind.Input)
                {
                    inputNode = compSrc;
                }
            }

            var triggerData = triggerNode?.TriggerData ?? new TriggerNodeData { Type = TriggerKind.OnChange };
            bool requiresInput = triggerData.Type is not (TriggerKind.Interval or TriggerKind.TimeWindow);

            if (requiresInput && inputNode == null)
            {
                string missingNodeId = triggerNode?.Id ?? directComparatorNode?.Id ?? actionNode.Id;
                string missingNodeLabel = GetNodeLabel(triggerNode, GetNodeLabel(directComparatorNode, "Trigger"));
                diagnostics.Add(new CompileDiagnostic(
                    ErrTriggerNoInput,
                    DiagnosticSeverity.Error,
                    $"Trigger/Comparator node '{missingNodeLabel}' requires an upstream Input Tag.",
                    NodeId: missingNodeId,
                    FieldName: "InputTag"));
                continue;
            }

            // Resolve Trigger Tag
            ushort triggerTagIndex = inputNode?.InputData?.TagIndex ?? 0;
            if (comparatorNode?.ComparatorData != null)
            {
                triggerTagIndex = comparatorNode.ComparatorData.InTagIndex;
            }
            var triggerTag = product.FindTagByIndex(triggerTagIndex);
            if (triggerTag == null)
            {
                if (requiresInput)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrTagNotFound,
                        DiagnosticSeverity.Error,
                        $"Trigger Tag index {triggerTagIndex} not found in device profile.",
                        NodeId: inputNode?.Id ?? comparatorNode?.Id ?? triggerNode?.Id ?? actionNode.Id));
                    continue;
                }
                // Fallback virtual/placeholder for interval/timewindow
                triggerTag = new TagDefinition(0, "NONE", TagKind.None, TagDataType.Boolean, isReadOnly: true);
            }

            // Fuse Comparator parameters into TriggerModel if present
            var compareOp = comparatorNode?.ComparatorData?.Operator ?? triggerData.CompareOp;
            var thresholdLo = comparatorNode?.ComparatorData != null ? comparatorNode.ComparatorData.ThresholdLo : triggerData.ThresholdLo;
            var thresholdHi = comparatorNode?.ComparatorData != null ? comparatorNode.ComparatorData.ThresholdHi : triggerData.ThresholdHi;

            var triggerModel = new TriggerModel(triggerTag, triggerData.Type)
            {
                CompareOp = compareOp,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi,
                IsFloat = IsFloatThreshold(thresholdLo, thresholdHi, triggerTag),
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

        // Bước 2b: Macro Expansion for Timer nodes (TON, TOF, TP) - Luôn sinh cho cả WireProfile 1 và >= 2
        foreach (var timerNode in timerNodes)
        {
            var tData = timerNode.TimerData ?? new TimerNodeData();
            ushort resolvedInTagIndex = tData.InTagIndex;
            ushort resolvedQTagIndex = tData.QTagIndex;
            TriggerNodeData? customTrigData = null;
            GuardModel interlockGuard = GuardModel.Empty;

            // Phân giải kết nối ngõ vào của Timer:
            // Hỗ trợ nhận từ Input (Boolean), Trigger (Comparator/Edge), hoặc Guard (Interlock)
            var inEdge = graph.GetIncomingEdges(timerNode.Id).FirstOrDefault();
            if (inEdge != null && nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode))
            {
                if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    resolvedInTagIndex = srcNode.InputData.TagIndex;
                }
                else if (srcNode.Kind == LogicNodeKind.Trigger && srcNode.TriggerData != null)
                {
                    customTrigData = srcNode.TriggerData;
                    var trigInEdge = graph.GetIncomingEdges(srcNode.Id).FirstOrDefault();
                    if (trigInEdge != null && nodeMap.TryGetValue(trigInEdge.SourceNodeId, out var inpNode) && inpNode.Kind == LogicNodeKind.Input && inpNode.InputData != null)
                    {
                        resolvedInTagIndex = inpNode.InputData.TagIndex;
                    }
                }
                else if (srcNode.Kind == LogicNodeKind.Guard && srcNode.GuardData != null)
                {
                    var gTag = product.FindTagByIndex(srcNode.GuardData.TagIndex);
                    if (gTag != null)
                    {
                        interlockGuard = new GuardModel(gTag, srcNode.GuardData.Negated);
                    }

                    // Upstream của Guard là Trigger
                    var guardInEdge = graph.GetIncomingEdges(srcNode.Id).FirstOrDefault();
                    if (guardInEdge != null && nodeMap.TryGetValue(guardInEdge.SourceNodeId, out var trigNode) && trigNode.Kind == LogicNodeKind.Trigger && trigNode.TriggerData != null)
                    {
                        customTrigData = trigNode.TriggerData;
                        var trigInEdge = graph.GetIncomingEdges(trigNode.Id).FirstOrDefault();
                        if (trigInEdge != null && nodeMap.TryGetValue(trigInEdge.SourceNodeId, out var inpNode) && inpNode.Kind == LogicNodeKind.Input && inpNode.InputData != null)
                        {
                            resolvedInTagIndex = inpNode.InputData.TagIndex;
                        }
                    }
                }
            }

            // Nếu Timer node được nối dây sang Action node downstream -> lấy QTagIndex từ Action node
            var outEdge = graph.GetOutgoingEdges(timerNode.Id).FirstOrDefault();
            if (outEdge != null && nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
            {
                resolvedQTagIndex = tgtNode.ActionData.TargetTagIndex;
            }

            var inTag = product.FindTagByIndex(resolvedInTagIndex);
            var qTag = product.FindTagByIndex(resolvedQTagIndex);

            var timerLabel = GetNodeLabel(timerNode, "Timer");
            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerLabel}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            if (qTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerLabel}' references unknown Output Tag {tData.QTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            string macroId = timerNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(timerNode.Label) ? timerNode.Label : timerNode.Id;

            switch (tData.Type)
            {
                case TimerMacroType.Ton:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // TON với Comparator Trigger (ví dụ AI0 > 80 duy trì liên tục trong PresetMs -> Q = 1)
                        // R0: Điều kiện đúng duy trì đủ PresetMs -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = tData.PresetMs
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 0, timerNode.Label);
                        candidateRules.Add((timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TON_ON", origin0));

                        // R1: Điều kiện đảo ngược rớt ngưỡng -> tức thời Reset Q = 0
                        var invertedOp = InvertCompareOp(customTrigData.CompareOp);
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnChange)
                        {
                            CompareOp = invertedOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 1, timerNode.Label);
                        candidateRules.Add((timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TON_OFF", origin1));
                    }
                    else
                    {
                        // R0: IN ON_RISE for PT -> SET Q = 1
                        var trigKind0 = customTrigData?.Type ?? TriggerKind.OnRise;
                        var trig0 = new TriggerModel(inTag, trigKind0) { ForMs = tData.PresetMs };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 0, timerNode.Label);
                        candidateRules.Add((timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TON_ON", origin0));

                        // R1: IN ON_FALL for 0 -> SET Q = 0
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = 0 };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 1, timerNode.Label);
                        candidateRules.Add((timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TON_OFF", origin1));
                    }
                    break;
                }

                case TimerMacroType.Tof:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // R0: Điều kiện đúng -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 0, timerNode.Label);
                        candidateRules.Add((timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TOF_ON", origin0));

                        // R1: Điều kiện đảo ngược duy trì đủ PresetMs, Guard Q == 1 -> SET Q = 0
                        var invertedOp = InvertCompareOp(customTrigData.CompareOp);
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnChange)
                        {
                            CompareOp = invertedOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = tData.PresetMs
                        };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var guard1 = new GuardModel(qTag, negated: false);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 1, timerNode.Label);
                        candidateRules.Add((timerNode, trig1, act1, guard1, $"{labelPrefix}_TOF_OFF", origin1));
                    }
                    else
                    {
                        // R0: IN ON_RISE for 0 -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = 0 };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 0, timerNode.Label);
                        candidateRules.Add((timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TOF_ON", origin0));

                        // R1: IN ON_FALL for PT, Guard Q == 1 -> SET Q = 0
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = tData.PresetMs };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var guard1 = new GuardModel(qTag, negated: false);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 1, timerNode.Label);
                        candidateRules.Add((timerNode, trig1, act1, guard1, $"{labelPrefix}_TOF_OFF", origin1));
                    }
                    break;
                }

                case TimerMacroType.Tp:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // R0: Điều kiện đúng, Guard Q == 0 -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var guard0 = new GuardModel(qTag, negated: true);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 0, timerNode.Label);
                        candidateRules.Add((timerNode, trig0, act0, guard0, $"{labelPrefix}_TP_START", origin0));

                        // R1: Q ON_RISE for PT -> SET Q = 0
                        var trig1 = new TriggerModel(qTag, TriggerKind.OnRise) { ForMs = tData.PresetMs };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 1, timerNode.Label);
                        candidateRules.Add((timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TP_EXPIRE", origin1));
                    }
                    else
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
                    }
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
            TriggerNodeData? customCuTrigData = null;

            // Phân giải kết nối dây từ graph:
            // 1. Incoming edges (CU/CD hoặc R)
            foreach (var inEdge in graph.GetIncomingEdges(counterNode.Id))
            {
                if (nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode))
                {
                    bool isResetPort = string.Equals(inEdge.TargetPort, "R", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(inEdge.TargetPort, "Reset", StringComparison.OrdinalIgnoreCase);

                    if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                    {
                        if (isResetPort) resolvedResetTagIndex = srcNode.InputData.TagIndex;
                        else resolvedCuTagIndex = srcNode.InputData.TagIndex;
                    }
                    else if (srcNode.Kind == LogicNodeKind.Trigger && srcNode.TriggerData != null)
                    {
                        var trigInEdge = graph.GetIncomingEdges(srcNode.Id).FirstOrDefault();
                        if (trigInEdge != null && nodeMap.TryGetValue(trigInEdge.SourceNodeId, out var inpNode) && inpNode.Kind == LogicNodeKind.Input && inpNode.InputData != null)
                        {
                            if (isResetPort)
                            {
                                resolvedResetTagIndex = inpNode.InputData.TagIndex;
                            }
                            else
                            {
                                resolvedCuTagIndex = inpNode.InputData.TagIndex;
                                customCuTrigData = srcNode.TriggerData;
                            }
                        }
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
            if (cvTag == null)
            {
                // Quy ước: Counter thứ i dùng tag COUNTER[i] làm CV nếu cData.CvTagIndex chưa được cấu hình
                int counterIdx = counterNodes.IndexOf(counterNode);
                if (counterIdx >= 0)
                {
                    var defaultCounterTag = product.FindTagByName($"COUNTER{counterIdx}");
                    if (defaultCounterTag != null)
                    {
                        cvTag = defaultCounterTag;
                        cData.CvTagIndex = defaultCounterTag.TagIndex;
                    }
                }
            }

            var qTag = product.FindTagByIndex(resolvedQTagIndex);
            TagDefinition? resetTag = resolvedResetTagIndex.HasValue ? product.FindTagByIndex(resolvedResetTagIndex.Value) : null;

            var counterLabel = GetNodeLabel(counterNode, "Counter");
            if (cuTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterLabel}' references unknown Count Input Tag {resolvedCuTagIndex}.",
                    NodeId: counterNode.Id));
                continue;
            }

            if (cvTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterLabel}' references unknown CV Register Tag {cData.CvTagIndex}.",
                    NodeId: counterNode.Id));
                continue;
            }

            if (qTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Counter node '{counterLabel}' references unknown Output Tag {cData.QTagIndex}.",
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
            TriggerModel trigCount;
            if (customCuTrigData != null && customCuTrigData.CompareOp != CompareOperator.None)
            {
                bool cIsFloat = IsFloatThreshold(customCuTrigData.ThresholdLo, customCuTrigData.ThresholdHi, cuTag);
                trigCount = new TriggerModel(cuTag, customCuTrigData.Type)
                {
                    CompareOp = customCuTrigData.CompareOp,
                    ThresholdLo = customCuTrigData.ThresholdLo,
                    ThresholdHi = customCuTrigData.ThresholdHi,
                    IsFloat = cIsFloat,
                    ForMs = 0
                };
            }
            else
            {
                trigCount = new TriggerModel(cuTag, customCuTrigData?.Type ?? TriggerKind.OnRise) { ForMs = 0 };
            }
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

            var scaleLabel = GetNodeLabel(scaleNode, "Scale");

            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrScaleNoInput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleLabel}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: scaleNode.Id));
                continue;
            }

            if (outTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrScaleNoOutput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleLabel}' requires an Output Tag (connect OUT to an Action node).",
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

        // Step 5: Cập nhật RuleRef cho các Timer blocks dựa trên thứ tự cuối cùng của các rules
        // rule_ref = (vị trí của rule đếm giờ của Timer đó trong bảng rule gửi xuống, tính từ 0) + 1
        // - TON: Rule có expansion index = 0 (IN ON_RISE for PT -> SET Q = 1)
        // - TOF: Rule có expansion index = 1 (IN ON_FALL for PT, Guard Q == 1 -> SET Q = 0)
        // - TP:  Rule có expansion index = 1 (Q ON_RISE for PT -> SET Q = 0)
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
            compiledMqttConfigs,
            compiledMqttSubConfigs,
            compiledModbusConfigs,
            compiledModbusWriteConfigs,
            compiledDirectWriteConfigs);

        return CompileResult.Success(program, diagnostics);
    }

    private static CompareOperator InvertCompareOp(CompareOperator op) => op switch
    {
        CompareOperator.GreaterThan => CompareOperator.LessThanOrEqual,
        CompareOperator.LessThan => CompareOperator.GreaterThanOrEqual,
        CompareOperator.GreaterThanOrEqual => CompareOperator.LessThan,
        CompareOperator.LessThanOrEqual => CompareOperator.GreaterThan,
        CompareOperator.Equal => CompareOperator.NotEqual,
        CompareOperator.NotEqual => CompareOperator.Equal,
        _ => CompareOperator.None
    };

    private static bool IsFloatThreshold(double lo, double hi, TagDefinition tag) =>
        Math.Abs(lo - Math.Round(lo)) > 1e-6 ||
        Math.Abs(hi - Math.Round(hi)) > 1e-6 ||
        tag.DataType == TagDataType.Float32;

    private static string GetNodeLabel(LogicNode? node, string fallback = "Node") =>
        !string.IsNullOrWhiteSpace(node?.Label) ? node.Label : fallback;
}
