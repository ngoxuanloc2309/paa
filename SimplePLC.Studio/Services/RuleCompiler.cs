using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services;

public interface IRuleCompiler
{
    SimplePLC.Studio.Models.CompileResult Compile(
        IEnumerable<GraphNodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections,
        TagCatalogViewModel tagCatalog,
        ProductDefinition? product = null,
        IEnumerable<SimplePLC.Studio.Models.ModbusPortConfigModel>? modbusPorts = null);
}

public sealed class RuleCompiler : IRuleCompiler
{
    private readonly SimplePLC.Application.Logic.Compilation.RuleCompiler _coreCompiler = new();

    public SimplePLC.Studio.Models.CompileResult Compile(
        IEnumerable<GraphNodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections,
        TagCatalogViewModel tagCatalog,
        ProductDefinition? product = null,
        IEnumerable<SimplePLC.Studio.Models.ModbusPortConfigModel>? modbusPorts = null)
    {
        ProductDefinition targetProduct = product 
            ?? tagCatalog?.CurrentProduct 
            ?? AppServices.Instance.CurrentProduct 
            ?? ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var graph = new LogicGraph();

        var nodeList = nodes.ToList();
        var connectionList = connections.ToList();
        var studioDiagnostics = new List<SimplePLC.Studio.Models.Diagnostic>();

        // Capability Interlock / Hardware Feature Gating Check
        var portList = modbusPorts?.ToList();
        int effectivePhysicalPorts = product != null
            ? (int)targetProduct.PhysicalRs485PortCount
            : (portList != null && portList.Count > 0 ? Math.Max((int)targetProduct.PhysicalRs485PortCount, portList.Count) : (int)targetProduct.PhysicalRs485PortCount);
        if (targetProduct.HardwareCapabilities != SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.NONE)
        {
            foreach (var node in nodeList)
            {
                if (node is MqttPublishNodeViewModel && !targetProduct.SupportsMqttPublish)
                {
                    studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                        SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                        string.Format(LocalizationService.Instance.IsVietnamese
                            ? "Thiết bị mục tiêu '{0}' không có cấu hình phần cứng hỗ trợ MQTT Publish."
                            : "Target device '{0}' hardware profile does not support MQTT Publish.", targetProduct.ProductName),
                        NodeId: node.Id));
                }
                else if (node is MqttSubscribeNodeViewModel && !targetProduct.SupportsMqttSubscribe)
                {
                    studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                        SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                        string.Format(LocalizationService.Instance.IsVietnamese
                            ? "Thiết bị mục tiêu '{0}' không có cấu hình phần cứng hỗ trợ MQTT Subscribe."
                            : "Target device '{0}' hardware profile does not support MQTT Subscribe.", targetProduct.ProductName),
                        NodeId: node.Id));
                }
                else if (node is ModbusReadNodeViewModel mbRead)
                {
                    if (!targetProduct.SupportsModbusMaster)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Thiết bị mục tiêu '{0}' không có cấu hình phần cứng hỗ trợ Modbus Master."
                                : "Target device '{0}' hardware profile does not support Modbus Master.", targetProduct.ProductName),
                            NodeId: node.Id));
                    }
                    else if (effectivePhysicalPorts == 0)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Thiết bị mục tiêu '{0}' không có cổng RS-485 vật lý nào."
                                : "Target device '{0}' has no physical RS-485 ports.", targetProduct.ProductName),
                            NodeId: node.Id));
                    }
                    else if (mbRead.PortIndex >= effectivePhysicalPorts)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Khối Modbus Read được gán cho Cổng {0}, vượt quá số cổng RS-485 vật lý hiện hữu của thiết bị ({1} cổng)."
                                : "Modbus Read block is assigned to Port {0}, exceeding device's physical RS-485 port count ({1}).",
                                mbRead.PortIndex + 1, effectivePhysicalPorts),
                            NodeId: node.Id));
                    }
                }
                else if (node is ModbusWriteNodeViewModel mbWrite)
                {
                    if (!targetProduct.SupportsModbusMaster)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Thiết bị mục tiêu '{0}' không có cấu hình phần cứng hỗ trợ Modbus Master."
                                : "Target device '{0}' hardware profile does not support Modbus Master.", targetProduct.ProductName),
                            NodeId: node.Id));
                    }
                    else if (effectivePhysicalPorts == 0)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Thiết bị mục tiêu '{0}' không có cổng RS-485 vật lý nào."
                                : "Target device '{0}' has no physical RS-485 ports.", targetProduct.ProductName),
                            NodeId: node.Id));
                    }
                    else if (mbWrite.PortIndex >= effectivePhysicalPorts)
                    {
                        studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Instance.IsVietnamese
                                ? "Khối Modbus Write được gán cho Cổng {0}, vượt quá số cổng RS-485 vật lý hiện hữu của thiết bị ({1} cổng)."
                                : "Modbus Write block is assigned to Port {0}, exceeding device's physical RS-485 port count ({1}).",
                                mbWrite.PortIndex + 1, effectivePhysicalPorts),
                            NodeId: node.Id));
                    }
                }
            }
        }

        // 1. Build POCO Graph Nodes
        int actionOrder = 0;
        foreach (var node in nodeList)
        {
            switch (node)
            {
                case InputNodeViewModel inp:
                    ushort inTagIdx = ResolveTagIndex(inp.Tag, targetProduct);
                    graph.Nodes.Add(LogicNode.CreateInput(inp.Id, inTagIdx, inp.DisplayTitle));
                    break;

                case TriggerNodeViewModel trig:
                    graph.Nodes.Add(LogicNode.CreateTrigger(
                        trig.Id,
                        (TriggerKind)trig.TriggerType,
                        (CompareOperator)trig.CompareOp,
                        trig.ThresholdLo,
                        trig.ThresholdHi,
                        trig.ForMs,
                        trig.DisplayTitle));
                    break;

                case GuardNodeViewModel grd:
                    ushort grdTagIdx = ResolveTagIndex(grd.GuardTag, targetProduct);
                    graph.Nodes.Add(LogicNode.CreateGuard(grd.Id, grdTagIdx, grd.Negate, grd.DisplayTitle));
                    break;

                case ActionNodeViewModel act:
                    ushort actTagIdx = ResolveTagIndex(act.TargetTag, targetProduct);
                    graph.Nodes.Add(LogicNode.CreateAction(
                        act.Id,
                        actTagIdx,
                        (ActionKind)act.ActionType,
                        act.ActionParam,
                        executionOrder: actionOrder++,
                        act.DisplayTitle));
                    break;

                case TimerNodeViewModel tm:
                    ushort? tmInTag = tm.InputTag != null && tm.InputTag.Kind != TagKind.None ? ResolveTagIndex(tm.InputTag, targetProduct) : null;
                    ushort? tmQTag = tm.OutputTag != null && tm.OutputTag.Kind != TagKind.None ? ResolveTagIndex(tm.OutputTag, targetProduct) : null;

                    // Resolve connections directly if wired on canvas
                    var inConn = connectionList.FirstOrDefault(c => c.Target?.Node == tm &&
                        (string.Equals(c.Target.Title, "In", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == tm.InputConnectors.FirstOrDefault()));
                    if (inConn?.Source?.Node is InputNodeViewModel tmInNode && tmInNode.Tag != null)
                    {
                        tmInTag = ResolveTagIndex(tmInNode.Tag, targetProduct);
                    }
                    else if (inConn?.Source?.Node is TriggerNodeViewModel trigVm)
                    {
                        var trigInConn = connectionList.FirstOrDefault(c => c.Target?.Node == trigVm);
                        if (trigInConn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                        {
                            tmInTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                        }
                    }
                    else if (inConn?.Source?.Node is GuardNodeViewModel guardVm)
                    {
                        var guardInConn = connectionList.FirstOrDefault(c => c.Target?.Node == guardVm);
                        if (guardInConn?.Source?.Node is TriggerNodeViewModel gTrigVm)
                        {
                            var trigInConn = connectionList.FirstOrDefault(c => c.Target?.Node == gTrigVm);
                            if (trigInConn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                            {
                                tmInTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                            }
                        }
                    }

                    var tmQConn = connectionList.FirstOrDefault(c => c.Source?.Node == tm &&
                        (string.Equals(c.Source.Title, "Q", StringComparison.OrdinalIgnoreCase) ||
                         c.Source == tm.OutputConnectors.FirstOrDefault()));
                    if (tmQConn?.Target?.Node is ActionNodeViewModel tmActNode && tmActNode.TargetTag != null)
                    {
                        tmQTag = ResolveTagIndex(tmActNode.TargetTag, targetProduct);
                    }

                    // Nếu khối Timer thiếu ngõ vào IN
                    if (!tmInTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-TIMER-IN"), tm.DisplayTitle),
                            NodeId: tm.Id,
                            FieldName: "In"));
                    }

                    // Nếu khối Timer thiếu ngõ ra Q
                    if (!tmQTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-TIMER-Q"), tm.DisplayTitle),
                            NodeId: tm.Id,
                            FieldName: "Q"));
                    }

                    if (!tmInTag.HasValue || !tmQTag.HasValue)
                    {
                        break;
                    }

                    TimerMacroType tmType = tm.TimerMode switch
                    {
                        "TOF" => TimerMacroType.Tof,
                        "TP" => TimerMacroType.Tp,
                        "CYCLE" => TimerMacroType.Cycle,
                        _ => TimerMacroType.Ton
                    };
                    graph.Nodes.Add(LogicNode.CreateTimer(
                        tm.Id,
                        tmType,
                        tmInTag.Value,
                        tm.PresetMs,
                        tmQTag.Value,
                        executionOrder: actionOrder++,
                        tm.DisplayTitle,
                        onTimeMs: tm.OnTimeMs,
                        offTimeMs: tm.OffTimeMs,
                        startWithOn: tm.StartWithOn));
                    break;

                case CounterNodeViewModel cnt:
                    ushort? cntInTag = cnt.InputTag != null && cnt.InputTag.Kind != TagKind.None ? ResolveTagIndex(cnt.InputTag, targetProduct) : null;
                    var defaultCounterTag = tagCatalog?.AllTags.FirstOrDefault(t => t.Kind == TagKind.Counter);
                    ushort cntCvTag = cnt.CvTag != null && cnt.CvTag.Kind != TagKind.None 
                        ? ResolveTagIndex(cnt.CvTag, targetProduct)
                        : (defaultCounterTag != null
                            ? ResolveTagIndex(defaultCounterTag, targetProduct)
                            : (targetProduct.Tags.FirstOrDefault(t => t.Kind == TagKind.Counter)?.TagIndex ?? 116));
                    ushort? cntQTag = cnt.OutputTag != null && cnt.OutputTag.Kind != TagKind.None ? ResolveTagIndex(cnt.OutputTag, targetProduct) : null;
                    ushort? cntResetTag = cnt.ResetTag != null && cnt.ResetTag.Kind != TagKind.None ? ResolveTagIndex(cnt.ResetTag, targetProduct) : null;

                    // Resolve connections directly if wired on canvas
                    var cuConn = connectionList.FirstOrDefault(c => c.Target?.Node == cnt &&
                        (string.Equals(c.Target.Title, "CU", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "CD", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == cnt.InputConnectors.FirstOrDefault()));
                    if (cuConn?.Source?.Node is InputNodeViewModel inNode && inNode.Tag != null)
                    {
                        cntInTag = ResolveTagIndex(inNode.Tag, targetProduct);
                    }
                    else if (cuConn?.Source?.Node is TriggerNodeViewModel cuTrigVm)
                    {
                        var trigInConn = connectionList.FirstOrDefault(c => c.Target?.Node == cuTrigVm);
                        if (trigInConn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                        {
                            cntInTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                        }
                    }

                    var rConn = connectionList.FirstOrDefault(c => c.Target?.Node == cnt &&
                        (string.Equals(c.Target.Title, "R", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "Reset", StringComparison.OrdinalIgnoreCase) ||
                         (cnt.InputConnectors.Count > 1 && c.Target == cnt.InputConnectors[1])));
                    if (rConn?.Source?.Node is InputNodeViewModel rNode && rNode.Tag != null)
                    {
                        cntResetTag = ResolveTagIndex(rNode.Tag, targetProduct);
                    }

                    var qConn = connectionList.FirstOrDefault(c => c.Source?.Node == cnt &&
                        (string.Equals(c.Source.Title, "Q", StringComparison.OrdinalIgnoreCase) ||
                         c.Source == cnt.OutputConnectors.FirstOrDefault()));
                    if (qConn?.Target?.Node is ActionNodeViewModel actNode && actNode.TargetTag != null)
                    {
                        cntQTag = ResolveTagIndex(actNode.TargetTag, targetProduct);
                    }

                    // Nếu khối Counter thiếu ngõ vào đếm
                    if (!cntInTag.HasValue)
                    {
                        string portName = cnt.CounterMode == "CTD" ? "CD" : "CU";
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-COUNTER-IN"), cnt.DisplayTitle, portName),
                            NodeId: cnt.Id,
                            FieldName: portName));
                    }

                    // Nếu khối Counter thiếu ngõ ra Q
                    if (!cntQTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-COUNTER-Q"), cnt.DisplayTitle),
                            NodeId: cnt.Id,
                            FieldName: "Q"));
                    }

                    if (!cntInTag.HasValue || !cntQTag.HasValue)
                    {
                        break;
                    }

                    CounterMacroType cntType = cnt.CounterMode switch
                    {
                        "CTD" => CounterMacroType.Ctd,
                        _ => CounterMacroType.Ctu
                    };
                    graph.Nodes.Add(LogicNode.CreateCounter(
                        cnt.Id,
                        cntType,
                        cntInTag.Value,
                        cntResetTag,
                        cntCvTag,
                        cnt.PresetValue,
                        cntQTag.Value,
                        executionOrder: actionOrder++,
                        cnt.DisplayTitle));
                    break;

                case ScaleNodeViewModel scl:
                    ushort sclInTag = scl.InputTag != null ? ResolveTagIndex(scl.InputTag, targetProduct) : (ushort)0;
                    var sclInConn = connectionList.FirstOrDefault(c => c.Target?.Node == scl &&
                        (string.Equals(c.Target.Title, "IN", StringComparison.OrdinalIgnoreCase) ||
                          c.Target == scl.InputConnectors.FirstOrDefault()));
                    if (sclInConn?.Source?.Node is InputNodeViewModel sclInVm && sclInVm.Tag != null)
                    {
                        sclInTag = ResolveTagIndex(sclInVm.Tag, targetProduct);
                    }

                    ushort? sclOutTag = scl.OutputTag != null ? ResolveTagIndex(scl.OutputTag, targetProduct) : null;
                    var sclOutConn = connectionList.FirstOrDefault(c => c.Source?.Node == scl);
                    if (sclOutConn?.Target?.Node is ActionNodeViewModel actOut && actOut.TargetTag != null)
                    {
                        sclOutTag = ResolveTagIndex(actOut.TargetTag, targetProduct);
                    }

                    graph.Nodes.Add(LogicNode.CreateScale(
                        scl.Id,
                        sclInTag,
                        sclOutTag,
                        scl.Gain,
                        scl.Offset,
                        scl.IsClamped,
                        scl.ClampMin,
                        scl.ClampMax,
                        scl.DecimalPlaces,
                        scl.Unit,
                        executionOrder: actionOrder++,
                        scl.DisplayTitle));
                    break;

                case ComparatorNodeViewModel cmp:
                    ushort cmpInTag = cmp.InputTag != null && cmp.InputTag.Kind != TagKind.None ? ResolveTagIndex(cmp.InputTag, targetProduct) : (ushort)0;
                    var cmpInConn = connectionList.FirstOrDefault(c => c.Target?.Node == cmp &&
                        (string.Equals(c.Target.Title, "IN", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == cmp.InputConnectors.FirstOrDefault()));
                    if (cmpInConn?.Source?.Node is InputNodeViewModel cmpInVm && cmpInVm.Tag != null)
                    {
                        cmpInTag = ResolveTagIndex(cmpInVm.Tag, targetProduct);
                    }
                    else if (cmpInConn?.Source?.Node is ScaleNodeViewModel cmpSclSource)
                    {
                        if (cmpSclSource.OutputTag != null) cmpInTag = ResolveTagIndex(cmpSclSource.OutputTag, targetProduct);
                        else if (cmpSclSource.InputTag != null) cmpInTag = ResolveTagIndex(cmpSclSource.InputTag, targetProduct);
                    }

                    graph.Nodes.Add(LogicNode.CreateComparator(
                        cmp.Id,
                        cmpInTag,
                        (CompareOperator)cmp.CompareOp,
                        cmp.ThresholdLo,
                        cmp.ThresholdHi,
                        executionOrder: actionOrder++,
                        label: cmp.DisplayTitle));
                    break;

                case MqttPublishNodeViewModel mqtt:
                    ushort? mqttEnTag = null;
                    ushort? mqttDataTag = mqtt.DataTag != null && mqtt.DataTag.Kind != TagKind.None ? ResolveTagIndex(mqtt.DataTag, targetProduct) : null;

                    // Resolve incoming connections to Trig (EN) only when in Event mode
                    if (mqtt.IsEventMode)
                    {
                        var trigConn = connectionList.FirstOrDefault(c => c.Target?.Node == mqtt &&
                            (c.Target == mqtt.TriggerConnector ||
                             string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(c.Target.Title, "EN", StringComparison.OrdinalIgnoreCase)));
                        if (trigConn?.Source?.Node is InputNodeViewModel trigInVm && trigInVm.Tag != null)
                        {
                            mqttEnTag = ResolveTagIndex(trigInVm.Tag, targetProduct);
                        }
                        else if (trigConn?.Source?.Node is ComparatorNodeViewModel cmpTrig && cmpTrig.InputTag != null)
                        {
                            mqttEnTag = ResolveTagIndex(cmpTrig.InputTag, targetProduct);
                        }
                    }

                    var valConn = connectionList.FirstOrDefault(c => c.Target?.Node == mqtt &&
                        (c.Target == mqtt.DefaultValConnector ||
                         string.Equals(c.Target.Title, "Val", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "IN", StringComparison.OrdinalIgnoreCase)));
                    if (valConn?.Source?.Node is InputNodeViewModel valInVm && valInVm.Tag != null)
                    {
                        mqttDataTag = ResolveTagIndex(valInVm.Tag, targetProduct);
                    }
                    else if (valConn?.Source?.Node is ScaleNodeViewModel sclVal && sclVal.OutputTag != null)
                    {
                        mqttDataTag = ResolveTagIndex(sclVal.OutputTag, targetProduct);
                    }

                    var fieldDtos = mqtt.Fields.Select(f =>
                    {
                        ushort tagIndex = f.Tag != null ? ResolveTagIndex(f.Tag, targetProduct) : (ushort)0;
                        var fConn = connectionList.FirstOrDefault(c => c.Target?.Node == mqtt &&
                            (c.Target == f.Connector || (!string.IsNullOrEmpty(f.Key) && string.Equals(c.Target.Title, f.Key, StringComparison.OrdinalIgnoreCase))));
                        if (fConn?.Source?.Node is InputNodeViewModel fInVm && fInVm.Tag != null)
                        {
                            tagIndex = ResolveTagIndex(fInVm.Tag, targetProduct);
                        }
                        else if (fConn?.Source?.Node is ScaleNodeViewModel fScl && fScl.OutputTag != null)
                        {
                            tagIndex = ResolveTagIndex(fScl.OutputTag, targetProduct);
                        }
                        else if (fConn?.Source?.Node is ModbusReadNodeViewModel mbSrc)
                        {
                            var srcPin = mbSrc.Pins.FirstOrDefault(p => p.Connector == fConn.Source || string.Equals(p.Label, fConn.Source.Title, StringComparison.OrdinalIgnoreCase) || string.Equals(p.DisplayTitle, fConn.Source.Title, StringComparison.OrdinalIgnoreCase));
                            if (srcPin?.TargetTag != null)
                            {
                                tagIndex = ResolveTagIndex(srcPin.TargetTag, targetProduct);
                            }
                        }

                        return new SimplePLC.Protocol.Dto.MqttPayloadFieldDto
                        {
                            Key = f.Key,
                            TagIndex = tagIndex,
                            Unit = f.Unit
                        };
                    }).ToList();

                    if (mqtt.IsCustomTemplateVisible && mqtt.CustomConnectors.Count > 0)
                    {
                        var customDtos = new List<SimplePLC.Protocol.Dto.MqttPayloadFieldDto>();
                        for (int i = 0; i < mqtt.CustomConnectors.Count; i++)
                        {
                            var c = mqtt.CustomConnectors[i];
                            ushort tagIndex = 0xFFFF;
                            var cConn = connectionList.FirstOrDefault(conn => conn.Target?.Node == mqtt &&
                                (conn.Target == c || string.Equals(conn.Target.Title, c.Title, StringComparison.OrdinalIgnoreCase)));
                            if (cConn?.Source?.Node is InputNodeViewModel cInVm && cInVm.Tag != null)
                            {
                                tagIndex = ResolveTagIndex(cInVm.Tag, targetProduct);
                            }
                            else if (cConn?.Source?.Node is ScaleNodeViewModel cScl && cScl.OutputTag != null)
                            {
                                tagIndex = ResolveTagIndex(cScl.OutputTag, targetProduct);
                            }
                            else
                            {
                                var mappedTag = mqtt.GetCustomConnectorTag(c.Title);
                                if (mappedTag != null)
                                {
                                    tagIndex = ResolveTagIndex(mappedTag, targetProduct);
                                }
                            }

                            if (tagIndex != 0xFFFF)
                            {
                                customDtos.Add(new SimplePLC.Protocol.Dto.MqttPayloadFieldDto
                                {
                                    Key = c.Title,
                                    TagIndex = tagIndex
                                });
                                if (mqttDataTag == null)
                                {
                                    mqttDataTag = tagIndex;
                                }
                            }
                        }
                        if (customDtos.Count > 0)
                        {
                            fieldDtos = customDtos;
                        }
                    }

                    string effectiveTemplate = mqtt.CustomTemplate;
                    string effectivePayloadMode = mqtt.PayloadMode;

                    if (!mqtt.IsCustomTemplateVisible && (fieldDtos.Count > 0 || mqttDataTag != null))
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append('{');
                        if (mqtt.IncludeDevice) sb.Append("\"device\":\"{device}\",");
                        if (mqtt.IncludeTimestamp) sb.Append("\"ts\":{ts},");
                        if (mqtt.WrapValues) sb.Append("\"values\":{");

                        if (fieldDtos.Count > 0)
                        {
                            for (int i = 0; i < fieldDtos.Count; i++)
                            {
                                if (i > 0) sb.Append(',');
                                string k = string.IsNullOrWhiteSpace(fieldDtos[i].Key) ? $"val{i + 1}" : fieldDtos[i].Key;
                                sb.Append($"\"{k}\":{{val{i + 1}}}");
                            }
                        }
                        else if (mqtt.DataTag != null)
                        {
                            string k = mqtt.DataTag.Name.ToLowerInvariant();
                            sb.Append($"\"{k}\":{{val1}}");
                        }

                        if (mqtt.WrapValues) sb.Append('}');
                        sb.Append('}');

                        effectiveTemplate = sb.ToString();
                        effectivePayloadMode = "Custom";
                    }

                    graph.Nodes.Add(LogicNode.CreateMqttPublish(
                        mqtt.Id,
                        mqttEnTag,
                        mqttDataTag,
                        mqtt.Topic,
                        mqtt.PublishMode,
                        mqtt.Deadband,
                        mqtt.IntervalMs,
                        mqtt.Qos,
                        mqtt.Retain,
                        effectivePayloadMode,
                        mqtt.Unit,
                        mqtt.IncludeDevice,
                        mqtt.IncludeTimestamp,
                        fieldDtos,
                        effectiveTemplate,
                        executionOrder: actionOrder++,
                        label: mqtt.DisplayTitle));
                    break;

                case MqttSubscribeNodeViewModel sub:
                    // MQTT Subscribe là tiến trình thường trực, không cần chân EN
                    ushort? subEnTag = null;
                    var pinDtos = new List<SimplePLC.Protocol.Dto.MqttPinBindingDto>();
                    foreach (var pin in sub.Pins)
                    {
                        var dto = pin.ToDto();
                        if (pin.TargetTag != null && pin.TargetTag.Kind != TagKind.None)
                        {
                            dto.TargetTagIndex = ResolveTagIndex(pin.TargetTag, targetProduct);
                        }
                        else
                        {
                            var pinConn = connectionList.FirstOrDefault(c => c.Source?.Node == sub &&
                                (c.Source == pin.Connector ||
                                 string.Equals(c.Source.Title, pin.Label, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(c.Source.Id, pin.Id, StringComparison.OrdinalIgnoreCase)));
                            if (pinConn?.Target?.Node is ActionNodeViewModel subActNode && subActNode.TargetTag != null)
                            {
                                dto.TargetTagIndex = ResolveTagIndex(subActNode.TargetTag, targetProduct);
                            }
                        }
                        pinDtos.Add(dto);
                    }

                    graph.Nodes.Add(LogicNode.CreateMqttSubscribe(
                        sub.Id,
                        subEnTag,
                        sub.Topic,
                        sub.Qos,
                        sub.QueueMode,
                        sub.QueueCapacity,
                        sub.IgnoreRetained,
                        sub.TimeoutMs,
                        sub.FailSafeAction,
                        pinDtos,
                        executionOrder: actionOrder++,
                        label: sub.DisplayTitle));
                    break;

                case ModbusReadNodeViewModel mb:
                    ushort? mbEnTag = null;
                    var mbEnConn = connectionList.FirstOrDefault(c => c.Target?.Node == mb &&
                        (string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "EN", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == mb.TrigConnector ||
                         c.Target == mb.EnableConnector ||
                         c.Target == mb.InputConnectors.FirstOrDefault()));
                    if (mbEnConn?.Source?.Node is InputNodeViewModel enMbInVm && enMbInVm.Tag != null)
                    {
                        mbEnTag = ResolveTagIndex(enMbInVm.Tag, targetProduct);
                    }
                    else if (mbEnConn?.Source?.Node is TriggerNodeViewModel trigMb)
                    {
                        var trigIn = connectionList.FirstOrDefault(c => c.Target?.Node == trigMb);
                        if (trigIn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                        {
                            mbEnTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                        }
                    }

                    var mbPinDtos = new List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>();
                    foreach (var pin in mb.Pins)
                    {
                        var dto = pin.ToDto();
                        if (pin.TargetTag != null && pin.TargetTag.Kind != TagKind.None)
                        {
                            dto.TargetTagIndex = ResolveTagIndex(pin.TargetTag, targetProduct);
                        }
                        else
                        {
                            var pinConn = connectionList.FirstOrDefault(c => c.Source?.Node == mb &&
                                (c.Source == pin.Connector ||
                                 string.Equals(c.Source.Title, pin.Label, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(c.Source.Id, pin.Id, StringComparison.OrdinalIgnoreCase)));
                            if (pinConn?.Target?.Node is ActionNodeViewModel mbActNode && mbActNode.TargetTag != null)
                            {
                                dto.TargetTagIndex = ResolveTagIndex(mbActNode.TargetTag, targetProduct);
                            }
                            else if (pinConn?.Target?.Node is MqttPublishNodeViewModel mqttTarget)
                            {
                                var targetField = mqttTarget.Fields.FirstOrDefault(fld => fld.Connector == pinConn.Target || string.Equals(fld.Key, pinConn.Target.Title, StringComparison.OrdinalIgnoreCase));
                                if (targetField?.Tag != null)
                                {
                                    dto.TargetTagIndex = ResolveTagIndex(targetField.Tag, targetProduct);
                                }
                            }
                        }
                        mbPinDtos.Add(dto);
                    }

                    if (modbusPorts != null)
                    {
                        var targetPort = modbusPorts.FirstOrDefault(p => p.PortIndex == mb.PortIndex);
                        if (targetPort != null && !targetPort.Enabled)
                        {
                            studioDiagnostics.Add(new(
                                SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                                LocalizationService.Instance.IsVietnamese
                                    ? $"Khối '{mb.DisplayTitle}' sử dụng Cổng {mb.PortIndex + 1} ({targetPort.PortName}) nhưng cổng này đang bị vô hiệu hóa trong cấu hình thiết bị."
                                    : $"Block '{mb.DisplayTitle}' references Port {mb.PortIndex + 1} ({targetPort.PortName}) which is disabled in device settings.",
                                NodeId: mb.Id));
                        }
                    }

                    ushort? mbValidTag = null;
                    var mbValidConn = connectionList.FirstOrDefault(c => c.Source?.Node == mb &&
                        (c.Source == mb.ValidConnector || string.Equals(c.Source.Title, "Valid", StringComparison.OrdinalIgnoreCase)));
                    if (mbValidConn?.Target?.Node is ActionNodeViewModel actValid && actValid.TargetTag != null)
                    {
                        mbValidTag = ResolveTagIndex(actValid.TargetTag, targetProduct);
                    }

                    ushort? mbErrorTag = null;
                    var mbErrorConn = connectionList.FirstOrDefault(c => c.Source?.Node == mb &&
                        (c.Source == mb.ErrorConnector || string.Equals(c.Source.Title, "Error", StringComparison.OrdinalIgnoreCase)));
                    if (mbErrorConn?.Target?.Node is ActionNodeViewModel actError && actError.TargetTag != null)
                    {
                        mbErrorTag = ResolveTagIndex(actError.TargetTag, targetProduct);
                    }

                    ushort? mbErrCodeTag = null;
                    var mbErrCodeConn = connectionList.FirstOrDefault(c => c.Source?.Node == mb &&
                        (c.Source == mb.ErrCodeConnector || string.Equals(c.Source.Title, "ErrCode", StringComparison.OrdinalIgnoreCase)));
                    if (mbErrCodeConn?.Target?.Node is ActionNodeViewModel actErrCode && actErrCode.TargetTag != null)
                    {
                        mbErrCodeTag = ResolveTagIndex(actErrCode.TargetTag, targetProduct);
                    }

                    graph.Nodes.Add(LogicNode.CreateModbusRead(
                        mb.Id,
                        mbEnTag,
                        mb.SlaveId,
                        mb.FunctionCode,
                        mb.StartAddress,
                        mb.RegCount,
                        mb.PollIntervalMs,
                        mb.TimeoutMs,
                        mbPinDtos,
                        portIndex: mb.PortIndex,
                        executionOrder: actionOrder++,
                        label: mb.DisplayTitle,
                        validTag: mbValidTag,
                        errorTag: mbErrorTag,
                        errCodeTag: mbErrCodeTag));
                    break;

                case ModbusWriteNodeViewModel mbw:
                    ushort? mbwTrigTag = null;
                    var mbwTrigConn = connectionList.FirstOrDefault(c => c.Target?.Node == mbw &&
                        (string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == mbw.TrigConnector ||
                         c.Target == mbw.InputConnectors.FirstOrDefault()));
                    if (mbwTrigConn?.Source?.Node is InputNodeViewModel mbwTrigInVm && mbwTrigInVm.Tag != null)
                    {
                        mbwTrigTag = ResolveTagIndex(mbwTrigInVm.Tag, targetProduct);
                    }
                    else if (mbwTrigConn?.Source?.Node is TriggerNodeViewModel trigWNode)
                    {
                        var trigIn = connectionList.FirstOrDefault(c => c.Target?.Node == trigWNode);
                        if (trigIn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                        {
                            mbwTrigTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                        }
                    }

                    ushort? ResolveConnectorTag(ConnectorViewModel? connector, string connectorName)
                    {
                        var conn = connectionList.FirstOrDefault(c => c.Target?.Node == mbw &&
                            (c.Target == connector || string.Equals(c.Target.Title, connectorName, StringComparison.OrdinalIgnoreCase)));
                        if (conn?.Source?.Node is InputNodeViewModel inVm && inVm.Tag != null)
                            return ResolveTagIndex(inVm.Tag, targetProduct);
                        if (conn?.Source?.Node is TriggerNodeViewModel trigNode)
                        {
                            var tIn = connectionList.FirstOrDefault(c => c.Target?.Node == trigNode);
                            if (tIn?.Source?.Node is InputNodeViewModel tInNode && tInNode.Tag != null)
                                return ResolveTagIndex(tInNode.Tag, targetProduct);
                        }
                        if (conn?.Source?.Node is ScaleNodeViewModel sclVm && sclVm.OutputTag != null)
                            return ResolveTagIndex(sclVm.OutputTag, targetProduct);
                        return null;
                    }

                    ushort? mbwChannelTag = ResolveConnectorTag(mbw.ChannelConnector, "Channel");
                    ushort? mbwCommandTag = ResolveConnectorTag(mbw.CommandConnector, "Command");
                    ushort? mbwParamTag = ResolveConnectorTag(mbw.ParamConnector, "Param");

                    var mbwPinDtos = new List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>();
                    foreach (var pin in mbw.Pins)
                    {
                        var dto = pin.ToDto();
                        if (pin.TargetTag != null && pin.TargetTag.Kind != TagKind.None)
                        {
                            dto.TargetTagIndex = ResolveTagIndex(pin.TargetTag, targetProduct);
                        }
                        else
                        {
                            var pinConn = connectionList.FirstOrDefault(c => c.Target?.Node == mbw &&
                                (c.Target == pin.Connector ||
                                 string.Equals(c.Target.Title, pin.Label, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(c.Target.Id, pin.Id, StringComparison.OrdinalIgnoreCase)));
                            if (pinConn?.Source?.Node is InputNodeViewModel pinInNode && pinInNode.Tag != null)
                            {
                                dto.TargetTagIndex = ResolveTagIndex(pinInNode.Tag, targetProduct);
                            }
                            else if (pinConn?.Source?.Node is ScaleNodeViewModel sclVm && sclVm.OutputTag != null)
                            {
                                dto.TargetTagIndex = ResolveTagIndex(sclVm.OutputTag, targetProduct);
                            }
                        }
                        mbwPinDtos.Add(dto);
                    }

                    if (modbusPorts != null)
                    {
                        var targetPort = modbusPorts.FirstOrDefault(p => p.PortIndex == mbw.PortIndex);
                        if (targetPort != null && !targetPort.Enabled)
                        {
                            studioDiagnostics.Add(new(
                                SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                                LocalizationService.Instance.IsVietnamese
                                    ? $"Khối '{mbw.DisplayTitle}' sử dụng Cổng {mbw.PortIndex + 1} ({targetPort.PortName}) nhưng cổng này đang bị vô hiệu hóa trong cấu hình thiết bị."
                                    : $"Block '{mbw.DisplayTitle}' references Port {mbw.PortIndex + 1} ({targetPort.PortName}) which is disabled in device settings.",
                                NodeId: mbw.Id));
                        }
                    }

                    graph.Nodes.Add(LogicNode.CreateModbusWrite(
                        mbw.Id,
                        trigTag: mbwTrigTag,
                        slaveId: mbw.SlaveId,
                        writeMode: mbw.WriteMode,
                        functionCode: mbw.FunctionCode,
                        startAddress: mbw.StartAddress,
                        pointCount: mbw.PointCount,
                        baseAddressCommand: mbw.BaseAddressCommand,
                        baseAddressParam: mbw.BaseAddressParam,
                        channelCount: mbw.ChannelCount,
                        isExclusiveChannel: mbw.IsExclusiveChannel,
                        triggerMode: mbw.TriggerMode,
                        cyclicIntervalMs: mbw.CyclicIntervalMs,
                        timeoutMs: mbw.TimeoutMs,
                        channelTag: mbwChannelTag,
                        commandTag: mbwCommandTag,
                        paramTag: mbwParamTag,
                        portIndex: mbw.PortIndex,
                        executionOrder: actionOrder++,
                        label: mbw.DisplayTitle,
                        pins: mbwPinDtos));
                    break;

                case DirectWriteNodeViewModel dw:
                    int maxDo = targetProduct.Resources?.DigitalOutputs ?? 8;
                    if ((byte)dw.TargetPin >= maxDo)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            LocalizationService.Instance.IsVietnamese
                                ? $"Chân ngõ ra DO{(byte)dw.TargetPin} vượt quá số lượng DO phần cứng hỗ trợ ({maxDo} DO)."
                                : $"Hardware pin DO{(byte)dw.TargetPin} exceeds device limit ({maxDo} DO).",
                            NodeId: dw.Id));
                    }

                    ushort? dwTrigTag = null;
                    var dwTrigConn = connectionList.FirstOrDefault(c => c.Target?.Node == dw &&
                        (string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == dw.TrigConnector ||
                         c.Target == dw.InputConnectors.FirstOrDefault()));
                    if (dwTrigConn?.Source?.Node is InputNodeViewModel dwTrigInVm && dwTrigInVm.Tag != null)
                    {
                        dwTrigTag = ResolveTagIndex(dwTrigInVm.Tag, targetProduct);
                    }
                    else if (dwTrigConn?.Source?.Node is TriggerNodeViewModel trigDwNode)
                    {
                        var trigIn = connectionList.FirstOrDefault(c => c.Target?.Node == trigDwNode);
                        if (trigIn?.Source?.Node is InputNodeViewModel trigInNode && trigInNode.Tag != null)
                        {
                            dwTrigTag = ResolveTagIndex(trigInNode.Tag, targetProduct);
                        }
                    }

                    ushort? ResolveDwConnectorTag(ConnectorViewModel? connector, string connectorName)
                    {
                        var conn = connectionList.FirstOrDefault(c => c.Target?.Node == dw &&
                            (c.Target == connector || string.Equals(c.Target.Title, connectorName, StringComparison.OrdinalIgnoreCase)));
                        if (conn?.Source?.Node is InputNodeViewModel inVm && inVm.Tag != null)
                            return ResolveTagIndex(inVm.Tag, targetProduct);
                        if (conn?.Source?.Node is TriggerNodeViewModel trigNode)
                        {
                            var tIn = connectionList.FirstOrDefault(c => c.Target?.Node == trigNode);
                            if (tIn?.Source?.Node is InputNodeViewModel tInNode && tInNode.Tag != null)
                                return ResolveTagIndex(tInNode.Tag, targetProduct);
                        }
                        if (conn?.Source?.Node is ScaleNodeViewModel sclVm && sclVm.OutputTag != null)
                            return ResolveTagIndex(sclVm.OutputTag, targetProduct);
                        return null;
                    }

                    ushort? dwChannelTag = ResolveDwConnectorTag(dw.ChannelConnector, "Chan");
                    ushort? dwParamTag = ResolveDwConnectorTag(dw.ParamConnector, "Param");

                    graph.Nodes.Add(LogicNode.CreateDirectWrite(
                        dw.Id,
                        trigTag: dwTrigTag,
                        targetPin: dw.TargetPin,
                        mode: dw.Mode,
                        channelCount: dw.ChannelCount,
                        isExclusiveChannel: dw.IsExclusiveChannel,
                        pulseWidthUs: dw.PulseWidthUs,
                        pwmFrequencyHz: dw.PwmFrequencyHz,
                        triggerMode: dw.TriggerMode,
                        timeoutMs: dw.TimeoutMs,
                        channelTag: dwChannelTag,
                        paramTag: dwParamTag,
                        defaultColorRgb: dw.DefaultColorRgb,
                        executionOrder: actionOrder++,
                        label: dw.DisplayTitle));
                    break;

                default:
                    studioDiagnostics.Add(new(SimplePLC.Studio.Models.DiagnosticSeverity.Error, LocalizationService.Tr("DiagUnsupportedNode"), NodeId: node.Id));
                    break;
            }
        }

        // 2. Build POCO Graph Edges
        var validNodeIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
        foreach (var conn in connectionList)
        {
            if (conn.Source?.Node != null && conn.Target?.Node != null)
            {
                // Chỉ bổ sung cạnh nếu cả Source Node và Target Node đều có mặt trong đồ thị.
                // Tránh lỗi kỹ thuật "Target/Source node '<GUID>' not found" khi một khối macro dở dang chưa được nạp vào đồ thị.
                if (validNodeIds.Contains(conn.Source.Node.Id) && validNodeIds.Contains(conn.Target.Node.Id))
                {
                    graph.Edges.Add(new LogicEdge(
                        conn.Source.Node.Id,
                        conn.Target.Node.Id,
                        conn.Source.Title,
                        conn.Target.Title));
                }
            }
        }

        // 3. Delegate to Core Application Compiler
        var coreResult = _coreCompiler.Compile(graph, targetProduct);

        // 4. Map Diagnostics
        foreach (var diag in coreResult.Diagnostics)
        {
            var severity = diag.Severity switch
            {
                SimplePLC.Application.Logic.Compilation.DiagnosticSeverity.Error => SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                SimplePLC.Application.Logic.Compilation.DiagnosticSeverity.Warning => SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                _ => SimplePLC.Studio.Models.DiagnosticSeverity.Info
            };

            var msg = LocalizeDiagnostic(diag, nodeList);

            studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                severity,
                msg,
                NodeId: diag.NodeId,
                FieldName: diag.FieldName,
                RelatedNodeIds: diag.RelatedNodeIds));
        }

        // 5. Project Domain Rules to RuleItemModels for UI display
        var rules = new List<RuleItemModel>();
        if (coreResult.Program != null)
        {
            var allTags = tagCatalog?.AllTags ?? (IEnumerable<TagModel>)Array.Empty<TagModel>();
            foreach (var domainRule in coreResult.Program.Rules)
            {
                var ruleItem = RuleItemModel.FromDomainRule(domainRule, allTags);
                ruleItem.Id = $"R{domainRule.RuleIndex + 1}";

                // Gắn SourceMap attribution từ Macro (Disassembly attribution)
                if (coreResult.Program.SourceMap.TryGetValue(domainRule.RuleIndex, out var origin))
                {
                    ruleItem.DiagramId = origin.SourceNodeId;
                    string labelPart = !string.IsNullOrWhiteSpace(origin.DisplayLabel) ? $" \"{origin.DisplayLabel}\"" : "";
                    int macroRuleCount = coreResult.Program.SourceMap.Values.Count(v => v.SourceNodeId == origin.SourceNodeId);
                    if (macroRuleCount <= 0) macroRuleCount = 2;
                    ruleItem.MacroAttribution = $"{origin.MacroType}{labelPart} ({origin.ExpansionIndex + 1}/{macroRuleCount})";
                }

                ruleItem.UpdateNarrative();
                rules.Add(ruleItem);
            }
        }

        return new SimplePLC.Studio.Models.CompileResult(rules, studioDiagnostics, coreResult.Program);
    }

    private static string LocalizeDiagnostic(SimplePLC.Application.Logic.Compilation.CompileDiagnostic diag, IReadOnlyList<GraphNodeViewModel>? nodes = null)
    {
        var targetNode = !string.IsNullOrEmpty(diag.NodeId)
            ? nodes?.FirstOrDefault(n => n.Id == diag.NodeId)
            : null;
        var nodeLabel = targetNode?.DisplayTitle ?? (!string.IsNullOrEmpty(diag.NodeId) ? "Node" : diag.FieldName ?? "");

        var key = "Diag_" + diag.Code;
        var template = LocalizationService.Tr(key);
        if (template != key)
        {
            try
            {
                if (template.Contains("{0}"))
                {
                    if (diag.Code == SimplePLC.Application.Logic.Compilation.RuleDependencyGraph.ErrCircularDependency ||
                        diag.Code == SimplePLC.Application.Logic.Graph.GraphGrammarV1.ErrCycleDetected)
                    {
                        var cyclePath = diag.Message
                            .Replace("Cycle detected in logic graph: ", "")
                            .Replace("Graph contains a circular dependency: ", "");
                        return string.Format(template, cyclePath);
                    }

                    // Handle templates with {1} placeholder (e.g. Counter port name CU/CD)
                    if (template.Contains("{1}"))
                    {
                        var portName = diag.FieldName ?? "";
                        return string.Format(template, nodeLabel, portName);
                    }

                    return string.Format(template, nodeLabel);
                }
                return template;
            }
            catch
            {
                return template;
            }
        }

        // Fallback: If no localized template found, sanitize any raw GUIDs from the core message
        var fallbackMsg = diag.Message;
        if (!string.IsNullOrEmpty(diag.NodeId) && !string.IsNullOrEmpty(nodeLabel))
        {
            fallbackMsg = fallbackMsg.Replace(diag.NodeId, nodeLabel);
            if (diag.NodeId.Length >= 8)
            {
                fallbackMsg = fallbackMsg.Replace(diag.NodeId[..8], nodeLabel);
            }
        }
        return fallbackMsg;
    }

    private static ushort ResolveTagIndex(TagModel? tag, ProductDefinition product)
    {
        if (tag == null || tag.Kind == TagKind.None) return 0;
        ushort idx = (ushort)tag.Index;
        var def = product.FindTagByIndex(idx);
        if (def != null && string.Equals(def.Name, tag.Name, StringComparison.OrdinalIgnoreCase))
            return idx;
        var byName = product.FindTagByName(tag.Name);
        return byName?.TagIndex ?? idx;
    }
}

