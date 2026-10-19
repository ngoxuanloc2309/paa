using System.Text.Json.Serialization;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Models;

public class ProjectMetadata
{
    public string ProjectName { get; set; } = "Untitled";
    public string Author { get; set; } = "SynaptiX Engineer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int SchemaVersion { get; set; } = 1;
    public string TargetDevice { get; set; } = "SynaptiX Controller";
    public string Description { get; set; } = string.Empty;

    // Hardware Target & Resource Profile (Contract V2.0)
    public ushort DeviceClass { get; set; } = 1; // REMOTE_IO
    public ushort ProductVariant { get; set; } = 1; // VARIANT_8DI_8DO_4AI
    public int DigitalInputs { get; set; } = 8;
    public int DigitalOutputs { get; set; } = 8;
    public int AnalogInputs { get; set; } = 4;
    public int VirtualFlags { get; set; } = 32;
    public int VirtualRegisters { get; set; } = 32;
    public int RetentiveRegisters { get; set; } = 32;
    public int Counters { get; set; } = 8;
    public int MaxRules { get; set; } = 100;

    // MQTT IIoT Broker Settings (Project Level)
    public bool MqttBrokerEnabled { get; set; } = true;
    public string MqttBrokerHost { get; set; } = string.Empty;
    public int MqttBrokerPort { get; set; } = 1883;
    public string MqttClientId { get; set; } = string.Empty;
    public string MqttUsername { get; set; } = string.Empty;
    public string MqttPassword { get; set; } = string.Empty;
    public bool MqttTlsEnabled { get; set; } = false;
    public bool MqttCleanSession { get; set; } = true;
    public ushort MqttKeepAliveSec { get; set; } = 60;
}

public class ProjectTagData
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TagKind Kind { get; set; }
    public int Channel { get; set; }
    public string Group { get; set; } = string.Empty;
    public int Value { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TagDataType DataType { get; set; } = TagDataType.Int32;
}

public class ProjectNodeData
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Input", "Trigger", "Guard", "Action"
    public double LocationX { get; set; }
    public double LocationY { get; set; }
    public string CustomLabel { get; set; } = string.Empty;

    /// <summary>
    /// Metadata thứ tự khởi tạo nội bộ phục vụ compiler tie-breaking ổn định (NOT runtime priority).
    /// </summary>
    public int StableOrder { get; set; } = 0;

    // Input node properties
    public string TagName { get; set; } = string.Empty;

    // Trigger node properties
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TriggerType TriggerType { get; set; } = TriggerType.ON_RISE;
    public uint ForMs { get; set; }

    // Guard node properties
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompareOp CompareOp { get; set; } = CompareOp.EQ;
    public double ThresholdLo { get; set; }
    public double ThresholdHi { get; set; }
    public bool Negate { get; set; }

    // Action node properties
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionType ActionType { get; set; } = ActionType.SET_TAG;
    public string TargetTagName { get; set; } = string.Empty;
    public int ActionParam { get; set; } = 1;

    // Timer node properties
    public string TimerMode { get; set; } = "TON"; // "TON", "TOF", "TP"
    public uint PresetMs { get; set; }
    public string OutputTagName { get; set; } = string.Empty;

    // Counter node properties
    public string CounterMode { get; set; } = "CTU"; // "CTU", "CTD"
    public string CvTagName { get; set; } = string.Empty;
    public int PresetValue { get; set; } = 10;
    public string ResetTagName { get; set; } = string.Empty;

    // Scale node properties
    public double Gain { get; set; } = 0.01;
    public double Offset { get; set; } = 0.0;
    public string Unit { get; set; } = "bar";
    public int DecimalPlaces { get; set; } = 1;
    public bool IsClamped { get; set; } = true;
    public double ClampMin { get; set; } = 0.0;
    public double ClampMax { get; set; } = 100.0;

    // MQTT Publish node properties
    public string MqttTopic { get; set; } = "synaptix/telemetry/data";
    public string MqttPayloadMode { get; set; } = "JSON"; // "Raw", "JSON", "Custom"
    public string MqttCustomTemplate { get; set; } = string.Empty;
    public int MqttQos { get; set; } = 0; // 0, 1, 2
    public bool MqttRetain { get; set; } = false;
    public string MqttPublishMode { get; set; } = "Event"; // "Event", "COV", "Cyclic"
    public double MqttDeadband { get; set; } = 0.5;
    public string MqttUnit { get; set; } = string.Empty;
    public bool MqttIncludeDevice { get; set; } = true;
    public bool MqttIncludeTimestamp { get; set; } = true;
    public List<MqttPayloadFieldData> MqttFields { get; set; } = new();

    // MQTT Subscribe node properties
    public string MqttSubTopic { get; set; } = "synaptix/commands/inbound";
    public int MqttSubQos { get; set; } = 1;
    public string MqttSubQueueMode { get; set; } = "Fifo"; // "Fifo", "LatestWins"
    public int MqttSubQueueCapacity { get; set; } = 16;
    public bool MqttSubIgnoreRetained { get; set; } = true;
    public uint MqttSubTimeoutMs { get; set; } = 0;
    public string MqttSubFailSafeAction { get; set; } = "KeepLastValue";
    public List<MqttSubPinData> MqttSubPins { get; set; } = new();
}

public class MqttPayloadFieldData
{
    public string Key { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public class MqttSubPinData
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public string DataType { get; set; } = "Int16";
    public string TargetTagName { get; set; } = string.Empty;
    public ushort? TargetTagIndex { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double DefaultValue { get; set; } = 0.0;
}

public class ProjectConnectionData
{
    public string SourceNodeId { get; set; } = string.Empty;
    public int SourceConnectorIndex { get; set; }
    public string SourceConnectorTitle { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public int TargetConnectorIndex { get; set; }
    public string TargetConnectorTitle { get; set; } = string.Empty;
}

public class ProjectRuleData
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool Enabled { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TriggerType TriggerType { get; set; } = TriggerType.ON_RISE;
    public string TriggerTagName { get; set; } = string.Empty;
    public uint ForMs { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompareOp CompareOp { get; set; } = CompareOp.NONE;
    public string GuardTagName { get; set; } = string.Empty;
    public double ThresholdLo { get; set; }
    public double ThresholdHi { get; set; }
    public bool GuardNegated { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionType ActionType { get; set; } = ActionType.SET_TAG;
    public string ActionTagName { get; set; } = string.Empty;
    public int ActionParam { get; set; } = 1;
    public string Narrative { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;
    public string? DiagramId { get; set; }
    public List<ProjectNodeData>? SourceNodes { get; set; }
    public List<ProjectConnectionData>? SourceConnections { get; set; }
}

public class ProjectModel
{
    public ProjectMetadata Metadata { get; set; } = new();
    public List<ProjectTagData> Tags { get; set; } = new();
    public List<ProjectNodeData> Nodes { get; set; } = new();
    public List<ProjectConnectionData> Connections { get; set; } = new();
    public List<ProjectRuleData> Rules { get; set; } = new();
    public string? CurrentEditingRuleId { get; set; }
    public string? CurrentDiagramId { get; set; }
    public List<int> WatchlistTagIndices { get; set; } = new();
}
