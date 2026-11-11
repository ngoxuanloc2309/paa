using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using SimplePLC.Application.Abstractions;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Modbus;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Trạng thái hiển thị theo chuẩn ANSI/ISA-101 của công cụ đọc thử Modbus.
/// </summary>
public enum LiveProbeStatusState
{
    Idle,
    Success,
    Warning,
    Danger
}

/// <summary>
/// ViewModel quản lý Bảng Giám Sát Cảm Biến Qua Thiết Bị (Device Sensor Diagnostics &amp; Inspector)
/// tại cột Inspector bên phải của màn hình cấu hình Modbus.
/// </summary>
public partial class ModbusLiveTestInspectorViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IModbusLiveProbeService _probeService;
    private readonly IComPortDiscoveryService? _portDiscoveryService;
    private readonly IRuntimeStateStore? _stateStore;
    private CancellationTokenSource? _pollingCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTargetChannel))]
    [NotifyPropertyChangedFor(nameof(TargetSummary))]
    [NotifyPropertyChangedFor(nameof(TargetTagDisplay))]
    [NotifyPropertyChangedFor(nameof(TargetTagAlias))]
    [NotifyPropertyChangedFor(nameof(TargetDataType))]
    [NotifyPropertyChangedFor(nameof(TargetScaleDisplay))]
    private ModbusMasterChannelItemViewModel? _targetChannel;

    partial void OnTargetChannelChanged(ModbusMasterChannelItemViewModel? oldValue, ModbusMasterChannelItemViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTargetChannelPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnTargetChannelPropertyChanged;
        }

        RefreshTargetChannelBindings();
        ResetReadings();
    }

    private void OnTargetChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshTargetChannelBindings();
    }

    private void RefreshTargetChannelBindings()
    {
        OnPropertyChanged(nameof(HasTargetChannel));
        OnPropertyChanged(nameof(TargetSummary));
        OnPropertyChanged(nameof(TargetTagDisplay));
        OnPropertyChanged(nameof(TargetTagAlias));
        OnPropertyChanged(nameof(TargetDataType));
        OnPropertyChanged(nameof(TargetScaleDisplay));
        ReadOnceCommand.NotifyCanExecuteChanged();
        ToggleContinuousPollingCommand.NotifyCanExecuteChanged();
    }

    public bool HasTargetChannel => TargetChannel != null;

    public string TargetSummary => TargetChannel != null
        ? $"Slave {TargetChannel.SlaveId} · {TargetChannel.FunctionCodeBadge} · {TargetChannel.Address} ({TargetChannel.TargetTagDisplay})"
        : LocalizationService.Instance["LiveTestNoChannelSelected"];

    public string TargetTagDisplay => TargetChannel?.TargetTagDisplay ?? "—";
    public string TargetTagAlias => !string.IsNullOrWhiteSpace(TargetChannel?.TargetTagAlias) ? TargetChannel.TargetTagAlias : "—";
    public string TargetDataType => TargetChannel != null ? $"{TargetChannel.DataType} ({TargetChannel.Endianness})" : "—";
    public string TargetScaleDisplay => TargetChannel != null ? $"× {TargetChannel.Scale:G6}" : "1:1";

    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private uint _baudRate = 9600;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceConnectionBadge))]
    private bool _isPortConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceConnectionBadge))]
    private bool _isDeviceConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceConnectionBadge))]
    private string _devicePortName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceConnectionBadge))]
    private string _deviceModelName = "SYNX-DTU-485";

    public string DeviceConnectionBadge => IsDeviceConnected || IsPortConnected
        ? $"● {(string.IsNullOrWhiteSpace(DevicePortName) ? SelectedPort : DevicePortName)} · {DeviceModelName}"
        : LocalizationService.Instance["InspectorSensorStatusOffline"];

    [ObservableProperty]
    private bool _isConfigDeployed = true;

    [ObservableProperty]
    private bool _isProbing;

    [ObservableProperty]
    private bool _isPollingActive;

    [ObservableProperty]
    private string _liveScaledValue = "—";

    [ObservableProperty]
    private string _liveRawValue = "—";

    [ObservableProperty]
    private string _responseLatency = "—";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _troubleshootingHint = string.Empty;

    [ObservableProperty]
    private LiveProbeStatusState _statusState = LiveProbeStatusState.Idle;

    public ModbusLiveTestInspectorViewModel(
        IModbusLiveProbeService? probeService = null,
        IComPortDiscoveryService? portDiscoveryService = null,
        IRuntimeStateStore? stateStore = null)
    {
        _probeService = probeService ?? new ModbusLiveProbeService();
        _portDiscoveryService = portDiscoveryService;
        _stateStore = stateStore ?? AppServices.Instance?.StateStore;
        if (_stateStore != null)
        {
            _stateStore.SnapshotUpdated += OnSnapshotUpdated;
        }

        _statusMessage = LocalizationService.Instance["LiveTestStatusIdle"];
        if (_portDiscoveryService != null)
        {
            RefreshPorts();
        }
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }
    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(InspectorHeaderTitle));
        OnPropertyChanged(nameof(InspectorHeaderSubtitle));
        OnPropertyChanged(nameof(TargetSummary));
        OnPropertyChanged(nameof(SelectedRegisterOrChannelSummary));
        OnPropertyChanged(nameof(SlaveAccessRightDisplay));
        OnPropertyChanged(nameof(SlaveFunctionCodeDisplay));
        OnPropertyChanged(nameof(SlaveDescriptionDisplay));
        OnPropertyChanged(nameof(SlaveScadaLengthDisplay));
        OnPropertyChanged(nameof(SlaveScadaMappingGuide));
        OnPropertyChanged(nameof(SlaveServerStatusBadge));
        OnPropertyChanged(nameof(DeviceConnectionBadge));

        SelectedSlaveRegister?.NotifyLanguageChanged();

        if (!IsDeviceConnected)
        {
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusOffline"];
            TroubleshootingHint = LocalizationService.Instance["InspectorSensorOfflineHint"];
        }
        else if (!IsConfigDeployed)
        {
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusPendingDeploy"];
            TroubleshootingHint = LocalizationService.Instance["InspectorSensorPendingDeployHint"];
        }
        else if (TargetChannel == null && StatusState == LiveProbeStatusState.Idle)
        {
            StatusMessage = LocalizationService.Instance["LiveTestStatusIdle"];
        }
    }


    /// <summary>
    /// Cập nhật trạng thái phần cứng thời gian thực từ DtuGatewayStudioViewModel / MainViewModel.
    /// </summary>
    public void UpdateDeviceConnection(bool isConnected, string? port, string? model, bool isDeployed = true)
    {
        IsDeviceConnected = isConnected;
        IsPortConnected = isConnected;
        DevicePortName = port ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(port))
        {
            SelectedPort = port;
        }
        DeviceModelName = string.IsNullOrWhiteSpace(model) ? "SYNX-DTU-485" : model;
        IsConfigDeployed = isDeployed;

        if (!isConnected)
        {
            StatusState = LiveProbeStatusState.Idle;
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusOffline"];
            TroubleshootingHint = LocalizationService.Instance["InspectorSensorOfflineHint"];
        }
        else if (!isDeployed)
        {
            StatusState = LiveProbeStatusState.Warning;
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusPendingDeploy"];
            TroubleshootingHint = LocalizationService.Instance["InspectorSensorPendingDeployHint"];
        }
        else
        {
            StatusState = LiveProbeStatusState.Idle;
            StatusMessage = LocalizationService.Instance["LiveTestStatusGood"];
            TroubleshootingHint = string.Empty;
        }
    }

    // =========================================================================
    // MODBUS SLAVE REGISTER INSPECTOR PROPERTIES (DIRECTION A)
    // =========================================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMasterMode))]
    [NotifyPropertyChangedFor(nameof(InspectorHeaderTitle))]
    [NotifyPropertyChangedFor(nameof(InspectorHeaderSubtitle))]
    [NotifyPropertyChangedFor(nameof(SelectedRegisterOrChannelSummary))]
    private bool _isSlaveMode;

    public bool IsMasterMode => !IsSlaveMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlaveServerStatusBadge))]
    [NotifyPropertyChangedFor(nameof(SlaveScadaQueryWireHex))]
    [NotifyPropertyChangedFor(nameof(SlavePlcResponseWireHex))]
    private byte _slaveId = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSlaveRegister))]
    [NotifyPropertyChangedFor(nameof(SlaveRegisterAddressDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveDeviceTagDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveDataTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveAccessRightDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveFunctionCodeDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveDescriptionDisplay))]
    [NotifyPropertyChangedFor(nameof(SlaveScadaQueryWireHex))]
    [NotifyPropertyChangedFor(nameof(SlavePlcResponseWireHex))]
    [NotifyPropertyChangedFor(nameof(SelectedRegisterOrChannelSummary))]
    private ModbusSlaveFixedRegisterViewModel? _selectedSlaveRegister;

    partial void OnSelectedSlaveRegisterChanged(ModbusSlaveFixedRegisterViewModel? oldValue, ModbusSlaveFixedRegisterViewModel? newValue)
    {
        OnPropertyChanged(nameof(HasSelectedSlaveRegister));
        OnPropertyChanged(nameof(SelectedRegisterOrChannelSummary));
        OnPropertyChanged(nameof(SlaveRegisterAddressDisplay));
        OnPropertyChanged(nameof(SlaveDeviceTagDisplay));
        OnPropertyChanged(nameof(SlaveDataTypeDisplay));
        OnPropertyChanged(nameof(SlaveAccessRightDisplay));
        OnPropertyChanged(nameof(SlaveFunctionCodeDisplay));
        OnPropertyChanged(nameof(SlaveDescriptionDisplay));
        OnPropertyChanged(nameof(SlaveScadaAddress1Based));
        OnPropertyChanged(nameof(SlaveScadaOffset0Based));
        OnPropertyChanged(nameof(SlaveScadaLengthDisplay));
        OnPropertyChanged(nameof(SlaveScadaMappingGuide));
        OnPropertyChanged(nameof(SlaveServerStatusBadge));
        OnPropertyChanged(nameof(SlaveScadaQueryWireHex));
        OnPropertyChanged(nameof(SlavePlcResponseWireHex));
        UpdateSlaveLiveValue();
    }

    public bool HasSelectedSlaveRegister => SelectedSlaveRegister != null;

    public string InspectorHeaderTitle => IsSlaveMode
        ? LocalizationService.Instance["DtuInspectorSlaveHeaderTitle"]
        : LocalizationService.Instance["DtuInspectorHeaderTitle"];

    public string InspectorHeaderSubtitle => IsSlaveMode
        ? string.Format(LocalizationService.Instance["DtuInspectorSlaveHeaderSubtitle"], SlaveId, BaudRate)
        : LocalizationService.Instance["DtuInspectorHeaderSubtitle"];

    public string SelectedRegisterOrChannelSummary => IsSlaveMode
        ? (SelectedSlaveRegister != null ? $"{SelectedSlaveRegister.ModbusAddress} · {SelectedSlaveRegister.DeviceTag}" : LocalizationService.Instance["ModbusSlaveInspectorNoRegSelected"])
        : TargetSummary;

    public string SlaveRegisterAddressDisplay => SelectedSlaveRegister != null
        ? $"{SelectedSlaveRegister.ModbusAddress} ({SelectedSlaveRegister.AddressHex})"
        : "—";

    public string SlaveScadaAddress1Based => SelectedSlaveRegister?.ModbusAddress ?? "—";

    public string SlaveScadaOffset0Based => SelectedSlaveRegister != null
        ? $"{SelectedSlaveRegister.StartOffset} (0x{SelectedSlaveRegister.StartOffset:X4})"
        : "—";

    public string SlaveDeviceTagDisplay => SelectedSlaveRegister?.DeviceTag ?? "—";
    public string SlaveDataTypeDisplay => SelectedSlaveRegister?.DataType ?? "—";
    public string SlaveAccessRightDisplay => SelectedSlaveRegister != null
        ? (SelectedSlaveRegister.IsReadOnly ? "Read-Only (RO)" : "Read/Write (RW)")
        : "—";
    public string SlaveFunctionCodeDisplay => SelectedSlaveRegister?.FunctionCode ?? "—";
    public string SlaveDescriptionDisplay => SelectedSlaveRegister?.Description ?? "—";

    public string SlaveScadaLengthDisplay
    {
        get
        {
            if (SelectedSlaveRegister == null) return "—";
            ushort count = SelectedSlaveRegister.RegisterCount;
            byte fc = SelectedSlaveRegister.PrimaryFunctionCode;
            if (fc == 0x01 || fc == 0x02)
            {
                return $"{count} Bits";
            }
            if (count == 1)
            {
                return "1 Reg (16-bit ABCD)";
            }
            return $"{count} Regs ({count * 16}-bit ABCD)";
        }
    }

    public string SlaveScadaMappingGuide
    {
        get
        {
            if (SelectedSlaveRegister == null) return "—";
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string tag = SelectedSlaveRegister.PrimaryTagName ?? string.Empty;

            if (tag.Equals("DI_WORD", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• Bit 0 = DI0, Bit 1 = DI1 ... Bit 15 = DI15\n• 1 = Ngõ vào Kích hoạt (ON), 0 = Tắt (OFF)\n• SCADA đọc 1 lần lấy toàn bộ 16 ngõ vào số."
                    : "• Bit 0 = DI0, Bit 1 = DI1 ... Bit 15 = DI15\n• 1 = Input Active (ON), 0 = Inactive (OFF)\n• SCADA polls once to acquire all 16 digital inputs.";
            }
            if (tag.Equals("DO_WORD", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• Bit 0 = DO0, Bit 1 = DO1 ... Bit 15 = DO15\n• 1 = Đóng Relay (ON), 0 = Ngắt Relay (OFF)\n• Cho phép SCADA ghi đồng thời 16 ngõ ra qua FC 06/16."
                    : "• Bit 0 = DO0, Bit 1 = DO1 ... Bit 15 = DO15\n• 1 = Relay Energized (ON), 0 = De-energized (OFF)\n• Allows SCADA to write all 16 outputs via FC 06/16.";
            }
            if (tag.Equals("VFLAG_DWORD", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• Bit 0 = VFLAG0 ... Bit 31 = VFLAG31 (32 cờ logic)\n• Chiếm 2 thanh ghi: 40003 (High) và 40004 (Low)\n• Đọc/ghi qua FC 03, FC 06, FC 16."
                    : "• Bit 0 = VFLAG0 ... Bit 31 = VFLAG31 (32 logic flags)\n• Spans 2 regs: 40003 (High) and 40004 (Low)\n• Read/Write via FC 03, FC 06, FC 16.";
            }
            if (tag.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 4 kênh Analog Float32 (IEEE-754, High Word First):\n  - AI0: 40017 .. 40018\n  - AI1: 40019 .. 40020\n  - AI2: 40021 .. 40022\n  - AI3: 40023 .. 40024"
                    : "• 4 Analog Channels Float32 (IEEE-754, High Word First):\n  - AI0: 40017 .. 40018\n  - AI1: 40019 .. 40020\n  - AI2: 40021 .. 40022\n  - AI3: 40023 .. 40024";
            }
            if (tag.StartsWith("VREG_R", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 32 thanh ghi lưu khi mất điện (Retentive Flash)\n• Offset: 40097 + (Index * 2)\n• Tự động khôi phục giá trị cài đặt sau khi khởi động."
                    : "• 32 Retentive Non-Volatile Registers (Power-off safe)\n• Offset: 40097 + (Index * 2)\n• Automatically restores setpoints after power cycle.";
            }
            if (tag.StartsWith("VREG", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 32 thanh ghi nội bộ 32-bit (Int32 / Float32 IEEE-754)\n• Offset: 40033 + (Index * 2)\n• Hỗ trợ đọc/ghi đồng bộ giữa SCADA và PLC logic."
                    : "• 32 Virtual Registers 32-bit (Int32 / Float32 IEEE-754)\n• Offset: 40033 + (Index * 2)\n• Supports sync read/write between SCADA and PLC logic.";
            }
            if (tag.StartsWith("COUNTER", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 8 bộ đếm sự kiện/xung (Int32 PV, 2 thanh ghi/bộ đếm)\n• Offset: 40161 + (Index * 2)\n• Đọc/ghi giá trị đếm qua FC 03, FC 06, FC 16."
                    : "• 8 Event/Pulse Counters (Int32 PV, 2 regs per counter)\n• Offset: 40161 + (Index * 2)\n• Read/Write counter value via FC 03, FC 06, FC 16.";
            }
            if (tag.StartsWith("SYS_DIAG", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• Thông số chẩn đoán hệ thống SynaptiX:\n  - 40241: Uptime giây (UINT32, 2 regs)\n  - 40243: Chu kỳ quét scan ms (UINT16)\n  - 40244: Mã trạng thái CPU Status (UINT16)\n  - 40245: Đồng hồ RTC timestamp (UINT32, 2 regs)"
                    : "• SynaptiX System Diagnostics:\n  - 40241: Uptime seconds (UINT32, 2 regs)\n  - 40243: Scan cycle ms (UINT16)\n  - 40244: CPU Status code (UINT16)\n  - 40245: RTC Timestamp (UINT32, 2 regs)";
            }
            if (tag.StartsWith("DI", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 16 bit trạng thái ngõ vào số riêng lẻ (10001 .. 10016)\n• Đọc bằng mã hàm FC 02 (Read Discrete Inputs)\n• 1 = Kích hoạt (Active), 0 = Không kích hoạt."
                    : "• 16 discrete digital input bits (10001 .. 10016)\n• Read via Function Code FC 02 (Read Discrete Inputs)\n• 1 = Active, 0 = Inactive.";
            }
            if (tag.StartsWith("DO", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 16 bit điều khiển Relay riêng lẻ (00001 .. 00016)\n• Đọc bằng FC 01, điều khiển từng Relay bằng FC 05, nhiều Relay bằng FC 15."
                    : "• 16 individual Relay output coils (00001 .. 00016)\n• Read via FC 01, write single coil via FC 05, multiple via FC 15.";
            }
            if (tag.StartsWith("VFLAG", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? "• 32 bit cờ logic nội bộ riêng lẻ (00101 .. 00132)\n• Đọc bằng FC 01, điều khiển bằng FC 05 / 15."
                    : "• 32 individual logic flag coils (00101 .. 00132)\n• Read via FC 01, write via FC 05 / 15.";
            }

            return SelectedSlaveRegister.Description ?? "—";
        }
    }

    public string SlaveServerStatusBadge =>
        string.Format(LocalizationService.Instance["ModbusSlaveInspectorServerBadge"], SlaveId, BaudRate);

    public string SlaveScadaQueryWireHex
    {
        get
        {
            if (SelectedSlaveRegister == null) return "—";
            ushort offset = SelectedSlaveRegister.StartOffset;
            ushort count = SelectedSlaveRegister.RegisterCount;
            byte fc = SelectedSlaveRegister.PrimaryFunctionCode;
            return $"[ {SlaveId:X2} ] [ {fc:X2} ] [ {offset >> 8:X2} {offset & 0xFF:X2} ] [ {count >> 8:X2} {count & 0xFF:X2} ] [ CRC ]";
        }
    }

    public string SlavePlcResponseWireHex
    {
        get
        {
            if (SelectedSlaveRegister == null) return "—";
            ushort count = SelectedSlaveRegister.RegisterCount;
            byte fc = SelectedSlaveRegister.PrimaryFunctionCode;
            int byteCount = fc switch
            {
                0x01 or 0x02 => (count + 7) / 8,
                _ => count * 2
            };
            return $"[ {SlaveId:X2} ] [ {fc:X2} ] [ {byteCount:X2} ] [ DATA x{byteCount}B ] [ CRC ]";
        }
    }

    [ObservableProperty]
    private string _slaveLiveMemoryValue = "—";

    [ObservableProperty]
    private string _slaveLiveMemoryFormat = "—";

    private void OnSnapshotUpdated(SimplePLC.Application.Models.RuntimeDeviceSnapshot snapshot)
    {
        if (IsSlaveMode && SelectedSlaveRegister != null)
        {
            UpdateSlaveLiveValue();
        }
    }

    public void UpdateSlaveLiveValue()
    {
        if (SelectedSlaveRegister == null)
        {
            SlaveLiveMemoryValue = "—";
            SlaveLiveMemoryFormat = LocalizationService.Instance["ModbusSlaveInspectorNoRegSelected"];
            return;
        }

        var store = _stateStore;
        if (store == null)
        {
            SlaveLiveMemoryValue = "0";
            SlaveLiveMemoryFormat = "Standby (Offline)";
            return;
        }

        string fullTag = SelectedSlaveRegister.DeviceTag ?? string.Empty;

        try
        {
            if (fullTag.Equals("DI_WORD", StringComparison.OrdinalIgnoreCase))
            {
                ushort bitmask = 0;
                var allTags = store.GetAllTags();
                for (ushort i = 0; i < 16; i++)
                {
                    string name = $"DI{i}";
                    var t = allTags.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (t != null && t.RawValue != 0)
                        bitmask |= (ushort)(1 << i);
                }
                SlaveLiveMemoryValue = $"0x{bitmask:X4}";
                SlaveLiveMemoryFormat = $"16-bit Bitmask · {bitmask} (Dec)";
            }
            else if (fullTag.Equals("DO_WORD", StringComparison.OrdinalIgnoreCase))
            {
                ushort bitmask = 0;
                var allTags = store.GetAllTags();
                for (ushort i = 0; i < 16; i++)
                {
                    string name = $"DO{i}";
                    var t = allTags.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (t != null && t.RawValue != 0)
                        bitmask |= (ushort)(1 << i);
                }
                SlaveLiveMemoryValue = $"0x{bitmask:X4}";
                SlaveLiveMemoryFormat = $"16-bit Bitmask · {bitmask} (Dec)";
            }
            else if (fullTag.Equals("VFLAG_DWORD", StringComparison.OrdinalIgnoreCase))
            {
                uint bitmask = 0;
                var allTags = store.GetAllTags();
                for (ushort i = 0; i < 32; i++)
                {
                    string name = $"VFLAG{i}";
                    var t = allTags.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (t != null && t.RawValue != 0)
                        bitmask |= (1u << i);
                }
                SlaveLiveMemoryValue = $"0x{bitmask:X8}";
                SlaveLiveMemoryFormat = $"32-bit Bitmask · {bitmask} (Dec)";
            }
            else if (fullTag.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            {
                var allTags = store.GetAllTags();
                var t0 = allTags.FirstOrDefault(x => x.Name.Equals("AI0", StringComparison.OrdinalIgnoreCase));
                float f0 = t0 != null ? BitConverter.Int32BitsToSingle(t0.RawValue) : 0f;
                SlaveLiveMemoryValue = $"{f0:F2}";
                SlaveLiveMemoryFormat = $"AI0 Float32 (IEEE-754) · Raw: 0x{(t0?.RawValue ?? 0):X8}";
            }
            else if (fullTag.StartsWith("VREG", StringComparison.OrdinalIgnoreCase))
            {
                var allTags = store.GetAllTags();
                var t0 = allTags.FirstOrDefault(x => x.Name.Equals("VREG0", StringComparison.OrdinalIgnoreCase));
                SlaveLiveMemoryValue = t0 != null ? t0.RawValue.ToString() : "0";
                SlaveLiveMemoryFormat = "VREG0 32-bit Register Value";
            }
            else if (fullTag.StartsWith("COUNTER", StringComparison.OrdinalIgnoreCase))
            {
                var allTags = store.GetAllTags();
                var t0 = allTags.FirstOrDefault(x => x.Name.Equals("COUNTER0", StringComparison.OrdinalIgnoreCase));
                SlaveLiveMemoryValue = t0 != null ? t0.RawValue.ToString() : "0";
                SlaveLiveMemoryFormat = "COUNTER0 PV (Int32)";
            }
            else if (fullTag.StartsWith("SYS_DIAG", StringComparison.OrdinalIgnoreCase))
            {
                SlaveLiveMemoryValue = "NORMAL";
                SlaveLiveMemoryFormat = "System Diagnostics Normal";
            }
            else if (fullTag.StartsWith("DI", StringComparison.OrdinalIgnoreCase))
            {
                var allTags = store.GetAllTags();
                var t0 = allTags.FirstOrDefault(x => x.Name.Equals("DI0", StringComparison.OrdinalIgnoreCase));
                int val = (t0 != null && t0.RawValue != 0) ? 1 : 0;
                SlaveLiveMemoryValue = val.ToString();
                SlaveLiveMemoryFormat = $"DI0 (Discrete Input): {(val == 1 ? "HIGH" : "LOW")}";
            }
            else if (fullTag.StartsWith("DO", StringComparison.OrdinalIgnoreCase))
            {
                var allTags = store.GetAllTags();
                var t0 = allTags.FirstOrDefault(x => x.Name.Equals("DO0", StringComparison.OrdinalIgnoreCase));
                int val = (t0 != null && t0.RawValue != 0) ? 1 : 0;
                SlaveLiveMemoryValue = val.ToString();
                SlaveLiveMemoryFormat = $"DO0 (Coil Output): {(val == 1 ? "ENERGIZED" : "DE-ENERGIZED")}";
            }
            else
            {
                SlaveLiveMemoryValue = "0";
                SlaveLiveMemoryFormat = "Standard Register Standby";
            }
        }
        catch
        {
            SlaveLiveMemoryValue = "0";
            SlaveLiveMemoryFormat = "Standby";
        }
    }


    [RelayCommand]
    public void RefreshPorts()
    {
        if (_portDiscoveryService == null) return;

        var current = SelectedPort;
        AvailablePorts.Clear();

        try
        {
            var ports = _portDiscoveryService.GetAvailablePorts();
            foreach (var p in ports)
            {
                AvailablePorts.Add(p);
            }

            if (current != null && AvailablePorts.Contains(current))
            {
                SelectedPort = current;
            }
            else if (AvailablePorts.Count > 0)
            {
                SelectedPort = AvailablePorts[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            StatusState = LiveProbeStatusState.Warning;
        }
    }

    [RelayCommand]
    public async Task TogglePortConnectionAsync()
    {
        if (IsPortConnected)
        {
            await DisconnectPortAsync().ConfigureAwait(false);
        }
        else
        {
            await ConnectPortAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ConnectPortAsync()
    {
        string portToOpen = !string.IsNullOrWhiteSpace(SelectedPort) ? SelectedPort : DevicePortName;
        if (string.IsNullOrWhiteSpace(portToOpen)) return;

        try
        {
            await _probeService.OpenPortAsync(portToOpen, (int)BaudRate).ConfigureAwait(false);
            IsPortConnected = _probeService.IsPortOpen;
            IsDeviceConnected = _probeService.IsPortOpen;
            StatusMessage = LocalizationService.Instance["LiveTestPortConnected"];
            StatusState = LiveProbeStatusState.Idle;
            TroubleshootingHint = string.Empty;
        }
        catch (Exception ex)
        {
            IsPortConnected = false;
            IsDeviceConnected = false;
            StatusMessage = ex.Message;
            StatusState = LiveProbeStatusState.Danger;
            TroubleshootingHint = "Không thể mở cổng kết nối. Kiểm tra xem có phần mềm khác đang chiếm cổng không.";
        }
    }

    [RelayCommand]
    public async Task DisconnectPortAsync()
    {
        StopPolling();
        try
        {
            await _probeService.ClosePortAsync().ConfigureAwait(false);
        }
        finally
        {
            IsPortConnected = false;
            IsDeviceConnected = false;
            StatusMessage = LocalizationService.Instance["LiveTestPortDisconnected"];
            StatusState = LiveProbeStatusState.Idle;
            TroubleshootingHint = string.Empty;
        }
    }

    [RelayCommand]
    public async Task ReadOnceAsync()
    {
        if (TargetChannel == null)
        {
            StatusState = LiveProbeStatusState.Idle;
            StatusMessage = LocalizationService.Instance["LiveTestNoChannelSelected"];
            return;
        }

        if (!IsDeviceConnected && !IsPortConnected)
        {
            StatusState = LiveProbeStatusState.Warning;
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusOffline"];
            TroubleshootingHint = LocalizationService.Instance["InspectorSensorOfflineHint"];
            return;
        }

        if (IsProbing) return;

        // Auto-open port if not open yet
        string portToUse = !string.IsNullOrWhiteSpace(SelectedPort) ? SelectedPort : DevicePortName;
        if (!_probeService.IsPortOpen && !string.IsNullOrWhiteSpace(portToUse))
        {
            try
            {
                await _probeService.OpenPortAsync(portToUse, (int)BaudRate).ConfigureAwait(false);
                IsPortConnected = _probeService.IsPortOpen;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                StatusState = LiveProbeStatusState.Danger;
                TroubleshootingHint = "Không thể kết nối cổng COM tới thiết bị.";
                return;
            }
        }

        IsProbing = true;
        try
        {
            var result = await _probeService.ProbeAsync(
                TargetChannel.SlaveId,
                TargetChannel.FunctionCode,
                TargetChannel.Address,
                TargetChannel.DataType,
                TargetChannel.Endianness,
                TargetChannel.Scale).ConfigureAwait(false);

            ApplyProbeResult(result);
        }
        finally
        {
            IsProbing = false;
        }
    }

    [RelayCommand]
    public void ToggleContinuousPolling()
    {
        if (IsPollingActive)
        {
            StopPolling();
        }
        else
        {
            StartPolling();
        }
    }

    private void StartPolling()
    {
        if ((!IsPortConnected && !IsDeviceConnected) || TargetChannel == null) return;

        IsPollingActive = true;
        _pollingCts = new CancellationTokenSource();
        var ct = _pollingCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && (IsPortConnected || IsDeviceConnected))
            {
                if (TargetChannel != null && !IsProbing)
                {
                    IsProbing = true;
                    try
                    {
                        var result = await _probeService.ProbeAsync(
                            TargetChannel.SlaveId,
                            TargetChannel.FunctionCode,
                            TargetChannel.Address,
                            TargetChannel.DataType,
                            TargetChannel.Endianness,
                            TargetChannel.Scale,
                            timeoutMs: 900,
                            cancellationToken: ct).ConfigureAwait(false);

                        ApplyProbeResult(result);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    finally
                    {
                        IsProbing = false;
                    }
                }

                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            IsPollingActive = false;
        }, ct);
    }

    private void StopPolling()
    {
        if (_pollingCts != null)
        {
            try { _pollingCts.Cancel(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown */ }
            try { _pollingCts.Dispose(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown */ }
            _pollingCts = null;
        }
        IsPollingActive = false;
    }

    private void ApplyProbeResult(ModbusProbeResult result)
    {
        if (result.IsSuccess)
        {
            LiveScaledValue = result.ScaledValue.HasValue ? $"{result.ScaledValue.Value:G6}" : "—";
            LiveRawValue = result.RawValueDisplay;
            ResponseLatency = string.Format(LocalizationService.Instance["LiveTestLatencyFmt"], result.LatencyMs);
            StatusMessage = LocalizationService.Instance["InspectorSensorStatusGood"];
            TroubleshootingHint = string.Empty;
            StatusState = LiveProbeStatusState.Success;
        }
        else
        {
            LiveRawValue = "—";
            ResponseLatency = result.LatencyMs > 0 ? $"{result.LatencyMs} ms" : "—";
            StatusMessage = result.FaultKind switch
            {
                ModbusProbeFaultKind.Timeout => LocalizationService.Instance["InspectorSensorStatusTimeout"],
                ModbusProbeFaultKind.CrcError => LocalizationService.Instance["LiveTestErrorCrc"],
                _ => result.DiagnosticMessage
            };
            TroubleshootingHint = result.FaultKind switch
            {
                ModbusProbeFaultKind.Timeout => LocalizationService.Instance["InspectorSensorTimeoutHint"],
                ModbusProbeFaultKind.CrcError => LocalizationService.Instance["LiveTestErrorCrcHint"],
                _ => result.TroubleshootingHint
            };
            StatusState = result.FaultKind switch
            {
                ModbusProbeFaultKind.Timeout => LiveProbeStatusState.Danger,
                ModbusProbeFaultKind.CrcError => LiveProbeStatusState.Warning,
                ModbusProbeFaultKind.ModbusException => LiveProbeStatusState.Warning,
                _ => LiveProbeStatusState.Danger
            };
        }
    }

    private void ResetReadings()
    {
        LiveScaledValue = "—";
        LiveRawValue = "—";
        ResponseLatency = "—";
    }

    public async ValueTask DisposeAsync()
    {
        StopPolling();
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        if (_stateStore != null)
        {
            _stateStore.SnapshotUpdated -= OnSnapshotUpdated;
        }
        if (TargetChannel != null)
        {
            TargetChannel.PropertyChanged -= OnTargetChannelPropertyChanged;
            TargetChannel = null;
        }
        await _probeService.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
