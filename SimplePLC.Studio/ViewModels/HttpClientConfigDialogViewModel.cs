using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình chuyên sâu khối REST API HTTP Client / Webhook.
/// Quản lý tham số kết nối, thời gian chờ, bộ đệm Snapshot, bảng ghép trường Visual Field Builder,
/// chế độ kích hoạt Event/Cyclic, và kiểm tra cú pháp JSON thời gian thực.
/// </summary>
public partial class HttpClientConfigDialogViewModel : ObservableObject
{
    /// <summary>
    /// Tham chiếu tới Node đồ họa gốc trên canvas FBD.
    /// </summary>
    public HttpClientNodeViewModel Node { get; }

    /// <summary>
    /// Danh mục toàn bộ Tag trong dự án PLC phục vụ chọn nguồn dữ liệu.
    /// </summary>
    public TagCatalogViewModel? TagCatalog { get; }

    /// <summary>
    /// Phương thức HTTP (POST / PUT).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryEndpointText))]
    private string _method = "POST";

    /// <summary>
    /// Địa chỉ URL Endpoint của REST API Webhook.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryEndpointText))]
    private string _url = "http://api.myserver.com/telemetry";

    /// <summary>
    /// Tiêu đề HTTP tùy chỉnh (ví dụ Content-Type, Authorization).
    /// </summary>
    [ObservableProperty]
    private string _customHeaders = "Content-Type: application/json";

    /// <summary>
    /// Thời gian chờ phản hồi tối đa (giây).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryTimeoutText))]
    private ushort _timeoutSec = 10;

    /// <summary>
    /// Chế độ kích hoạt gửi bản tin: Event (theo chân Trig FBD) hoặc Cyclic (định kỳ theo chu kỳ timer).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPeriodicMode))]
    [NotifyPropertyChangedFor(nameof(IsEventMode))]
    [NotifyPropertyChangedFor(nameof(SummaryTriggerText))]
    private string _triggerMode = "Event";

