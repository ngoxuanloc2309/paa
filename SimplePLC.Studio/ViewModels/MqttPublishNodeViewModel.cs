using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
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
    [NotifyPropertyChangedFor(nameof(IsJsonMode))]
    [NotifyPropertyChangedFor(nameof(IsRawMode))]
    private string _payloadMode = "JSON"; // "JSON", "Raw", "Custom"

    public bool IsCustomTemplateVisible => string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase);
    public bool IsJsonMode => string.Equals(PayloadMode, "JSON", StringComparison.OrdinalIgnoreCase);
    public bool IsRawMode => string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    private bool _wrapValues = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PreviewPayloadText))]
    [NotifyPropertyChangedFor(nameof(CustomTemplateLength))]
    [NotifyPropertyChangedFor(nameof(CustomTemplateCounterText))]
    [NotifyPropertyChangedFor(nameof(IsCustomTemplateExceeded))]
    [NotifyPropertyChangedFor(nameof(HasCustomTemplateError))]
    [NotifyPropertyChangedFor(nameof(CustomTemplateValidationError))]
    private string _customTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"values\": {\n    \"{tag}\": {val}\n  }\n}";

    public int CustomTemplateLength => CustomTemplate?.Length ?? 0;
    public string CustomTemplateCounterText => $"{CustomTemplateLength} / 128 ký tự";
    public bool IsCustomTemplateExceeded => CustomTemplateLength > 128;

    public bool HasCustomTemplateError => IsCustomTemplateExceeded || !string.IsNullOrEmpty(CustomTemplateValidationError);

    public string CustomTemplateValidationError
    {
        get
        {
            if (IsCustomTemplateExceeded)
                return "Bản mẫu JSON vượt quá giới hạn 128 ký tự (64 registers) của firmware MCU.";
            if (string.IsNullOrWhiteSpace(CustomTemplate))
                return string.Empty;

            var matches = System.Text.RegularExpressions.Regex.Matches(CustomTemplate, @"\{([^\{\}\s]+)\}");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string token = match.Groups[1].Value;
                if (token is "val" or "tag" or "device" or "ts" or "unit" or "vals")
                    continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^val[1-8](:[0-3])?$"))
                    continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^tag[1-8]$"))
                    continue;
                if (Fields.Any(f => string.Equals(f.Key, token, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (Fields.Any(f => f.Tag != null && string.Equals(f.Tag.Name, token, StringComparison.OrdinalIgnoreCase)))
                    continue;

                return $"Token '{{{token}}}' không được firmware MCU hỗ trợ. Hỗ trợ: {{val1}}..{{val8}}, {{val1:N}}, {{tag1}}..{{tag8}}, {{vals}}, {{ts}}, {{device}}.";
            }
            return string.Empty;
        }
    }

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
    [NotifyPropertyChangedFor(nameof(PublishModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(IsEventMode))]
    [NotifyPropertyChangedFor(nameof(IsCovMode))]
    [NotifyPropertyChangedFor(nameof(IsPeriodicMode))]
    private string _publishMode = "Event"; // "Event", "COV", "Cyclic"

    partial void OnPublishModeChanged(string value)
    {
        SynchronizeConnectors();
    }

    public bool IsEventMode => string.Equals(PublishMode, "Event", StringComparison.OrdinalIgnoreCase);
    public bool IsCovMode => string.Equals(PublishMode, "COV", StringComparison.OrdinalIgnoreCase);
    public bool IsPeriodicMode => string.Equals(PublishMode, "Cyclic", StringComparison.OrdinalIgnoreCase) || string.Equals(PublishMode, "Periodic", StringComparison.OrdinalIgnoreCase);

    public string PublishModeSummaryText => IsPeriodicMode ? $"{IntervalMs}ms" : (IsCovMode ? $"COV (Δ{Deadband})" : "On Trig");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PublishModeSummaryText))]
    private double _deadband = 0.5;

    [ObservableProperty]
    private string _deadbandText = "0.5";

    partial void OnDeadbandTextChanged(string value)
    {
        if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
        {
            if (Math.Abs(Deadband - parsed) > 1e-9) Deadband = parsed;
        }
    }

    partial void OnDeadbandChanged(double value)
    {
        if (!NumericInputHelper.TryParseFlexibleDouble(DeadbandText, out double cur) || Math.Abs(cur - value) > 1e-9)
        {
            DeadbandText = NumericInputHelper.FormatDouble(value);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(PublishModeSummaryText))]
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

    public ConnectorViewModel TriggerConnector { get; }
    public ConnectorViewModel DefaultValConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }

    public MqttPublishNodeViewModel(TagModel? dataTag = null, string topic = "synaptix/telemetry/data")
    {
        Title = "MQTT PUB";
        Icon = "📡";
        DataTag = dataTag;
        Topic = topic;
        DeadbandText = NumericInputHelper.FormatDouble(Deadband);

        // Dedicated Connectors
        TriggerConnector = new ConnectorViewModel
        {
            Title = "Trig",
            TooltipKey = "PinTooltipTrig",
            IsInput = true,
            Node = this
        };

        DefaultValConnector = new ConnectorViewModel
        {
            Title = "Val",
            TooltipKey = "PinTooltipVal",
            IsInput = true,
            Node = this
        };

        DoneConnector = new ConnectorViewModel
        {
            Title = "Done",
            TooltipKey = "PinTooltipDone",
            IsInput = false,
            Node = this
        };

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipErr",
            IsInput = false,
            Node = this
        };

        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(ErrorConnector);

        Fields.CollectionChanged += OnFieldsCollectionChanged;

        SynchronizeConnectors();
    }

    private void OnFieldsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (MqttPayloadFieldViewModel oldField in e.OldItems)
            {
                oldField.PropertyChanged -= OnFieldPropertyChanged;
                if (oldField.Connector != null)
                {
                    InputConnectors.Remove(oldField.Connector);
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (MqttPayloadFieldViewModel newField in e.NewItems)
            {
                newField.PropertyChanged += OnFieldPropertyChanged;
            }
        }

        SynchronizeConnectors();

        OnPropertyChanged(nameof(PreviewPayloadText));
        OnPropertyChanged(nameof(DataTagBadgeText));
        OnPropertyChanged(nameof(NarrativeText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnFieldPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is MqttPayloadFieldViewModel field && e.PropertyName == nameof(MqttPayloadFieldViewModel.Key))
        {
            if (field.Connector != null)
            {
                string key = string.IsNullOrWhiteSpace(field.Key) ? "val" : field.Key;
                field.Connector.Title = key;
                field.Connector.SetBilingualDescription(
                    $"Trường dữ liệu JSON '{key}' đóng gói vào bản tin MQTT",
                    $"JSON payload field '{key}' to be published via MQTT");
            }
            OnPropertyChanged(nameof(PreviewPayloadText));
        }
    }

    /// <summary>
    /// Đồng bộ hệ thống Connector trên Canvas:
    /// - Chân Trig: Hiển thị khi ở chế độ Event (On Trigger), tự động ẩn khi ở chế độ COV hoặc Chu kỳ (Cyclic/Periodic).
    /// - Chân Val: Hiển thị chân mặc định 'Val' khi Fields rỗng; tự động thay thế bằng các chân mang tên trường (Key) tương ứng khi Fields có dữ liệu.
    /// </summary>
    public void SynchronizeConnectors()
    {
        // 1. Đồng bộ chân Trig
        if (IsEventMode)
        {
            if (!InputConnectors.Contains(TriggerConnector))
            {
                InputConnectors.Insert(0, TriggerConnector);
            }
        }
        else
        {
            if (InputConnectors.Contains(TriggerConnector))
            {
                InputConnectors.Remove(TriggerConnector);
            }
        }

        // 2. Đồng bộ chân dữ liệu giá trị (Val mặc định vs Dynamic Fields)
        if (Fields.Count == 0)
        {
            var toRemove = InputConnectors.Where(c => c != TriggerConnector && c != DefaultValConnector).ToList();
            foreach (var c in toRemove)
            {
                InputConnectors.Remove(c);
            }

            if (!InputConnectors.Contains(DefaultValConnector))
            {
                InputConnectors.Add(DefaultValConnector);
            }
        }
        else
        {
            if (InputConnectors.Contains(DefaultValConnector))
            {
                InputConnectors.Remove(DefaultValConnector);
            }

            foreach (var field in Fields)
            {
                SynchronizeConnectorForField(field);
            }

            var activeFieldConnectors = Fields.Select(f => f.Connector).Where(c => c != null).ToHashSet();
            var obsolete = InputConnectors.Where(c => c != TriggerConnector && c != DefaultValConnector && !activeFieldConnectors.Contains(c)).ToList();
            foreach (var c in obsolete)
            {
                InputConnectors.Remove(c);
            }
        }
    }

    /// <summary>
    /// Đồng bộ chân Connector cho một trường trong Visual Field Builder.
    /// </summary>
    public void SynchronizeConnectorForField(MqttPayloadFieldViewModel field)
    {
        string fieldKey = string.IsNullOrWhiteSpace(field.Key)
            ? (field.Tag?.Name.ToLowerInvariant() ?? "val")
            : field.Key;

        if (field.Connector != null && InputConnectors.Contains(field.Connector))
        {
            field.Connector.Title = fieldKey;
            field.Connector.SetBilingualDescription(
                $"Trường dữ liệu JSON '{fieldKey}' đóng gói vào bản tin MQTT",
                $"JSON payload field '{fieldKey}' to be published via MQTT");
            return;
        }

        var connector = new ConnectorViewModel
        {
            Id = field.Id,
            Title = fieldKey,
            IsInput = true,
            Node = this
        };
        connector.SetBilingualDescription(
            $"Trường dữ liệu JSON '{fieldKey}' đóng gói vào bản tin MQTT",
            $"JSON payload field '{fieldKey}' to be published via MQTT");
        field.Connector = connector;
        InputConnectors.Add(connector);
    }

    /// <summary>
    /// Sinh nội dung payload dựa trên cấu hình:
    /// - Custom: Dùng mẫu tùy biến do người dùng tự gõ, thay thế các biến {val}, {tag}, {device}, {ts}, hoặc {field_name}
    /// - Raw: Trả về giá trị thô trực tiếp
    /// - JSON: Tự động gom nhóm các trường (có tùy chọn bọc "values", kèm "device", kèm "ts")
    /// </summary>
    public string GeneratePayload(double value, string deviceName = "SynaptiX_Controller")
    {
        string tagName = DataTag?.Name ?? "TAG";
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 1. Chế độ Tùy biến mẫu tự do (Custom Template)
        if (string.Equals(PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(CustomTemplate))
            {
                return value.ToString("0.##", CultureInfo.InvariantCulture);
            }

            string result = CustomTemplate
                .Replace("{val}", value.ToString("0.##", CultureInfo.InvariantCulture))
                .Replace("{tag}", tagName)
                .Replace("{device}", deviceName)
                .Replace("{unit}", Unit ?? string.Empty)
                .Replace("{ts}", nowMillis.ToString());

            // Hỗ trợ token {vals} (mảng JSON của các giá trị đã định dạng theo kiểu dữ liệu Tag)
            if (Fields.Count > 0)
            {
                var valsList = Fields.Select(f => FormatTagValueString(f.Tag, 0));
                result = result.Replace("{vals}", "[" + string.Join(", ", valsList) + "]");
            }
            else
            {
                result = result.Replace("{vals}", "[" + value.ToString("0.##", CultureInfo.InvariantCulture) + "]");
            }

            // Hỗ trợ token {valK}, {valK:N}, {tagK} (K = 1..8)
            for (int k = 1; k <= 8; k++)
            {
                int fIdx = k - 1;
                TagModel? fTag = (fIdx < Fields.Count) ? Fields[fIdx].Tag : null;
                string fValStr = FormatTagValueString(fTag, k == 1 ? value : 0.0);
                string fTagName = (fIdx < Fields.Count)
                    ? (Fields[fIdx].Tag?.Name ?? Fields[fIdx].Key)
                    : (k == 1 ? tagName : $"tag{k}");

                result = result.Replace($"{{val{k}}}", fValStr);
                result = result.Replace($"{{tag{k}}}", fTagName);

                for (int n = 0; n <= 3; n++)
                {
                    string nFmt = n > 0 ? "0." + new string('0', n) : "0";
                    double numVal = (fTag != null && fTag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
                        ? BitConverter.Int32BitsToSingle(fTag.Value)
                        : (fTag != null ? fTag.Value : (k == 1 ? value : 0.0));
                    result = result.Replace($"{{val{k}:{n}}}", numVal.ToString(nFmt, CultureInfo.InvariantCulture));
                }
            }

            // Thay thế cả các biến theo tên trường trong Fields (nếu có)
            foreach (var field in Fields)
            {
                string fVal = FormatTagValueString(field.Tag, 0);
                if (!string.IsNullOrWhiteSpace(field.Key))
                {
                    result = result.Replace("{" + field.Key + "}", fVal);
                }
                if (field.Tag != null && !string.IsNullOrWhiteSpace(field.Tag.Name))
                {
                    result = result.Replace("{" + field.Tag.Name + "}", fVal);
                }
            }

            return result;
        }

        // 2. Chế độ Giá trị thô (Raw Value)
        if (string.Equals(PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // 3. Chế độ Visual Field Builder (Nhiều trường gom nhóm)
        if (Fields.Count > 0)
        {
            var valuesDict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var field in Fields)
            {
                string key = string.IsNullOrWhiteSpace(field.Key)
                    ? (field.Tag?.Name.ToLowerInvariant() ?? "field")
                    : field.Key;

                object fieldVal = FormatTagValue(field.Tag, 0);
                valuesDict[key] = fieldVal;
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

            if (WrapValues)
            {
                envelope["values"] = valuesDict;
            }
            else
            {
                foreach (var kvp in valuesDict)
                {
                    envelope[kvp.Key] = kvp.Value;
                }
            }

            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        }

        // 4. Mặc định đơn biến (Single Tag)
        var defaultObj = new System.Collections.Generic.Dictionary<string, object>();
        if (IncludeDevice) defaultObj["device"] = deviceName;
        if (IncludeTimestamp) defaultObj["ts"] = nowMillis;

        if (WrapValues)
        {
            defaultObj["values"] = new System.Collections.Generic.Dictionary<string, object>
            {
                { tagName.ToLowerInvariant(), Math.Round(value, 2) }
            };
        }
        else
        {
            defaultObj[tagName.ToLowerInvariant()] = Math.Round(value, 2);
        }

        return JsonSerializer.Serialize(defaultObj, new JsonSerializerOptions { WriteIndented = true });
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

    private static object FormatTagValue(TagModel? tag, double fallbackValue)
    {
        if (tag == null) return fallbackValue;
        if (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
        {
            float f = BitConverter.Int32BitsToSingle(tag.Value);
            return (double)Math.Round(f, 4);
        }
        if (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
        {
            return tag.Value != 0 ? 1 : 0;
        }
        return tag.Value;
    }

    private static string FormatTagValueString(TagModel? tag, double fallbackValue)
    {
        if (tag == null) return fallbackValue.ToString("0.##", CultureInfo.InvariantCulture);
        if (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
        {
            float f = BitConverter.Int32BitsToSingle(tag.Value);
            return f.ToString("0.##", CultureInfo.InvariantCulture);
        }
        if (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
        {
            return tag.Value != 0 ? "1" : "0";
        }
        return tag.Value.ToString();
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

    [RelayCommand]
    public void ResetCustomTemplate()
    {
        CustomTemplate = "{\n  \"device\": \"{device}\",\n  \"ts\": {ts},\n  \"values\": {\n    \"{tag}\": {val}\n  }\n}";
    }
}
