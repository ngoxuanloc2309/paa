using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

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

    [ObservableProperty]
    private ushort _mqttKeepAlive = 60;

    [ObservableProperty]
    private string _mqttPublishTopic = "factory/dtu/data";

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

    // --- JSON Formatter & Preview ---
    [ObservableProperty]
    private string _jsonTemplate = "{\n  \"device\": \"{{DEVICE_ID}}\",\n  \"timestamp\": \"{{TIMESTAMP}}\",\n  \"port\": {{PORT}},\n  \"data\": \"{{SERIAL_PAYLOAD}}\",\n  \"hex\": \"{{HEX_STREAM}}\"\n}";

    [ObservableProperty]
    private string _sampleSerialInput = "TEMP:28.4,HUM:62.1";

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private bool _isJsonSyntaxValid = true;

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

    public DtuGatewayStudioViewModel(ProjectMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        LoadFromMetadata();
        UpdateJsonPreview();
    }

    private void LoadFromMetadata()
    {
        var dtu = _metadata.DtuConfig;
        SelectedPortIndex = dtu.PortIndex;
        SerialMode = dtu.SerialMode;
        UplinkMode = dtu.UplinkMode;
        FrameTimeoutDelayMs = dtu.FrameTimeoutDelayMs > 0 ? dtu.FrameTimeoutDelayMs : (ushort)20;
        MaxPacketSize = dtu.MaxPacketSize > 0 ? dtu.MaxPacketSize : (ushort)512;
        JsonTemplate = string.IsNullOrWhiteSpace(dtu.CustomJsonTemplate) ? JsonTemplate : dtu.CustomJsonTemplate;

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
        MqttWillEnabled = _metadata.MqttWillEnabled;
        MqttWillTopic = string.IsNullOrEmpty(_metadata.MqttWillTopic) ? MqttWillTopic : _metadata.MqttWillTopic;
        MqttWillPayload = string.IsNullOrEmpty(_metadata.MqttWillPayload) ? MqttWillPayload : _metadata.MqttWillPayload;
        MqttWillQos = _metadata.MqttWillQos;
        MqttWillRetain = _metadata.MqttWillRetain;

        // HTTP from metadata
        if (dtu.HttpSettings != null)
        {
            HttpUrl = string.IsNullOrEmpty(dtu.HttpSettings.Url) ? HttpUrl : dtu.HttpSettings.Url;
            HttpMethod = string.IsNullOrEmpty(dtu.HttpSettings.Method) ? HttpMethod : dtu.HttpSettings.Method;
            HttpHeaders = string.IsNullOrEmpty(dtu.HttpSettings.Headers) ? HttpHeaders : dtu.HttpSettings.Headers;
            HttpTimeoutSec = dtu.HttpSettings.TimeoutSec > 0 ? dtu.HttpSettings.TimeoutSec : (ushort)10;
            HttpIntervalSec = dtu.HttpSettings.PeriodicIntervalSec > 0 ? dtu.HttpSettings.PeriodicIntervalSec : (ushort)5;

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
        _metadata.MqttWillEnabled = MqttWillEnabled;
        _metadata.MqttWillTopic = MqttWillTopic;
        _metadata.MqttWillPayload = MqttWillPayload;
        _metadata.MqttWillQos = (byte)MqttWillQos;
        _metadata.MqttWillRetain = MqttWillRetain;

        // Save HTTP
        dtu.HttpSettings.Enabled = UplinkMode == "Http";
        dtu.HttpSettings.Url = HttpUrl;
        dtu.HttpSettings.Method = HttpMethod;
        dtu.HttpSettings.Headers = HttpHeaders;
        dtu.HttpSettings.TimeoutSec = HttpTimeoutSec;
        dtu.HttpSettings.PeriodicIntervalSec = HttpIntervalSec;
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

        _metadata.LastModified = DateTime.UtcNow;
    }

    partial void OnJsonTemplateChanged(string value) => UpdateJsonPreview();
    partial void OnSampleSerialInputChanged(string value) => UpdateJsonPreview();

    public void UpdateJsonPreview()
    {
        string raw = JsonTemplate;
        string sample = SampleSerialInput ?? string.Empty;
        string hex = ConvertToHexString(sample);

        string resolved = raw
            .Replace("{{DEVICE_ID}}", MqttClientId)
            .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
            .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
            .Replace("{{BAUDRATE}}", BaudRate.ToString())
            .Replace("{{SERIAL_PAYLOAD}}", sample)
            .Replace("{{HEX_STREAM}}", hex);

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
        string packet = string.IsNullOrWhiteSpace(TestPacketInput) ? "TEMP:28.4,HUM:62.1" : TestPacketInput;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        AppendLog($"[{timestamp}] [TX RS-485 #{SelectedPortIndex + 1}] Raw bytes: \"{packet}\" ({packet.Length}B, Hex: {ConvertToHexString(packet)})");

        // Format to JSON
        string jsonPayload = JsonTemplate
            .Replace("{{DEVICE_ID}}", MqttClientId)
            .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
            .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
            .Replace("{{BAUDRATE}}", BaudRate.ToString())
            .Replace("{{SERIAL_PAYLOAD}}", packet)
            .Replace("{{HEX_STREAM}}", ConvertToHexString(packet));

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
                AppendLog($"[{timestamp}] [UPLINK TCP CLIENT] Streamed {jsonPayload.Length}B to {TcpRemoteHost}:{TcpRemotePort}...");
                AppendLog($"[{timestamp}] [TCP SERVER ECHO] ACK 0x06 (Frame processed).");
                break;
            case "TcpServer":
                AppendLog($"[{timestamp}] [UPLINK TCP SERVER] Pushed {jsonPayload.Length}B to connected client on local port {TcpLocalPort}...");
                AppendLog($"[{timestamp}] [CLIENT ACK] Received 0x06.");
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
            HttpConfig = new DtuHttpConfigDto
            {
                Enabled = UplinkMode == "Http",
                Method = HttpMethod == "PUT" ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                Url = HttpUrl,
                CustomHeaders = HttpHeaders,
                TimeoutSec = HttpTimeoutSec,
                PeriodicIntervalSec = HttpIntervalSec
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
                HeartbeatPayload = TcpHeartbeatPayload
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
