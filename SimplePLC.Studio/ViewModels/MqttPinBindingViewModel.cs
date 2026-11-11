using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using System;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel đại diện cho cấu hình một chân ngõ ra động (Dynamic Pin) của khối MQTT Subscribe.
/// </summary>
public partial class MqttPinBindingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _label = "data";

    [ObservableProperty]
    private string _jsonPath = "data";

    [ObservableProperty]
    private MqttDataType _dataType = MqttDataType.Int16;

    partial void OnDataTypeChanged(MqttDataType oldValue, MqttDataType newValue)
    {
        OnPropertyChanged(nameof(AvailableTags));
        if (TagProvider != null && TargetTag != null && TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None)
        {
            var available = AvailableTags;
            if (!available.Any(t => t == TargetTag || (t.Kind == TargetTag.Kind && t.Index == TargetTag.Index)))
            {
                TargetTag = available.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTargetTag))]
    [NotifyPropertyChangedFor(nameof(TargetTagIndex))]
    [NotifyPropertyChangedFor(nameof(TargetTagName))]
    private TagModel? _targetTag;

    private ushort? _initialTargetTagIndex;

    partial void OnTargetTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        _initialTargetTagIndex = null;
        if (newValue != null && newValue.Kind != SimplePLC.Domain.Enums.TagKind.None)
        {
            if (newValue.IsDigital || newValue.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
            {
                if (DataType != MqttDataType.Bool) DataType = MqttDataType.Bool;
            }
            else if (newValue.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
            {
                if (DataType != MqttDataType.Float) DataType = MqttDataType.Float;
            }
            else if (newValue.DataType == SimplePLC.Domain.Enums.TagDataType.Int32)
            {
                if (DataType != MqttDataType.Int16 && DataType != MqttDataType.Int32)
                {
                    DataType = MqttDataType.Int32;
                }
            }
        }
    }

    /// <summary>
    /// Hàm ủy quyền cung cấp danh sách Tag tương thích dựa trên kiểu dữ liệu MQTT và vai trò WriteTarget.
    /// </summary>
    public Func<MqttDataType, IEnumerable<TagModel>>? TagProvider { get; set; }

    /// <summary>
    /// Danh sách các Tag có thể ghi tương thích với DataType hiện tại của Pin.
    /// </summary>
    public IEnumerable<TagModel> AvailableTags => TagProvider != null
        ? TagProvider(DataType)
        : System.Linq.Enumerable.Empty<TagModel>();

    /// <summary>
    /// Tên Tag đích phục vụ hiển thị và lưu trữ Metadata.
    /// </summary>
    public string TargetTagName => TargetTag?.Name ?? string.Empty;

    [ObservableProperty]
    private double? _minValue;

    [ObservableProperty]
    private double? _maxValue;

    [ObservableProperty]
    private double _defaultValue = 0.0;

    [ObservableProperty]
    private double _currentValue = 0.0;

    [ObservableProperty]
    private string _currentStringValue = "--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomResolvedPath))]
    private string _resolvedPath = string.Empty;

    public bool HasCustomResolvedPath =>
        !string.IsNullOrWhiteSpace(ResolvedPath) &&
        !string.Equals(ResolvedPath, Label, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tên trường trong bản tin JSON, đồng thời làm tên chân ngõ ra trên Canvas.
    /// Giúp người lập trình không chuyên chỉ cần thao tác 1 trường duy nhất mà không bị rối.
    /// </summary>
    public string FieldName
    {
        get => string.IsNullOrEmpty(JsonPath) ? Label : JsonPath;
        set
        {
            JsonPath = value;
            Label = value;
            if (Connector != null)
            {
                Connector.Title = value;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(JsonPath));
            OnPropertyChanged(nameof(Label));
        }
    }

    partial void OnJsonPathChanged(string? oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(Label) || Label == oldValue || Label == "data")
        {
            Label = newValue;
            if (Connector != null)
            {
                Connector.Title = newValue;
            }
        }
        OnPropertyChanged(nameof(FieldName));
    }

    partial void OnLabelChanged(string? oldValue, string newValue)
    {
        if (Connector != null)
        {
            Connector.Title = newValue;
        }
        OnPropertyChanged(nameof(FieldName));
    }

    /// <summary>
    /// Tham chiếu tới Connector trên Canvas để cập nhật nhãn khi đổi tên.
    /// </summary>
    public ConnectorViewModel? Connector { get; set; }

    public bool HasTargetTag => TargetTag != null;

    public ushort? TargetTagIndex => (TargetTag != null)
        ? (TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None && TargetTag.Index < 65535 ? (ushort)TargetTag.Index : null)
        : _initialTargetTagIndex;



    public MqttPinBindingViewModel() { }

    public MqttPinBindingViewModel(
        string label,
        string jsonPath,
        MqttDataType dataType = MqttDataType.Int16,
        ushort? targetTagIndex = null,
        double? minValue = null,
        double? maxValue = null,
        double defaultValue = 0.0)
    {
        Label = label;
        JsonPath = jsonPath;
        DataType = dataType;
        _initialTargetTagIndex = targetTagIndex;
        MinValue = minValue;
        MaxValue = maxValue;
        DefaultValue = defaultValue;
    }

    public MqttPinBindingDto ToDto() => new()
    {
        Id = Id,
        Label = Label,
        JsonPath = !string.IsNullOrWhiteSpace(ResolvedPath) ? ResolvedPath : JsonPath,
        DataType = DataType,
        TargetTagIndex = TargetTagIndex,
        MinValue = MinValue,
        MaxValue = MaxValue,
        DefaultValue = DefaultValue
    };

    public void LoadFromDto(MqttPinBindingDto dto, TagCatalogViewModel? tagCatalog = null)
    {
        Id = dto.Id;
        Label = dto.Label;
        JsonPath = dto.JsonPath;
        DataType = dto.DataType;
        MinValue = dto.MinValue;
        MaxValue = dto.MaxValue;
        DefaultValue = dto.DefaultValue;
        _initialTargetTagIndex = dto.TargetTagIndex;
        if (tagCatalog != null && dto.TargetTagIndex.HasValue)
        {
            TargetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Index == dto.TargetTagIndex.Value);
        }
    }
}
