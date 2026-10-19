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

    [ObservableProperty]
    private TagModel? _targetTag;

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

    public ushort? TargetTagIndex => (TargetTag != null && TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None && TargetTag.Index < 65535)
        ? (ushort)TargetTag.Index
        : null;

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

    public void LoadFromDto(MqttPinBindingDto dto)
    {
        Id = dto.Id;
        Label = dto.Label;
        JsonPath = dto.JsonPath;
        DataType = dto.DataType;
        MinValue = dto.MinValue;
        MaxValue = dto.MaxValue;
        DefaultValue = dto.DefaultValue;
    }
}
