using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Validation;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa IIoT Telemetry MQTT PUBLISH.
/// Cho phép đẩy dữ liệu đo lường, trạng thái hoạt động hoặc cảnh báo lên MQTT Broker.
/// Hỗ trợ cả 2 chế độ:
/// - Đơn biến từ dây nối (Single Wire: đọc chân Val)
/// - Đa biến gom nhóm (Multi-Field Batch: gom nhiều Tag vào 1 JSON qua Visual Field Builder).
/// </summary>
public partial class MqttPublishNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (DataTag != null && DataTag.Kind != TagKind.None && DataTag.Index < 65535) ? (ushort)DataTag.Index : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    [NotifyPropertyChangedFor(nameof(HasTopicError))]
    [NotifyPropertyChangedFor(nameof(TopicValidationError))]
    private string _topic = "synaptix/telemetry/data";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    [NotifyPropertyChangedFor(nameof(IsCustomTemplateVisible))]
    private string _payloadMode = "JSON"; // "JSON", "Raw", "Custom"

    public bool IsCustomTemplateVisible => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    [NotifyPropertyChangedFor(nameof(CustomTemplateLength))]
    [NotifyPropertyChangedFor(nameof(CustomTemplateCounterText))]
    [NotifyPropertyChangedFor(nameof(IsCustomTemplateExceeded))]
    private string _customTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"values\": {\n    \"{tag}\": {val}\n  }\n}";

    public int CustomTemplateLength => CustomTemplate?.Length ?? 0;
    public string CustomTemplateCounterText => $"{CustomTemplateLength} / 128 chars";
    public bool IsCustomTemplateExceeded => CustomTemplateLength > 128;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(QosBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsQos0))]
    [NotifyPropertyChangedFor(nameof(IsQos1))]
    [NotifyPropertyChangedFor(nameof(IsQos2))]
    private int _qos = 0; // 0, 1, 2

    public bool IsQos0 => Qos == 0;
    public bool IsQos1 => Qos == 1;
    public bool IsQos2 => Qos == 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(RetainBadgeText))]
    private bool _retain = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(IsEventMode))]
    [NotifyPropertyChangedFor(nameof(IsCovMode))]
    [NotifyPropertyChangedFor(nameof(IsPeriodicMode))]
    private string _publishMode = "Event"; // "Event", "COV", "Cyclic"

    public bool IsEventMode => string.Equals(PublishMode, "Event", StringComparison.OrdinalIgnoreCase);
    public bool IsCovMode => string.Equals(PublishMode, "COV", StringComparison.OrdinalIgnoreCase);
    public bool IsPeriodicMode => string.Equals(PublishMode, "Cyclic", StringComparison.OrdinalIgnoreCase) || string.Equals(PublishMode, "Periodic", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private double _deadband = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private uint _intervalMs = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private string _unit = "bar";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    [NotifyPropertyChangedFor(nameof(DataTagBadgeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private TagModel? _dataTag;

    // Visual Field Builder: Danh sách các trường dữ liệu gom nhóm
    public ObservableCollection<MqttPayloadFieldViewModel> Fields { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private bool _includeDevice = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private bool _includeTimestamp = true;

    // Live Telemetry & Simulation State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private double _liveValue = 0.0;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    private bool _hasReceivedUpdate = false;

    [ObservableProperty]
    private string _lastPublishedPayload = "--";

    [ObservableProperty]
    private string _lastPublishedTime = "--";

    [ObservableProperty]
    private int _publishCount = 0;

    [ObservableProperty]
    private bool _hasPublishError = false;

    [ObservableProperty]
    private string _lastErrorMessage = string.Empty;

    public int LiveRawValue => (int)LiveValue;

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        LiveQuality = snapshot.Quality;
        HasReceivedUpdate = true;
        LiveValue = snapshot.RawValue;
        LiveValueText = snapshot.RawValue.ToString();
        IsLiveActive = snapshot.RawValue != 0;
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        LiveQuality = TagQuality.Unknown;
        IsLiveActive = false;
    }

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }

    public string QosBadgeText => $"QoS {Qos}";

    public string RetainBadgeText => Retain ? "Retain: ON" : "Retain: OFF";

    public string DataTagBadgeText => Fields.Count > 0
        ? $"{Fields.Count} Tags"
        : (DataTag != null ? DataTag.Name : "—");

    public bool HasTopicError => !MqttTopicValidator.Validate(Topic, out _);

    public string TopicValidationError
    {
        get
        {
            MqttTopicValidator.Validate(Topic, out string err);
            return err;
        }
    }

    public string PreviewPayloadText => GeneratePayload(LiveValue > 0 ? LiveValue : 45.2, "SynaptiX_Controller");

    public string TelemetryStatusPillText => PublishCount > 0
        ? string.Format(LocalizationService.Tr("InspectorMqttSentMsgs"), PublishCount)
        : LocalizationService.Tr("InspectorMqttReadyToPublish");

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"{Title} [{Topic}] ({PayloadMode}, QoS{Qos})"
        : $"{CustomLabel} [{Topic}] ({PayloadMode}, QoS{Qos})";

    public string NarrativeText
    {
        get
        {
            string tagStr = Fields.Count > 0
                ? string.Format(LocalizationService.Tr("InspectorMqttFieldCountSummary"), Fields.Count)
                : (DataTag != null ? DataTag.DisplayName : LocalizationService.Tr("InspectorMqttTagNone"));
            string retainStr = Retain ? LocalizationService.Tr("InspectorMqttRetainOn") : LocalizationService.Tr("InspectorMqttRetainOff");
            return string.Format(LocalizationService.Tr("InspectorMqttNarrative"), Topic, PayloadMode, tagStr, Qos, retainStr);
        }
    }

    public MqttPublishNodeViewModel(TagModel? dataTag = null, string topic = "synaptix/telemetry/data")
    {
        Title = "MQTT PUB";
        Icon = "📡";
        DataTag = dataTag;
        Topic = topic;

        // Input Connectors
        InputConnectors.Add(new ConnectorViewModel
        {
            Title = "Trig",
            IsInput = true,
            Node = this
        });

        InputConnectors.Add(new ConnectorViewModel
        {
            Title = "Val",
            IsInput = true,
            Node = this
        });

        // Output Connectors
        OutputConnectors.Add(new ConnectorViewModel
        {
            Title = "Done",
            IsInput = false,
            Node = this
        });

        OutputConnectors.Add(new ConnectorViewModel
        {
            Title = "Err",
            IsInput = false,
            Node = this
        });

        Fields.CollectionChanged += OnFieldsCollectionChanged;
    }

    private void OnFieldsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PreviewPayloadText));
        OnPropertyChanged(nameof(DataTagBadgeText));
        OnPropertyChanged(nameof(NarrativeText));
    }

    /// <summary>
    /// Sinh nội dung payload dựa trên cấu hình:
    /// - Nếu có danh sách Fields trong Field Builder: sinh JSON đa biến { key1: val1, key2: val2, ... }
    /// - Nếu không: sinh theo PayloadMode (JSON, Raw hoặc Custom).
    /// </summary>
    public string GeneratePayload(double value, string deviceName = "SynaptiX_Controller")
    {
        string tagName = DataTag?.Name ?? "TAG";
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 1. Chế độ Visual Field Builder (Nhiều trường gom nhóm)
        if (Fields.Count > 0 && PayloadMode == "JSON")
        {
            var valuesDict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var field in Fields)
            {
                string key = string.IsNullOrWhiteSpace(field.Key)
                    ? (field.Tag?.Name.ToLowerInvariant() ?? "field")
                    : field.Key;

                double fieldVal = field.Tag != null ? field.Tag.Value : value;
                valuesDict[key] = Math.Round(fieldVal, 2);
            }

            var envelope = new System.Collections.Generic.Dictionary<string, object>();
            if (IncludeDevice)
            {
                envelope["device"] = deviceName;
            }
            if (IncludeTimestamp)
            {
                envelope["ts"] = nowMillis;
            }
            envelope["values"] = valuesDict;

            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        }

        // 2. Chế độ đơn biến truyền thống
        switch (PayloadMode)
        {
            case "Raw":
                return value.ToString("0.##");

            case "Custom":
                if (string.IsNullOrWhiteSpace(CustomTemplate))
                {
                    return value.ToString("0.##");
                }
                return CustomTemplate
                    .Replace("{val}", value.ToString("0.##"))
                    .Replace("{tag}", tagName)
                    .Replace("{device}", deviceName)
                    .Replace("{unit}", Unit ?? string.Empty)
                    .Replace("{ts}", nowMillis.ToString());

            case "JSON":
            default:
                var payloadObj = new
                {
                    device = deviceName,
                    ts = nowMillis,
                    values = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { tagName.ToLowerInvariant(), Math.Round(value, 2) }
                    }
                };
                return JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Ghi nhận trạng thái phát tin thành công.
    /// </summary>
    public void RecordPublishSuccess(double value, string deviceName = "SynaptiX_Controller")
    {
        LiveValue = value;
        LastPublishedPayload = GeneratePayload(value, deviceName);
        LastPublishedTime = DateTime.Now.ToString("HH:mm:ss");
        PublishCount++;
        HasPublishError = false;
        LastErrorMessage = string.Empty;
        IsLiveActive = true;
    }

    /// <summary>
    /// Ghi nhận trạng thái lỗi phát tin (mất kết nối broker, timeout...).
    /// </summary>
    public void RecordPublishError(string errorMessage)
    {
        HasPublishError = true;
        LastErrorMessage = errorMessage;
        IsLiveActive = false;
    }

    [RelayCommand]
    public void AddField(TagModel? defaultTag = null)
    {
        string defaultKey = defaultTag != null ? defaultTag.Name.ToLowerInvariant() : $"field_{Fields.Count + 1}";
        var newField = new MqttPayloadFieldViewModel(defaultKey, defaultTag, string.Empty, () => OnPropertyChanged(nameof(PreviewPayloadText)));
        Fields.Add(newField);
    }

    public void AddFieldWithDetails(string key, TagModel? tag, string unit)
    {
        var newField = new MqttPayloadFieldViewModel(key, tag, unit, () => OnPropertyChanged(nameof(PreviewPayloadText)));
        Fields.Add(newField);
    }

    [RelayCommand]
    public void RemoveField(MqttPayloadFieldViewModel? field)
    {
        if (field != null && Fields.Contains(field))
        {
            Fields.Remove(field);
        }
    }

    [RelayCommand]
    public void SetPayloadMode(string mode)
    {
        PayloadMode = mode;
    }

    [RelayCommand]
    public void SetQos(int qosLevel)
    {
        Qos = Math.Clamp(qosLevel, 0, 2);
    }

    [RelayCommand]
    public void SetQos0() => Qos = 0;

    [RelayCommand]
    public void SetQos1() => Qos = 1;

    [RelayCommand]
    public void SetQos2() => Qos = 2;

    [RelayCommand]
    public void SanitizeTopic()
    {
        Topic = MqttTopicValidator.Sanitize(Topic);
    }

    [RelayCommand]
    public void ApplyTelemetryPreset()
    {
        Topic = "synaptix/{device}/telemetry";
    }

    [RelayCommand]
    public void ApplyAlarmPreset()
    {
        Topic = "synaptix/{device}/alarms";
        Qos = 1; // Alarm ưu tiên QoS 1
    }

    [RelayCommand]
    public void ApplyStatusPreset()
    {
        Topic = "synaptix/{device}/status";
        Retain = true; // Status ưu tiên Retain
    }
}
