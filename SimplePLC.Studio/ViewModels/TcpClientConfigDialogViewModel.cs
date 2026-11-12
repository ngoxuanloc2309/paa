using System;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình chuyên sâu khối Socket TCP Client / Stream Passthrough.
/// Quản lý thông số máy chủ, tự động kết nối lại, gói tin báo danh, duy trì kết nối PING và cơ chế Apply / Cancel.
/// </summary>
public partial class TcpClientConfigDialogViewModel : ObservableObject
{
    /// <summary>
    /// Node ViewModel được liên kết trong FBD Canvas.
    /// </summary>
    public TcpClientNodeViewModel Node { get; }

    /// <summary>
    /// Danh mục tag toàn cục để chọn nguồn dữ liệu.
    /// </summary>
    public TagCatalogViewModel? TagCatalog { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummarySocketText))]
    private string _remoteHost = "192.168.1.100";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummarySocketText))]
    private ushort _remotePort = 8888;

    [ObservableProperty]
    private ushort _reconnectDelaySec = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryHeartbeatText))]
    [NotifyPropertyChangedFor(nameof(IsHeartbeatEnabled))]
    private ushort _heartbeatIntervalSec = 30;

    /// <summary>
    /// Chỉ báo chu kỳ Heartbeat có đang được kích hoạt (> 0s) hay không.
    /// </summary>
    public bool IsHeartbeatEnabled => HeartbeatIntervalSec > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryHeartbeatText))]
    private string _heartbeatPayload = "PING";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegistrationEnabled))]
    [NotifyPropertyChangedFor(nameof(SummaryRegistrationText))]
    private string _registrationType = "None";

    /// <summary>
    /// Chỉ báo cơ chế định danh Gateway có đang được bật (khác None) hay không.
    /// </summary>
    public bool IsRegistrationEnabled => !string.Equals(RegistrationType, "None", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _registrationPayload = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRawMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    [NotifyPropertyChangedFor(nameof(IsJsonMode))]
    [NotifyPropertyChangedFor(nameof(HasJsonError))]
    private string _payloadMode = "Raw";

    /// <summary>
    /// Chế độ Raw Passthrough (dòng byte thô).
    /// </summary>
    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chế độ Custom Payload Template (chuỗi tùy biến / JSON).
    /// </summary>
    public bool IsCustomMode => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chỉ báo chế độ JSON / Custom Payload có đang được kích hoạt.
    /// </summary>
    public bool IsJsonMode => IsCustomMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJsonError))]
    private bool _isJsonValid = true;

    /// <summary>
    /// Cờ báo hiệu template JSON có lỗi cú pháp hay không (chỉ áp dụng ở JSON mode).
    /// </summary>
    public bool HasJsonError => IsJsonMode && !IsJsonValid;

    [ObservableProperty]
    private string _jsonValidationError = string.Empty;

    [ObservableProperty]
    private string _customTemplate = "{val}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDelimiter))]
    [NotifyPropertyChangedFor(nameof(SummaryFramingText))]
    private string _framingDelimiter = "CRLF"; // "CRLF", "LF", "None", "Custom"

    /// <summary>
    /// Chỉ báo người dùng đang sử dụng ký tự phân cách tùy biến.
    /// </summary>
    public bool IsCustomDelimiter => string.Equals(FramingDelimiter, "Custom", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryFramingText))]
    private string _customDelimiterText = ";";

    [ObservableProperty]
    private SimplePLC.Studio.Models.TagModel? _dataTag;

    /// <summary>
    /// Danh sách các tùy chọn nguồn dữ liệu thô (mục đầu tiên là Chân cắm FBD DataIn Pin - Auto-link).
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<TagOptionItem> AvailableDataTags { get; } = new();

    /// <summary>
    /// Tùy chọn nguồn dữ liệu đang được chọn trong ComboBox.
    /// </summary>
    public TagOptionItem? SelectedDataTagOption
    {
        get
        {
            if (DataTag == null) return AvailableDataTags.FirstOrDefault();
            var matched = AvailableDataTags.FirstOrDefault(o => o.Tag == DataTag || (o.Tag != null && string.Equals(o.Tag.Name, DataTag.Name, StringComparison.OrdinalIgnoreCase)));
            if (matched != null) return matched;

            var item = new TagOptionItem(DataTag, DataTag.DisplayName);
            AvailableDataTags.Add(item);
            return item;
        }
        set
        {
            DataTag = value?.Tag;
            OnPropertyChanged(nameof(SelectedDataTagOption));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PayloadByteSizeText))]
    private string _previewStreamText = string.Empty;

    /// <summary>
    /// Chuỗi kích thước byte của dòng dữ liệu stream trong khung preview.
    /// </summary>
    public string PayloadByteSizeText => $"{System.Text.Encoding.UTF8.GetByteCount(PreviewStreamText ?? string.Empty)} bytes";

    /// <summary>
    /// Tóm tắt địa chỉ máy chủ từ xa socket.
    /// </summary>
    public string SummarySocketText => $"tcp://{RemoteHost}:{RemotePort}";

    /// <summary>
    /// Tóm tắt gói tin và chu kỳ Heartbeat PING.
    /// </summary>
    public string SummaryHeartbeatText
    {
        get
        {
            if (HeartbeatIntervalSec > 0)
            {
                string ping = string.IsNullOrWhiteSpace(HeartbeatPayload) ? "PING" : HeartbeatPayload;
                return $"{ping} ({HeartbeatIntervalSec}s)";
            }
            return Services.LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled";
        }
    }

    /// <summary>
    /// Tóm tắt chế độ gói tin báo danh (Registration).
    /// </summary>
    public string SummaryRegistrationText => RegistrationType switch
    {
        "OnConnect" => (Services.LocalizationService.Instance.IsVietnamese ? "Gửi khi kết nối (OnConnect)" : "OnConnect"),
        "PrependToEveryPacket" => (Services.LocalizationService.Instance.IsVietnamese ? "Đính kèm đầu gói tin" : "Prepend to packet"),
        _ => (Services.LocalizationService.Instance.IsVietnamese ? "Không dùng (None)" : "None")
    };

    /// <summary>
    /// Tóm tắt ký tự kết thúc dòng / phân khung (Framing delimiter).
    /// </summary>
    public string SummaryFramingText
    {
        get
        {
            if (FramingDelimiter == "CRLF") return "CRLF (\\r\\n)";
            if (FramingDelimiter == "LF") return "LF (\\n)";
            if (FramingDelimiter == "None") return Services.LocalizationService.Instance.IsVietnamese ? "Không ngắt dòng (None)" : "None";
            return $"Custom ({CustomDelimiterText})";
        }
    }

    /// <summary>
    /// Khởi tạo ViewModel hộp thoại cấu hình TCP Client với dữ liệu từ Node và TagCatalog.
    /// </summary>
    public TcpClientConfigDialogViewModel(TcpClientNodeViewModel node, TagCatalogViewModel? tagCatalog = null)
    {
        Node = node;
        TagCatalog = tagCatalog;

        // Sao chép thông số hiện tại từ Node sang buffer của Dialog
        RemoteHost = node.RemoteHost;
        RemotePort = node.RemotePort;
        ReconnectDelaySec = node.ReconnectDelaySec;
        HeartbeatIntervalSec = node.HeartbeatIntervalSec;
        HeartbeatPayload = string.IsNullOrWhiteSpace(node.HeartbeatPayload) ? "PING" : node.HeartbeatPayload;
        RegistrationType = string.IsNullOrEmpty(node.RegistrationType) ? "None" : node.RegistrationType;
        RegistrationPayload = node.RegistrationPayload;
        PayloadMode = string.IsNullOrEmpty(node.PayloadMode) ? "Raw" : node.PayloadMode;
        CustomTemplate = string.IsNullOrEmpty(node.CustomTemplate) ? "{val}" : node.CustomTemplate;
        FramingDelimiter = string.IsNullOrEmpty(node.FramingDelimiter) ? "CRLF" : node.FramingDelimiter;
        CustomDelimiterText = string.IsNullOrEmpty(node.CustomDelimiterText) ? ";" : node.CustomDelimiterText;
        DataTag = node.DataTag;

        // Khởi tạo danh sách nguồn dữ liệu thô (mục đầu tiên là Chân cắm FBD DataIn Pin)
        RebuildAvailableDataTags();
        Services.LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        UpdateStreamPreview();
    }

    private void RebuildAvailableDataTags()
    {
        AvailableDataTags.Clear();
        AvailableDataTags.Add(new TagOptionItem(null, Services.LocalizationService.Tr("SourceUseFbdPinTcp")));
        if (TagCatalog?.AllTags != null)
        {
            foreach (var tag in TagCatalog.AllTags.Where(t => t.Kind != SimplePLC.Domain.Enums.TagKind.None && !string.IsNullOrWhiteSpace(t.Name)))
            {
                AvailableDataTags.Add(new TagOptionItem(tag, tag.DisplayName));
            }
        }
        OnPropertyChanged(nameof(SelectedDataTagOption));
    }

    private void OnLanguageChanged()
    {
        RebuildAvailableDataTags();
        UpdateStreamPreview();
        OnPropertyChanged(nameof(SummaryHeartbeatText));
        OnPropertyChanged(nameof(SummaryFramingText));
        OnPropertyChanged(nameof(SummaryRegistrationText));
    }

    partial void OnRemoteHostChanged(string value) => UpdateStreamPreview();
    partial void OnRemotePortChanged(ushort value) => UpdateStreamPreview();
    partial void OnHeartbeatIntervalSecChanged(ushort value) => UpdateStreamPreview();
    partial void OnHeartbeatPayloadChanged(string value) => UpdateStreamPreview();
    partial void OnRegistrationTypeChanged(string value) => UpdateStreamPreview();
    partial void OnRegistrationPayloadChanged(string value) => UpdateStreamPreview();
    partial void OnPayloadModeChanged(string value) => UpdateStreamPreview();
    partial void OnCustomTemplateChanged(string value) => UpdateStreamPreview();
    partial void OnFramingDelimiterChanged(string value) => UpdateStreamPreview();
    partial void OnCustomDelimiterTextChanged(string value) => UpdateStreamPreview();
    partial void OnDataTagChanged(SimplePLC.Studio.Models.TagModel? value)
    {
        OnPropertyChanged(nameof(SelectedDataTagOption));
        UpdateStreamPreview();
    }

    /// <summary>
    /// Cập nhật hiển thị dòng byte truyền nhận socket mô phỏng dựa trên cấu hình hiện tại.
    /// </summary>
    public void UpdateStreamPreview()
    {
        string tagDesc = DataTag != null 
            ? $"{DataTag.DisplayName} ({DataTag.DataTypeText})" 
            : (Services.LocalizationService.Instance.IsVietnamese ? "Chân cắm DataIn (FBD Auto-link)" : "DataIn Pin (FBD Auto-link)");

        string delimiterSuffix = FramingDelimiter switch
        {
            "CRLF" => "\\r\\n",
            "LF" => "\\n",
            "Custom" => CustomDelimiterText ?? string.Empty,
            _ => string.Empty // "None"
        };

        if (IsCustomMode)
        {
            string tmpl = string.IsNullOrWhiteSpace(CustomTemplate) ? "{val}" : CustomTemplate;
            string rendered = tmpl
                .Replace("{val}", "25.5")
                .Replace("{tag}", DataTag?.Name ?? "VREG0")
                .Replace("{device}", "SPLC-DTU-01")
                .Replace("{ts}", "1714800000");

            try
            {
                using var doc = JsonDocument.Parse(rendered);
                IsJsonValid = true;
                JsonValidationError = string.Empty;
                string formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                PreviewStreamText = string.IsNullOrEmpty(delimiterSuffix) ? formatted : $"{formatted} {delimiterSuffix}";
            }
            catch (Exception ex)
            {
                bool looksLikeJson = rendered.TrimStart().StartsWith("{") || rendered.TrimStart().StartsWith("[");
                IsJsonValid = !looksLikeJson;
                JsonValidationError = looksLikeJson ? ex.Message : string.Empty;
                PreviewStreamText = string.IsNullOrEmpty(delimiterSuffix) ? rendered : $"{rendered}{delimiterSuffix}";
            }
        }
        else
        {
            IsJsonValid = true;
            JsonValidationError = string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== TCP SOCKET STREAM PROTOCOL ===");
            sb.AppendLine($"• Target Host   : tcp://{RemoteHost}:{RemotePort}");
            sb.AppendLine($"• Framing (EOL) : {SummaryFramingText}");
            sb.AppendLine($"• Registration  : {SummaryRegistrationText}");
            sb.AppendLine($"• Keepalive     : {SummaryHeartbeatText}");
            sb.AppendLine();
            sb.AppendLine("=== REAL-TIME PACKET STREAM ===");
            sb.AppendLine($"[CONNECT] -> {RemoteHost}:{RemotePort}");
            if (RegistrationType == "OnConnect" && !string.IsNullOrWhiteSpace(RegistrationPayload))
            {
                sb.AppendLine($"[TX AUTH] -> {RegistrationPayload}");
            }
            if (RegistrationType == "PrependToEveryPacket" && !string.IsNullOrWhiteSpace(RegistrationPayload))
            {
                sb.AppendLine($"[TX DATA] -> [{RegistrationPayload}] + Raw Bytes Stream: <{tagDesc}>{delimiterSuffix}");
            }
            else
            {
                sb.AppendLine($"[TX DATA] -> Raw Bytes Stream: <{tagDesc}>{delimiterSuffix}");
            }
            if (HeartbeatIntervalSec > 0)
            {
                string ping = string.IsNullOrWhiteSpace(HeartbeatPayload) ? "PING" : HeartbeatPayload;
                sb.AppendLine($"[TX PING] -> {ping} (Every {HeartbeatIntervalSec}s)");
            }
            else
            {
                sb.AppendLine("[TX PING] -> Disabled");
            }
            PreviewStreamText = sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Đặt lại các thông số cấu hình về giá trị mặc định chuẩn công nghiệp.
    /// </summary>
    [RelayCommand]
    public void ResetDefaults()
    {
        RemoteHost = "192.168.1.100";
        RemotePort = 8888;
        ReconnectDelaySec = 5;
        HeartbeatIntervalSec = 30;
        HeartbeatPayload = "PING";
        RegistrationType = "None";
        RegistrationPayload = string.Empty;
        PayloadMode = "Raw";
        CustomTemplate = "{val}";
        FramingDelimiter = "CRLF";
        CustomDelimiterText = ";";
        DataTag = null;
        UpdateStreamPreview();
    }

    /// <summary>
    /// Áp dụng các thay đổi từ buffer vào Node khi người dùng nhấn Áp dụng (Apply).
    /// </summary>
    public void Apply()
    {
        Node.RemoteHost = RemoteHost;
        Node.RemotePort = RemotePort;
        Node.ReconnectDelaySec = ReconnectDelaySec;
        Node.HeartbeatIntervalSec = HeartbeatIntervalSec;
        Node.HeartbeatPayload = HeartbeatPayload;
        Node.RegistrationType = RegistrationType;
        Node.RegistrationPayload = RegistrationPayload;
        Node.PayloadMode = PayloadMode;
        Node.CustomTemplate = CustomTemplate;
        Node.FramingDelimiter = FramingDelimiter;
        Node.CustomDelimiterText = CustomDelimiterText;
        Node.DataTag = DataTag;
    }
}
