using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Chứa thông tin cấu hình dự án, phần cứng mục tiêu và các thiết lập truyền thông (MQTT, Wi-Fi, Modbus, DTU).
/// </summary>
public class ProjectMetadata
{
    public string ProjectName { get; set; } = "Untitled";
    public string Author { get; set; } = "SynaptiX Engineer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int SchemaVersion { get; set; } = 1;
    public string TargetDevice { get; set; } = "SynaptiX SYNX-RIO-884";
    public string Description { get; set; } = string.Empty;

    // Hardware Target & Resource Profile (Contract V2.0)
    public ushort DeviceClass { get; set; } = 1; // REMOTE_IO
    public ushort ProductVariant { get; set; } = 1; // VARIANT_RIO_884
    public int DigitalInputs { get; set; } = 8;
    public int DigitalOutputs { get; set; } = 8;
    public int AnalogInputs { get; set; } = 4;
    public int VirtualFlags { get; set; } = 32;
    public int VirtualRegisters { get; set; } = 32;
    public int RetentiveRegisters { get; set; } = 32;
    public int Counters { get; set; } = 8;
    public int MaxRules { get; set; } = 100;
    public ushort PhysicalRs485PortCount { get; set; } = 2;
    public uint HardwareCapabilities { get; set; } = 0;

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

    // MQTT Telemetry (Publish) Settings (Project Level)
    public string MqttTopic { get; set; } = "synaptix/telemetry/data";
    public int MqttQos { get; set; } = 0;
    public bool MqttRetain { get; set; } = false;
    public string MqttPublishMode { get; set; } = "Cyclic";
    public List<MqttPayloadFieldData> MqttFields { get; set; } = new();
    public string MqttSubTopic { get; set; } = "factory/dtu/command";
    public int MqttSubQos { get; set; } = 1;
    public string MqttSubQueueMode { get; set; } = "Fifo";
    public List<MqttSubPinData> MqttSubPins { get; set; } = new();

    // MQTT TLS Certificate Settings (SPLC-SPEC-MQTT-003)
    public string MqttTlsCaCertPath { get; set; } = string.Empty;
    public string MqttTlsCaCertPem { get; set; } = string.Empty;
    public bool MqttTlsAllowSelfSigned { get; set; } = false;
    public bool MqttTlsMtlsEnabled { get; set; } = false;
    public string MqttTlsClientCertPath { get; set; } = string.Empty;
    public string MqttTlsClientCertPem { get; set; } = string.Empty;
    public string MqttTlsClientKeyPath { get; set; } = string.Empty;
    public string MqttTlsClientKeyPem { get; set; } = string.Empty;
    public string MqttTlsClientKeyPassword { get; set; } = string.Empty;
    public bool MqttTlsIgnoreHostname { get; set; } = false;
    public int MqttTlsProtocolVersion { get; set; } = 0;
    public string MqttTlsSniHostname { get; set; } = string.Empty;

    // Wi-Fi Configuration Settings (SPLC-SPEC-WIFI-004)
    public bool WifiEnabled { get; set; } = true;
    public bool WifiAutoReconnect { get; set; } = true;
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public bool WifiDhcpEnabled { get; set; } = true;
    public string WifiStaticIp { get; set; } = "192.168.1.50";
    public string WifiSubnetMask { get; set; } = "255.255.255.0";
    public string WifiGateway { get; set; } = "192.168.1.1";
    public string WifiDns { get; set; } = "8.8.8.8";

    // Modbus Multi-Port RS-485 Configuration Settings (SPLC-SPEC-MBM-003)
    public List<ModbusPortConfigModel> ModbusPorts { get; set; } = new()
    {
        new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Master", SlaveId = 1, BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = 1, ResponseTimeoutMs = 200, InterFrameDelayMs = 20, RetryCount = 1 },
        new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Master", SlaveId = 2, BaudRate = 19200, DataBits = 8, Parity = "Even", StopBits = 1, ResponseTimeoutMs = 200, InterFrameDelayMs = 20, RetryCount = 1 },
        new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = false, Role = "Master", SlaveId = 3, BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = 1, ResponseTimeoutMs = 200, InterFrameDelayMs = 20, RetryCount = 1 },
        new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false, Role = "Master", SlaveId = 4, BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = 1, ResponseTimeoutMs = 200, InterFrameDelayMs = 20, RetryCount = 1 }
    };

    // Modbus Slave Mapping Configuration Settings (SPLC-SPEC-SLAVE-001)
    public List<ModbusSlaveMapEntryModel> ModbusSlaveMapEntries { get; set; } = new();

    // MQTT Last Will & Testament (LWT) (SPLC-SPEC-MQTT-004)
    public bool MqttWillEnabled { get; set; } = false;
    public string MqttWillTopic { get; set; } = string.Empty;
    public string MqttWillPayload { get; set; } = "{\"status\":\"offline\"}";
    public byte MqttWillQos { get; set; } = 1;
    public bool MqttWillRetain { get; set; } = true;

    // DTU Gateway Configuration (SPLC-SPEC-DTU-001)
    public DtuConfigModel DtuConfig { get; set; } = new();

    // Zigbee Wireless Subsystem Configuration (SPLC-SPEC-ZIGBEE-006)
    public ZigbeeConfigModel ZigbeeConfig { get; set; } = new();
}

