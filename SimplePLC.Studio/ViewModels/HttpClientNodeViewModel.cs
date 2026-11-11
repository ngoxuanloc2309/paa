using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa REST API HTTP Client (HTTP_CLIENT).
/// Cho phép đẩy dữ liệu cảm biến, trạng thái lên Webhook / Cloud Server qua HTTP POST / PUT.
/// </summary>
public partial class HttpClientNodeViewModel : GraphNodeViewModel
{

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(UrlValidationError))]
    [NotifyPropertyChangedFor(nameof(HasUrlError))]
    private string _url = "http://api.myserver.com/telemetry";

    partial void OnUrlChanged(string value)
    {
        if (value != null && value.Length > 64)
        {
            Url = value[..64];
        }
    }

    public bool HasUrlError => !string.IsNullOrEmpty(UrlValidationError);

    public string UrlValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url))
                return LocalizationService.Instance.IsVietnamese ? "URL không được để trống." : "URL cannot be empty.";
            if (Url.Length > 64)
                return LocalizationService.Instance.IsVietnamese ? "URL không được vượt quá 64 ký tự (giới hạn firmware MCU)." : "URL exceeds 64 characters (firmware limit).";
            if (!Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return LocalizationService.Instance.IsVietnamese ? "URL phải bắt đầu bằng http:// hoặc https://" : "URL must start with http:// or https://";
            return string.Empty;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(MethodBadge))]
    private string _method = "POST"; // "POST", "PUT"

    public string MethodBadge => Method?.ToUpperInvariant() ?? "POST";

    [ObservableProperty]
    private string _customHeaders = "Content-Type: application/json";

    partial void OnCustomHeadersChanged(string value)
    {
        if (value != null && value.Length > 56)
        {
            CustomHeaders = value[..56];
        }
    }

    [ObservableProperty]
    private ushort _timeoutSec = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPeriodicMode))]
    [NotifyPropertyChangedFor(nameof(IsEventMode))]
    [NotifyPropertyChangedFor(nameof(TriggerModeName))]
    private string _triggerMode = "Event"; // "Event" (hoặc "OnDataReceived"), "Cyclic" (hoặc "Periodic")

    public bool IsPeriodicMode => string.Equals(TriggerMode, "Cyclic", StringComparison.OrdinalIgnoreCase) || string.Equals(TriggerMode, "Periodic", StringComparison.OrdinalIgnoreCase);
    public bool IsEventMode => !IsPeriodicMode;

    public string TriggerModeName => IsPeriodicMode
        ? (LocalizationService.Instance.IsVietnamese ? $"Định kỳ ({PeriodicIntervalSec}s)" : $"Cyclic ({PeriodicIntervalSec}s)")
        : (LocalizationService.Instance.IsVietnamese ? "Theo xung / Dữ liệu" : "On Trigger / Data");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerModeName))]
    private ushort _periodicIntervalSec = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldsMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomTemplateVisible))]
    [NotifyPropertyChangedFor(nameof(IsRawMode))]
    private string _payloadMode = "JSON"; // "JSON" (Fields Builder), "Custom", "Raw"

    public bool IsFieldsMode => string.Equals(PayloadMode, "JSON", StringComparison.OrdinalIgnoreCase) || string.Equals(PayloadMode, "Fields", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomTemplateVisible => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);
    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _customTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"val\": {val}\n}";

    /// <summary>
    /// Danh sách các trường dữ liệu được gán ghép trong bảng Visual Field Builder.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<HttpPayloadFieldViewModel> Fields { get; } = new();

    /// <summary>
    /// Tag nguồn dữ liệu đơn lẻ (cho chế độ Raw).
    /// </summary>
    [ObservableProperty]
    private TagModel? _dataTag;

    // Runtime Live Status
    [ObservableProperty]
    private bool _isBusyActive;

    [ObservableProperty]
    private bool _isDoneActive;

    [ObservableProperty]
    private bool _isErrorActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private int _lastStatusCode = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private uint _requestCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private string _lastErrorMessage = string.Empty;

    // Connectors
    public ConnectorViewModel TrigConnector { get; }
    public ConnectorViewModel DataConnector { get; }
    public ConnectorViewModel BusyConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }
    public ConnectorViewModel StatusCodeConnector { get; }

    public string StatusPillText
    {
        get
        {
            if (IsErrorActive || !string.IsNullOrWhiteSpace(LastErrorMessage))
                return LocalizationService.Instance.IsVietnamese ? $"LỖI: {LastErrorMessage}" : $"ERR: {LastErrorMessage}";
            if (IsBusyActive)
                return LocalizationService.Instance.IsVietnamese ? "ĐANG GỬI..." : "TX...";
            if (LastStatusCode > 0)
                return $"HTTP {LastStatusCode} (#{RequestCount})";
            if (RequestCount > 0)
                return LocalizationService.Instance.IsVietnamese ? $"HOÀN TẤT #{RequestCount}" : $"DONE #{RequestCount}";
            return LocalizationService.Instance.IsVietnamese ? "SẴN SÀNG" : "READY";
        }
    }

    public string NodeInfoSummary => $"{MethodBadge} {Url}";

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"HTTP_CLIENT [{MethodBadge}] {Url}"
        : CustomLabel;

    public HttpClientNodeViewModel(string url = "http://api.myserver.com/telemetry", string method = "POST")
    {
        Title = "HTTP_CLIENT";
        Icon = "🌐";
        Url = url;
        Method = method;

        TrigConnector = new ConnectorViewModel
        {
            Title = "Trig",
            TooltipKey = "PinTooltipHttpClientTrig",
            IsInput = true,
            IsEvent = true,
            Node = this
        };
        TrigConnector.SetBilingualDescription("Xung kích hoạt gửi HTTP Request", "Rising edge triggers HTTP Request transmission");

        DataConnector = new ConnectorViewModel
        {
            Title = "Data",
            TooltipKey = "PinTooltipHttpClientData",
            IsInput = true,
            IsEvent = false,
            Node = this
        };
        DataConnector.SetBilingualDescription("Dữ liệu payload đầu vào (cho chế độ Single-Wire)", "Input payload data (for Single-Wire mode)");

        BusyConnector = new ConnectorViewModel
        {
            Title = "Busy",
            TooltipKey = "PinTooltipHttpClientBusy",
            IsInput = false,
            Node = this
        };
        BusyConnector.SetBilingualDescription("Trạng thái đang chờ server phản hồi", "Active while waiting for server response");

        DoneConnector = new ConnectorViewModel
        {
            Title = "Done",
            TooltipKey = "PinTooltipHttpClientDone",
            IsInput = false,
            IsEvent = true,
            Node = this
        };
        DoneConnector.SetBilingualDescription("Xung báo gửi request thành công (2xx)", "Pulse on successful HTTP response (2xx)");

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipHttpClientErr",
            IsInput = false,
            IsEvent = true,
            Node = this
        };
        ErrorConnector.SetBilingualDescription("Cờ báo lỗi kết nối hoặc HTTP Status Code lỗi", "Set when network error or HTTP 4xx/5xx occurs");

        StatusCodeConnector = new ConnectorViewModel
        {
            Title = "Status",
            TooltipKey = "PinTooltipHttpClientStatus",
            IsInput = false,
            Node = this
        };
        StatusCodeConnector.SetBilingualDescription("Mã phản hồi HTTP Status Code (200, 400, 500,...)", "HTTP response status code (200, 400, 500, etc.)");

        InputConnectors.Add(TrigConnector);
        InputConnectors.Add(DataConnector);
        OutputConnectors.Add(BusyConnector);
        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(ErrorConnector);
        OutputConnectors.Add(StatusCodeConnector);
    }
}
