using TagOptionItem = SimplePLC.Studio.Models.TagOptionItem;
using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel đại diện cho cấu hình một chân ngõ ra động (Dynamic Pin) của khối MQTT Subscribe.
/// </summary>
public partial class MqttPinBindingViewModel : ObservableObject
{
    private readonly ObservableCollection<TagOptionItem> _tagOptions = new();

    /// <summary>
    /// Danh sách các tùy chọn Tag trong Dropdown, được gắn cờ IsSelectable và thông tin xung đột độc quyền.
    /// Sử dụng ReadOnlyObservableCollection duy trì tham chiếu ổn định để ngăn WPF ComboBox reset SelectedItem.
    /// </summary>
    public ReadOnlyObservableCollection<TagOptionItem> TagOptions { get; }
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
        RefreshTagOptions();
        if (TagProvider != null && TargetTag != null && TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None)
        {
            var available = AvailableTags;
            if (!available.Any(t => t == TargetTag || (t.Kind == TargetTag.Kind && t.Index == TargetTag.Index)))
            {
                TargetTag = available.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
            }
        }
    }

    private TagModel? _targetTag;

    /// <summary>
    /// Tag đích mà chân MQTT Subscribe này ghi đè dữ liệu vào.
    /// Có cơ chế bảo vệ chống hiện tượng WPF Selector tự động đẩy null khi làm mới ItemsSource.
    /// </summary>
    public TagModel? TargetTag
    {
        get => _targetTag;
        set
        {
            if (value == null && _targetTag != null && _targetTag.Kind != SimplePLC.Domain.Enums.TagKind.None && !string.Equals(_targetTag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!EqualityComparer<TagModel>.Default.Equals(_targetTag, value))
            {
                var old = _targetTag;
                _targetTag = value;
                OnTargetTagChanged(old, value);
                OnPropertyChanged(nameof(TargetTag));
                OnPropertyChanged(nameof(HasTargetTag));
                OnPropertyChanged(nameof(TargetTagIndex));
                OnPropertyChanged(nameof(TargetTagName));
            }
        }
    }

    private ushort? _initialTargetTagIndex;

    private void OnTargetTagChanged(TagModel? oldValue, TagModel? newValue)
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
        TagWriteOwnershipService.Instance.NotifyOwnershipChanged();
    }

    private Func<MqttDataType, IEnumerable<TagModel>>? _tagProvider;

    /// <summary>
    /// Hàm ủy quyền cung cấp danh sách Tag tương thích dựa trên kiểu dữ liệu MQTT và vai trò WriteTarget.
    /// </summary>
    public Func<MqttDataType, IEnumerable<TagModel>>? TagProvider
    {
        get => _tagProvider;
        set
        {
            _tagProvider = value;
            RefreshTagOptions();
        }
    }

    /// <summary>
    /// Danh sách các Tag có thể ghi tương thích với DataType hiện tại của Pin.
    /// </summary>
    public IEnumerable<TagModel> AvailableTags => TagProvider != null
        ? TagProvider(DataType)
        : System.Linq.Enumerable.Empty<TagModel>();

    /// <summary>
    /// Làm mới danh sách tùy chọn Tag trong Dropdown với cơ chế cập nhật tại chỗ (in-place update)
    /// để bảo toàn hoàn toàn lựa chọn hiện tại của WPF ComboBox mà không kích hoạt Collection Reset.
    /// </summary>
    public void RefreshTagOptions()
    {
        var currentTags = AvailableTags.ToList();

        bool match = _tagOptions.Count == currentTags.Count;
        if (match)
        {
            for (int i = 0; i < currentTags.Count; i++)
            {
                if (!ReferenceEquals(_tagOptions[i].Tag, currentTags[i]))
                {
                    match = false;
                    break;
                }
            }
        }

        if (match)
        {
            foreach (var opt in _tagOptions)
            {
                UpdateTagOptionConflict(opt);
            }
        }
        else
        {
            var existingByTag = _tagOptions.ToDictionary(o => o.Tag);
            _tagOptions.Clear();
            foreach (var tag in currentTags)
            {
                if (existingByTag.TryGetValue(tag, out var existingOpt))
                {
                    UpdateTagOptionConflict(existingOpt);
                    _tagOptions.Add(existingOpt);
                }
                else
                {
                    var newOpt = new TagOptionItem(tag, true, tag.DropdownDisplayName);
                    UpdateTagOptionConflict(newOpt);
                    _tagOptions.Add(newOpt);
                }
            }
        }
    }

    private void UpdateTagOptionConflict(TagOptionItem opt)
    {
        opt.DisplayText = opt.Tag.DropdownDisplayName;
        if (opt.Tag.Kind == SimplePLC.Domain.Enums.TagKind.None || string.Equals(opt.Tag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            opt.IsSelectable = true;
            opt.ConflictBadge = string.Empty;
            opt.ConflictTooltip = string.Empty;
            return;
        }

        var claimant = TagWriteOwnershipService.Instance.GetClaimant(opt.Tag, this);
        if (claimant != null)
        {
            opt.IsSelectable = false;
            opt.ConflictBadge = claimant.DisplayBadge;
            opt.ConflictTooltip = claimant.TooltipDetail;
        }
        else
        {
            opt.IsSelectable = true;
            opt.ConflictBadge = string.Empty;
            opt.ConflictTooltip = string.Empty;
        }
    }

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
            ResolvedPath = string.Empty;
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
        ResolvedPath = string.Empty;
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



    public MqttPinBindingViewModel()
    {
        TagOptions = new ReadOnlyObservableCollection<TagOptionItem>(_tagOptions);
        TagWriteOwnershipService.Instance.OwnershipChanged += RefreshTagOptions;
    }

    public MqttPinBindingViewModel(
        string label,
        string jsonPath,
        MqttDataType dataType = MqttDataType.Int16,
        ushort? targetTagIndex = null,
        double? minValue = null,
        double? maxValue = null,
        double defaultValue = 0.0) : this()
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