/// <summary>
/// Cấu hình trạm truyền thông DTU Gateway (Serial Mode, Uplink Mode, JSON Template).
/// </summary>
public class DtuConfigModel
{
    public bool Enabled { get; set; } = false;
    public byte PortIndex { get; set; } = 0;
    public string SerialMode { get; set; } = "ModbusMaster"; // ModbusMaster, ModbusSlave
    public string UplinkMode { get; set; } = "Mqtt"; // Mqtt, Http, TcpClient, TcpServer
    public ushort FrameTimeoutDelayMs { get; set; } = 20;
    public ushort MaxPacketSize { get; set; } = 512;
    public ushort PublishIntervalSec { get; set; } = 10;

    /// <summary>
    /// Chế độ định dạng dữ liệu truyền lên mạng: "Standard" (chuẩn công nghiệp IEC), "Custom" (mẫu JSON tùy biến), hoặc "Raw" (dữ liệu thô không bọc JSON).
    /// </summary>
    public string PayloadMode { get; set; } = "Standard";

    /// <summary>
    /// Có đính kèm mã định danh thiết bị trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludeDeviceId { get; set; } = true;

    /// <summary>
    /// Có đính kèm thời gian UTC ISO-8601 trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Có đính kèm chỉ số cổng vật lý RS-485 trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludePort { get; set; } = true;

    /// <summary>
    /// Có đính kèm số thứ tự gói tin tăng dần trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludeSequence { get; set; } = true;

    /// <summary>
    /// Có đính kèm chuỗi byte Hex trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludeHexStream { get; set; } = false;

    /// <summary>
    /// Có đính kèm độ dài byte gói tin trong bản tin JSON chuẩn công nghiệp hay không.
    /// </summary>
    public bool IncludePayloadLength { get; set; } = false;

    public DtuHttpModel HttpSettings { get; set; } = new();
    public DtuTcpModel TcpSettings { get; set; } = new();
    public string CustomJsonTemplate { get; set; } = "{\n  \"device\": \"{{DEVICE_ID}}\",\n  \"timestamp\": \"{{TIMESTAMP}}\",\n  \"data\": \"{{SERIAL_PAYLOAD}}\"\n}";
}

/// <summary>
/// Cấu hình kênh HTTP REST Client của DTU Gateway.
/// </summary>
public class DtuHttpModel
{
    public bool Enabled { get; set; } = false;
    public string Method { get; set; } = "POST"; // POST, PUT
    public ushort TimeoutSec { get; set; } = 10;
    public ushort PeriodicIntervalSec { get; set; } = 5;

    /// <summary>
    /// Chế độ kích hoạt gửi bản tin HTTP: "OnDataReceived" (khi có dữ liệu RS-485) hoặc "Periodic" (chu kỳ).
    /// </summary>
    public string TriggerMode { get; set; } = "OnDataReceived";
    public string Url { get; set; } = "https://api.factory-cloud.io/v1/telemetry";
    public string Headers { get; set; } = "Content-Type: application/json";
    public List<DtuHttpFieldModel> Fields { get; set; } = new();
}

/// <summary>
/// Đại diện cho một trường dữ liệu (Key-Value) trong HTTP Payload của DTU.
/// </summary>
public partial class DtuHttpFieldModel : ObservableObject
{
    /// <summary>
    /// Tên trường dữ liệu (JSON Key).
    /// </summary>
    [ObservableProperty]
    private string _key = "field1";
    /// <summary>
    /// Nguồn trích xuất giá trị (Payload, DeviceId, Timestamp, Custom).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomValueEditable))]
    private string _valueSource = "Payload";
    /// <summary>
    /// Giá trị tĩnh do người dùng cấu hình khi nguồn là Custom.
    /// </summary>
    [ObservableProperty]
    private string _customValue = string.Empty;

