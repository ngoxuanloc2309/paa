using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa IIoT Telemetry MQTT SUBSCRIBE (Khối nhận dữ liệu Inbound).
/// Hỗ trợ chân ngõ ra động (Dynamic Output Pins), phân tích cú pháp Atomic All-or-Nothing,
/// hàng đợi FIFO/Latest-Wins và giả lập kiểm thử trực tiếp trên Canvas.
/// </summary>
public partial class MqttSubscribeNodeViewModel : GraphNodeViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(HasTopicError))]
    [NotifyPropertyChangedFor(nameof(TopicValidationError))]
    private string _topic = "synaptix/commands/inbound";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsQos0))]
    [NotifyPropertyChangedFor(nameof(IsQos1))]
    [NotifyPropertyChangedFor(nameof(IsQos2))]
    private int _qos = 1; // 0, 1, 2

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(QueueModeSummaryText))]
    private MqttQueueMode _queueMode = MqttQueueMode.Fifo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueueModeSummaryText))]
    private int _queueCapacity = 16;

    public string QueueModeSummaryText => QueueMode == MqttQueueMode.Fifo
        ? $"FIFO ({QueueCapacity})"
        : "Latest Wins";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IgnoreRetained))]
    private bool _ignoreRetainedOnBoot = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailSafeTimeoutMs))]
    private uint _timeoutMs = 0; // 0 = disabled

    [ObservableProperty]
    private MqttFailSafeAction _failSafeAction = MqttFailSafeAction.KeepLastValue;

    public bool IgnoreRetained
    {
        get => IgnoreRetainedOnBoot;
        set => IgnoreRetainedOnBoot = value;
    }

    public uint FailSafeTimeoutMs
    {
        get => TimeoutMs;
        set => TimeoutMs = value;
    }

    [RelayCommand] public void SetQos0() => Qos = 0;
    [RelayCommand] public void SetQos1() => Qos = 1;
    [RelayCommand] public void SetQos2() => Qos = 2;

    public bool IsQos0 => Qos == 0;
    public bool IsQos1 => Qos == 1;
    public bool IsQos2 => Qos == 2;

    [RelayCommand]
    public void SanitizeTopic()
    {
        Topic = MqttTopicValidator.Sanitize(Topic);
    }

    // Danh sách chân ngõ ra động (Dynamic Pins)
    public ObservableCollection<MqttPinBindingViewModel> Pins { get; } = new();

    public string PinsCountSummary => string.Format(LocalizationService.Tr("InspectorMqttSubOutputsSummary"), Pins.Count);

    // Live Diagnostics & Status
    [ObservableProperty]
    private bool _isLiveActive = false;

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private bool _isValidActive = false; // Xung báo có tin mới

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubscribeError))]
    private bool _isErrorActive = false;

    public bool HasSubscribeError
    {
        get => IsErrorActive;
        set => IsErrorActive = value;
    }

    [ObservableProperty]
    private MqttErrorCode _lastErrorCode = MqttErrorCode.None;

    [ObservableProperty]
    private string _lastErrorMessage = string.Empty;

    [ObservableProperty]
    private uint _msgCount = 0;

    [ObservableProperty]
    private uint _droppedCount = 0;

    [ObservableProperty]
    private string _lastReceivedPayload = "--";

    [ObservableProperty]
    private string _lastReceivedTime = "--";

    // Khung giả lập Simulation
    [ObservableProperty]
    private string _simInputPayload = "{\"grid\": 5, \"color\": 1}";

    [ObservableProperty]
    private string _simParseResultText = string.Empty;

    public ConnectorViewModel EnableConnector { get; }
    public ConnectorViewModel ValidConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }

    public bool HasTopicError => !MqttTopicValidator.Validate(Topic, out _);

    public string TopicValidationError
    {
        get
        {
            MqttTopicValidator.Validate(Topic, out string err);
            return err;
        }
    }

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"{Title} [{Topic}] (QoS{Qos}, {Pins.Count} Pins)"
        : $"{CustomLabel} [{Topic}] (QoS{Qos}, {Pins.Count} Pins)";

    public string StatusPillText
    {
        get
        {
            if (IsErrorActive) return $"ERR: {LastErrorCode}";
            if (MsgCount > 0) return $"{MsgCount} msgs";
            return "READY";
        }
    }

    public MqttSubscribeNodeViewModel(string topic = "synaptix/commands/inbound")
    {
        Title = "MQTT SUB";
        Icon = "📥";
        Topic = topic;

        // Chân vào cố định
        EnableConnector = new ConnectorViewModel
        {
            Title = "EN",
            TooltipKey = "PinTooltipMqttSubEn",
            IsInput = true,
            Node = this
        };
        InputConnectors.Add(EnableConnector);

        // Chân ra cố định
        ValidConnector = new ConnectorViewModel
        {
            Title = "Valid",
            TooltipKey = "PinTooltipMqttSubValid",
            IsInput = false,
            Node = this
        };
        OutputConnectors.Add(ValidConnector);

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipMqttSubErr",
            IsInput = false,
            Node = this
        };
        OutputConnectors.Add(ErrorConnector);

        Pins.CollectionChanged += OnPinsCollectionChanged;
    }

    private void OnPinsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (MqttPinBindingViewModel oldPin in e.OldItems)
            {
                oldPin.PropertyChanged -= OnPinPropertyChanged;
                if (oldPin.Connector != null)
                {
                    OutputConnectors.Remove(oldPin.Connector);
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (MqttPinBindingViewModel newPin in e.NewItems)
            {
                newPin.PropertyChanged += OnPinPropertyChanged;
                SynchronizeConnectorForPin(newPin);
            }
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PinsCountSummary));
    }

    private void OnPinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is MqttPinBindingViewModel pin && e.PropertyName == nameof(MqttPinBindingViewModel.Label))
        {
            if (pin.Connector != null)
            {
                pin.Connector.Title = pin.Label;
            }
        }
    }

    /// <summary>
    /// Đồng bộ chân Connector trên Canvas cho một Pin binding.
    /// </summary>
    public void SynchronizeConnectorForPin(MqttPinBindingViewModel pin)
    {
        if (pin.Connector != null && OutputConnectors.Contains(pin.Connector))
        {
            pin.Connector.Title = pin.Label;
            pin.Connector.SetBilingualDescription(
                $"Trích xuất trường JSON '{pin.JsonPath}' ({pin.DataType}) từ bản tin MQTT",
                $"Extract JSON field '{pin.JsonPath}' ({pin.DataType}) from MQTT message");
            return;
        }

        var connector = new ConnectorViewModel
        {
            Id = pin.Id,
            Title = pin.Label,
            IsInput = false,
            Node = this
        };
        connector.SetBilingualDescription(
            $"Trích xuất trường JSON '{pin.JsonPath}' ({pin.DataType}) từ bản tin MQTT",
            $"Extract JSON field '{pin.JsonPath}' ({pin.DataType}) from MQTT message");
        pin.Connector = connector;
        OutputConnectors.Add(connector);
    }

    /// <summary>
    /// Thêm một chân động mới vào khối.
    /// </summary>
    [RelayCommand]
    public void AddPin(MqttPinBindingViewModel? customPin = null)
    {
        string newLabel = customPin?.Label ?? $"data_{Pins.Count + 1}";
        var pin = customPin ?? new MqttPinBindingViewModel
        {
            Label = newLabel,
            JsonPath = newLabel,
            DataType = MqttDataType.Int16
        };

        Pins.Add(pin);
    }

    /// <summary>
    /// Xóa một chân động khỏi khối.
    /// </summary>
    [RelayCommand]
    public void RemovePin(MqttPinBindingViewModel pin)
    {
        if (pin != null && Pins.Contains(pin))
        {
            Pins.Remove(pin);
        }
    }

    /// <summary>
    /// Xử lý một gói tin MQTT nhận được theo quy chuẩn Atomic All-or-Nothing.
    /// </summary>
    public MqttParseResult ProcessInboundPayload(string payload)
    {
        LastReceivedPayload = payload;
        LastReceivedTime = DateTime.Now.ToString("HH:mm:ss");

        var pinDtos = Pins.Select(p => p.ToDto()).ToList();
        var result = MqttSubscriptionParser.Parse(payload, pinDtos, lenient: true);

        if (result.IsSuccess)
        {
            IsErrorActive = false;
            LastErrorCode = MqttErrorCode.None;
            LastErrorMessage = string.Empty;
            MsgCount++;

            // Cập nhật giá trị và đường dẫn phân giải vào các pin
            foreach (var pin in Pins)
            {
                if (result.ResolvedPaths.TryGetValue(pin.Id, out string? resPath))
                {
                    pin.ResolvedPath = resPath;
                }

                if (result.NumericValues.TryGetValue(pin.Id, out double numVal))
                {
                    pin.CurrentValue = numVal;
                    pin.CurrentStringValue = numVal.ToString(CultureInfo.InvariantCulture);
                }
                else if (result.StringValues.TryGetValue(pin.Id, out string? strVal))
                {
                    pin.CurrentStringValue = strVal;
                }
            }

            IsValidActive = true;
        }
        else
        {
            IsErrorActive = true;
            LastErrorCode = result.ErrorCode;
            LastErrorMessage = result.ErrorMessage;
            DroppedCount++;
            IsValidActive = false;
        }

        OnPropertyChanged(nameof(StatusPillText));
        return result;
    }

    /// <summary>
    /// Giả lập gửi một gói tin từ màn hình Inspector Simulator.
    /// </summary>
    [RelayCommand]
    public void SimulateSend()
    {
        SimulateSend(SimInputPayload);
    }

    public void SimulateSend(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;

        var res = ProcessInboundPayload(payload);
        if (res.IsSuccess)
        {
            SimParseResultText = $"✔ Thành công! Nhận {Pins.Count} trường.";
        }
        else
        {
            SimParseResultText = $"✘ Lỗi [{res.ErrorCode}]: {res.ErrorMessage}";
        }
    }

    public MqttSubscriptionConfigDto ToDto() => new()
    {
        NodeId = Id,
        Topic = Topic,
        Qos = Qos,
        QueueMode = QueueMode,
        QueueCapacity = QueueCapacity,
        IgnoreRetainedOnBoot = IgnoreRetainedOnBoot,
        TimeoutMs = TimeoutMs,
        FailSafeAction = FailSafeAction,
        Pins = Pins.Select(p => p.ToDto()).ToList()
    };

    public void LoadFromDto(MqttSubscriptionConfigDto dto)
    {
        Topic = dto.Topic;
        Qos = dto.Qos;
        QueueMode = dto.QueueMode;
        QueueCapacity = dto.QueueCapacity;
        IgnoreRetainedOnBoot = dto.IgnoreRetainedOnBoot;
        TimeoutMs = dto.TimeoutMs;
        FailSafeAction = dto.FailSafeAction;

        Pins.Clear();
        foreach (var pinDto in dto.Pins)
        {
            var pinVm = new MqttPinBindingViewModel();
            pinVm.LoadFromDto(pinDto);
            Pins.Add(pinVm);
        }
    }
}
