namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Kết quả trích xuất cấu hình ngoại vi truyền thông (MQTT, Modbus, Direct Write, DTU).
/// </summary>
public sealed class PeripheralExtractionResult
{
    public List<MqttTelemetryConfigDto> MqttConfigs { get; } = new();
    public List<MqttSubscriptionConfigDto> MqttSubConfigs { get; } = new();
    public List<ModbusMasterReadConfigDto> ModbusReadConfigs { get; } = new();
    public List<ModbusMasterWriteConfigDto> ModbusWriteConfigs { get; } = new();
    public List<DirectWriteConfigDto> DirectWriteConfigs { get; } = new();
    public DtuHttpConfigDto? DtuHttpConfig { get; set; }
    public DtuTcpConfigDto? DtuTcpConfig { get; set; }
    public DtuConfigDto? DtuConfig { get; set; }
    public List<CompileDiagnostic> Diagnostics { get; } = new();
}

/// <summary>
/// Trích xuất và tiền thẩm định cấu hình các ngoại vi truyền thông trong đồ thị FBD.
/// </summary>
public static class PeripheralConfigExtractor
{
    /// <summary>
    /// Trích xuất toàn bộ cấu hình các khối ngoại vi (Modbus, MQTT, DirectWrite, DTU).
    /// </summary>
    public static PeripheralExtractionResult Extract(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        IReadOnlyList<ModbusPortConfigDto>? modbusPorts)
    {
        var result = new PeripheralExtractionResult();

        var mqttNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttPublish).ToList();
        var mqttSubNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.MqttSubscribe).ToList();
        var modbusReadNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusRead).ToList();
        var modbusWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.ModbusWrite).ToList();
        var directWriteNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.DirectWrite).ToList();
        var httpNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.HttpClient).ToList();
        var tcpNodes = graph.Nodes.Where(n => n.Kind == LogicNodeKind.TcpClient).ToList();

        // Safety Gate: Modbus Master nodes cannot target a port configured as Modbus Slave
        if (modbusPorts != null)
        {
            foreach (var mbNode in modbusReadNodes)
            {
                byte portIdx = mbNode.ModbusReadData?.PortIndex ?? 0;
                var port = modbusPorts.FirstOrDefault(p => p.PortIndex == portIdx);
                if (port != null && port.Role == ModbusPortRole.Slave)
                {
                    result.Diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrModbusPortSlaveConflict,
                        DiagnosticSeverity.Error,
                        $"RS-485 Port #{portIdx + 1} is configured as Modbus Slave and cannot be assigned to Modbus Master block '{CompilationHelpers.GetNodeLabel(mbNode, "MB_READ")}'.",
                        NodeId: mbNode.Id));
                }
            }

            foreach (var mbNode in modbusWriteNodes)
            {
                byte portIdx = mbNode.ModbusWriteData?.PortIndex ?? 0;
                var port = modbusPorts.FirstOrDefault(p => p.PortIndex == portIdx);
                if (port != null && port.Role == ModbusPortRole.Slave)
                {
                    result.Diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrModbusPortSlaveConflict,
                        DiagnosticSeverity.Error,
                        $"RS-485 Port #{portIdx + 1} is configured as Modbus Slave and cannot be assigned to Modbus Master block '{CompilationHelpers.GetNodeLabel(mbNode, "MB_WRITE")}'.",
                        NodeId: mbNode.Id));
                }
            }

            if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return result;
            }
        }

        // 1. Extract MQTT Telemetry Configs
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
                    else if (srcNode.Kind == LogicNodeKind.ModbusRead && srcNode.ModbusReadData != null)
                    {
                        var pin = srcNode.ModbusReadData.Pins.FirstOrDefault(p =>
                            string.Equals(p.Label, inEdge.SourcePort, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Id, inEdge.SourcePort, StringComparison.OrdinalIgnoreCase));

                        if (pin != null && pin.TargetTagIndex.HasValue)
                        {
                            tagIdx = pin.TargetTagIndex.Value;
                        }
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

            string effectiveTemplate = mData.CustomTemplate;
            string effectivePayloadMode = mData.PayloadMode;

            if (!string.Equals(mData.PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase) &&
                (telemetryFields.Count > 0 || mData.DataTagIndex.HasValue))
            {
                var sb = new System.Text.StringBuilder();
                sb.Append('{');
                if (mData.IncludeDevice) sb.Append("\"device\":\"{device}\",");
                if (mData.IncludeTimestamp) sb.Append("\"ts\":{ts},");
                if (mData.WrapValues) sb.Append("\"values\":{");

                if (telemetryFields.Count > 0)
                {
                    for (int f = 0; f < telemetryFields.Count; f++)
                    {
                        if (f > 0) sb.Append(',');
                        string k = string.IsNullOrWhiteSpace(telemetryFields[f].Key) ? $"val{f + 1}" : telemetryFields[f].Key;
                        sb.Append($"\"{k}\":{{val{f + 1}}}");
                    }
                }
                else if (mData.DataTagIndex.HasValue)
                {
                    var t = product.FindTagByIndex(mData.DataTagIndex.Value);
                    string k = t?.Name.ToLowerInvariant() ?? "val";
                    sb.Append($"\"{k}\":{{val1}}");
                }

                if (mData.WrapValues) sb.Append('}');
                sb.Append('}');

                effectiveTemplate = sb.ToString();
                effectivePayloadMode = "Custom";
            }

            result.MqttConfigs.Add(new MqttTelemetryConfigDto
            {
                NodeId = mqttNode.Id,
                Topic = mData.Topic,
                PublishMode = mData.PublishMode,
                Deadband = mData.Deadband,
                IntervalMs = mData.IntervalMs,
                Qos = mData.Qos,
                Retain = mData.Retain,
                PayloadMode = effectivePayloadMode,
                EnTagIndex = enTag,
                DataTagIndex = mData.DataTagIndex,
                CustomTemplate = effectiveTemplate,
                IncludeDevice = mData.IncludeDevice,
                IncludeTimestamp = mData.IncludeTimestamp,
                Fields = telemetryFields
            });
        }

        // 2. Extract MQTT Subscription Configs
        foreach (var subNode in mqttSubNodes)
        {
            var sData = subNode.MqttSubscribeData ?? new MqttSubscribeNodeData();
            result.MqttSubConfigs.Add(new MqttSubscriptionConfigDto
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

        // 3. Extract Modbus Master Read Configs
        for (int i = 0; i < modbusReadNodes.Count; i++)
        {
            var mbNode = modbusReadNodes[i];
            var mbData = mbNode.ModbusReadData ?? new ModbusReadNodeData();
            result.ModbusReadConfigs.Add(new ModbusMasterReadConfigDto
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
                ValidTagIndex = mbData.ValidTagIndex,
                ErrorTagIndex = mbData.ErrorTagIndex,
                ErrCodeTagIndex = mbData.ErrCodeTagIndex,
                Pins = new List<ModbusMasterPinBindingDto>(mbData.Pins)
            });
        }

        // 4. Extract Modbus Master Write Configs
        for (int i = 0; i < modbusWriteNodes.Count; i++)
        {
            var mbNode = modbusWriteNodes[i];
            var mbData = mbNode.ModbusWriteData ?? new ModbusWriteNodeData();
            result.ModbusWriteConfigs.Add(new ModbusMasterWriteConfigDto
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

            // ICD MBM-002 & RTOS buffer limits validation
            if (mbData.WriteMode == ModbusWriteMode.DirectRegisters)
            {
                if (mbData.PointCount < 1 || mbData.PointCount > 16)
                {
                    result.Diagnostics.Add(new CompileDiagnostic(
                        "ERR_MB_WRITE_DIRECT_LIMIT",
                        DiagnosticSeverity.Error,
                        $"Modbus Write node '{CompilationHelpers.GetNodeLabel(mbNode, "MB_WRITE")}' in Direct Mode has PointCount={mbData.PointCount}. Must be between 1 and 16 points (RTOS buffer & physical pins limit).",
                        NodeId: mbNode.Id));
                }
            }
            else
            {
                if (mbData.ChannelCount < 1 || mbData.ChannelCount > 64)
                {
                    result.Diagnostics.Add(new CompileDiagnostic(
                        "ERR_MB_WRITE_CHANNEL_LIMIT",
                        DiagnosticSeverity.Error,
                        $"Modbus Write node '{CompilationHelpers.GetNodeLabel(mbNode, "MB_WRITE")}' in Channel-Indexed Mode has ChannelCount={mbData.ChannelCount}. Must be between 1 and 64 channels.",
                        NodeId: mbNode.Id));
                }
            }
        }

        // 5. Extract Direct Write Configs (Direct Physical Output Actuators)
        for (int i = 0; i < directWriteNodes.Count; i++)
        {
            var dwNode = directWriteNodes[i];
            var dwData = dwNode.DirectWriteData ?? new DirectWriteNodeData();
            result.DirectWriteConfigs.Add(new DirectWriteConfigDto
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

        // 6. Extract DTU Configs (HTTP & TCP)
        if (httpNodes.Count > 0 || tcpNodes.Count > 0)
        {
            var compiledDtu = new DtuConfigDto
            {
                Enabled = true,
                PortIndex = 0,
                SerialMode = DtuSerialMode.Transference
            };

            var firstHttp = httpNodes.FirstOrDefault();
            if (firstHttp?.HttpClientData != null)
            {
                var hData = firstHttp.HttpClientData;
                compiledDtu.Enabled = true;
                compiledDtu.UplinkMode = DtuUplinkMode.Http;
                compiledDtu.PayloadMode = string.Equals(hData.PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase)
                    ? DtuPayloadMode.Raw
                    : DtuPayloadMode.Custom;

                result.DtuHttpConfig = new DtuHttpConfigDto
                {
                    Enabled = true,
                    Method = string.Equals(hData.Method, "PUT", StringComparison.OrdinalIgnoreCase) ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                    Url = hData.Url ?? string.Empty,
                    CustomHeaders = hData.CustomHeaders ?? string.Empty,
                    TimeoutSec = hData.TimeoutSec,
                    PeriodicIntervalSec = hData.PeriodicIntervalSec,
                    TriggerMode = string.Equals(hData.TriggerMode, "Periodic", StringComparison.OrdinalIgnoreCase) ? DtuHttpTriggerMode.Periodic : DtuHttpTriggerMode.OnDataReceived
                };
                compiledDtu.HttpConfig = result.DtuHttpConfig;
            }

            var firstTcp = tcpNodes.FirstOrDefault();
            if (firstTcp?.TcpClientData != null)
            {
                var tData = firstTcp.TcpClientData;
                compiledDtu.Enabled = true;
                if (firstHttp == null)
                {
                    compiledDtu.UplinkMode = DtuUplinkMode.TcpClient;
                    compiledDtu.PayloadMode = string.Equals(tData.PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase)
                        ? DtuPayloadMode.Custom
                        : DtuPayloadMode.Raw;
                }

                result.DtuTcpConfig = new DtuTcpConfigDto
                {
                    Enabled = true,
                    SocketMode = DtuTcpSocketMode.Client,
                    RemoteHost = tData.RemoteHost ?? string.Empty,
                    RemotePort = tData.RemotePort,
                    ReconnectDelaySec = tData.ReconnectDelaySec,
                    HeartbeatIntervalSec = tData.HeartbeatIntervalSec,
                    HeartbeatPayload = tData.HeartbeatPayload ?? string.Empty,
                    RegistrationType = tData.RegistrationType switch
                    {
                        "OnConnect" => DtuTcpRegistrationType.OnConnect,
                        "PrependToEveryPacket" => DtuTcpRegistrationType.PrependToEveryPacket,
                        _ => DtuTcpRegistrationType.None
                    },
                    RegistrationPayload = tData.RegistrationPayload ?? string.Empty
                };
                compiledDtu.TcpConfig = result.DtuTcpConfig;
            }

            result.DtuConfig = compiledDtu;
        }

        return result;
    }
}