    /// <summary>
    /// Cho biết trường CustomValue có được phép chỉnh sửa hay không (chỉ khi ValueSource == "Custom").
    /// </summary>
    public bool IsCustomValueEditable => string.Equals(ValueSource, "Custom", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Cấu hình kênh truyền TCP Socket Client / Server của DTU Gateway.
/// </summary>
/// <summary>
/// Cấu hình phân hệ truyền thông không dây Zigbee Modbus (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public class ZigbeeConfigModel
{
    public bool Enabled { get; set; } = false;
    public string Role { get; set; } = "Slave"; // Slave | Master
    public byte SlaveId { get; set; } = 1;
    public string PanIdHex { get; set; } = "1A2B";
    public ushort Channel { get; set; } = 15;
    public string Antenna { get; set; } = "Pcb"; // Pcb | External
    public bool SecurityEnabled { get; set; } = false;
    public bool PreserveKeyOnDeploy { get; set; } = true;
    public string NetworkName { get; set; } = "Zigbee_Node_1";
}

/// <summary>
/// Cấu hình truyền thông trong suốt Socket TCP Client / Server của DTU Gateway.
/// </summary>
public class DtuTcpModel
{
    public bool Enabled { get; set; } = false;
    public string SocketMode { get; set; } = "Client"; // Client, Server
    public string RemoteHost { get; set; } = "192.168.1.100";
    public ushort RemotePort { get; set; } = 8888;
    public ushort LocalPort { get; set; } = 8888;
    public ushort ReconnectDelaySec { get; set; } = 5;
    public ushort HeartbeatIntervalSec { get; set; } = 30;
    public string HeartbeatPayload { get; set; } = "PING";

    /// <summary>
    /// Cơ chế gửi gói tin định danh: "None" (tắt), "OnConnect" (khi mở socket), hoặc "PrependToEveryPacket" (gắn đầu gói).
    /// </summary>
    public string RegistrationType { get; set; } = "None";

    /// <summary>
    /// Nội dung chuỗi báo danh định danh (hỗ trợ nhập văn bản hoặc macro {{DEVICE_ID}}).
    /// </summary>
    public string RegistrationPayload { get; set; } = "{{DEVICE_ID}}";
}

/// <summary>
/// Đại diện cho một biến/tag I/O hoặc nội bộ trong dự án SynaptiX.
/// </summary>
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

/// <summary>
/// Đại diện cho một khối chức năng (Node) trên sơ đồ FBD / Rule Canvas.
/// </summary>
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
    public string TimeUnit { get; set; } = "s";
    public double DurationValue { get; set; } = 3.0;
    public string OutputTagName { get; set; } = string.Empty;

    public uint OnTimeMs { get; set; } = 1000;

    public uint OffTimeMs { get; set; } = 1000;

    public bool StartWithOn { get; set; } = true;

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

    // Modbus Read node properties
    public byte ModbusPortIndex { get; set; } = 0;
    public byte ModbusSlaveId { get; set; } = 1;
    public string ModbusFunctionCode { get; set; } = "ReadHoldingRegisters";
    public string ModbusReadMode { get; set; } = "Cyclic";
    public ushort? ModbusTrigTagIndex { get; set; }
    public ushort ModbusStartAddress { get; set; } = 0;
    public ushort ModbusRegCount { get; set; } = 2;
    public uint ModbusPollIntervalMs { get; set; } = 500;
    public uint ModbusTimeoutMs { get; set; } = 300;
    public List<ModbusPinData> ModbusPins { get; set; } = new();

    // Modbus Write node properties
    public byte ModbusWritePortIndex { get; set; } = 0;
    public byte ModbusWriteSlaveId { get; set; } = 1;
    public string ModbusWriteMode { get; set; } = "ChannelIndexed";
    public string ModbusWriteFunctionCode { get; set; } = "WriteSingleRegister";
    public ushort ModbusWriteStartAddress { get; set; } = 0;
    public ushort ModbusWritePointCount { get; set; } = 1;
    public ushort ModbusWriteBaseAddressCommand { get; set; } = 0x0100;
    public ushort ModbusWriteBaseAddressParam { get; set; } = 0x0120;
    public ushort ModbusWriteChannelCount { get; set; } = 9;
    public bool ModbusWriteIsExclusiveChannel { get; set; } = true;
    public string ModbusWriteTriggerMode { get; set; } = "OnTriggerRisingEdge";
    public uint ModbusWriteCyclicIntervalMs { get; set; } = 500;
    public uint ModbusWriteTimeoutMs { get; set; } = 300;
    public float ModbusWriteDeadband { get; set; } = 0.5f;
    public List<ModbusPinData> ModbusWritePins { get; set; } = new();

    // Direct Write node properties
    public string DirectWriteTargetPin { get; set; } = "Do0";
    public string DirectWriteMode { get; set; } = "DigitalOutput";
    public ushort DirectWriteChannelCount { get; set; } = 18;
    public bool DirectWriteIsExclusiveChannel { get; set; } = true;
    public uint DirectWritePulseWidthUs { get; set; } = 50;
    public uint DirectWritePwmFrequencyHz { get; set; } = 1000;
    public string DirectWriteTriggerMode { get; set; } = "OnTriggerRisingEdge";
    public float DirectWriteDeadband { get; set; } = 0.5f;
    public uint DirectWriteTimeoutMs { get; set; } = 300;
    public uint DirectWriteDefaultColorRgb { get; set; } = 0x00FF00;

    // DTU Gateway node properties
    public byte DtuPortIndex { get; set; } = 0;
    public string DtuSerialMode { get; set; } = "Transference"; // Transference, ModbusMaster, ModbusSlave
    public string DtuUplinkMode { get; set; } = "Mqtt"; // Mqtt, Http, TcpClient, TcpServer
    public string DtuCustomJsonTemplate { get; set; } = string.Empty;
    public List<DtuHttpFieldModel> DtuHttpFields { get; set; } = new();

    // HTTP Client node properties
    public string HttpUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string HttpHeaders { get; set; } = "Content-Type: application/json";
    public ushort HttpTimeoutSec { get; set; } = 10;
    public string HttpTriggerMode { get; set; } = "OnDataReceived";
    public ushort HttpIntervalSec { get; set; } = 5;
    public string HttpPayloadMode { get; set; } = "Custom";
    public string HttpCustomTemplate { get; set; } = string.Empty;

    // TCP Client node properties
    public string TcpRemoteHost { get; set; } = string.Empty;
    public ushort TcpRemotePort { get; set; } = 8888;
    public ushort TcpReconnectDelaySec { get; set; } = 5;
    public ushort TcpHeartbeatIntervalSec { get; set; } = 30;
    public string TcpHeartbeatPayload { get; set; } = string.Empty;
    public string TcpRegistrationType { get; set; } = "None";
    public string TcpRegistrationPayload { get; set; } = string.Empty;
    public string TcpPayloadMode { get; set; } = "Raw";
}

/// <summary>
/// Đại diện cho trường ánh xạ trong khối MQTT Publisher Node.
/// </summary>
public class MqttPayloadFieldData
{
    public string Key { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Đại diện cho chân ngõ ra ánh xạ từ bản tin JSON nhận được của MQTT Subscriber Node.
/// </summary>
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

/// <summary>
/// Đại diện cho cấu hình ánh xạ điểm dữ liệu Modbus trong các Node Modbus Read / Write.
/// </summary>
public class ModbusPinData
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ushort RegOffset { get; set; } = 0;
    public string DataType { get; set; } = "Int16";
    public string Endianness { get; set; } = "BigEndian_ABCD";
    public float Multiplier { get; set; } = 1.0f;
    public string TargetTagName { get; set; } = string.Empty;
    public ushort? TargetTagIndex { get; set; }
}

/// <summary>
/// Đại diện cho dây nối logic giữa các khối chức năng trên sơ đồ FBD.
/// </summary>
public class ProjectConnectionData
{
    public string SourceNodeId { get; set; } = string.Empty;
    public int SourceConnectorIndex { get; set; }
    public string SourceConnectorTitle { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public int TargetConnectorIndex { get; set; }
    public string TargetConnectorTitle { get; set; } = string.Empty;
}

/// <summary>
/// Đại diện cho luật điều khiển logic (Trigger - Guard - Action) trong dự án.
/// </summary>
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

/// <summary>
/// Mô hình gốc lưu trữ toàn bộ dữ liệu của một dự án SynaptiX (.splc).
/// </summary>
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

/// <summary>
/// Cấu hình chi tiết cho một cổng truyền thông nối tiếp RS-485 (Master / Slave).
/// </summary>
public class ModbusPortConfigModel
{
    public byte PortIndex { get; set; } = 0;
    public string PortName { get; set; } = "RS-485 #1";
    public bool Enabled { get; set; } = true;
    public string Role { get; set; } = "Master"; // "Master", "Slave"
    public byte SlaveId { get; set; } = 1;
    public uint BaudRate { get; set; } = 9600;
    public byte DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None"; // "None", "Even", "Odd"
    public byte StopBits { get; set; } = 1;
    public ushort ResponseTimeoutMs { get; set; } = 200;
    public ushort InterFrameDelayMs { get; set; } = 20;
    public ushort RetryCount { get; set; } = 1;

