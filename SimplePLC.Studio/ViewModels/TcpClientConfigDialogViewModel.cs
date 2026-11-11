using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình chuyên sâu khối Socket TCP Client / Stream Passthrough.
/// Quản lý thông số máy chủ, tự động kết nối lại, gói tin báo danh, duy trì kết nối PING và cơ chế Apply / Cancel.
/// </summary>
public partial class TcpClientConfigDialogViewModel : ObservableObject
{
    public TcpClientNodeViewModel Node { get; }
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
    private string _payloadMode = "Raw";

    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomMode => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _customTemplate = "{val}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDelimiter))]
    [NotifyPropertyChangedFor(nameof(SummaryFramingText))]
    private string _framingDelimiter = "CRLF"; // "CRLF", "LF", "None", "Custom"

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
    private string _previewStreamText = string.Empty;

    public string SummarySocketText => $"tcp://{RemoteHost}:{RemotePort}";

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

    public void UpdateStreamPreview()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[CONNECT] -> {RemoteHost}:{RemotePort}");

        if (RegistrationType == "OnConnect" && !string.IsNullOrWhiteSpace(RegistrationPayload))
        {
            sb.AppendLine($"[TX AUTH] -> {RegistrationPayload}");
        }

        if (HeartbeatIntervalSec > 0)
        {
            string ping = string.IsNullOrWhiteSpace(HeartbeatPayload) ? "PING" : HeartbeatPayload;
            sb.AppendLine($"[TX PING] -> {ping} (Every {HeartbeatIntervalSec}s)");
        }
        else
        {
            string disabledText = Services.LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled";
            sb.AppendLine($"[TX PING] -> {disabledText}");
        }

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

        string dataContent;
        if (IsRawMode)
        {
            dataContent = $"Raw Bytes Stream: <{tagDesc}>";
        }
        else
        {
            string tmpl = string.IsNullOrWhiteSpace(CustomTemplate) ? "{val}" : CustomTemplate;
            dataContent = tmpl
                .Replace("{val}", "25.5")
                .Replace("{tag}", DataTag?.Name ?? "VREG0")
                .Replace("{device}", "SPLC-DTU-01")
                .Replace("{ts}", "1714800000");
        }

        if (RegistrationType == "PrependToEveryPacket" && !string.IsNullOrWhiteSpace(RegistrationPayload))
        {
            sb.AppendLine($"[TX DATA] -> [{RegistrationPayload}] + {dataContent}{delimiterSuffix}");
        }
        else
        {
            sb.AppendLine($"[TX DATA] -> {dataContent}{delimiterSuffix}");
        }

        PreviewStreamText = sb.ToString().TrimEnd();
    }

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
