namespace SimplePLC.Studio.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;

public partial class LogicEditorViewModel
{
    /// <summary>
    /// Tái dựng toàn bộ sơ đồ Function Block Diagram (FBD) và các mạch mạng Logic từ hình ảnh thiết bị (Full Device Image).
    /// Khôi phục chính xác: Khối MODBUS_READ (Slave, FC, Pins, Cổng), Khối MQTT_PUB (Topic, QoS, Payload Fields),
    /// Khối Action đèn báo Valid/Error và toàn bộ hệ thống dây nối tín hiệu (Connections).
    /// </summary>
    public void ReconstructFullGraphFromDevice(FullDeviceConfigurationDto config, IEnumerable<RuleItemModel> rules)
    {
        RecordSnapshot("Reconstruct Full Graph From Device");

        Connections.Clear();
        Nodes.Clear();
        SelectedNode = null;

        var ruleList = rules?.ToList() ?? new List<RuleItemModel>();
        CurrentDiagramId = Guid.NewGuid().ToString("N");
        foreach (var r in ruleList)
        {
            r.DiagramId = CurrentDiagramId;
        }

        if (ruleList.Count > 0)
        {
            EditingRuleId = ruleList.Count == 1 ? ruleList[0].Id : string.Join(", ", ruleList.Select(r => r.Id));
        }

        var consumedRuleIds = new HashSet<string>();

        double currentY = 80;

        // 1. Dựng lại các khối Modbus RTU Master Read
        if (config.ModbusMasterReads != null && config.ModbusMasterReads.Count > 0)
        {
            foreach (var mb in config.ModbusMasterReads)
            {
                var mbReadNode = new ModbusReadNodeViewModel
                {
                    PortIndex = mb.PortIndex,
                    SlaveId = mb.SlaveId,
                    FunctionCode = (ModbusMasterFunctionCode)mb.FunctionCode,
                    StartAddress = mb.StartAddress,
                    RegCount = mb.RegCount,
                    PollIntervalMs = mb.PollIntervalMs,
                    TimeoutMs = mb.TimeoutMs,
                    ReadMode = (ModbusReadMode)mb.ReadMode,
                    Location = new Point(80, currentY)
                };

                if (mb.Pins != null)
                {
                    int pinIdx = 0;
                    foreach (var pin in mb.Pins)
                    {
                        var pinVm = ModbusPinBindingViewModel.FromDto(pin);
                        var targetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == pin.TargetTagIndex);
                        pinVm.TargetTag = targetTag;
                        mbReadNode.Pins.Add(pinVm);

                        // Dựng Action đích trên Canvas cho thanh ghi nhận dữ liệu từ chân Modbus
                        if (targetTag != null)
                        {
                            var actTarget = new ActionNodeViewModel(targetTag)
                            {
                                CustomLabel = $"➔ {targetTag.Name}",
                                ActionType = ActionType.SET_TAG,
                                Location = new Point(520, currentY + 180 + pinIdx * 90)
                            };
                            Nodes.Add(actTarget);
                            if (pinVm.Connector != null && actTarget.InputConnectors.Count > 0)
                            {
                                Connect(pinVm.Connector, actTarget.InputConnectors[0]);
                            }

                            var matchedRule = ruleList.FirstOrDefault(r => r.ActionTag?.Index == targetTag.Index);
                            if (matchedRule != null) consumedRuleIds.Add(matchedRule.Id);
                        }
                        pinIdx++;
                    }
                }

                Nodes.Add(mbReadNode);

                // Dựng Action Đèn báo Valid nếu có ValidTagIndex
                if (mb.ValidTagIndex.HasValue)
                {
                    var validTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == mb.ValidTagIndex.Value);
                    if (validTag != null)
                    {
                        var actValid = new ActionNodeViewModel(validTag)
                        {
                            ActionType = ActionType.SET_TAG,
                            ActionParam = 1,
                            Location = new Point(520, currentY - 20)
                        };
                        Nodes.Add(actValid);
                        Connect(mbReadNode.ValidConnector, actValid.InputConnectors[0]);

                        var matchedRule = ruleList.FirstOrDefault(r => r.ActionTag?.Index == validTag.Index);
                        if (matchedRule != null) consumedRuleIds.Add(matchedRule.Id);
                    }
                }

                // Dựng Action Đèn báo Error nếu có ErrorTagIndex
                if (mb.ErrorTagIndex.HasValue)
                {
                    var errorTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == mb.ErrorTagIndex.Value);
                    if (errorTag != null)
                    {
                        var actError = new ActionNodeViewModel(errorTag)
                        {
                            ActionType = ActionType.SET_TAG,
                            ActionParam = 1,
                            Location = new Point(520, currentY + 80)
                        };
                        Nodes.Add(actError);
                        Connect(mbReadNode.ErrorConnector, actError.InputConnectors[0]);

                        var matchedRule = ruleList.FirstOrDefault(r => r.ActionTag?.Index == errorTag.Index);
                        if (matchedRule != null) consumedRuleIds.Add(matchedRule.Id);
                    }
                }