    /// <summary>
    /// Danh s?ch c?c k?nh thu th?p d? li?u v? l?nh ?i?u khi?n khi c?ng ho?t ??ng ? ch? ?? Modbus Master.
    /// </summary>
    public List<ModbusMasterChannelModel> MasterChannels { get; set; } = new();

    public ModbusPortConfigModel Clone()
    {
        return new ModbusPortConfigModel
        {
            PortIndex = this.PortIndex,
            PortName = this.PortName,
            Enabled = this.Enabled,
            Role = this.Role,
            SlaveId = this.SlaveId,
            BaudRate = this.BaudRate,
            DataBits = this.DataBits,
            Parity = this.Parity,
            StopBits = this.StopBits,
            ResponseTimeoutMs = this.ResponseTimeoutMs,
            InterFrameDelayMs = this.InterFrameDelayMs,
            RetryCount = this.RetryCount,
            MasterChannels = this.MasterChannels.Select(c => c.Clone()).ToList()
        };
    }

    public string FormatDropdownLabel(bool isVietnamese, bool isForceDisabledWarning = false)
    {
        string prefix = isVietnamese ? "Cổng" : "Port";
        string roleTag = Role == "Slave" ? (isVietnamese ? " [SLAVE]" : " [SLAVE]") : "";
        string disabledTag = isForceDisabledWarning ? (isVietnamese ? " [ĐÃ TẮT]" : " [DISABLED]") : "";
        return $"{prefix} {PortIndex + 1}: {PortName}{roleTag}{disabledTag}";
    }