    /// <summary>
    /// Cờ chỉ báo chế độ phát định kỳ theo chu kỳ thời gian.
    /// </summary>
    public bool IsPeriodicMode => string.Equals(TriggerMode, "Cyclic", StringComparison.OrdinalIgnoreCase) || string.Equals(TriggerMode, "Periodic", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cờ chỉ báo chế độ phát theo sự kiện (xung kích hoạt chân Trig).
    /// </summary>
    public bool IsEventMode => !IsPeriodicMode;

    /// <summary>
    /// Chu kỳ gửi định kỳ (giây) khi ở chế độ Cyclic.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryTriggerText))]
    private ushort _periodicIntervalSec = 30;

    /// <summary>
    /// Định dạng tải trọng: JSON (Field Builder), Custom (Mẫu tự do), Raw (Dữ liệu thô).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldsMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    [NotifyPropertyChangedFor(nameof(IsRawMode))]
    [NotifyPropertyChangedFor(nameof(IsJsonMode))]
    [NotifyPropertyChangedFor(nameof(SummaryContentType))]
    private string _payloadMode = "JSON";

    /// <summary>
    /// Cờ chỉ báo chế độ sử dụng bảng ghép trường Visual Field Builder.
    /// </summary>
    public bool IsFieldsMode => string.Equals(PayloadMode, "JSON", StringComparison.OrdinalIgnoreCase) || string.Equals(PayloadMode, "Fields", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cờ chỉ báo chế độ tùy biến bản mẫu tự do.
    /// </summary>
    public bool IsCustomMode => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cờ chỉ báo chế độ dữ liệu thô từ chân Data / Tag.
    /// </summary>
    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cờ chỉ báo đang ở một trong các chế độ JSON (Fields hoặc Custom Template).
    /// </summary>
    public bool IsJsonMode => IsFieldsMode || IsCustomMode;

    /// <summary>
    /// Mẫu JSON tự do khi ở chế độ Custom.
    /// </summary>
    [ObservableProperty]
    private string _customTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"val\": {val}\n}";

    /// <summary>
    /// Tag nguồn dữ liệu đơn lẻ (dành cho chế độ Raw Single).
    /// </summary>
    [ObservableProperty]
    private TagModel? _dataTag;

    /// <summary>
    /// Danh sách các tùy chọn nguồn dữ liệu thô (mục đầu tiên là Chân cắm FBD DATA Pin - Auto-link).
    /// </summary>
    public ObservableCollection<DtuDataTagOptionItem> AvailableDataTags { get; } = new();

    /// <summary>
    /// Tùy chọn nguồn dữ liệu đang được chọn trong ComboBox.
    /// </summary>
    public DtuDataTagOptionItem? SelectedDataTagOption
    {
        get
        {
            if (DataTag == null) return AvailableDataTags.FirstOrDefault();
            var matched = AvailableDataTags.FirstOrDefault(o => o.Tag == DataTag || (o.Tag != null && string.Equals(o.Tag.Name, DataTag.Name, StringComparison.OrdinalIgnoreCase)));
            if (matched != null) return matched;

            var item = new DtuDataTagOptionItem(DataTag, DataTag.DisplayName);
            AvailableDataTags.Add(item);
            return item;
        }
        set
        {
            DataTag = value?.Tag;
            OnPropertyChanged(nameof(SelectedDataTagOption));
        }
    }

    /// <summary>
    /// Danh sách các trường dữ liệu được gán ghép trong bảng Visual Field Builder (buffer độc lập).
    /// </summary>
    public ObservableCollection<HttpPayloadFieldViewModel> Fields { get; } = new();

    /// <summary>
    /// Chuỗi văn bản xem trước tải trọng sau khi biên dịch và thay thế placeholder.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PayloadByteSizeText))]
    private string _previewPayloadText = string.Empty;

    /// <summary>
    /// Chuỗi tóm tắt phương thức và địa chỉ URL máy chủ tiếp nhận.
    /// </summary>
    public string SummaryEndpointText => $"{Method?.ToUpperInvariant()} {Url}";

    /// <summary>
    /// Chuỗi tóm tắt cơ chế kích hoạt gửi bản tin.
    /// </summary>
    public string SummaryTriggerText => IsPeriodicMode
        ? (Services.LocalizationService.Instance.IsVietnamese ? $"Định kỳ ({PeriodicIntervalSec}s)" : $"Periodic ({PeriodicIntervalSec}s)")
        : (Services.LocalizationService.Instance.IsVietnamese ? "Theo xung chân Trig (FBD)" : "On Event (Trig Pin)");

    /// <summary>
    /// Chuỗi ước tính kích thước byte của gói tin tải trọng.
    /// </summary>
    public string PayloadByteSizeText
    {
        get
        {
            int bytes = System.Text.Encoding.UTF8.GetByteCount(PreviewPayloadText ?? string.Empty);
            return $"{bytes} bytes";
        }
    }

    /// <summary>
    /// Tóm tắt nguồn dữ liệu tải trọng thô (Chân cắm FBD DATA Pin hoặc tên Tag).
    /// </summary>
    public string SummarySourceDataText => DataTag != null
        ? $"{DataTag.DisplayName} ({DataTag.DataTypeText})"
        : (Services.LocalizationService.Instance.IsVietnamese ? "Chân DATA (FBD Auto-link)" : "DATA Pin (FBD Auto-link)");

    /// <summary>
    /// Kiểu nội dung HTTP Content-Type tương ứng với định dạng tải trọng hiện tại.
    /// </summary>
    public string SummaryContentType => IsRawMode ? "text/plain" : "application/json";

    /// <summary>
    /// Thời gian chờ phản hồi từ máy chủ HTTP.
    /// </summary>
    public string SummaryTimeoutText => $"{TimeoutSec}s";

    /// <summary>
    /// Cờ chỉ báo tải trọng JSON có hợp lệ cú pháp hay không.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJsonError))]
    private bool _isJsonValid = true;

    /// <summary>
    /// Cờ chỉ báo có lỗi cú pháp JSON hay không.
    /// </summary>
    public bool HasJsonError => !IsJsonValid;

    /// <summary>
    /// Thông báo chi tiết lỗi cú pháp JSON (nếu có).
    /// </summary>
    [ObservableProperty]
    private string _jsonValidationError = string.Empty;

    /// <summary>
    /// Khởi tạo ViewModel cấu hình HTTP Client với bản sao lưu độc lập (Snapshot Buffer).
    /// </summary>
    /// <param name="node">Node đồ họa gốc.</param>
    /// <param name="tagCatalog">Danh mục Tag toàn dự án.</param>
    public HttpClientConfigDialogViewModel(HttpClientNodeViewModel node, TagCatalogViewModel? tagCatalog)
    {
        Node = node;
        TagCatalog = tagCatalog;

        // Sao chép thông số từ Node sang Buffer
        Method = node.Method;
        Url = node.Url;
        CustomHeaders = node.CustomHeaders;
        TimeoutSec = node.TimeoutSec;
        TriggerMode = string.IsNullOrEmpty(node.TriggerMode) ? "Event" : node.TriggerMode;
        PeriodicIntervalSec = node.PeriodicIntervalSec;
        PayloadMode = string.IsNullOrEmpty(node.PayloadMode) ? "JSON" : node.PayloadMode;
        CustomTemplate = node.CustomTemplate;
        DataTag = node.DataTag;

        // Khởi tạo danh sách nguồn dữ liệu thô (mục đầu tiên là Chân cắm FBD DATA Pin)
        RebuildAvailableDataTags();
        Services.LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        // Sao chép danh sách Fields
        foreach (var field in node.Fields)
        {
            Fields.Add(new HttpPayloadFieldViewModel(field.Key, field.Tag, field.Unit, UpdatePreview));
        }

        // Nếu danh sách Fields trống và đang ở chế độ JSON, tạo sẵn 1 trường mẫu
        if (Fields.Count == 0 && TagCatalog?.AllTags?.Count > 0)
        {
            var defaultTag = TagCatalog.AllTags.FirstOrDefault(t => t.Kind != TagKind.None && !string.IsNullOrWhiteSpace(t.Name))
                             ?? TagCatalog.AllTags.FirstOrDefault();
            string initialKey = (defaultTag != null && defaultTag.Kind != TagKind.None) ? defaultTag.Name.ToLowerInvariant() : "temperature";
            Fields.Add(new HttpPayloadFieldViewModel(initialKey, defaultTag, string.Empty, UpdatePreview));
        }

        UpdatePreview();
    }

    /// <summary>
    /// Xây dựng lại danh sách lựa chọn nguồn dữ liệu thô đồng bộ với ngôn ngữ hiện tại.
    /// </summary>
    public void RebuildAvailableDataTags()
    {
        var currentTag = DataTag;
        AvailableDataTags.Clear();
        AvailableDataTags.Add(new DtuDataTagOptionItem(null, Services.LocalizationService.Tr("SourceUseFbdPin")));
        if (TagCatalog?.AllTags != null)
        {
            foreach (var tag in TagCatalog.AllTags.Where(t => t.Kind != SimplePLC.Domain.Enums.TagKind.None && !string.IsNullOrWhiteSpace(t.Name)))
            {
                AvailableDataTags.Add(new DtuDataTagOptionItem(tag, tag.DisplayName));
            }
        }
        if (currentTag != null && !AvailableDataTags.Any(o => o.Tag == currentTag || (o.Tag != null && string.Equals(o.Tag.Name, currentTag.Name, StringComparison.OrdinalIgnoreCase))))
        {
            AvailableDataTags.Add(new DtuDataTagOptionItem(currentTag, currentTag.DisplayName));
        }
        OnPropertyChanged(nameof(SelectedDataTagOption));
    }

    private void OnLanguageChanged()
    {
        RebuildAvailableDataTags();
        UpdatePreview();
        OnPropertyChanged(nameof(SummaryTriggerText));
        OnPropertyChanged(nameof(SummarySourceDataText));
    }

    partial void OnCustomTemplateChanged(string value) => UpdatePreview();

    partial void OnPayloadModeChanged(string value)
    {
        if (IsRawMode)
        {
            if (string.Equals(CustomHeaders, "Content-Type: application/json", StringComparison.OrdinalIgnoreCase))
            {
                CustomHeaders = "Content-Type: text/plain";
            }
        }
        else
        {
            if (string.Equals(CustomHeaders, "Content-Type: text/plain", StringComparison.OrdinalIgnoreCase))
            {
                CustomHeaders = "Content-Type: application/json";
            }
        }

        OnPropertyChanged(nameof(SummaryContentType));
        UpdatePreview();
    }

    partial void OnDataTagChanged(TagModel? value)
    {
        OnPropertyChanged(nameof(SelectedDataTagOption));
        OnPropertyChanged(nameof(SummarySourceDataText));
        UpdatePreview();
    }

    /// <summary>
    /// Thêm một trường dữ liệu mới vào bảng Visual Field Builder.
    /// </summary>
    [RelayCommand]
    public void AddField()
    {
        var defaultTag = TagCatalog?.AllTags?.FirstOrDefault(t => t.Kind != TagKind.None && !string.IsNullOrWhiteSpace(t.Name))
                         ?? TagCatalog?.AllTags?.FirstOrDefault();
        string keyName = (defaultTag != null && defaultTag.Kind != TagKind.None) ? defaultTag.Name.ToLowerInvariant() : $"field_{Fields.Count + 1}";
        Fields.Add(new HttpPayloadFieldViewModel(keyName, defaultTag, string.Empty, UpdatePreview));
        UpdatePreview();
    }

    /// <summary>
    /// Xóa một trường dữ liệu khỏi bảng Visual Field Builder.
    /// </summary>
    /// <param name="field">Trường dữ liệu cần xóa.</param>
    [RelayCommand]
    public void RemoveField(HttpPayloadFieldViewModel? field)
    {
        if (field != null && Fields.Contains(field))
        {
            Fields.Remove(field);
            UpdatePreview();
        }
    }

    /// <summary>
    /// Chèn token placeholder vào mẫu JSON tại vị trí con trỏ chuột.
    /// </summary>
    /// <param name="placeholder">Chuỗi placeholder cần chèn.</param>
    /// <param name="caretIndex">Vị trí con trỏ chuột trong ô nhập.</param>
    /// <returns>Vị trí con trỏ mới sau khi chèn.</returns>
    public int InsertPlaceholderAtCaret(string placeholder, int caretIndex)
    {
        if (string.IsNullOrEmpty(placeholder)) return caretIndex;

        string current = CustomTemplate ?? string.Empty;
        if (caretIndex < 0 || caretIndex > current.Length)
        {
            caretIndex = current.Length;
        }

        string newText = current.Insert(caretIndex, placeholder);
        CustomTemplate = newText;
        return caretIndex + placeholder.Length;
    }

    /// <summary>
    /// Chèn token placeholder dự phòng khi không có thông tin con trỏ.
    /// </summary>
    /// <param name="placeholder">Chuỗi token.</param>
    [RelayCommand]
    public void InsertPlaceholder(string placeholder)
    {
        if (string.IsNullOrEmpty(placeholder)) return;
        InsertPlaceholderAtCaret(placeholder, CustomTemplate?.Length ?? 0);
    }

    /// <summary>
    /// Khôi phục toàn bộ thông số về giá trị mặc định chuẩn công nghiệp.
    /// </summary>
    [RelayCommand]
    public void ResetDefaults()
    {
        Method = "POST";
        Url = "http://api.myserver.com/telemetry";
        CustomHeaders = "Content-Type: application/json";
        TimeoutSec = 10;
        TriggerMode = "Event";
        PeriodicIntervalSec = 30;
        PayloadMode = "JSON";
        CustomTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"val\": {val}\n}";
        DataTag = null;

        Fields.Clear();
        if (TagCatalog?.AllTags?.Count > 0)
        {
            var defaultTag = TagCatalog.AllTags.FirstOrDefault(t => t.Kind != TagKind.None && !string.IsNullOrWhiteSpace(t.Name))
                             ?? TagCatalog.AllTags.FirstOrDefault();
            string initialKey = (defaultTag != null && defaultTag.Kind != TagKind.None) ? defaultTag.Name.ToLowerInvariant() : "temperature";
            Fields.Add(new HttpPayloadFieldViewModel(initialKey, defaultTag, string.Empty, UpdatePreview));
        }

        UpdatePreview();
    }

    /// <summary>
    /// Áp dụng các thay đổi từ buffer vào Node khi người dùng nhấn Áp dụng (Apply).
    /// </summary>
    public void Apply()
    {
        Node.Method = Method;
        Node.Url = Url;
        Node.CustomHeaders = CustomHeaders;
        Node.TimeoutSec = TimeoutSec;
        Node.TriggerMode = TriggerMode;
        Node.PeriodicIntervalSec = PeriodicIntervalSec;
        Node.PayloadMode = PayloadMode;
        Node.CustomTemplate = CustomTemplate;
        Node.DataTag = DataTag;

        // Đồng bộ danh sách Fields vào Node
        Node.Fields.Clear();
        foreach (var field in Fields)
        {
            Node.Fields.Add(new HttpPayloadFieldViewModel(field.Key, field.Tag, field.Unit));
        }
    }

    /// <summary>
    /// Cập nhật nội dung xem trước tải trọng và kiểm tra cú pháp JSON.
    /// </summary>
    public void UpdatePreview()
    {
        if (IsRawMode)
        {
            // CHỈ hiển thị thuần túy giá trị tải trọng thực tế gửi đi (Axiom 2 - Visual Truth)
            // Thông tin nguồn (Chân DATA / Tag) được phản ánh riêng ở Hộp chẩn đoán kỹ thuật.
            PreviewPayloadText = "25.5";
            IsJsonValid = false;
            JsonValidationError = string.Empty;
            return;
        }

        if (IsFieldsMode)
        {
            var dict = new Dictionary<string, object>();
            dict["device"] = "SPLC-DTU-01";
            dict["ts"] = 1714800000;

            foreach (var f in Fields)
            {
                string key = string.IsNullOrWhiteSpace(f.Key) ? (f.Tag?.Name ?? "field") : f.Key;
                if (f.Tag != null)
                {
                    if (f.Tag.DataType == TagDataType.Float32)
                        dict[key] = 25.5;
                    else if (f.Tag.DataType == TagDataType.Boolean)
                        dict[key] = 1;
                    else
                        dict[key] = 100;
                }
                else
                {
                    dict[key] = 0;
                }
            }

            try
            {
                PreviewPayloadText = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                IsJsonValid = true;
                JsonValidationError = string.Empty;
            }
            catch (Exception ex)
            {
                PreviewPayloadText = "{}";
                IsJsonValid = false;
                JsonValidationError = ex.Message;
            }
            return;
        }

        // Chế độ Custom Template
        var tmpl = CustomTemplate ?? string.Empty;
        var rendered = tmpl
            .Replace("{device}", "SPLC-DTU-01")
            .Replace("{ts}", "1714800000")
            .Replace("{val}", "25.5")
            .Replace("{tag}", "VREG0");

        // Kiểm tra tính hợp lệ cú pháp JSON và tự động thụt lề 2 spaces khi hợp lệ
        try
        {
            using var doc = JsonDocument.Parse(rendered);
            IsJsonValid = true;
            JsonValidationError = string.Empty;
            PreviewPayloadText = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            IsJsonValid = false;
            JsonValidationError = ex.Message;
            PreviewPayloadText = rendered;
        }
    }
}

/// <summary>
/// Mục tùy chọn nguồn dữ liệu Tag hoặc Chân cắm FBD trong ComboBox.
/// </summary>
public record DtuDataTagOptionItem(TagModel? Tag, string DisplayName);