                currentY += 260;
            }
        }

        // 2. Dựng lại các khối Modbus RTU Master Write
        if (config.ModbusMasterWrites != null && config.ModbusMasterWrites.Count > 0)
        {
            foreach (var mw in config.ModbusMasterWrites)
            {
                var mbWriteNode = new ModbusWriteNodeViewModel(mw.SlaveId, mw.WriteMode)
                {
                    FunctionCode = mw.FunctionCode,
                    StartAddress = mw.StartAddress,
                    PointCount = mw.PointCount,
                    PortIndex = mw.PortIndex,
                    CyclicIntervalMs = mw.CyclicIntervalMs,
                    TimeoutMs = mw.TimeoutMs,
                    TriggerMode = mw.TriggerMode,
                    Location = new Point(80, currentY)
                };

                if (mw.Pins != null)
                {
                    foreach (var pin in mw.Pins)
                    {
                        var pinVm = ModbusPinBindingViewModel.FromDto(pin);
                        pinVm.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == pin.TargetTagIndex);
                        mbWriteNode.Pins.Add(pinVm);
                    }
                }

                Nodes.Add(mbWriteNode);
                currentY += 260;
            }
        }

        // 3. Dựng lại các khối MQTT Telemetry Publish
        if (config.MqttTelemetries != null && config.MqttTelemetries.Count > 0)
        {
            foreach (var mq in config.MqttTelemetries)
            {
                var mqttPubNode = new MqttPublishNodeViewModel(null, mq.Topic)
                {
                    Qos = mq.Qos,
                    PublishMode = mq.PublishMode,
                    IntervalMs = mq.IntervalMs,
                    PayloadMode = mq.PayloadMode,
                    Deadband = mq.Deadband,
                    Retain = mq.Retain,
                    CustomTemplate = mq.CustomTemplate,
                    Location = new Point(520, currentY)
                };

                var mbReadsOnCanvas = Nodes.OfType<ModbusReadNodeViewModel>().ToList();

                if (mq.Fields != null)
                {
                    int fieldIdx = 0;
                    foreach (var f in mq.Fields)
                    {
                        var tag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == f.TagIndex);
                        var matchingPin = mbReadsOnCanvas.SelectMany(m => m.Pins).FirstOrDefault(p => p.TargetTag?.Index == f.TagIndex);
                        string fieldKey = matchingPin != null
                            ? matchingPin.Label.ToLowerInvariant()
                            : (!string.IsNullOrWhiteSpace(f.Key) && !f.Key.StartsWith("tag_") ? f.Key : (tag != null ? tag.Name.ToLowerInvariant() : $"tag_{f.TagIndex}"));

                        mqttPubNode.AddFieldWithDetails(fieldKey, tag, f.Unit ?? "");
                        fieldIdx++;
                    }
                }

                Nodes.Add(mqttPubNode);
                currentY += Math.Max(260, (mq.Fields?.Count ?? 1) * 75 + 40);
            }
        }

        // 4. Dựng lại các khối MQTT Subscription
        if (config.MqttSubscriptions != null && config.MqttSubscriptions.Count > 0)
        {
            foreach (var ms in config.MqttSubscriptions)
            {
                var mqttSubNode = new MqttSubscribeNodeViewModel(ms.Topic);
                mqttSubNode.LoadFromDto(ms);
                mqttSubNode.Location = new Point(80, currentY);

                Nodes.Add(mqttSubNode);
                currentY += 260;
            }
        }

        // 5. Dựng lại các khối DirectWrite / PULSE_OUT (SPX Sort-to-Light, UCS16703, PWM)
        if (config.DirectWrites != null && config.DirectWrites.Count > 0)
        {
            foreach (var dw in config.DirectWrites)
            {
                var dwNode = new DirectWriteNodeViewModel(dw.TargetPin, dw.Mode)
                {
                    ChannelCount = dw.ChannelCount,
                    IsExclusiveChannel = dw.IsExclusiveChannel,
                    PulseWidthUs = dw.PulseWidthUs,
                    PwmFrequencyHz = dw.PwmFrequencyHz,
                    TriggerMode = dw.TriggerMode,
                    TimeoutMs = dw.TimeoutMs,
                    Location = new Point(560, currentY)
                };

                Nodes.Add(dwNode);
                currentY += 260;
            }
        }

        // 6. Các Rule logic chuẩn còn lại (không nằm trong macro của Modbus hay Subsystems)
        var remainingRules = ruleList.Where(r => !consumedRuleIds.Contains(r.Id)).ToList();
        if (remainingRules.Count > 0)
        {
            double startX = 60;
            foreach (var rule in remainingRules)
            {
                InputNodeViewModel? inpNode = null;
                if (rule.TriggerTag != null && rule.TriggerTag.Kind != TagKind.None)
                {
                    inpNode = new InputNodeViewModel(rule.TriggerTag)
                    {
                        Location = new Point(startX, currentY)
                    };
                    Nodes.Add(inpNode);
                }

                var trigNode = new TriggerNodeViewModel
                {
                    TriggerType = rule.TriggerType,
                    ForMs = rule.ForMs,
                    CompareOp = rule.CompareOp,
                    ThresholdLo = rule.ThresholdLo,
                    ThresholdHi = rule.ThresholdHi,
                    Location = new Point(startX + 240, currentY)
                };
                Nodes.Add(trigNode);

                if (inpNode != null)
                {
                    Connect(inpNode.OutputConnectors[0], trigNode.InputConnectors[0]);
                }

                ConnectorViewModel outConnector = trigNode.OutputConnectors[0];

                bool hasValidGuard = rule.GuardTag != null &&
                                     rule.GuardTag.Kind != TagKind.None &&
                                     !string.Equals(rule.GuardTag.Name, "NONE", StringComparison.OrdinalIgnoreCase);

                if (hasValidGuard && rule.GuardTag != null)
                {
                    var grdNode = new GuardNodeViewModel(rule.GuardTag)
                    {
                        Negate = rule.GuardNegated,
                        Location = new Point(startX + 480, currentY)
                    };
                    Nodes.Add(grdNode);
                    Connect(outConnector, grdNode.InputConnectors[0]);
                    outConnector = grdNode.OutputConnectors[0];
                }

                if (rule.ActionTag != null && rule.ActionTag.Kind != TagKind.None)
                {
                    double actionX = hasValidGuard ? (startX + 720) : (startX + 480);
                    var actNode = new ActionNodeViewModel(rule.ActionTag)
                    {
                        ActionType = rule.ActionType,
                        ActionParam = rule.ActionParam,
                        Location = new Point(actionX, currentY)
                    };
                    Nodes.Add(actNode);
                    Connect(outConnector, actNode.InputConnectors[0]);
                }

                currentY += 260;
            }
        }

        CompileAndSaveRules(true);
    }
}