    public string FormatFullSummary(bool isVietnamese, bool isForceDisabledWarning = false)
    {
        string prefix = isVietnamese ? "Cổng" : "Port";
        char parityChar = string.IsNullOrEmpty(Parity) ? 'N' : Parity[0];
        string disabledTag = isForceDisabledWarning ? (isVietnamese ? " [ĐÃ TẮT]" : " [DISABLED]") : "";
        if (Role == "Slave")
        {
            return $"{prefix} {PortIndex + 1}: {PortName} · {BaudRate} bps · {DataBits}-{char.ToUpperInvariant(parityChar)}-{StopBits} · SLAVE (ID: {SlaveId}){disabledTag}";
        }
        return $"{prefix} {PortIndex + 1}: {PortName} · {BaudRate} bps · {DataBits}-{char.ToUpperInvariant(parityChar)}-{StopBits} · Timeout {ResponseTimeoutMs}ms · Retries {RetryCount}{disabledTag}";
    }
}

/// <summary>
/// Đại diện cho một mục ánh xạ trong bảng nhớ Modbus RTU Slave.
/// </summary>
public class ModbusSlaveMapEntryModel
{
    public ushort ModbusAddress { get; set; } = 0;
    public ushort TargetTagIndex { get; set; } = 0;
    public string TargetTagName { get; set; } = string.Empty;
    public string AccessRight { get; set; } = "ReadOnly"; // "ReadOnly", "ReadWrite"
    public byte DataType { get; set; } = 2; // 1=BOOL, 2=INT32, 3=REAL, 4=UINT16, 5=INT16
    public byte PortMask { get; set; } = 0x0F;
    public string Description { get; set; } = string.Empty;

    public ModbusSlaveMapEntryModel Clone()
    {
        return new ModbusSlaveMapEntryModel
        {
            ModbusAddress = this.ModbusAddress,
            TargetTagIndex = this.TargetTagIndex,
            TargetTagName = this.TargetTagName,
            AccessRight = this.AccessRight,
            DataType = this.DataType,
            PortMask = this.PortMask,
            Description = this.Description
        };
    }
}
