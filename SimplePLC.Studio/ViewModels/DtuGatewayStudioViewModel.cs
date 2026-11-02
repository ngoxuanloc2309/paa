using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel quản lý toàn bộ cấu hình, trạng thái vận hành và chẩn đoán của DTU Gateway Studio.
/// </summary>
public sealed partial class DtuGatewayStudioViewModel : ObservableObject
{
    private readonly ProjectMetadata _metadata;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0: RS-485, 1: Uplink, 2: JSON, 3: Live Monitor

    // --- RS-485 Port Settings ---
    [ObservableProperty]
    private int _selectedPortIndex = 0;

    [ObservableProperty]
    private string _serialMode = "Transference"; // Transference, ModbusMaster, ModbusSlave

    [ObservableProperty]
    private uint _baudRate = 9600;

    [ObservableProperty]
    private byte _dataBits = 8;

    [ObservableProperty]
    private string _parity = "None";

    [ObservableProperty]
    private byte _stopBits = 1;

    [ObservableProperty]
    private ushort _frameTimeoutDelayMs = 20;

    [ObservableProperty]
    private ushort _maxPacketSize = 512;

    [ObservableProperty]
    private ushort _responseTimeoutMs = 200;

    [ObservableProperty]
    private byte _slaveId = 1;

    // --- Uplink Network Settings ---
    [ObservableProperty]
    private string _uplinkMode = "Mqtt"; // Mqtt, Http, TcpClient, TcpServer

    // MQTT
    [ObservableProperty]
    private string _mqttHost = "broker.hivemq.com";

    [ObservableProperty]
    private int _mqttPort = 1883;

    [ObservableProperty]
    private string _mqttClientId = "synaptix_dtu_01";

    [ObservableProperty]
    private string _mqttUsername = string.Empty;

    [ObservableProperty]
    private string _mqttPassword = string.Empty;

    [ObservableProperty]
    private bool _mqttUseTls = false;

