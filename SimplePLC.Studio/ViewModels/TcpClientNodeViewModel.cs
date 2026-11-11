using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa truyền thông mạng TCP Client (TCP_CLIENT).
/// Cho phép mở socket TCP và đẩy dữ liệu (Raw bytes hoặc Custom String) trực tiếp lên Server từ xa.
/// </summary>
public partial class TcpClientNodeViewModel : GraphNodeViewModel
{

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(HostValidationError))]
    [NotifyPropertyChangedFor(nameof(HasHostError))]
    private string _remoteHost = "192.168.1.100";

    partial void OnRemoteHostChanged(string value)
    {
        if (value != null && value.Length > 48)
        {
            RemoteHost = value[..48];
        }
    }

    public bool HasHostError => !string.IsNullOrEmpty(HostValidationError);

    public string HostValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RemoteHost))
                return LocalizationService.Instance.IsVietnamese ? "Địa chỉ máy chủ (Host) không được để trống." : "Remote host cannot be empty.";
            if (RemoteHost.Length > 48)
                return LocalizationService.Instance.IsVietnamese ? "Địa chỉ máy chủ không vượt quá 48 ký tự." : "Host length cannot exceed 48 characters.";
            return string.Empty;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    private ushort _remotePort = 8888;

    partial void OnRemotePortChanged(ushort value)
    {
        if (value == 0)
        {
            RemotePort = 1;
        }
    }

    [ObservableProperty]
    private ushort _reconnectDelaySec = 5;

    [ObservableProperty]
    private ushort _heartbeatIntervalSec = 30;

    [ObservableProperty]
    private string _heartbeatPayload = "PING";

    [ObservableProperty]
    private string _registrationType = "None"; // "None", "OnConnect", "PrependToEveryPacket"

    [ObservableProperty]
    private string _registrationPayload = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRawMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    private string _payloadMode = "Raw"; // "Raw", "Custom"

    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomMode => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Bản mẫu định dạng chuỗi tùy biến khi PayloadMode = Custom.
    /// </summary>
    [ObservableProperty]
    private string _customTemplate = "{val}";

    /// <summary>
    /// Kiểu ký tự kết thúc khung truyền (Framing Delimiter): "CRLF", "LF", "None", "Custom".
    /// </summary>
    [ObservableProperty]
    private string _framingDelimiter = "CRLF";

    /// <summary>
    /// Ký tự ngắt khung tùy biến khi FramingDelimiter = Custom.
    /// </summary>
    [ObservableProperty]
    private string _customDelimiterText = ";";

    [ObservableProperty]
    private TagModel? _dataTag;

    // Runtime Live Status
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private bool _isConnectedActive;

    [ObservableProperty]
    private bool _isDoneActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private bool _isErrorActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private uint _sendCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private uint _receiveCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private string _lastErrorMessage = string.Empty;

    // Connectors
    public ConnectorViewModel SendConnector { get; }
    public ConnectorViewModel DataInConnector { get; }
    public ConnectorViewModel ConnectedConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }
    public ConnectorViewModel DataOutConnector { get; }

    public string StatusPillText
    {
        get
        {
            if (IsErrorActive || !string.IsNullOrWhiteSpace(LastErrorMessage))
                return LocalizationService.Instance.IsVietnamese ? $"LỖI: {LastErrorMessage}" : $"ERR: {LastErrorMessage}";
            if (IsConnectedActive)
                return SendCount > 0
                    ? (LocalizationService.Instance.IsVietnamese ? $"KẾT NỐI (TX:{SendCount})" : $"CONN (TX:{SendCount})")
                    : (LocalizationService.Instance.IsVietnamese ? "ĐÃ KẾT NỐI" : "CONNECTED");
            return LocalizationService.Instance.IsVietnamese ? "MẤT KẾT NỐI" : "DISCONNECTED";
        }
    }

    public string NodeInfoSummary => $"{RemoteHost}:{RemotePort} ({PayloadMode})";

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"TCP_CLIENT [{RemoteHost}:{RemotePort}]"
        : CustomLabel;

    public TcpClientNodeViewModel(string remoteHost = "192.168.1.100", ushort remotePort = 8888)
    {
        Title = "TCP_CLIENT";
        Icon = "🔌";
        RemoteHost = remoteHost;
        RemotePort = remotePort;

        SendConnector = new ConnectorViewModel
        {
            Title = "Send",
            TooltipKey = "PinTooltipTcpClientSend",
            IsInput = true,
            IsEvent = true,
            Node = this
        };
        SendConnector.SetBilingualDescription("Xung kích hoạt đẩy dữ liệu lên TCP Server", "Rising edge triggers TCP data dispatch");

        DataInConnector = new ConnectorViewModel
        {
            Title = "DataIn",
            TooltipKey = "PinTooltipTcpClientDataIn",
            IsInput = true,
            IsEvent = false,
            Node = this
        };
        DataInConnector.SetBilingualDescription("Dữ liệu thô (Bytes) hoặc chuỗi gửi đi", "Raw bytes or string payload to send");

        ConnectedConnector = new ConnectorViewModel
        {
            Title = "Conn",
            TooltipKey = "PinTooltipTcpClientConn",
            IsInput = false,
            Node = this
        };
        ConnectedConnector.SetBilingualDescription("Trạng thái socket TCP đang mở và duy trì kết nối", "High (1) when TCP connection is active and established");

        DoneConnector = new ConnectorViewModel
        {
            Title = "Done",
            TooltipKey = "PinTooltipTcpClientDone",
            IsInput = false,
            IsEvent = true,
            Node = this
        };
        DoneConnector.SetBilingualDescription("Xung báo hoàn thành truyền gói tin TCP", "Pulse upon successful TCP packet dispatch");

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipTcpClientErr",
            IsInput = false,
            IsEvent = true,
            Node = this
        };
        ErrorConnector.SetBilingualDescription("Cờ báo lỗi kết nối hoặc mất liên lạc socket", "Active when TCP socket error or connection drops");

        DataOutConnector = new ConnectorViewModel
        {
            Title = "DataOut",
            TooltipKey = "PinTooltipTcpClientDataOut",
            IsInput = false,
            Node = this
        };
        DataOutConnector.SetBilingualDescription("Dữ liệu nhận về từ TCP Server", "Data packet received from TCP Server");

        InputConnectors.Add(SendConnector);
        InputConnectors.Add(DataInConnector);
        OutputConnectors.Add(ConnectedConnector);
        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(ErrorConnector);
        OutputConnectors.Add(DataOutConnector);
    }
}
