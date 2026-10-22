using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Model quan sát biểu diễn từng cổng RS-485 trong giao diện cấu hình đa cổng.
/// </summary>
public partial class ModbusPortItemViewModel : ObservableObject
{
    public byte PortIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private string _portName = "RS-485 #1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPill))]
    [NotifyPropertyChangedFor(nameof(StatusPillColor))]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private uint _baudRate = 9600;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private byte _dataBits = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private string _parity = "None"; // "None", "Even", "Odd"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private byte _stopBits = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private ushort _responseTimeoutMs = 200;

    [ObservableProperty]
    private ushort _interFrameDelayMs = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private ushort _retryCount = 1;

    public string DisplayName =>
        (SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese ? "Cổng " : "Port ") + $"{PortIndex + 1}: {PortName}";

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(StatusPill));
    }

    public string SummaryBadge =>
        $"{BaudRate} bps · {DataBits}-{Parity.Substring(0, 1).ToUpperInvariant()}-{StopBits} · Timeout {ResponseTimeoutMs}ms · Retries {RetryCount}";

    public string StatusPill => Enabled 
        ? (LocalizationService.Instance.IsVietnamese ? "HOẠT ĐỘNG" : "ACTIVE") 
        : (LocalizationService.Instance.IsVietnamese ? "VÔ HIỆU" : "DISABLED");

    public string StatusPillColor => Enabled ? "#107C41" : "#64748B";

    public ModbusPortItemViewModel(ModbusPortConfigModel model)
    {
        PortIndex = model.PortIndex;
        _portName = model.PortName;
        _enabled = model.Enabled;
        _baudRate = model.BaudRate;
        _dataBits = model.DataBits == 0 ? (byte)8 : model.DataBits;
        _parity = string.IsNullOrWhiteSpace(model.Parity) ? "None" : model.Parity;
        _stopBits = model.StopBits == 0 ? (byte)1 : model.StopBits;
        _responseTimeoutMs = model.ResponseTimeoutMs == 0 ? (ushort)200 : model.ResponseTimeoutMs;
        _interFrameDelayMs = model.InterFrameDelayMs;
        _retryCount = model.RetryCount;
    }

    public void CopyToModel(ModbusPortConfigModel model)
    {
        model.PortIndex = PortIndex;
        model.PortName = PortName;
        model.Enabled = Enabled;
        model.BaudRate = BaudRate;
        model.DataBits = DataBits;
        model.Parity = Parity;
        model.StopBits = StopBits;
        model.ResponseTimeoutMs = ResponseTimeoutMs;
        model.InterFrameDelayMs = InterFrameDelayMs;
        model.RetryCount = RetryCount;
    }
}

/// <summary>
/// ViewModel điều khiển hộp thoại cấu hình Đa cổng RS-485 Modbus Master (SPLC-SPEC-MBM-003).
/// Cho phép kỹ sư cấu hình độc lập 4 cổng UART/RS-485: Baud rate, Parity, Stop bits, Timeout và Retries.
/// </summary>
public partial class ModbusPortConfigViewModel : ObservableObject
{
    private readonly ProjectMetadata _metadata;

    public ObservableCollection<ModbusPortItemViewModel> Ports { get; } = new();

    [ObservableProperty]
    private ModbusPortItemViewModel? _selectedPort;

    public IReadOnlyList<uint> AvailableBaudRates { get; } = new uint[]
    {
        4800, 9600, 19200, 38400, 57600, 115200
    };

    public IReadOnlyList<string> AvailableParities { get; } = new string[]
    {
        "None", "Even", "Odd"
    };

    public IReadOnlyList<byte> AvailableStopBits { get; } = new byte[]
    {
        1, 2
    };

    public IReadOnlyList<byte> AvailableDataBits { get; } = new byte[]
    {
        8, 7
    };

    [ObservableProperty]
    private bool _hasPhysicalPorts = true;

    public event Action? RequestClose;

    public ModbusPortConfigViewModel(ProjectMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        int physicalLimit = _metadata.PhysicalRs485PortCount;
        if (physicalLimit > 4) physicalLimit = 4;

        if (_metadata.ModbusPorts == null || _metadata.ModbusPorts.Count == 0)
        {
            _metadata.ModbusPorts = new List<ModbusPortConfigModel>();
            for (int i = 0; i < physicalLimit; i++)
            {
                _metadata.ModbusPorts.Add(new ModbusPortConfigModel
                {
                    PortIndex = (byte)i,
                    PortName = $"RS-485 #{i + 1}",
                    Enabled = i < 2,
                    BaudRate = i == 1 ? 19200u : 9600u,
                    DataBits = 8,
                    Parity = i == 1 ? "Even" : "None",
                    StopBits = 1,
                    ResponseTimeoutMs = 200,
                    InterFrameDelayMs = 20,
                    RetryCount = 1
                });
            }
        }
        else
        {
            while (_metadata.ModbusPorts.Count < physicalLimit)
            {
                int idx = _metadata.ModbusPorts.Count;
                _metadata.ModbusPorts.Add(new ModbusPortConfigModel
                {
                    PortIndex = (byte)idx,
                    PortName = $"RS-485 #{idx + 1}",
                    Enabled = false,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = "None",
                    StopBits = 1,
                    ResponseTimeoutMs = 200,
                    InterFrameDelayMs = 20,
                    RetryCount = 1
                });
            }
        }

        for (int i = 0; i < physicalLimit && i < _metadata.ModbusPorts.Count; i++)
        {
            Ports.Add(new ModbusPortItemViewModel(_metadata.ModbusPorts[i]));
        }

        HasPhysicalPorts = Ports.Count > 0;
        SelectedPort = Ports.FirstOrDefault();
        SimplePLC.Studio.Services.LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        foreach (var port in Ports)
        {
            port.NotifyLanguageChanged();
        }
    }

    [RelayCommand]
    public void Apply()
    {
        for (int i = 0; i < Ports.Count && i < _metadata.ModbusPorts.Count; i++)
        {
            Ports[i].CopyToModel(_metadata.ModbusPorts[i]);
        }
        _metadata.LastModified = DateTime.UtcNow;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        if (SelectedPort != null)
        {
            SelectedPort.BaudRate = SelectedPort.PortIndex == 1 ? 19200u : 9600u;
            SelectedPort.Parity = SelectedPort.PortIndex == 1 ? "Even" : "None";
            SelectedPort.StopBits = 1;
            SelectedPort.DataBits = 8;
            SelectedPort.ResponseTimeoutMs = 200;
            SelectedPort.InterFrameDelayMs = 20;
            SelectedPort.RetryCount = 1;
            SelectedPort.PortName = $"RS-485 #{SelectedPort.PortIndex + 1}";
        }
    }
}