    // MQTT TLS / SSL & mTLS Settings (SPLC-SPEC-MQTT-003)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaCert))]
    private string _mqttTlsCaCertPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaCert))]
    private string _mqttTlsCaCertPem = string.Empty;

    [ObservableProperty]
    private bool _mqttTlsAllowSelfSigned = false;

    [ObservableProperty]
    private bool _mqttTlsIgnoreHostname = false;

    [ObservableProperty]
    private int _mqttTlsProtocolVersion = 0; // 0: Auto (TLS 1.2/1.3), 1: TLS 1.2, 2: TLS 1.3

    [ObservableProperty]
    private string _mqttTlsSniHostname = string.Empty;

    [ObservableProperty]
    private bool _mqttTlsMtlsEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientCert))]
    private string _mqttTlsClientCertPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientCert))]
    private string _mqttTlsClientCertPem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientKey))]
    private string _mqttTlsClientKeyPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientKey))]
    private string _mqttTlsClientKeyPem = string.Empty;

    [ObservableProperty]
    private string _mqttTlsClientKeyPassword = string.Empty;

    // Certificate details (Badges)
    [ObservableProperty]
    private string _caCertSubject = string.Empty;

    [ObservableProperty]
    private string _caCertIssuer = string.Empty;

    [ObservableProperty]
    private string _caCertExpiry = string.Empty;

    [ObservableProperty]
    private string _caCertSha256 = string.Empty;

    public bool HasCaCert => !string.IsNullOrWhiteSpace(MqttTlsCaCertPath) || !string.IsNullOrWhiteSpace(MqttTlsCaCertPem);

    [ObservableProperty]
    private string _clientCertSubject = string.Empty;

    [ObservableProperty]
    private string _clientCertIssuer = string.Empty;

    [ObservableProperty]
    private string _clientCertExpiry = string.Empty;

    [ObservableProperty]
    private string _clientCertSha256 = string.Empty;

    public bool HasClientCert => !string.IsNullOrWhiteSpace(MqttTlsClientCertPath) || !string.IsNullOrWhiteSpace(MqttTlsClientCertPem);

    public bool HasClientKey => !string.IsNullOrWhiteSpace(MqttTlsClientKeyPath) || !string.IsNullOrWhiteSpace(MqttTlsClientKeyPem);

    [ObservableProperty]
    private ushort _mqttKeepAlive = 60;

    [ObservableProperty]
    private string _mqttPublishTopic = "factory/dtu/data";

    [ObservableProperty]
    private string _mqttSubscribeTopic = "factory/dtu/command";

    [ObservableProperty]
    private int _mqttQos = 1;

    [ObservableProperty]
    private bool _mqttRetain = false;

    // MQTT LWT
    [ObservableProperty]
    private bool _mqttWillEnabled = true;

    [ObservableProperty]
    private string _mqttWillTopic = "factory/dtu/status";

    [ObservableProperty]
    private string _mqttWillPayload = "{\"status\":\"offline\"}";

    [ObservableProperty]
    private int _mqttWillQos = 1;

    [ObservableProperty]
    private bool _mqttWillRetain = true;

    // HTTP REST API
    [ObservableProperty]
    private string _httpUrl = "https://api.factory-cloud.io/v1/telemetry";

    [ObservableProperty]
    private string _httpMethod = "POST";

    [ObservableProperty]
    private string _httpHeaders = "Content-Type: application/json\nAuthorization: Bearer token123";

    [ObservableProperty]
    private ushort _httpTimeoutSec = 10;

    [ObservableProperty]
    private ushort _httpIntervalSec = 5;

    [ObservableProperty]
    private string _httpTriggerMode = "OnDataReceived"; // OnDataReceived, Periodic

    /// <summary>
    /// Cho biết chế độ kích hoạt HTTP có phải là gửi theo chu kỳ thời gian (Periodic) hay không.
    /// </summary>
    public bool IsHttpPeriodicTrigger => HttpTriggerMode == "Periodic";

    public ObservableCollection<DtuHttpFieldModel> HttpFields { get; } = new();

    [RelayCommand]
    private void AddHttpField()
    {
        HttpFields.Add(new DtuHttpFieldModel { Key = $"field{HttpFields.Count + 1}", ValueSource = "Payload" });
    }

    [RelayCommand]
    private void RemoveHttpField(DtuHttpFieldModel? field)
    {
        if (field != null)
        {
            HttpFields.Remove(field);
        }
    }

    // TCP Socket
    [ObservableProperty]
    private string _tcpSocketMode = "Client"; // Client, Server

    [ObservableProperty]
    private string _tcpRemoteHost = "192.168.1.100";

    [ObservableProperty]
    private ushort _tcpRemotePort = 8888;

    [ObservableProperty]
    private ushort _tcpLocalPort = 8888;

    [ObservableProperty]
    private ushort _tcpReconnectDelaySec = 5;

    [ObservableProperty]
    private ushort _tcpHeartbeatIntervalSec = 30;

    [ObservableProperty]
    private string _tcpHeartbeatPayload = "PING";

    [ObservableProperty]
    private string _tcpRegistrationType = "None"; // None, OnConnect, PrependToEveryPacket

    [ObservableProperty]
    private string _tcpRegistrationPayload = "{{DEVICE_ID}}";

    // --- JSON Formatter & Preview ---
    [ObservableProperty]
    private string _jsonTemplate = "{\n  \"device\": \"{{DEVICE_ID}}\",\n  \"timestamp\": \"{{TIMESTAMP}}\",\n  \"port\": {{PORT}},\n  \"data\": \"{{SERIAL_PAYLOAD}}\",\n  \"hex\": \"{{HEX_STREAM}}\"\n}";

    [ObservableProperty]
    private string _sampleSerialInput = "TEMP:28.4,HUM:62.1";

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private bool _isJsonSyntaxValid = true;

    [ObservableProperty]
    private string _payloadMode = "Standard"; // Standard, Custom, Raw

    [ObservableProperty]
    private bool _includeDeviceId = true;

    [ObservableProperty]
    private bool _includeTimestamp = true;

    [ObservableProperty]
    private bool _includePort = true;

    [ObservableProperty]
    private bool _includeSequence = true;

    [ObservableProperty]
    private bool _includeHexStream = false;

    [ObservableProperty]
    private bool _includePayloadLength = false;

    [ObservableProperty]
    private string _terminalSendFormat = "Ascii"; // Ascii, Hex

    /// <summary>
    /// Gets or sets whether the diagnostic terminal transmits raw bytes in HEX format instead of ASCII.
    /// </summary>
    public bool TestPacketIsHex
    {
        get => TerminalSendFormat == "Hex";
        set => TerminalSendFormat = value ? "Hex" : "Ascii";
    }

    [ObservableProperty]
    private long _packetSequenceCounter = 1;

    // --- Live Diagnostic Monitor ---
    [ObservableProperty]
    private string _testPacketInput = "TEMP:28.4,HUM:62.1";

    [ObservableProperty]
    private string _terminalLog = "--- SYNAPTIX DTU GATEWAY DIAGNOSTIC TERMINAL ---\n[SYSTEM] DTU Subsystem initialized.\n[SYSTEM] Ready for live serial-to-uplink packets.\n";

    [ObservableProperty]
    private bool _isDeploying = false;

    [ObservableProperty]
    private string _deployStatusMessage = string.Empty;

    public event Action? RequestClose;

    /// <summary>
    /// Callback yêu cầu Studio mở hộp thoại cấu hình MQTT Broker nâng cao (TLS/mTLS/Diagnostics).
    /// </summary>
    public Action? OpenMqttBrokerConfigRequested { get; set; }

    public DtuGatewayStudioViewModel(ProjectMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ReloadFromMetadata();
        UpdateJsonPreview();
    }

    /// <summary>
    /// Tải lại toàn bộ các tham số cấu hình từ ProjectMetadata vào các thuộc tính giao diện.
    /// </summary>
    public void ReloadFromMetadata()
    {
        var dtu = _metadata.DtuConfig;
        SelectedPortIndex = dtu.PortIndex;
        SerialMode = dtu.SerialMode;
        UplinkMode = dtu.UplinkMode;
        FrameTimeoutDelayMs = dtu.FrameTimeoutDelayMs > 0 ? dtu.FrameTimeoutDelayMs : (ushort)20;
        MaxPacketSize = dtu.MaxPacketSize > 0 ? dtu.MaxPacketSize : (ushort)512;
        JsonTemplate = string.IsNullOrWhiteSpace(dtu.CustomJsonTemplate) ? JsonTemplate : dtu.CustomJsonTemplate;

        // Payload mode & field inclusions
        PayloadMode = string.IsNullOrEmpty(dtu.PayloadMode) ? "Standard" : dtu.PayloadMode;
        IncludeDeviceId = dtu.IncludeDeviceId;
        IncludeTimestamp = dtu.IncludeTimestamp;
        IncludePort = dtu.IncludePort;
        IncludeSequence = dtu.IncludeSequence;
        IncludeHexStream = dtu.IncludeHexStream;
        IncludePayloadLength = dtu.IncludePayloadLength;

        // Port specifics from ModbusPorts[0]
        if (_metadata.ModbusPorts != null && _metadata.ModbusPorts.Count > 0)
        {
            var p0 = _metadata.ModbusPorts[0];
            BaudRate = p0.BaudRate;
            DataBits = p0.DataBits;
            Parity = p0.Parity;
            StopBits = p0.StopBits;
            ResponseTimeoutMs = p0.ResponseTimeoutMs;
            SlaveId = p0.SlaveId;
        }

        // MQTT from metadata
        MqttHost = string.IsNullOrEmpty(_metadata.MqttBrokerHost) ? MqttHost : _metadata.MqttBrokerHost;
        MqttPort = _metadata.MqttBrokerPort > 0 ? _metadata.MqttBrokerPort : 1883;
        MqttClientId = string.IsNullOrEmpty(_metadata.MqttClientId) ? MqttClientId : _metadata.MqttClientId;
        MqttUsername = _metadata.MqttUsername;
        MqttPassword = _metadata.MqttPassword;
        MqttUseTls = _metadata.MqttTlsEnabled;
        MqttKeepAlive = _metadata.MqttKeepAliveSec > 0 ? _metadata.MqttKeepAliveSec : (ushort)60;
        MqttSubscribeTopic = string.IsNullOrEmpty(_metadata.MqttSubTopic) ? MqttSubscribeTopic : _metadata.MqttSubTopic;
        MqttWillEnabled = _metadata.MqttWillEnabled;
        MqttWillTopic = string.IsNullOrEmpty(_metadata.MqttWillTopic) ? MqttWillTopic : _metadata.MqttWillTopic;
        MqttWillPayload = string.IsNullOrEmpty(_metadata.MqttWillPayload) ? MqttWillPayload : _metadata.MqttWillPayload;
        MqttWillQos = _metadata.MqttWillQos;
        MqttWillRetain = _metadata.MqttWillRetain;

        // MQTT TLS & mTLS from metadata
        MqttTlsCaCertPath = _metadata.MqttTlsCaCertPath ?? string.Empty;
        MqttTlsCaCertPem = _metadata.MqttTlsCaCertPem ?? string.Empty;
        MqttTlsAllowSelfSigned = _metadata.MqttTlsAllowSelfSigned;
        MqttTlsIgnoreHostname = _metadata.MqttTlsIgnoreHostname;
        MqttTlsProtocolVersion = _metadata.MqttTlsProtocolVersion >= 0 && _metadata.MqttTlsProtocolVersion <= 2 ? _metadata.MqttTlsProtocolVersion : 0;
        MqttTlsSniHostname = _metadata.MqttTlsSniHostname ?? string.Empty;
        MqttTlsMtlsEnabled = _metadata.MqttTlsMtlsEnabled;
        MqttTlsClientCertPath = _metadata.MqttTlsClientCertPath ?? string.Empty;
        MqttTlsClientCertPem = _metadata.MqttTlsClientCertPem ?? string.Empty;
        MqttTlsClientKeyPath = _metadata.MqttTlsClientKeyPath ?? string.Empty;
        MqttTlsClientKeyPem = _metadata.MqttTlsClientKeyPem ?? string.Empty;
        MqttTlsClientKeyPassword = _metadata.MqttTlsClientKeyPassword ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(MqttTlsCaCertPath) || !string.IsNullOrWhiteSpace(MqttTlsCaCertPem))
        {
            ParseCaCert(MqttTlsCaCertPem, MqttTlsCaCertPath);
        }
        if (!string.IsNullOrWhiteSpace(MqttTlsClientCertPath) || !string.IsNullOrWhiteSpace(MqttTlsClientCertPem))
        {
            ParseClientCert(MqttTlsClientCertPem, MqttTlsClientCertPath);
        }

        // HTTP from metadata
        if (dtu.HttpSettings != null)
        {
            HttpUrl = string.IsNullOrEmpty(dtu.HttpSettings.Url) ? HttpUrl : dtu.HttpSettings.Url;
            HttpMethod = string.IsNullOrEmpty(dtu.HttpSettings.Method) ? HttpMethod : dtu.HttpSettings.Method;
            HttpHeaders = string.IsNullOrEmpty(dtu.HttpSettings.Headers) ? HttpHeaders : dtu.HttpSettings.Headers;
            HttpTimeoutSec = dtu.HttpSettings.TimeoutSec > 0 ? dtu.HttpSettings.TimeoutSec : (ushort)10;
            HttpIntervalSec = dtu.HttpSettings.PeriodicIntervalSec > 0 ? dtu.HttpSettings.PeriodicIntervalSec : (ushort)5;
            HttpTriggerMode = string.IsNullOrEmpty(dtu.HttpSettings.TriggerMode) ? "OnDataReceived" : dtu.HttpSettings.TriggerMode;

            HttpFields.Clear();
            if (dtu.HttpSettings.Fields != null)
            {
                foreach (var f in dtu.HttpSettings.Fields)
                {
                    HttpFields.Add(new DtuHttpFieldModel { Key = f.Key, ValueSource = f.ValueSource, CustomValue = f.CustomValue });
                }
            }
            if (HttpFields.Count == 0)
            {
                HttpFields.Add(new DtuHttpFieldModel { Key = "device_id", ValueSource = "DeviceId" });
                HttpFields.Add(new DtuHttpFieldModel { Key = "payload", ValueSource = "Payload" });
                HttpFields.Add(new DtuHttpFieldModel { Key = "timestamp", ValueSource = "Timestamp" });
            }
        }

        // TCP from metadata
        if (dtu.TcpSettings != null)
        {
            TcpSocketMode = string.IsNullOrEmpty(dtu.TcpSettings.SocketMode) ? TcpSocketMode : dtu.TcpSettings.SocketMode;
            TcpRemoteHost = string.IsNullOrEmpty(dtu.TcpSettings.RemoteHost) ? TcpRemoteHost : dtu.TcpSettings.RemoteHost;
            TcpRemotePort = dtu.TcpSettings.RemotePort > 0 ? dtu.TcpSettings.RemotePort : (ushort)8888;
            TcpLocalPort = dtu.TcpSettings.LocalPort > 0 ? dtu.TcpSettings.LocalPort : (ushort)8888;
            TcpReconnectDelaySec = dtu.TcpSettings.ReconnectDelaySec > 0 ? dtu.TcpSettings.ReconnectDelaySec : (ushort)5;
            TcpHeartbeatIntervalSec = dtu.TcpSettings.HeartbeatIntervalSec;
            TcpHeartbeatPayload = dtu.TcpSettings.HeartbeatPayload ?? string.Empty;
            TcpRegistrationType = string.IsNullOrEmpty(dtu.TcpSettings.RegistrationType) ? "None" : dtu.TcpSettings.RegistrationType;
            TcpRegistrationPayload = string.IsNullOrEmpty(dtu.TcpSettings.RegistrationPayload) ? "{{DEVICE_ID}}" : dtu.TcpSettings.RegistrationPayload;
        }
    }

    public void SaveToMetadata()
    {
        var dtu = _metadata.DtuConfig;
        dtu.Enabled = true;
        dtu.PortIndex = (byte)SelectedPortIndex;
        dtu.SerialMode = SerialMode;
        dtu.UplinkMode = UplinkMode;
        dtu.FrameTimeoutDelayMs = FrameTimeoutDelayMs;
        dtu.MaxPacketSize = MaxPacketSize;
        dtu.CustomJsonTemplate = JsonTemplate;
        dtu.PayloadMode = PayloadMode;
        dtu.IncludeDeviceId = IncludeDeviceId;
        dtu.IncludeTimestamp = IncludeTimestamp;
        dtu.IncludePort = IncludePort;
        dtu.IncludeSequence = IncludeSequence;
        dtu.IncludeHexStream = IncludeHexStream;
        dtu.IncludePayloadLength = IncludePayloadLength;

        // Save RS-485
        if (_metadata.ModbusPorts != null && _metadata.ModbusPorts.Count > 0)
        {
            var p0 = _metadata.ModbusPorts[0];
            p0.BaudRate = BaudRate;
            p0.DataBits = DataBits;
            p0.Parity = Parity;
            p0.StopBits = StopBits;
            p0.ResponseTimeoutMs = ResponseTimeoutMs;
            p0.SlaveId = SlaveId;
            p0.Role = SerialMode == "ModbusSlave" ? "Slave" : "Master";
        }

        // Save MQTT
        _metadata.MqttBrokerHost = MqttHost;
        _metadata.MqttBrokerPort = MqttPort;
        _metadata.MqttClientId = MqttClientId;
        _metadata.MqttUsername = MqttUsername;
        _metadata.MqttPassword = MqttPassword;
        _metadata.MqttTlsEnabled = MqttUseTls;
        _metadata.MqttKeepAliveSec = MqttKeepAlive;
        _metadata.MqttSubTopic = MqttSubscribeTopic;
        _metadata.MqttWillEnabled = MqttWillEnabled;
        _metadata.MqttWillTopic = MqttWillTopic;
        _metadata.MqttWillPayload = MqttWillPayload;
        _metadata.MqttWillQos = (byte)MqttWillQos;
        _metadata.MqttWillRetain = MqttWillRetain;

        // Save MQTT TLS & mTLS
        _metadata.MqttTlsCaCertPath = MqttTlsCaCertPath ?? string.Empty;
        _metadata.MqttTlsCaCertPem = MqttTlsCaCertPem ?? string.Empty;
        _metadata.MqttTlsAllowSelfSigned = MqttTlsAllowSelfSigned;
        _metadata.MqttTlsIgnoreHostname = MqttTlsIgnoreHostname;
        _metadata.MqttTlsProtocolVersion = MqttTlsProtocolVersion;
        _metadata.MqttTlsSniHostname = MqttTlsSniHostname?.Trim() ?? string.Empty;
        _metadata.MqttTlsMtlsEnabled = MqttTlsMtlsEnabled;
        _metadata.MqttTlsClientCertPath = MqttTlsClientCertPath ?? string.Empty;
        _metadata.MqttTlsClientCertPem = MqttTlsClientCertPem ?? string.Empty;
        _metadata.MqttTlsClientKeyPath = MqttTlsClientKeyPath ?? string.Empty;
        _metadata.MqttTlsClientKeyPem = MqttTlsClientKeyPem ?? string.Empty;
        _metadata.MqttTlsClientKeyPassword = MqttTlsClientKeyPassword ?? string.Empty;

        // Save HTTP
        dtu.HttpSettings.Enabled = UplinkMode == "Http";
        dtu.HttpSettings.Url = HttpUrl;
        dtu.HttpSettings.Method = HttpMethod;
        dtu.HttpSettings.Headers = HttpHeaders;
        dtu.HttpSettings.TimeoutSec = HttpTimeoutSec;
        dtu.HttpSettings.PeriodicIntervalSec = HttpIntervalSec;
        dtu.HttpSettings.TriggerMode = HttpTriggerMode;
        dtu.HttpSettings.Fields = System.Linq.Enumerable.ToList(HttpFields);

        // Save TCP
        dtu.TcpSettings.Enabled = UplinkMode is "TcpClient" or "TcpServer";
        dtu.TcpSettings.SocketMode = TcpSocketMode;
        dtu.TcpSettings.RemoteHost = TcpRemoteHost;
        dtu.TcpSettings.RemotePort = TcpRemotePort;
        dtu.TcpSettings.LocalPort = TcpLocalPort;
        dtu.TcpSettings.ReconnectDelaySec = TcpReconnectDelaySec;
        dtu.TcpSettings.HeartbeatIntervalSec = TcpHeartbeatIntervalSec;
        dtu.TcpSettings.HeartbeatPayload = TcpHeartbeatPayload;
        dtu.TcpSettings.RegistrationType = TcpRegistrationType;
        dtu.TcpSettings.RegistrationPayload = TcpRegistrationPayload;

        _metadata.LastModified = DateTime.UtcNow;
    }

    partial void OnMqttUseTlsChanged(bool value)
    {
        if (value && MqttPort == 1883)
        {
            MqttPort = 8883;
        }
        else if (!value && MqttPort == 8883)
        {
            MqttPort = 1883;
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp chứng chỉ Root CA của máy chủ Broker (.crt, .cer, .pem).
    /// </summary>
    [RelayCommand]
    public void BrowseCaCert()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerCaCertLabel"],
            Filter = "Certificate Files (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                string pemContent = File.ReadAllText(dialog.FileName);
                ParseCaCert(pemContent, dialog.FileName);
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load CA cert: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa cấu hình chứng chỉ CA riêng và quay về Root CA hệ thống.
    /// </summary>
    [RelayCommand]
    public void ClearCaCert()
    {
        MqttTlsCaCertPath = string.Empty;
        MqttTlsCaCertPem = string.Empty;
        CaCertSubject = string.Empty;
        CaCertIssuer = string.Empty;
        CaCertExpiry = string.Empty;
        CaCertSha256 = string.Empty;
        OnPropertyChanged(nameof(HasCaCert));
    }

    /// <summary>
    /// Phân tích và nạp thông tin chứng chỉ máy chủ (CA Root Certificate).
    /// </summary>
    private void ParseCaCert(string pemContent, string filePath)
    {
        try
        {
            X509Certificate2 cert;
            if (File.Exists(filePath))
            {
                cert = new X509Certificate2(filePath);
            }
            else
            {
                cert = X509Certificate2.CreateFromPem(pemContent);
            }

            MqttTlsCaCertPath = filePath;
            MqttTlsCaCertPem = pemContent;
            CaCertSubject = cert.Subject;
            CaCertIssuer = cert.Issuer;
            CaCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            CaCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            OnPropertyChanged(nameof(HasCaCert));
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to parse CA cert: {ex.Message}");
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp chứng chỉ thiết bị Client (.crt, .cer, .pem, .pfx, .p12).
    /// </summary>
    [RelayCommand]
    public void BrowseClientCert()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerClientCertLabel"],
            Filter = "Certificate / Bundle Files (*.crt;*.cer;*.pem;*.pfx;*.p12)|*.crt;*.cer;*.pem;*.pfx;*.p12|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext is ".pfx" or ".p12")
                {
                    MqttTlsClientCertPath = dialog.FileName;
                    MqttTlsClientCertPem = string.Empty;
                    var cert = new X509Certificate2(dialog.FileName, MqttTlsClientKeyPassword, X509KeyStorageFlags.EphemeralKeySet);
                    ClientCertSubject = cert.Subject;
                    ClientCertIssuer = cert.Issuer;
                    ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");
                    byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
                    ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
                    MqttTlsClientKeyPath = dialog.FileName;
                    OnPropertyChanged(nameof(HasClientCert));
                    OnPropertyChanged(nameof(HasClientKey));
                }
                else
                {
                    string pemContent = File.ReadAllText(dialog.FileName);
                    ParseClientCert(pemContent, dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load Client cert: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa chứng chỉ Client Certificate.
    /// </summary>
    [RelayCommand]
    public void ClearClientCert()
    {
        MqttTlsClientCertPath = string.Empty;
        MqttTlsClientCertPem = string.Empty;
        ClientCertSubject = string.Empty;
        ClientCertIssuer = string.Empty;
        ClientCertExpiry = string.Empty;
        ClientCertSha256 = string.Empty;
        OnPropertyChanged(nameof(HasClientCert));
    }

    /// <summary>
    /// Phân tích và nạp thông tin chứng chỉ Client.
    /// </summary>
    private void ParseClientCert(string pemContent, string filePath)
    {
        try
        {
            X509Certificate2 cert;
            if (File.Exists(filePath))
            {
                cert = new X509Certificate2(filePath);
            }
            else
            {
                cert = X509Certificate2.CreateFromPem(pemContent);
            }

            MqttTlsClientCertPath = filePath;
            MqttTlsClientCertPem = pemContent;
            ClientCertSubject = cert.Subject;
            ClientCertIssuer = cert.Issuer;
            ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            OnPropertyChanged(nameof(HasClientCert));
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to parse Client cert: {ex.Message}");
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp khóa bí mật Client Private Key (.key, .pem).
    /// </summary>
    [RelayCommand]
    public void BrowseClientKey()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerClientKeyLabel"],
            Filter = "Key Files (*.key;*.pem)|*.key;*.pem|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                MqttTlsClientKeyPath = dialog.FileName;
                MqttTlsClientKeyPem = File.ReadAllText(dialog.FileName);
                OnPropertyChanged(nameof(HasClientKey));
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load Client Key: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa tệp khóa bí mật Client Private Key.
    /// </summary>
    [RelayCommand]
    public void ClearClientKey()
    {
        MqttTlsClientKeyPath = string.Empty;
        MqttTlsClientKeyPem = string.Empty;
        OnPropertyChanged(nameof(HasClientKey));
    }

    partial void OnJsonTemplateChanged(string value) => UpdateJsonPreview();
    partial void OnSampleSerialInputChanged(string value) => UpdateJsonPreview();
    partial void OnPayloadModeChanged(string value) => UpdateJsonPreview();
    partial void OnIncludeDeviceIdChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeTimestampChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludePortChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeSequenceChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeHexStreamChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludePayloadLengthChanged(bool value) => UpdateJsonPreview();
    partial void OnTerminalSendFormatChanged(string value) => OnPropertyChanged(nameof(TestPacketIsHex));
    partial void OnHttpTriggerModeChanged(string value) => OnPropertyChanged(nameof(IsHttpPeriodicTrigger));

    public void UpdateJsonPreview()
    {
        if (PayloadMode == "Raw")
        {
            JsonPreview = SampleSerialInput ?? string.Empty;
            IsJsonSyntaxValid = true;
            return;
        }

        if (PayloadMode == "Standard")
        {
            string stdSample = SampleSerialInput ?? string.Empty;
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            if (IncludeDeviceId) dict["device_id"] = string.IsNullOrEmpty(MqttClientId) ? "SYNX-DTU-01" : MqttClientId;
            if (IncludeTimestamp) dict["timestamp"] = DateTime.UtcNow.ToString("o");
            if (IncludePort) dict["port"] = SelectedPortIndex + 1;
            if (IncludeSequence) dict["seq"] = PacketSequenceCounter;
            if (IncludePayloadLength) dict["len"] = stdSample.Length;
            if (IncludeHexStream) dict["hex"] = ConvertToHexString(stdSample);
            dict["payload"] = stdSample;
            dict["status"] = "OK";

            try
            {
                JsonPreview = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                IsJsonSyntaxValid = true;
            }
            catch
            {
                JsonPreview = stdSample;
                IsJsonSyntaxValid = false;
            }
            return;
        }

        string raw = JsonTemplate;
        string sample = SampleSerialInput ?? string.Empty;
        string hex = ConvertToHexString(sample);

        string resolved = raw
            .Replace("{{DEVICE_ID}}", MqttClientId)

            .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
            .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
            .Replace("{{BAUDRATE}}", BaudRate.ToString())
            .Replace("{{SERIAL_PAYLOAD}}", sample)
            .Replace("{{HEX_STREAM}}", hex)
            .Replace("{{PAYLOAD_LEN}}", sample.Length.ToString())
            .Replace("{{PACKET_SEQ}}", PacketSequenceCounter.ToString());

        try
        {
            using var doc = JsonDocument.Parse(resolved);
            JsonPreview = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            IsJsonSyntaxValid = true;
        }
        catch
        {
            JsonPreview = resolved;
            IsJsonSyntaxValid = false;
        }
    }

    private static string ConvertToHexString(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    [RelayCommand]
    private void SendTestPacket()
    {
        if (string.IsNullOrWhiteSpace(TestPacketInput))
            return;

        string packet = TestPacketInput.Trim();
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");


        if (TerminalSendFormat == "Hex")
        {
            string cleanHex = packet.Replace(" ", "").Replace("-", "");
            if (cleanHex.Length == 0 || cleanHex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(cleanHex, @"\A\b[0-9a-fA-F]+\b\Z"))
            {
                AppendLog($"[{timestamp}] " + LocalizationService.Instance["DtuTerminalInvalidHexError"]);
                return;
            }

            AppendLog($"[{timestamp}] [TX-HEX RS-485 #{SelectedPortIndex + 1}] Hex Stream: {packet} ({cleanHex.Length / 2}B)");
        }
        else
        {
            AppendLog($"[{timestamp}] [TX-ASCII RS-485 #{SelectedPortIndex + 1}] Raw bytes: \"{packet}\" ({packet.Length}B, Hex: {ConvertToHexString(packet)})");
        }

        PacketSequenceCounter++;




        // Format to JSON
        string outgoingPayload;
        if (PayloadMode == "Raw")
        {
            outgoingPayload = packet;
        }
        else if (PayloadMode == "Standard")
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            if (IncludeDeviceId) dict["device_id"] = string.IsNullOrEmpty(MqttClientId) ? "SYNX-DTU-01" : MqttClientId;
            if (IncludeTimestamp) dict["timestamp"] = DateTime.UtcNow.ToString("o");
            if (IncludePort) dict["port"] = SelectedPortIndex + 1;
            if (IncludeSequence) dict["seq"] = PacketSequenceCounter;
            if (IncludePayloadLength) dict["len"] = packet.Length;
            if (IncludeHexStream) dict["hex"] = ConvertToHexString(packet);
            dict["payload"] = packet;
            dict["status"] = "OK";
            outgoingPayload = JsonSerializer.Serialize(dict);
        }
        else
        {
            outgoingPayload = JsonTemplate
                .Replace("{{DEVICE_ID}}", MqttClientId)
                .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
                .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
                .Replace("{{BAUDRATE}}", BaudRate.ToString())
                .Replace("{{SERIAL_PAYLOAD}}", packet)
                .Replace("{{HEX_STREAM}}", ConvertToHexString(packet))
                .Replace("{{PAYLOAD_LEN}}", packet.Length.ToString())
                .Replace("{{PACKET_SEQ}}", PacketSequenceCounter.ToString());
    
        }

        string jsonPayload = outgoingPayload;




        // Simulate Uplink forwarding
        switch (UplinkMode)
        {
            case "Mqtt":
                AppendLog($"[{timestamp}] [UPLINK MQTT] Publishing {jsonPayload.Length}B to topic '{MqttPublishTopic}' (QoS {MqttQos}, TLS: {MqttUseTls})...");
                AppendLog($"[{timestamp}] [MQTT BROKER ACK] Message PUBACK received from {MqttHost}:{MqttPort}.");
                break;
            case "Http":
                AppendLog($"[{timestamp}] [UPLINK HTTP] {HttpMethod} {HttpUrl} ({jsonPayload.Length}B)...");
                AppendLog($"[{timestamp}] [HTTP RESPONSE] 200 OK (Response: {{\"status\":\"success\",\"records_inserted\":1}})");
                break;
            case "TcpClient":
                string prefixInfo = TcpRegistrationType == "PrependToEveryPacket" ? $" [RegPrefix: {TcpRegistrationPayload}]" : "";
                AppendLog($"[{timestamp}] [UPLINK TCP CLIENT] Streamed {jsonPayload.Length}B to {TcpRemoteHost}:{TcpRemotePort}{prefixInfo}...");
                AppendLog($"[{timestamp}] [TCP SERVER ECHO] ACK 0x06 (Frame processed).");
                break;
            case "TcpServer":
                AppendLog($"[{timestamp}] [UPLINK TCP SERVER] Pushed {jsonPayload.Length}B to connected client on local port {TcpLocalPort}...");
                AppendLog($"[{timestamp}] [CLIENT ACK] Received 0x06.");
                UpdateJsonPreview();
                break;
        }
    }

    [RelayCommand]
    private void ClearTerminal()
    {
        TerminalLog = $"--- SYNAPTIX DTU GATEWAY DIAGNOSTIC TERMINAL ---\n[{DateTime.Now:HH:mm:ss}] Terminal cleared.\n";
    }

    [RelayCommand]
    private void Save()
    {
        SaveToMetadata();
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Mở hộp thoại cấu hình MQTT Broker chuyên sâu (TLS/mTLS x509, diagnostics),
    /// đồng bộ dữ liệu hai chiều trước và sau khi hộp thoại hiển thị.
    /// </summary>
    [RelayCommand]
    private void OpenAdvancedMqttConfig()
    {
        SaveToMetadata();
        OpenMqttBrokerConfigRequested?.Invoke();
        ReloadFromMetadata();
    }

    [RelayCommand]
    private async Task DeployToDeviceAsync()
    {
        IsDeploying = true;
        DeployStatusMessage = LocalizationService.Tr("Deploying");
        AppendLog($"[{DateTime.Now:HH:mm:ss}] [DEPLOY] Commencing DTU hardware profile staging...");

        SaveToMetadata();

        try
        {
            var dtuDto = BuildDtuConfigDto();
            var willDto = BuildMqttWillDto();

            if (AppServices.Instance.RuleWriter != null)
            {
                var result = await AppServices.Instance.RuleWriter.DeployRulesAsync(
                    slaveId: 1,
                    rules: Array.Empty<RuleRecordDto>(),
                    dtuConfig: dtuDto,
                    mqttWillConfig: willDto,
                    cancellationToken: default,
                    progressLogger: (msg) => AppendLog($"[DEPLOY] {msg}"));

                if (result.IsSuccess)
                {
                    DeployStatusMessage = "✓ " + (LocalizationService.Instance.IsVietnamese ? "Nạp DTU thành công!" : "DTU Config Deployed!");
                    AppendLog($"[DEPLOY SUCCESS] Hardware version {result.ActiveVersion} verified. DTU Gateway operational.");
                }
                else
                {
                    DeployStatusMessage = "✗ " + result.ErrorMessage;
                    AppendLog($"[DEPLOY ERROR] Code: {result.ErrorCode}, Message: {result.ErrorMessage}");
                }
            }
            else
            {
                // Simulator fallback test
                await Task.Delay(300);
                DeployStatusMessage = "✓ " + (LocalizationService.Instance.IsVietnamese ? "Đã lưu cấu hình DTU (Offline/Mock)!" : "DTU Config Saved!");
                AppendLog("[DEPLOY] Offline configuration saved to project metadata successfully.");
            }
        }
        catch (Exception ex)
        {
            DeployStatusMessage = "✗ " + ex.Message;
            AppendLog($"[DEPLOY EXCEPTION] {ex.Message}");
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public DtuConfigDto BuildDtuConfigDto()
    {
        var dtu = new DtuConfigDto
        {
            Enabled = true,
            PortIndex = (byte)SelectedPortIndex,
            SerialMode = SerialMode switch
            {
                "ModbusMaster" => DtuSerialMode.ModbusMaster,
                "ModbusSlave" => DtuSerialMode.ModbusSlave,
                _ => DtuSerialMode.Transference
            },
            UplinkMode = UplinkMode switch
            {
                "Mqtt" => DtuUplinkMode.Mqtt,
                "Http" => DtuUplinkMode.Http,
                "TcpClient" => DtuUplinkMode.TcpClient,
                "TcpServer" => DtuUplinkMode.TcpServer,
                _ => DtuUplinkMode.None
            },
            FrameTimeoutDelayMs = FrameTimeoutDelayMs,
            MaxPacketSize = MaxPacketSize,
            PayloadMode = PayloadMode switch
            {
                "Custom" => DtuPayloadMode.Custom,
                "Raw" => DtuPayloadMode.Raw,
                _ => DtuPayloadMode.Standard
            },
            PayloadFlags = (byte)(
                (IncludeDeviceId ? 1 : 0) |
                (IncludeTimestamp ? 2 : 0) |
                (IncludePort ? 4 : 0) |
                (IncludeSequence ? 8 : 0) |
                (IncludeHexStream ? 16 : 0) |
                (IncludePayloadLength ? 32 : 0)
            ),
            HttpConfig = new DtuHttpConfigDto
            {
                Enabled = UplinkMode == "Http",
                Method = HttpMethod == "PUT" ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                Url = HttpUrl,
                CustomHeaders = HttpHeaders,
                TimeoutSec = HttpTimeoutSec,
                PeriodicIntervalSec = HttpIntervalSec,
                TriggerMode = HttpTriggerMode == "Periodic" ? DtuHttpTriggerMode.Periodic : DtuHttpTriggerMode.OnDataReceived
            },
            TcpConfig = new DtuTcpConfigDto
            {
                Enabled = UplinkMode is "TcpClient" or "TcpServer",
                SocketMode = TcpSocketMode == "Server" ? DtuTcpSocketMode.Server : DtuTcpSocketMode.Client,
                RemoteHost = TcpRemoteHost,
                RemotePort = TcpRemotePort,
                LocalPort = TcpLocalPort,
                ReconnectDelaySec = TcpReconnectDelaySec,
                HeartbeatIntervalSec = TcpHeartbeatIntervalSec,
                HeartbeatPayload = TcpHeartbeatPayload,
                RegistrationType = TcpRegistrationType switch
                {
                    "OnConnect" => DtuTcpRegistrationType.OnConnect,
                    "PrependToEveryPacket" => DtuTcpRegistrationType.PrependToEveryPacket,
                    _ => DtuTcpRegistrationType.None
                },
                RegistrationPayload = TcpRegistrationPayload
            },
            MqttWillConfig = BuildMqttWillDto()
        };

        return dtu;
    }

    public MqttWillConfigDto BuildMqttWillDto()
    {
        return new MqttWillConfigDto
        {
            Enabled = MqttWillEnabled,
            Topic = MqttWillTopic,
            Payload = MqttWillPayload,
            Qos = (byte)MqttWillQos,
            Retain = MqttWillRetain
        };
    }

    private void AppendLog(string message)
    {
        TerminalLog += message + "\n";
    }
}
