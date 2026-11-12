using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel quản lý toàn bộ cấu hình, trạng thái vận hành và chẩn đoán của DTU Gateway Studio.
/// </summary>
public sealed partial class DtuGatewayStudioViewModel : ObservableObject
{
    private readonly ProjectMetadata _metadata;
    /// <summary>
    /// Danh mục Tag toàn dự án dùng để tra cứu hoặc tạo mới Tag khi sinh FBD.
    /// </summary>
    public TagCatalogViewModel? TagCatalog { get; set; }

    /// <summary>
    /// ViewModel bộ soạn thảo FBD Logic dùng để tự động thả và nối dây các khối Gateway.
    /// </summary>
    public LogicEditorViewModel? LogicEditor { get; set; }

    /// <summary>
    /// Callback yêu cầu đóng cửa sổ Wizard (truyền true nếu hoàn tất nạp/sinh FBD thành công).
    /// </summary>
    public Action<bool>? RequestCloseDialog { get; set; }

    /// <summary>
    /// Callback yêu cầu đọc cấu hình trực tiếp từ phần cứng DTU.
    /// </summary>
    public Func<Task>? UploadFromDeviceRequested { get; set; }

    /// <summary>
    /// Callback mở hộp thoại trình hướng dẫn nhanh 1-2-3 (Quick Wizard).
    /// </summary>
    public Action? OpenQuickWizardRequested { get; set; }

    // --- HARDWARE CONNECTION & ACTION BAR STATE ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedRssi))]
    [NotifyPropertyChangedFor(nameof(SignalQualityPercent))]
    private bool _isHardwareConnected;

    [ObservableProperty]
    private string _hardwarePortName = string.Empty;

    [ObservableProperty]
    private string _hardwareModelName = "SYNX-DTU-485";

    [ObservableProperty]
    private string _hardwareConnectionStatusBadge = "○ Chưa kết nối phần cứng";

    /// <summary>
    /// Cập nhật trạng thái kết nối phần cứng thời gian thực từ MainViewModel / LifecycleManager.
    /// </summary>
    public void UpdateHardwareConnectionState(bool isConnected, string port, string model)
    {
        IsHardwareConnected = isConnected;
        HardwarePortName = port;
        HardwareModelName = string.IsNullOrWhiteSpace(model) ? "SYNX-DTU-485" : model;
        LiveTestInspectorVM.UpdateDeviceConnection(isConnected, port, HardwareModelName);

        if (isConnected)
        {
            string portText = string.IsNullOrWhiteSpace(port) ? "COM" : port;
            HardwareConnectionStatusBadge = LocalizationService.Instance.IsVietnamese
                ? $"{portText}: ĐÃ KẾT NỐI ({HardwareModelName})"
                : $"{portText}: CONNECTED ({HardwareModelName})";
        }
        else
        {
            HardwareConnectionStatusBadge = LocalizationService.Instance.IsVietnamese
                ? "Chưa kết nối phần cứng"
                : "Hardware Disconnected";
        }
    }

    [RelayCommand]
    public async Task UploadFromDeviceAsync()
    {
        if (UploadFromDeviceRequested != null)
        {
            await UploadFromDeviceRequested.Invoke();
        }
        else
        {
            DeployStatusMessage = LocalizationService.Instance.IsVietnamese
                ? "Chưa kết nối phần cứng hoặc tính năng Upload không khả dụng."
                : "Device not connected or Upload handler unavailable.";
        }
    }

    [RelayCommand]
    public void OpenQuickWizard()
    {
        OpenQuickWizardRequested?.Invoke();
    }

    // --- VERTICAL SIDEBAR NAVIGATION STATE ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSectionRs485))]
    [NotifyPropertyChangedFor(nameof(IsSectionWifi))]
    [NotifyPropertyChangedFor(nameof(IsSectionMqtt))]
    [NotifyPropertyChangedFor(nameof(IsSectionHttp))]
    [NotifyPropertyChangedFor(nameof(IsSectionTcp))]
    [NotifyPropertyChangedFor(nameof(IsSectionZigbee))]
    [NotifyPropertyChangedFor(nameof(IsSectionUplink))]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorTitle))]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorSubtitle))]
    private int _selectedSectionIndex = 0; // 0: RS-485, 1: Wi-Fi, 2: MQTT Client, 3: HTTP Client, 4: TCP Client, 5: Zigbee Wireless

    public bool IsSectionRs485 => SelectedSectionIndex == 0;
    public bool IsSectionWifi => SelectedSectionIndex == 1;
    public bool IsSectionMqtt => SelectedSectionIndex == 2;
    public bool IsSectionHttp => SelectedSectionIndex == 3;
    public bool IsSectionTcp => SelectedSectionIndex == 4;
    public bool IsSectionZigbee => SelectedSectionIndex == 5;
    public bool IsSectionUplink => IsSectionMqtt || IsSectionHttp || IsSectionTcp;

    public string ActiveInspectorTitle
    {
        get
        {
            return SelectedSectionIndex switch
            {
                0 => LiveTestInspectorVM?.InspectorHeaderTitle ?? LocalizationService.Instance["DtuInspectorHeaderTitle"],
                1 => LocalizationService.Instance["InspectorWifiHeaderTitle"],
                2 => LocalizationService.Instance["InspectorMqttHeaderTitle"],
                3 => LocalizationService.Instance["InspectorHttpHeaderTitle"],
                4 => LocalizationService.Instance["InspectorTcpHeaderTitle"],
                5 => LocalizationService.Instance["InspectorZigbeeHeaderTitle"],
                _ => LocalizationService.Instance["DtuInspectorHeaderTitle"]
            };
        }
    }

    public string ActiveInspectorSubtitle
    {
        get
        {
            return SelectedSectionIndex switch
            {
                0 => LiveTestInspectorVM?.InspectorHeaderSubtitle ?? LocalizationService.Instance["DtuInspectorHeaderSubtitle"],
                1 => WifiEnabled
                    ? (string.IsNullOrWhiteSpace(WifiSsid)
                        ? LocalizationService.Instance["InspectorWifiHeaderSubtitle"]
                        : string.Format(LocalizationService.Instance["InspectorWifiConnectedSubtitle"], WifiSsid, WifiDhcpEnabled ? "DHCP" : WifiStaticIp))
                    : LocalizationService.Instance["InspectorWifiDisabledSubtitle"],
                2 => LocalizationService.Instance["InspectorMqttHeaderSubtitle"],
                3 => LocalizationService.Instance["InspectorHttpHeaderSubtitle"],
                4 => LocalizationService.Instance["InspectorTcpHeaderSubtitle"],
                5 => LocalizationService.Instance["InspectorZigbeeHeaderSubtitle"],
                _ => LocalizationService.Instance["DtuInspectorHeaderSubtitle"]
            };
        }
    }

    [RelayCommand]
    public void SelectSection(object? param)
    {
        if (param is int i) SelectedSectionIndex = i;
        else if (param != null && int.TryParse(param.ToString(), out int parsed)) SelectedSectionIndex = parsed;
    }

        [ObservableProperty]
    private bool _isInspectorExpanded = true;

    [ObservableProperty]
    private bool _isWireFrameExpanded = false;

    /// <summary>
    /// Đóng/mở chi tiết khung truyền Modbus RTU Wire Frame (Moxa Style) on-demand.
    /// </summary>
    [RelayCommand]
    private void ToggleWireFrame()
    {
        IsWireFrameExpanded = !IsWireFrameExpanded;
    }

    /// <summary>
    /// Thu gọn hoặc mở rộng bảng Inspector bên phải để tối ưu hóa không gian làm việc.
    /// </summary>
    [RelayCommand]
    public void ToggleInspector()
    {
        IsInspectorExpanded = !IsInspectorExpanded;
    }

    // Status Badges for Sidebar Navigation
    /// <summary>
    /// Huy hiệu trạng thái tóm tắt cấu hình RS-485 Modbus hiển thị trên thanh điều hướng bên trái (Sidebar).
    /// Phản ánh trung thực số cổng vật lý đang bật (ví dụ: "2 cổng" / "2 ports") hoặc tổng số kênh truyền (ví dụ: "3 kênh" / "3 channels").
    /// Tuân thủ Tiên đề 2 (Visual Truth): Giao diện phản ánh chính xác trạng thái cấu hình thực tế.
    /// </summary>
    public string Rs485StatusBadge
    {
        get
        {
            if (ModbusPortConfigVM != null && ModbusPortConfigVM.Ports.Count > 0)
            {
                var ports = ModbusPortConfigVM.Ports;
                var activePorts = ports.Where(p => p.Enabled).ToList();
                if (activePorts.Count == 0)
                {
                    return LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled";
                }

                if (ModbusPortConfigVM.SelectedPort?.IsSlaveMode ?? (SerialMode == "ModbusSlave"))
                {
                    return "Slave";
                }

                int totalChannels = activePorts.Sum(p => p.MasterChannels.Count);
                if (totalChannels > 0)
                {
                    return LocalizationService.Instance.IsVietnamese
                        ? $"{totalChannels} kênh"
                        : (totalChannels == 1 ? "1 channel" : $"{totalChannels} channels");
                }

                return LocalizationService.Instance.IsVietnamese
                    ? $"{activePorts.Count} cổng"
                    : (activePorts.Count == 1 ? "1 port" : $"{activePorts.Count} ports");
            }

            if (SerialMode == "ModbusSlave")
                return "Slave";

            int count = FastRegisters.Count;
            if (count > 0)
            {
                return LocalizationService.Instance.IsVietnamese
                    ? $"{count} kênh"
                    : (count == 1 ? "1 channel" : $"{count} channels");
            }

            return $"{BaudRate} bps";
        }
    }

    /// <summary>
    /// Thuộc tính tốc độ Baud phục vụ Inspector và DataBinding.
    /// </summary>
    public uint SerialBaudRate => BaudRate;

    public string WifiStatusBadge => WifiEnabled
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    public string UplinkStatusBadge => UplinkMode switch { "Http" => "HTTP REST", "TcpClient" => "TCP Socket", _ => "MQTT" };

    public string MqttSidebarBadgeText => (MqttBrokerConfigVM?.IsEnabled ?? false)
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    public string HttpSidebarBadgeText => HttpEnabled
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    public string TcpSidebarBadgeText => TcpEnabled
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    // --- WI-FI NETWORK CONFIGURATION ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WifiStatusBadge))]
    [NotifyPropertyChangedFor(nameof(FormattedRssi))]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorSubtitle))]
    [NotifyPropertyChangedFor(nameof(WifiActiveDisplay))]
    [NotifyPropertyChangedFor(nameof(IsWifiRequiredNoticeVisible))]
    private bool _wifiEnabled = true;

    /// <summary>
    /// Cho biết có hiển thị banner nhắc nhở bật Wi-Fi trong tab MQTT hay không
    /// (chỉ xuất hiện khi thiết bị có Wi-Fi nhưng Wi-Fi đang tắt trong khi MQTT lại đang bật).
    /// </summary>
    public bool IsWifiRequiredNoticeVisible =>
        HasWifiCapability &&
        !WifiEnabled &&
        (MqttBrokerConfigVM?.IsEnabled ?? false);

    /// <summary>
    /// Lệnh kích hoạt nhanh Wi-Fi từ banner gợi ý trong tab MQTT.
    /// </summary>
    [RelayCommand]
    public void EnableWifi()
    {
        WifiEnabled = true;
        if (WifiVM != null)
        {
            WifiVM.IsEnabled = true;
        }
    }

    /// <summary>
    /// Chuỗi hiển thị trạng thái kích hoạt Wi-Fi (Đã bật / Đã tắt).
    /// </summary>
    public string WifiActiveDisplay => WifiEnabled 
        ? (LocalizationService.Instance.IsVietnamese ? "Đã bật" : "Enabled") 
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WifiAutoReconnectDisplay))]
    private bool _wifiAutoReconnect = true;

    /// <summary>
    /// Chuỗi hiển thị trạng thái tự động kết nối lại (Bật / Tắt).
    /// </summary>
    public string WifiAutoReconnectDisplay => WifiAutoReconnect 
        ? (LocalizationService.Instance.IsVietnamese ? "Bật" : "Enabled") 
        : (LocalizationService.Instance.IsVietnamese ? "Tắt" : "Disabled");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WifiStatusBadge))]
    [NotifyPropertyChangedFor(nameof(WifiSsidDisplay))]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorSubtitle))]
    private string _wifiSsid = string.Empty;

    /// <summary>
    /// Chuỗi hiển thị tên mạng Wi-Fi (SSID). Nếu chưa đặt SSID, hiển thị chuỗi giữ chỗ rõ ràng.
    /// </summary>
    public string WifiSsidDisplay => !string.IsNullOrWhiteSpace(WifiSsid)
        ? WifiSsid
        : LocalizationService.Instance["WifiNoSsidPlaceholder"];

    [ObservableProperty]
    private string _wifiPassword = string.Empty;

    [ObservableProperty]
    private bool _isWifiPasswordVisible = false;

    [RelayCommand]
    public void ToggleWifiPasswordVisibility() => IsWifiPasswordVisible = !IsWifiPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorSubtitle))]
    [NotifyPropertyChangedFor(nameof(WifiIpAssignmentDisplay))]
    private bool _wifiDhcpEnabled = true;

    /// <summary>
    /// Chuỗi hiển thị cơ chế cấp phát IP (DHCP Client tự động hoặc IP tĩnh).
    /// </summary>
    public string WifiIpAssignmentDisplay => WifiDhcpEnabled
        ? LocalizationService.Instance["WifiIpDhcpAuto"]
        : LocalizationService.Instance["WifiIpStaticManual"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveInspectorSubtitle))]
    private string _wifiStaticIp = "192.168.1.50";

    [ObservableProperty]
    private string _wifiSubnetMask = "255.255.255.0";

    [ObservableProperty]
    private string _wifiGateway = "192.168.1.1";

    [ObservableProperty]
    private string _wifiDns = "8.8.8.8";

    // --- UPLINK PROTOCOL SWITCHING HELPERS ---
    public bool IsUplinkMqtt => string.Equals(UplinkMode, "Mqtt", StringComparison.OrdinalIgnoreCase);
    public bool IsUplinkHttp => string.Equals(UplinkMode, "Http", StringComparison.OrdinalIgnoreCase);
    public bool IsUplinkTcp => string.Equals(UplinkMode, "TcpClient", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public void SetSerialMode(string mode)
    {
        SerialMode = mode;
        if (ModbusPortConfigVM?.SelectedPort != null)
        {
            ModbusPortConfigVM.SelectedPort.Role = mode == "ModbusSlave" ? "Slave" : "Master";
        }
        NotifyPortRelatedProperties();
    }

    [RelayCommand]
    public void SetUplinkMode(string mode)
    {
        UplinkMode = mode;
        OnPropertyChanged(nameof(IsUplinkMqtt));
        OnPropertyChanged(nameof(IsUplinkHttp));
        OnPropertyChanged(nameof(IsUplinkTcp));
        OnPropertyChanged(nameof(UplinkStatusBadge));
        OnPropertyChanged(nameof(SummaryTopologyText));
        OnPropertyChanged(nameof(EstimatedScanCycleMs));
        OnPropertyChanged(nameof(EstimatedBusLoadText));
        OnPropertyChanged(nameof(EstimatedMonthlyDataMb));
        UpdateSummaryAndPreview();
    }

    // --- STEP WIZARD STATE (Multi-Step Guided Flow) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private int _currentStepIndex = 1; // 1: RS-485 Modbus, 2: Uplink Protocol, 3: Timing & Review

    public bool IsStep1 => CurrentStepIndex == 1;
    public bool IsStep2 => CurrentStepIndex == 2;
    public bool IsStep3 => CurrentStepIndex == 3;
    public bool CanGoBack => CurrentStepIndex > 1;
    public bool CanGoNext => CurrentStepIndex < 3 && IsCurrentStepValid;
    public bool IsLastStep => CurrentStepIndex == 3;

    public bool IsCurrentStepValid => CurrentStepIndex switch
    {
        1 => (ModbusPortConfigVM?.Ports.Any(p => p.MasterChannels.Count > 0 || p.IsSlaveMode) ?? false) || FastRegisters.Count > 0 || SerialMode == "ModbusSlave",
        2 => !string.IsNullOrWhiteSpace(UplinkTargetHost),
        3 => PublishIntervalSec > 0,
        _ => true
    };

    public string UplinkTargetHost => UplinkMode switch
    {
        "Http" => HttpUrl,
        "TcpClient" => TcpRemoteHost,
        _ => MqttHost
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    private ushort _publishIntervalSec = 10;

    [ObservableProperty]
    private bool _autoGenerateFbd = true;

    [ObservableProperty]
    private bool _sendOnChange = true;

    [ObservableProperty]
    private string _livePayloadPreview = "{\n  \"device\": \"SYNX-DTU-485\",\n  \"ts\": 1714800000,\n  \"NhietDo_C\": 25.5\n}";

    /// <summary>
    /// Danh sách các thanh ghi cảm biến cần đọc nhanh (Step 1).
    /// </summary>
    public ObservableCollection<DtuFastRegisterItem> FastRegisters { get; } = new();

    public string SummaryTopologyText
    {
        get
        {
            string src;
            if (SerialMode == "ModbusSlave" || (ModbusPortConfigVM?.SelectedPort?.IsSlaveMode ?? false))
            {
                src = "Modbus Slave";
            }
            else if (FastRegisters.Count > 0)
            {
                src = $"Modbus Master ({FastRegisters.Count} regs)";
            }
            else if (ModbusPortConfigVM != null && ModbusPortConfigVM.Ports.Count > 0)
            {
                int totalChannels = ModbusPortConfigVM.Ports.Sum(p => p.MasterChannels.Count);
                src = totalChannels > 0
                    ? $"Modbus Master ({totalChannels} channels)"
                    : $"Modbus Master ({ModbusPortConfigVM.Ports.Count(p => p.Enabled)} ports)";
            }
            else
            {
                src = "Modbus Master";
            }

            string dest = UplinkMode switch
            {
                "Http" => $"HTTP POST -> {HttpUrl}",
                "TcpClient" => $"TCP Socket -> {TcpRemoteHost}:{TcpRemotePort}",
                _ => $"MQTT -> {MqttHost}:{MqttPort} ({MqttPublishTopic})"
            };
            return $"{src}  ===>  {dest}";
        }
    }


    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0: RS-485, 1: Uplink, 2: JSON, 3: Live Monitor

    // --- RS-485 Port Settings ---
    [ObservableProperty]
    private int _selectedPortIndex = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    [NotifyPropertyChangedFor(nameof(IsMasterMode))]
    [NotifyPropertyChangedFor(nameof(IsSlaveMode))]
    [NotifyPropertyChangedFor(nameof(Rs485StatusBadge))]
    private string _serialMode = "ModbusMaster"; // ModbusMaster, ModbusSlave

    public bool IsMasterMode => SerialMode == "ModbusMaster";
    public bool IsSlaveMode => SerialMode == "ModbusSlave";

    [ObservableProperty]
    private uint _baudRate = 9600;

    [ObservableProperty]
    private byte _dataBits = 8;

    [ObservableProperty]
    private string _parity = "None";

    [ObservableProperty]
    private byte _stopBits = 1;

    [ObservableProperty]
    private ushort _frameTimeoutDelayMs = 20;

    [ObservableProperty]
    private ushort _maxPacketSize = 512;

    [ObservableProperty]
    private ushort _responseTimeoutMs = 200;

    [ObservableProperty]
    private byte _slaveId = 1;

    [ObservableProperty]
    private string _portName = "RS-485 #1";

    [ObservableProperty]
    private ushort _retryCount = 1;

    [ObservableProperty]
    private ushort _interFrameDelayMs = 20;

    public IReadOnlyList<uint> AvailableBaudRates { get; } = new uint[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
    public IReadOnlyList<string> AvailableParities { get; } = new string[] { "None", "Even", "Odd" };
    public IReadOnlyList<byte> AvailableDataBits { get; } = new byte[] { 7, 8 };
    public IReadOnlyList<byte> AvailableStopBits { get; } = new byte[] { 1, 2 };
    public IReadOnlyList<int> AvailableQosLevels { get; } = new int[] { 0, 1, 2 };

    [ObservableProperty]
    private ushort _pollingIntervalMs = 500;

    [ObservableProperty]
    private string _wifiDnsSecondary = "1.1.1.1";

    [ObservableProperty]
    private string _hardwareMacAddress = "24:6F:28:8A:4C:12";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedRssi))]
    [NotifyPropertyChangedFor(nameof(SignalQualityPercent))]
    private string _signalRssi = "-58 dBm (Tốt)";

    /// <summary>
    /// Phần trăm chất lượng tín hiệu Wi-Fi (0 - 100%) dùng cho thanh hiển thị trực quan.
    /// </summary>
    public int SignalQualityPercent
    {
        get
        {
            if (!WifiEnabled) return 0;
            if (WifiVM != null && WifiVM.SignalQualityPercent > 0)
            {
                return WifiVM.SignalQualityPercent;
            }
            if (!IsHardwareConnected) return 0;
            return 84; // -58 dBm tương đương ~84%
        }
    }

    public string FormattedRssi
    {
        get
        {
            if (!WifiEnabled)
            {
                return LocalizationService.Instance["WifiRssiRadioOff"];
            }
            if (!IsHardwareConnected)
            {
                return LocalizationService.Instance["WifiRssiOffline"];
            }
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (WifiVM != null && WifiVM.RssiDbm != -100 && WifiVM.RssiDbm != 0)
            {
                string quality = WifiVM.RssiDbm >= -65 
                    ? (isVi ? "Tốt" : "Good") 
                    : (WifiVM.RssiDbm >= -75 ? (isVi ? "Khá" : "Fair") : (isVi ? "Yếu" : "Weak"));
                return $"{WifiVM.RssiDbm} dBm ({quality})";
            }
            return isVi ? "-58 dBm (Tốt)" : "-58 dBm (Good)";
        }
    }

    [ObservableProperty]
    private string _connectionStateText = "Sẵn sàng kết nối";

    [ObservableProperty]
    private ObservableCollection<string> _availableSsids = new() { "SynaptiX_Lab_5G", "Factory_IoT_Gateway", "Plant_Floor_4.0", "Sensor_Mesh_Network" };

    [RelayCommand]
    public void ScanNetworks()
    {
        ConnectionStateText = LocalizationService.Instance["WifiScanningText"];
        AvailableSsids.Clear();
        AvailableSsids.Add("SynaptiX_Lab_5G");
        AvailableSsids.Add("Factory_IoT_Gateway");
        AvailableSsids.Add("Plant_Floor_4.0");
        AvailableSsids.Add("Sensor_Mesh_Network");
        ConnectionStateText = string.Format(LocalizationService.Instance["WifiScanFoundFormat"], AvailableSsids.Count);
    }

    public ObservableCollection<ModbusSlaveMapEntryModel> SlaveMapEntries { get; } = new();

    [RelayCommand]
    public void AddSlaveMapEntry()
    {
        SlaveMapEntries.Add(new ModbusSlaveMapEntryModel
        {
            ModbusAddress = (ushort)(40001 + SlaveMapEntries.Count),
            TargetTagName = $"SLAVE_REG_{SlaveMapEntries.Count + 1}",
            DataType = 2,
            AccessRight = "ReadWrite",
            Description = "Thanh ghi Modbus Slave"
        });
    }

    [RelayCommand]
    public void RemoveSlaveMapEntry(ModbusSlaveMapEntryModel? entry)
    {
        if (entry != null)
        {
            SlaveMapEntries.Remove(entry);
        }
    }

    // --- Uplink Network Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UplinkTargetHost))]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    private string _uplinkMode = "Mqtt"; // Mqtt, Http, TcpClient, TcpServer

    // MQTT
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UplinkTargetHost))]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    private string _mqttHost = "broker.hivemq.com";

    [ObservableProperty]
    private int _mqttPort = 1883;

    [ObservableProperty]
    private string _mqttClientId = "synaptix_dtu_01";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MqttAuthTypeDisplay))]
    private string _mqttUsername = string.Empty;

    [ObservableProperty]
    private string _mqttPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MqttTlsSecurityDisplay))]
    private bool _mqttUseTls = false;

    // MQTT TLS / SSL & mTLS Settings (SPLC-SPEC-MQTT-003)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaCert))]
    private string _mqttTlsCaCertPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaCert))]
    private string _mqttTlsCaCertPem = string.Empty;

    [ObservableProperty]
    private bool _mqttTlsAllowSelfSigned = false;

    [ObservableProperty]
    private bool _mqttTlsIgnoreHostname = false;

    [ObservableProperty]
    private int _mqttTlsProtocolVersion = 0; // 0: Auto (TLS 1.2/1.3), 1: TLS 1.2, 2: TLS 1.3

    [ObservableProperty]
    private string _mqttTlsSniHostname = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MqttAuthTypeDisplay))]
    private bool _mqttTlsMtlsEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientCert))]
    private string _mqttTlsClientCertPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientCert))]
    private string _mqttTlsClientCertPem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientKey))]
    private string _mqttTlsClientKeyPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientKey))]
    private string _mqttTlsClientKeyPem = string.Empty;

    [ObservableProperty]
    private string _mqttTlsClientKeyPassword = string.Empty;

    // Certificate details (Badges)
    [ObservableProperty]
    private string _caCertSubject = string.Empty;

    [ObservableProperty]
    private string _caCertIssuer = string.Empty;

    [ObservableProperty]
    private string _caCertExpiry = string.Empty;

    [ObservableProperty]
    private string _caCertSha256 = string.Empty;

    public bool HasCaCert => !string.IsNullOrWhiteSpace(MqttTlsCaCertPath) || !string.IsNullOrWhiteSpace(MqttTlsCaCertPem);

    /// <summary>
    /// Chuỗi hiển thị trạng thái mã hóa bảo mật TLS cho panel Inspector.
    /// </summary>
    public string MqttTlsSecurityDisplay => MqttUseTls
        ? (LocalizationService.Instance.IsVietnamese ? "Bật (TLS v1.2/1.3)" : "Enabled (TLS v1.2/1.3)")
        : (LocalizationService.Instance.IsVietnamese ? "Tắt" : "Disabled");

    /// <summary>
    /// Chuỗi hiển thị phương thức xác thực danh tính cho panel Inspector.
    /// </summary>
    public string MqttAuthTypeDisplay
    {
        get
        {
            if (MqttTlsMtlsEnabled)
            {
                return LocalizationService.Instance.IsVietnamese ? "Chứng chỉ mTLS x509" : "mTLS x509 Certificate";
            }
            if (!string.IsNullOrWhiteSpace(MqttUsername))
            {
                return LocalizationService.Instance.IsVietnamese ? "Tên người dùng / Mật khẩu" : "Username / Password";
            }
            return LocalizationService.Instance.IsVietnamese ? "Không xác thực" : "None (Anonymous)";
        }
    }

    /// <summary>
    /// Chuỗi hiển thị chế độ phát bản tin Telemetry Publish (định kỳ hoặc xung kích hoạt).
    /// </summary>
    public string MqttPublishModeDisplay
    {
        get
        {
            if (MqttTelemetryNode == null) return "—";
            if (MqttTelemetryNode.IsEventMode)
                return LocalizationService.Instance.IsVietnamese ? "Theo xung kích hoạt (Trig)" : "On Trigger (Trig)";
            if (MqttTelemetryNode.IsCovMode)
                return $"COV (Δ{MqttTelemetryNode.Deadband})";
            return LocalizationService.Instance.IsVietnamese 
                ? $"Định kỳ ({MqttTelemetryNode.IntervalMs}ms)" 
                : $"Periodic ({MqttTelemetryNode.IntervalMs}ms)";
        }
    }

    /// <summary>
    /// Chuỗi hiển thị chế độ hàng đợi nhận lệnh MQTT Subscribe.
    /// </summary>
    public string MqttQueueModeDisplay => MqttCommandNode?.QueueModeSummaryText ?? "FIFO (16)";

    /// <summary>
    /// Chuỗi tổng quan số lượng trường dữ liệu xuất bản Telemetry Publish.
    /// </summary>
    public string MqttPublishFieldsSummaryText
    {
        get
        {
            int count = MqttTelemetryNode?.Fields?.Count ?? 0;
            return LocalizationService.Instance.IsVietnamese ? $"{count} trường" : $"{count} fields";
        }
    }

    /// <summary>
    /// Chuỗi tổng quan số lượng cổng lệnh nhận về Command Subscribe.
    /// </summary>
    public string MqttSubscribePinsSummaryText
    {
        get
        {
            int count = MqttCommandNode?.Pins?.Count ?? 0;
            return LocalizationService.Instance.IsVietnamese ? $"{count} lệnh" : $"{count} commands";
        }
    }

    /// <summary>
    /// Chuỗi hiển thị tỷ lệ pin đã được ánh xạ tới Tag ghi hợp lệ.
    /// </summary>
    public string MqttMappedTagsSummaryText
    {
        get
        {
            int mapped = MqttCommandNode?.Pins?.Count(p => p.TargetTag != null && p.TargetTag.Kind != TagKind.None) ?? 0;
            int total = MqttCommandNode?.Pins?.Count ?? 0;
            return $"{mapped}/{total} tags";
        }
    }

    /// <summary>
    /// Ước tính dung lượng payload JSON mỗi bản tin telemetry gửi đi.
    /// </summary>
    public string MqttEstimatedPayloadSizeText
    {
        get
        {
            int fieldCount = MqttTelemetryNode?.Fields?.Count ?? 0;
            int estBytes = Math.Max(64, (fieldCount * 32) + 70);
            return $"~{estBytes} bytes / msg";
        }
    }

    /// <summary>
    /// Tên chủ đề xuất bản MQTT Telemetry (Thuộc tính tương thích hai chiều của MqttPublishTopic).
    /// </summary>
    public string MqttTopic
    {
        get => MqttPublishTopic;
        set => MqttPublishTopic = value;
    }

    public void NotifyMqttPubSummary()
    {
        OnPropertyChanged(nameof(MqttPublishTopic));
        OnPropertyChanged(nameof(MqttTopic));
        OnPropertyChanged(nameof(MqttPublishModeDisplay));
        OnPropertyChanged(nameof(MqttPublishFieldsSummaryText));
        OnPropertyChanged(nameof(MqttEstimatedPayloadSizeText));
    }

    public void NotifyMqttSubSummary()
    {
        OnPropertyChanged(nameof(MqttSubscribeTopic));
        OnPropertyChanged(nameof(MqttQueueModeDisplay));
        OnPropertyChanged(nameof(MqttSubscribePinsSummaryText));
        OnPropertyChanged(nameof(MqttMappedTagsSummaryText));
    }

    [ObservableProperty]
    private string _clientCertSubject = string.Empty;

    [ObservableProperty]
    private string _clientCertIssuer = string.Empty;

    [ObservableProperty]
    private string _clientCertExpiry = string.Empty;

    [ObservableProperty]
    private string _clientCertSha256 = string.Empty;

    public bool HasClientCert => !string.IsNullOrWhiteSpace(MqttTlsClientCertPath) || !string.IsNullOrWhiteSpace(MqttTlsClientCertPem);

    public bool HasClientKey => !string.IsNullOrWhiteSpace(MqttTlsClientKeyPath) || !string.IsNullOrWhiteSpace(MqttTlsClientKeyPem);

    [ObservableProperty]
    private ushort _mqttKeepAlive = 60;

    [ObservableProperty]
    private string _mqttPublishTopic = "factory/dtu/data";

    [ObservableProperty]
    private string _mqttSubscribeTopic = "factory/dtu/command";

    [ObservableProperty]
    private int _mqttQos = 1;

    [ObservableProperty]
    private bool _mqttRetain = false;

    // MQTT LWT
    [ObservableProperty]
    private bool _mqttWillEnabled = true;

    [ObservableProperty]
    private string _mqttWillTopic = "factory/dtu/status";

    [ObservableProperty]
    private string _mqttWillPayload = "offline";

    [ObservableProperty]
    private int _mqttWillQos = 1;

    [ObservableProperty]
    private bool _mqttWillRetain = true;

    // HTTP REST API
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HttpSidebarBadgeText))]
    private bool _httpEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UplinkTargetHost))]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    [NotifyPropertyChangedFor(nameof(HttpEndpointDisplay))]
    private string _httpUrl = "https://api.factory-cloud.io/v1/telemetry";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HttpSidebarBadgeText))]
    [NotifyPropertyChangedFor(nameof(HttpMethodDisplay))]
    private string _httpMethod = "POST";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HttpHeadersDisplay))]
    private string _httpHeaders = "Content-Type: application/json\nAuthorization: Bearer token123";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HttpTimeoutDisplay))]
    private ushort _httpTimeoutSec = 10;

    [ObservableProperty]
    private ushort _httpIntervalSec = 5;

    [ObservableProperty]
    private string _httpTriggerMode = "OnDataReceived"; // OnDataReceived, Periodic

    /// <summary>
    /// Cho biết chế độ kích hoạt HTTP có phải là gửi theo chu kỳ thời gian (Periodic) hay không.
    /// </summary>
    public bool IsHttpPeriodicTrigger => HttpTriggerMode == "Periodic";

    public ObservableCollection<DtuHttpFieldModel> HttpFields { get; } = new();

    public string HttpHeadersText { get => HttpHeaders; set => HttpHeaders = value; }
    public string HttpEndpointDisplay => string.IsNullOrWhiteSpace(HttpUrl) ? "https://..." : HttpUrl;
    public string HttpMethodDisplay => HttpMethod;
    public string HttpTimeoutDisplay => $"{HttpTimeoutSec}s";
    public string HttpHeadersDisplay => string.IsNullOrWhiteSpace(HttpHeaders)
        ? (LocalizationService.Instance.IsVietnamese ? "Mặc định (JSON)" : "Default (JSON)")
        : $"{HttpHeaders.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length} headers";
    public string HttpFieldCountDisplay => LocalizationService.Instance.IsVietnamese
        ? $"{HttpFields.Count} trường"
        : $"{HttpFields.Count} fields";

    [RelayCommand]
    private void AddHttpField()
    {
        HttpFields.Add(new DtuHttpFieldModel { Key = $"field{HttpFields.Count + 1}", ValueSource = "Payload" });
        OnPropertyChanged(nameof(HttpFieldCountDisplay));
    }

    [RelayCommand]
    private void RemoveHttpField(DtuHttpFieldModel? field)
    {
        if (field != null)
        {
            HttpFields.Remove(field);
            OnPropertyChanged(nameof(HttpFieldCountDisplay));
        }
    }

    // TCP Socket
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpSidebarBadgeText))]
    private bool _tcpEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpModeDisplay))]
    private string _tcpSocketMode = "Client"; // Client, Server

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UplinkTargetHost))]
    [NotifyPropertyChangedFor(nameof(SummaryTopologyText))]
    [NotifyPropertyChangedFor(nameof(TcpServerDisplay))]
    private string _tcpRemoteHost = "192.168.1.100";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpSidebarBadgeText))]
    [NotifyPropertyChangedFor(nameof(TcpPortDisplay))]
    private ushort _tcpRemotePort = 8888;

    [ObservableProperty]
    private ushort _tcpLocalPort = 8888;

    [ObservableProperty]
    private ushort _tcpReconnectDelaySec = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpHeartbeatDisplay))]
    private ushort _tcpHeartbeatIntervalSec = 30;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpHeartbeatDisplay))]
    private string _tcpHeartbeatPayload = "PING";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcpRegDisplay))]
    private string _tcpRegistrationType = "None"; // None, OnConnect, PrependToEveryPacket

    [ObservableProperty]
    private string _tcpRegistrationPayload = "{{DEVICE_ID}}";

    public string TcpServerHost { get => TcpRemoteHost; set => TcpRemoteHost = value; }
    public ushort TcpServerPort { get => TcpRemotePort; set => TcpRemotePort = value; }
    public string TcpModeDisplay => TcpSocketMode;
    public string TcpServerDisplay => string.IsNullOrWhiteSpace(TcpRemoteHost) ? "127.0.0.1" : TcpRemoteHost;
    public string TcpPortDisplay => $"Port {TcpRemotePort}";
    public string TcpHeartbeatDisplay => $"{TcpHeartbeatIntervalSec}s ({TcpHeartbeatPayload})";
    public string TcpRegDisplay => TcpRegistrationType;

    // --- JSON Formatter & Preview ---
    [ObservableProperty]
    private string _jsonTemplate = "{\n  \"device\": \"{{DEVICE_ID}}\",\n  \"timestamp\": \"{{TIMESTAMP}}\",\n  \"port\": {{PORT}},\n  \"data\": \"{{SERIAL_PAYLOAD}}\",\n  \"hex\": \"{{HEX_STREAM}}\"\n}";

    [ObservableProperty]
    private string _sampleSerialInput = "TEMP:28.4,HUM:62.1";

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private bool _isJsonSyntaxValid = true;

    [ObservableProperty]
    private string _payloadMode = "Standard"; // Standard, Custom, Raw

    [ObservableProperty]
    private bool _includeDeviceId = true;

    [ObservableProperty]
    private bool _includeTimestamp = true;

    [ObservableProperty]
    private bool _includePort = true;

    [ObservableProperty]
    private bool _includeSequence = true;

    [ObservableProperty]
    private bool _includeHexStream = false;

    [ObservableProperty]
    private bool _includePayloadLength = false;

    [ObservableProperty]
    private string _terminalSendFormat = "Ascii"; // Ascii, Hex

    /// <summary>
    /// Gets or sets whether the diagnostic terminal transmits raw bytes in HEX format instead of ASCII.
    /// </summary>
    public bool TestPacketIsHex
    {
        get => TerminalSendFormat == "Hex";
        set => TerminalSendFormat = value ? "Hex" : "Ascii";
    }

    [ObservableProperty]
    private long _packetSequenceCounter = 1;

    // --- Live Diagnostic Monitor ---
    [ObservableProperty]
    private string _testPacketInput = "TEMP:28.4,HUM:62.1";

    [ObservableProperty]
    private string _terminalLog = "--- SYNAPTIX DTU GATEWAY DIAGNOSTIC TERMINAL ---\n[SYSTEM] DTU Subsystem initialized.\n[SYSTEM] Ready for live serial-to-uplink packets.\n";

    [ObservableProperty]
    private bool _isDeploying = false;

    [ObservableProperty]
    private string _deployStatusMessage = string.Empty;

    public event Action? RequestClose;

    /// <summary>
    /// Callback thông báo cấu hình thiết bị đã được commit vào ProjectMetadata (được wire bởi MainViewModel để đánh dấu IsProjectDirty).
    /// </summary>
    public Action? NotifyConfigurationCommitted { get; set; }

    /// <summary>
    /// Callback yêu cầu Studio lưu toàn bộ dự án xuống tệp (.splc), được wire bởi MainViewModel.
    /// </summary>
    public Action? RequestSaveProject { get; set; }

    /// <summary>
    /// Callback yêu cầu Studio mở hộp thoại cấu hình MQTT Broker nâng cao (TLS/mTLS/Diagnostics).
    /// </summary>
    public Action? OpenMqttBrokerConfigRequested { get; set; }

    /// <summary>
    /// ViewModel cấu hình đa cổng RS-485 Modbus Master & Slave (Single Source of Truth).
    /// </summary>
    public ModbusPortConfigViewModel ModbusPortConfigVM { get; }

    private SPLC_HardwareCapabilities? _hardwareOverrideCapabilities;
    private int? _hardwareOverridePortLimit;

    /// <summary>
    /// Đồng bộ năng lực phần cứng thực tế và số cổng RS-485 từ MCU khi kết nối.
    /// Truyền null khi ngắt kết nối để phục hồi năng lực theo ProjectMetadata.
    /// </summary>
    /// <param name="product">Hồ sơ thiết bị ProductDefinition từ MCU, hoặc null khi offline.</param>
    public void SyncWithHardwareProduct(ProductDefinition? product)
    {
        if (product != null)
        {
            _hardwareOverrideCapabilities = product.HardwareCapabilities;
            _hardwareOverridePortLimit = product.PhysicalRs485PortCount;
            ModbusPortConfigVM?.SyncHardwarePortLimit(product.PhysicalRs485PortCount);
        }
        else
        {
            _hardwareOverrideCapabilities = null;
            _hardwareOverridePortLimit = null;
            ModbusPortConfigVM?.SyncHardwarePortLimit(null);
        }

        NotifyAllCapabilities();
        SelectFirstAvailableSection();
    }

    /// <summary>
    /// Bắn thông báo cập nhật toàn bộ thuộc tính năng lực phần cứng (Capabilities).
    /// </summary>
    public void NotifyAllCapabilities()
    {
        OnPropertyChanged(nameof(CurrentCapabilities));
        OnPropertyChanged(nameof(HasRs485Capability));
        OnPropertyChanged(nameof(HasWifiCapability));
        OnPropertyChanged(nameof(HasMqttCapability));
        OnPropertyChanged(nameof(HasMqttTlsCapability));
        OnPropertyChanged(nameof(HasHttpCapability));
        OnPropertyChanged(nameof(HasTcpSocketCapability));
        OnPropertyChanged(nameof(HasEthernetCapability));
        OnPropertyChanged(nameof(HasSdDatalogCapability));
        OnPropertyChanged(nameof(HasZigbeeCapability));
        OnPropertyChanged(nameof(HasSystemCapability));
        OnPropertyChanged(nameof(IsWifiRequiredNoticeVisible));
    }

    /// <summary>
    /// Năng lực phần cứng hiện tại được ánh xạ từ DevicePreset hoặc ProjectMetadata.
    /// </summary>
    public SPLC_HardwareCapabilities CurrentCapabilities
    {
        get
        {
            if (_hardwareOverrideCapabilities.HasValue)
                return _hardwareOverrideCapabilities.Value;

            if (_metadata.HardwareCapabilities != 0)
                return (SPLC_HardwareCapabilities)_metadata.HardwareCapabilities;

            var preset = HardwareDevicePreset.BuiltInPresets.FirstOrDefault(p =>
                (ushort)p.DeviceClass == _metadata.DeviceClass && p.DeviceVariant == _metadata.ProductVariant)
                ?? HardwareDevicePreset.BuiltInPresets.FirstOrDefault(p => (ushort)p.DeviceClass == _metadata.DeviceClass);

            return preset?.HardwareCapabilities ?? (SPLC_HardwareCapabilities.MQTT_PUB | SPLC_HardwareCapabilities.MQTT_SUB |
                   SPLC_HardwareCapabilities.HTTP_CLIENT | SPLC_HardwareCapabilities.TRANSPARENT_STREAM |
                   SPLC_HardwareCapabilities.WIFI | SPLC_HardwareCapabilities.ETHERNET |
                   SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.MODBUS_TCP);
        }
    }

    public bool HasRs485Capability => 
        CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.MODBUS_MASTER) || 
        CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.MODBUS_SLAVE);

    public bool HasMqttCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.MQTT_PUB) || CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.MQTT_SUB);
    public bool HasMqttTlsCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.MQTT_TLS_CERT);
    public bool HasHttpCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.HTTP_CLIENT);
    public bool HasTcpSocketCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.TRANSPARENT_STREAM);
    public bool HasWifiCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.WIFI);
    public bool HasEthernetCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.ETHERNET);
    public bool HasSdDatalogCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.SD_DATALOG);
    public bool HasZigbeeCapability => CurrentCapabilities.HasFlag(SPLC_HardwareCapabilities.ZIGBEE);

    public bool HasSystemCapability => 
        HasRs485Capability || HasWifiCapability || HasMqttCapability || HasHttpCapability || HasTcpSocketCapability || HasZigbeeCapability;

    /// <summary>
    /// Tự động chuyển đến section khả dụng đầu tiên nếu section hiện tại không được phần cứng hỗ trợ.
    /// </summary>
    public void SelectFirstAvailableSection()
    {
        if (IsSectionRs485 && !HasRs485Capability)
        {
            if (HasWifiCapability) SelectSection(1);
            else if (HasMqttCapability) SelectSection(2);
            else if (HasHttpCapability) SelectSection(3);
            else if (HasTcpSocketCapability) SelectSection(4);
        }
        else if (IsSectionWifi && !HasWifiCapability)
        {
            if (HasRs485Capability) SelectSection(0);
            else if (HasMqttCapability) SelectSection(2);
            else if (HasHttpCapability) SelectSection(3);
            else if (HasTcpSocketCapability) SelectSection(4);
        }
        else if (IsSectionMqtt && !HasMqttCapability)
        {
            if (HasRs485Capability) SelectSection(0);
            else if (HasWifiCapability) SelectSection(1);
            else if (HasHttpCapability) SelectSection(3);
            else if (HasTcpSocketCapability) SelectSection(4);
        }
        else if (IsSectionHttp && !HasHttpCapability)
        {
            if (HasRs485Capability) SelectSection(0);
            else if (HasWifiCapability) SelectSection(1);
            else if (HasMqttCapability) SelectSection(2);
            else if (HasTcpSocketCapability) SelectSection(4);
        }
        else if (IsSectionTcp && !HasTcpSocketCapability)
        {
            if (HasRs485Capability) SelectSection(0);
            else if (HasWifiCapability) SelectSection(1);
            else if (HasMqttCapability) SelectSection(2);
            else if (HasHttpCapability) SelectSection(3);
            else if (HasZigbeeCapability) SelectSection(5);
        }
        else if (IsSectionZigbee && !HasZigbeeCapability)
        {
            if (HasRs485Capability) SelectSection(0);
            else if (HasWifiCapability) SelectSection(1);
            else if (HasMqttCapability) SelectSection(2);
            else if (HasHttpCapability) SelectSection(3);
            else if (HasTcpSocketCapability) SelectSection(4);
        }
    }

    /// <summary>
    /// ViewModel cấu hình Broker MQTT (Endpoint, TLS/mTLS, Session, LWT, Ping reachability).
    /// </summary>
    public MqttBrokerConfigViewModel MqttBrokerConfigVM { get; }

    /// <summary>
    /// ViewModel cấu hình mạng Wi-Fi và giám sát tín hiệu không dây.
    /// </summary>
    public WifiConfigViewModel WifiVM { get; }

    /// <summary>
    /// ViewModel cấu hình xuất bản dữ liệu Telemetry MQTT (Topic, QoS, Retain, No-Code JSON Key to Tag mapping).
    /// </summary>
    public MqttConfigDialogViewModel MqttTelemetryVM { get; }

    /// <summary>
    /// Node MQTT Publish nền tảng cung cấp dữ liệu cho MqttTelemetryVM.
    /// </summary>
    public MqttPublishNodeViewModel MqttTelemetryNode { get; }

    /// <summary>
    /// ViewModel cấu hình nhận lệnh Command MQTT (Topic, QoS, Queue Mode, Output Pins list, Payload Test).
    /// </summary>
    public MqttSubConfigDialogViewModel MqttCommandVM { get; }

    /// <summary>
    /// Node MQTT Subscribe nền tảng cung cấp dữ liệu cho MqttCommandVM.
    /// </summary>
    public MqttSubscribeNodeViewModel MqttCommandNode { get; }

    /// <summary>
    /// Node HTTP Client ná»n táº£ng cung cáº¥p dá»¯ liá»‡u cho HttpVM.
    /// </summary>
    public HttpClientNodeViewModel HttpNode { get; }

    /// <summary>
    /// ViewModel cáº¥u hÃ¬nh chuyÃªn sÃ¢u REST API HTTP Client / Webhook (URL, Method, Headers, Visual Field Builder, Live JSON Preview).
    /// </summary>
    public HttpClientConfigDialogViewModel HttpVM { get; }

    /// <summary>
    /// Node TCP Client ná»n táº£ng cung cáº¥p dá»¯ liá»‡u cho TcpVM.
    /// </summary>
    public TcpClientNodeViewModel TcpNode { get; }

    /// <summary>
    /// ViewModel cáº¥u hÃ¬nh chuyÃªn sÃ¢u Socket TCP Client / Stream Passthrough (Host, Port, Registration, Heartbeat, Hex Stream Live Preview).
    /// </summary>
    public TcpClientConfigDialogViewModel TcpVM { get; }

    /// <summary>
    /// ViewModel cấu hình chuyên sâu mạng truyền thông không dây Zigbee Modbus (Channel, PAN ID, Station ID, Antenna, AES-128).
    /// </summary>
    public ZigbeeConfigViewModel ZigbeeVM { get; }

    /// <summary>
    /// Huy hiệu hiển thị tóm tắt trạng thái Zigbee trên thanh điều hướng bên trái.
    /// </summary>
    public string ZigbeeSidebarBadgeText => (ZigbeeVM?.IsEnabled ?? false)
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMqttSubSectionBroker))]
    [NotifyPropertyChangedFor(nameof(IsMqttSubSectionTelemetry))]
    [NotifyPropertyChangedFor(nameof(IsMqttSubSectionCommand))]
    [NotifyPropertyChangedFor(nameof(IsMqttTabBroker))]
    [NotifyPropertyChangedFor(nameof(IsMqttTabTelemetry))]
    [NotifyPropertyChangedFor(nameof(IsMqttTabCommand))]
    private int _mqttActiveSubSection = 0; // 0: Broker Server, 1: Telemetry Publish, 2: Command Subscribe

    public bool IsMqttSubSectionBroker => MqttActiveSubSection == 0;
    public bool IsMqttSubSectionTelemetry => MqttActiveSubSection == 1;
    public bool IsMqttSubSectionCommand => MqttActiveSubSection == 2;

    public bool IsMqttTabBroker
    {
        get => IsMqttSubSectionBroker;
        set { if (value) MqttActiveSubSection = 0; }
    }
    public bool IsMqttTabTelemetry
    {
        get => IsMqttSubSectionTelemetry;
        set { if (value) MqttActiveSubSection = 1; }
    }
    public bool IsMqttTabCommand
    {
        get => IsMqttSubSectionCommand;
        set { if (value) MqttActiveSubSection = 2; }
    }

    [RelayCommand]
    public void SelectMqttSubSection(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx) && idx >= 0 && idx <= 2)
        {
            MqttActiveSubSection = idx;
        }
    }

    /// <summary>
    /// ViewModel quản lý công cụ đọc thử và chẩn đoán thiết bị thật qua cổng COM.
    /// </summary>
    public ModbusLiveTestInspectorViewModel LiveTestInspectorVM { get; }

    private ModbusPortItemViewModel? _lastObservedPort;

    private void SyncFromSelectedPort()
    {
        if (_lastObservedPort != null)
        {
            _lastObservedPort.PropertyChanged -= OnSelectedPortPropertyChanged;
        }

        var port = ModbusPortConfigVM?.SelectedPort;
        _lastObservedPort = port;

        if (port != null)
        {
            port.PropertyChanged += OnSelectedPortPropertyChanged;
            PortName = port.PortName;
            BaudRate = port.BaudRate;
            DataBits = port.DataBits;
            Parity = port.Parity;
            StopBits = port.StopBits;
            ResponseTimeoutMs = port.ResponseTimeoutMs;
            RetryCount = port.RetryCount;
            InterFrameDelayMs = port.InterFrameDelayMs;
            SlaveId = port.SlaveId;
            SerialMode = port.IsSlaveMode ? "ModbusSlave" : "ModbusMaster";
            SelectedPortIndex = port.PortIndex;
            LiveTestInspectorVM.IsSlaveMode = port.IsSlaveMode;
            LiveTestInspectorVM.SlaveId = port.SlaveId;
            LiveTestInspectorVM.BaudRate = port.BaudRate;
            if (port.IsSlaveMode)
            {
                LiveTestInspectorVM.SelectedSlaveRegister = ModbusPortConfigVM?.SelectedFixedRegisterItem
                    ?? ModbusPortConfigVM?.FilteredFixedRegisterMap?.FirstOrDefault();
            }
            else
            {
                LiveTestInspectorVM.TargetChannel = port.SelectedMasterChannel;
            }
        }
        else
        {
            LiveTestInspectorVM.TargetChannel = null;
            LiveTestInspectorVM.SelectedSlaveRegister = null;
        }
        NotifyPortRelatedProperties();
    }

    private void OnSelectedPortPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ModbusPortItemViewModel port)
        {
            if (e.PropertyName == nameof(ModbusPortItemViewModel.Role) || e.PropertyName == nameof(ModbusPortItemViewModel.IsSlaveMode) || e.PropertyName == nameof(ModbusPortItemViewModel.IsMasterMode))
            {
                SerialMode = port.IsSlaveMode ? "ModbusSlave" : "ModbusMaster";
                LiveTestInspectorVM.IsSlaveMode = port.IsSlaveMode;
                if (port.IsSlaveMode)
                {
                    LiveTestInspectorVM.SelectedSlaveRegister = ModbusPortConfigVM?.SelectedFixedRegisterItem
                        ?? ModbusPortConfigVM?.FilteredFixedRegisterMap?.FirstOrDefault();
                }
                else
                {
                    LiveTestInspectorVM.TargetChannel = port.SelectedMasterChannel;
                }
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.BaudRate))
            {
                BaudRate = port.BaudRate;
                LiveTestInspectorVM.BaudRate = port.BaudRate;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.SelectedMasterChannel))
            {
                LiveTestInspectorVM.TargetChannel = port.SelectedMasterChannel;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.PortName))
            {
                PortName = port.PortName;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.SlaveId))
            {
                SlaveId = port.SlaveId;
                LiveTestInspectorVM.SlaveId = port.SlaveId;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.DataBits))
            {
                DataBits = port.DataBits;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.Parity))
            {
                Parity = port.Parity;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.StopBits))
            {
                StopBits = port.StopBits;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.ResponseTimeoutMs))
            {
                ResponseTimeoutMs = port.ResponseTimeoutMs;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.RetryCount))
            {
                RetryCount = port.RetryCount;
            }
            if (e.PropertyName == nameof(ModbusPortItemViewModel.InterFrameDelayMs))
            {
                InterFrameDelayMs = port.InterFrameDelayMs;
            }
        }
        NotifyPortRelatedProperties();
    }

    private void NotifyPortRelatedProperties()
    {
        OnPropertyChanged(nameof(BaudRate));
        OnPropertyChanged(nameof(SerialBaudRate));
        OnPropertyChanged(nameof(IsMasterMode));
        OnPropertyChanged(nameof(IsSlaveMode));
        OnPropertyChanged(nameof(Rs485StatusBadge));
        OnPropertyChanged(nameof(SummaryTopologyText));
        OnPropertyChanged(nameof(EstimatedScanCycleMs));
        OnPropertyChanged(nameof(EstimatedBusLoadText));
    }

    public DtuGatewayStudioViewModel(ProjectMetadata metadata, TagCatalogViewModel? tagCatalog = null, LogicEditorViewModel? logicEditor = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        TagCatalog = tagCatalog;
        LogicEditor = logicEditor;
        ModbusPortConfigVM = new ModbusPortConfigViewModel(_metadata, tagCatalog);
        MqttBrokerConfigVM = new MqttBrokerConfigViewModel(_metadata);
        MqttBrokerConfigVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MqttBrokerConfigVM.IsEnabled)) { OnPropertyChanged(nameof(MqttSidebarBadgeText)); OnPropertyChanged(nameof(IsWifiRequiredNoticeVisible)); }
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.Host)) MqttHost = MqttBrokerConfigVM.Host;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.Port)) MqttPort = MqttBrokerConfigVM.Port;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientId)) MqttClientId = MqttBrokerConfigVM.ClientId;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.Username)) MqttUsername = MqttBrokerConfigVM.Username;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.Password)) MqttPassword = MqttBrokerConfigVM.Password;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.UseTls)) MqttUseTls = MqttBrokerConfigVM.UseTls;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.KeepAliveSec)) MqttKeepAlive = MqttBrokerConfigVM.KeepAliveSec;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.WillEnabled)) MqttWillEnabled = MqttBrokerConfigVM.WillEnabled;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.WillTopic)) MqttWillTopic = MqttBrokerConfigVM.WillTopic;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.WillPayload)) MqttWillPayload = MqttBrokerConfigVM.WillPayload;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.WillQos)) MqttWillQos = MqttBrokerConfigVM.WillQos;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.WillRetain)) MqttWillRetain = MqttBrokerConfigVM.WillRetain;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.CaCertPath)) MqttTlsCaCertPath = MqttBrokerConfigVM.CaCertPath;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.AllowSelfSigned)) MqttTlsAllowSelfSigned = MqttBrokerConfigVM.AllowSelfSigned;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.CaCertPem)) MqttTlsCaCertPem = MqttBrokerConfigVM.CaCertPem;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.IgnoreHostname)) MqttTlsIgnoreHostname = MqttBrokerConfigVM.IgnoreHostname;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.TlsVersionIndex)) MqttTlsProtocolVersion = MqttBrokerConfigVM.TlsVersionIndex;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.SniHostname)) MqttTlsSniHostname = MqttBrokerConfigVM.SniHostname;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.UseMtls)) MqttTlsMtlsEnabled = MqttBrokerConfigVM.UseMtls;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientCertPath)) MqttTlsClientCertPath = MqttBrokerConfigVM.ClientCertPath;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientCertPem)) MqttTlsClientCertPem = MqttBrokerConfigVM.ClientCertPem;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientKeyPath)) MqttTlsClientKeyPath = MqttBrokerConfigVM.ClientKeyPath;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientKeyPem)) MqttTlsClientKeyPem = MqttBrokerConfigVM.ClientKeyPem;
            else if (e.PropertyName == nameof(MqttBrokerConfigVM.ClientKeyPassword)) MqttTlsClientKeyPassword = MqttBrokerConfigVM.ClientKeyPassword;
        };
        WifiVM = new WifiConfigViewModel(_metadata, AppServices.Instance.WifiClient);
        WifiVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WifiConfigViewModel.IsEnabled)) WifiEnabled = WifiVM.IsEnabled;
            else if (e.PropertyName == nameof(WifiConfigViewModel.AutoReconnect)) WifiAutoReconnect = WifiVM.AutoReconnect;
            else if (e.PropertyName == nameof(WifiConfigViewModel.Ssid)) WifiSsid = WifiVM.Ssid;
            else if (e.PropertyName == nameof(WifiConfigViewModel.Password)) WifiPassword = WifiVM.Password;
            else if (e.PropertyName == nameof(WifiConfigViewModel.IsDhcp)) WifiDhcpEnabled = WifiVM.IsDhcp;
            else if (e.PropertyName == nameof(WifiConfigViewModel.StaticIp)) WifiStaticIp = WifiVM.StaticIp;
            else if (e.PropertyName == nameof(WifiConfigViewModel.StaticSubnet)) WifiSubnetMask = WifiVM.StaticSubnet;
            else if (e.PropertyName == nameof(WifiConfigViewModel.StaticGateway)) WifiGateway = WifiVM.StaticGateway;
            else if (e.PropertyName == nameof(WifiConfigViewModel.StaticDns)) WifiDns = WifiVM.StaticDns;
            else if (e.PropertyName == nameof(WifiConfigViewModel.RssiDbm) || e.PropertyName == nameof(WifiConfigViewModel.SignalQualityPercent))
            {
                OnPropertyChanged(nameof(FormattedRssi));
                OnPropertyChanged(nameof(SignalQualityPercent));
            }
            OnPropertyChanged(nameof(WifiStatusBadge));
            OnPropertyChanged(nameof(WifiSsidDisplay));
            OnPropertyChanged(nameof(WifiIpAssignmentDisplay));
        };
        MqttTelemetryNode = new MqttPublishNodeViewModel();
        MqttTelemetryVM = new MqttConfigDialogViewModel(MqttTelemetryNode, TagCatalog ?? new TagCatalogViewModel());
        MqttCommandNode = new MqttSubscribeNodeViewModel();
        MqttTelemetryNode.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MqttTelemetryNode.Topic)) MqttPublishTopic = MqttTelemetryNode.Topic;
            NotifyMqttPubSummary();
        };
        MqttTelemetryNode.Fields.CollectionChanged += (s, e) => NotifyMqttPubSummary();

        MqttCommandNode.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MqttCommandNode.Topic)) MqttSubscribeTopic = MqttCommandNode.Topic;
            NotifyMqttSubSummary();
        };
        MqttCommandNode.Pins.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MqttPinBindingViewModel p in e.NewItems)
                {
                    p.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(p.TargetTag)) OnPropertyChanged(nameof(MqttMappedTagsSummaryText));
                    };
                }
            }
            NotifyMqttSubSummary();
        };
        foreach (var p in MqttCommandNode.Pins)
        {
            p.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(p.TargetTag)) OnPropertyChanged(nameof(MqttMappedTagsSummaryText));
            };
        }
        MqttCommandVM = new MqttSubConfigDialogViewModel(MqttCommandNode, TagCatalog ?? new TagCatalogViewModel());
        HttpNode = new HttpClientNodeViewModel();
        HttpVM = new HttpClientConfigDialogViewModel(HttpNode, TagCatalog ?? new TagCatalogViewModel());
        HttpVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HttpVM.Url)) HttpUrl = HttpVM.Url;
            if (e.PropertyName == nameof(HttpVM.Method)) HttpMethod = HttpVM.Method;
            if (e.PropertyName == nameof(HttpVM.CustomHeaders)) HttpHeaders = HttpVM.CustomHeaders;
            if (e.PropertyName == nameof(HttpVM.TimeoutSec)) HttpTimeoutSec = HttpVM.TimeoutSec;
            if (e.PropertyName == nameof(HttpVM.PeriodicIntervalSec)) HttpIntervalSec = HttpVM.PeriodicIntervalSec;
            if (e.PropertyName == nameof(HttpVM.TriggerMode)) HttpTriggerMode = HttpVM.TriggerMode == "Cyclic" ? "Periodic" : "OnDataReceived";
            OnPropertyChanged(nameof(HttpEndpointDisplay));
            OnPropertyChanged(nameof(HttpMethodDisplay));
            OnPropertyChanged(nameof(HttpTimeoutDisplay));
            OnPropertyChanged(nameof(HttpHeadersDisplay));
            OnPropertyChanged(nameof(HttpFieldCountDisplay));
            OnPropertyChanged(nameof(HttpSidebarBadgeText));
        };

        TcpNode = new TcpClientNodeViewModel();
        TcpVM = new TcpClientConfigDialogViewModel(TcpNode, TagCatalog ?? new TagCatalogViewModel());
        TcpVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TcpVM.RemoteHost)) TcpRemoteHost = TcpVM.RemoteHost;
            if (e.PropertyName == nameof(TcpVM.RemotePort)) TcpRemotePort = TcpVM.RemotePort;
            if (e.PropertyName == nameof(TcpVM.ReconnectDelaySec)) TcpReconnectDelaySec = TcpVM.ReconnectDelaySec;
            if (e.PropertyName == nameof(TcpVM.HeartbeatIntervalSec)) TcpHeartbeatIntervalSec = TcpVM.HeartbeatIntervalSec;
            if (e.PropertyName == nameof(TcpVM.HeartbeatPayload)) TcpHeartbeatPayload = TcpVM.HeartbeatPayload;
            if (e.PropertyName == nameof(TcpVM.RegistrationType)) TcpRegistrationType = TcpVM.RegistrationType;
            if (e.PropertyName == nameof(TcpVM.RegistrationPayload)) TcpRegistrationPayload = TcpVM.RegistrationPayload;
            OnPropertyChanged(nameof(TcpServerDisplay));
            OnPropertyChanged(nameof(TcpPortDisplay));
            OnPropertyChanged(nameof(TcpHeartbeatDisplay));
            OnPropertyChanged(nameof(TcpRegDisplay));
            OnPropertyChanged(nameof(TcpSidebarBadgeText));
        };

        ZigbeeVM = new ZigbeeConfigViewModel();
        if (_metadata.ZigbeeConfig != null)
        {
            ZigbeeVM.LoadFromModel(_metadata.ZigbeeConfig);
        }
        ZigbeeVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ZigbeeConfigViewModel.StatusSummaryBadge) ||
                e.PropertyName == nameof(ZigbeeConfigViewModel.StationId) ||
                e.PropertyName == nameof(ZigbeeConfigViewModel.IsEnabled))
            {
                OnPropertyChanged(nameof(ZigbeeSidebarBadgeText));
            }
        };
        LiveTestInspectorVM = new ModbusLiveTestInspectorViewModel();
        LiveTestInspectorVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LiveTestInspectorVM.InspectorHeaderTitle) ||
                e.PropertyName == nameof(LiveTestInspectorVM.InspectorHeaderSubtitle))
            {
                OnPropertyChanged(nameof(ActiveInspectorTitle));
                OnPropertyChanged(nameof(ActiveInspectorSubtitle));
            }
        };
        ModbusPortConfigVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ModbusPortConfigViewModel.SelectedPort))
            {
                SyncFromSelectedPort();
                OnPropertyChanged(nameof(Rs485StatusBadge));
                OnPropertyChanged(nameof(SummaryTopologyText));
            }
            if (e.PropertyName == nameof(ModbusPortConfigViewModel.SelectedFixedRegisterItem))
            {
                if (ModbusPortConfigVM.SelectedPort?.IsSlaveMode == true)
                {
                    LiveTestInspectorVM.SelectedSlaveRegister = ModbusPortConfigVM.SelectedFixedRegisterItem;
                }
            }
        };

        WireModbusPortEvents();

        // Mẫu mặc định 1 thanh ghi Modbus phục vụ DTU Quick Wizard
        FastRegisters.Add(new DtuFastRegisterItem(1, 40001, ModbusDataType.Real, "NhietDo_C", "Cảm biến nhiệt độ"));
        FastRegisters.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(SummaryTopologyText));
            OnPropertyChanged(nameof(Rs485StatusBadge));
            UpdateSummaryAndPreview();
        };

        ReloadFromMetadata();
        SyncFromSelectedPort();
        UpdateJsonPreview();
        UpdateSummaryAndPreview();

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Rs485StatusBadge));
        OnPropertyChanged(nameof(WifiStatusBadge));
        OnPropertyChanged(nameof(UplinkStatusBadge));
        OnPropertyChanged(nameof(ActiveInspectorTitle));
        OnPropertyChanged(nameof(ActiveInspectorSubtitle));
        OnPropertyChanged(nameof(FormattedRssi));
        OnPropertyChanged(nameof(SignalQualityPercent));
        OnPropertyChanged(nameof(WifiSsidDisplay));
        OnPropertyChanged(nameof(WifiIpAssignmentDisplay));
        OnPropertyChanged(nameof(ConnectionStateText));
        OnPropertyChanged(nameof(MqttTlsSecurityDisplay));
        OnPropertyChanged(nameof(MqttAuthTypeDisplay));
        OnPropertyChanged(nameof(MqttPublishModeDisplay));
        OnPropertyChanged(nameof(MqttPublishFieldsSummaryText));
        OnPropertyChanged(nameof(MqttSubscribePinsSummaryText));
        OnPropertyChanged(nameof(MqttEstimatedPayloadSizeText));
        OnPropertyChanged(nameof(MqttSidebarBadgeText));
        OnPropertyChanged(nameof(HttpSidebarBadgeText));
        OnPropertyChanged(nameof(TcpSidebarBadgeText));
        OnPropertyChanged(nameof(ZigbeeSidebarBadgeText));
    }

    private readonly HashSet<ModbusPortItemViewModel> _wiredPorts = new();

    /// <summary>
    /// Đăng ký theo dõi sự kiện thay đổi cổng và kênh truyền Modbus để cập nhật trạng thái huy hiệu Sidebar.
    /// </summary>
    private void WireModbusPortEvents()
    {
        if (ModbusPortConfigVM == null) return;

        void SubscribePort(ModbusPortItemViewModel port)
        {
            if (!_wiredPorts.Add(port)) return;

            port.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(ModbusPortItemViewModel.Enabled)
                    or nameof(ModbusPortItemViewModel.Role)
                    or nameof(ModbusPortItemViewModel.BaudRate))
                {
                    OnPropertyChanged(nameof(Rs485StatusBadge));
                    OnPropertyChanged(nameof(SummaryTopologyText));
                }
            };
            port.MasterChannels.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(Rs485StatusBadge));
                OnPropertyChanged(nameof(SummaryTopologyText));
            };
        }

        foreach (var port in ModbusPortConfigVM.Ports)
        {
            SubscribePort(port);
        }

        ModbusPortConfigVM.Ports.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ModbusPortItemViewModel port in e.NewItems)
                {
                    SubscribePort(port);
                }
            }
            OnPropertyChanged(nameof(Rs485StatusBadge));
            OnPropertyChanged(nameof(SummaryTopologyText));
        };
    }

    /// <summary>
    /// Tải lại toàn bộ các tham số cấu hình từ ProjectMetadata vào các thuộc tính giao diện.
    /// </summary>
    public void ReloadFromMetadata()
    {
        ModbusPortConfigVM?.ReloadPortsFromMetadata();
        WireModbusPortEvents();
        OnPropertyChanged(nameof(Rs485StatusBadge));
        OnPropertyChanged(nameof(SummaryTopologyText));
        MqttBrokerConfigVM?.ReloadFromMetadata();

        // Reload Telemetry Publish
        if (MqttTelemetryNode != null)
        {
            MqttTelemetryNode.Topic = string.IsNullOrEmpty(_metadata.MqttTopic) ? "synaptix/telemetry/data" : _metadata.MqttTopic;
            MqttPublishTopic = MqttTelemetryNode.Topic;
            MqttTelemetryNode.Qos = _metadata.MqttQos;
            MqttTelemetryNode.Retain = _metadata.MqttRetain;
            MqttTelemetryNode.PublishMode = string.IsNullOrEmpty(_metadata.MqttPublishMode) ? "Cyclic" : _metadata.MqttPublishMode;
            MqttTelemetryNode.Fields.Clear();
            if (_metadata.MqttFields != null)
            {
                foreach (var f in _metadata.MqttFields)
                {
                    var matchedTag = TagCatalog?.AllTags.FirstOrDefault(t => string.Equals(t.Name, f.TagName, StringComparison.OrdinalIgnoreCase))
                                     ?? (!string.IsNullOrEmpty(f.TagName) ? new TagModel { Name = f.TagName } : null);
                    MqttTelemetryNode.Fields.Add(new MqttPayloadFieldViewModel(f.Key, matchedTag));
                }
            }
        }

        // Reload Command Subscribe
        if (MqttCommandNode != null)
        {
            MqttCommandNode.Topic = string.IsNullOrEmpty(_metadata.MqttSubTopic) ? "factory/dtu/command" : _metadata.MqttSubTopic;
            MqttSubscribeTopic = MqttCommandNode.Topic;
            MqttCommandNode.Qos = _metadata.MqttSubQos;
            MqttCommandNode.QueueMode = string.Equals(_metadata.MqttSubQueueMode, "LatestWins", StringComparison.OrdinalIgnoreCase) 
                ? MqttQueueMode.LatestWins : MqttQueueMode.Fifo;
            MqttCommandNode.Pins.Clear();
            if (_metadata.MqttSubPins != null)
            {
                foreach (var pin in _metadata.MqttSubPins)
                {
                    var dt = MqttDataType.Int16;
                    if (Enum.TryParse<MqttDataType>(pin.DataType, true, out var parsedDt)) dt = parsedDt;
                    var pinVm = new MqttPinBindingViewModel(pin.Label, pin.JsonPath, dt);
                    if (TagCatalog != null)
                    {
                        pinVm.TagProvider = pDt => TagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, pDt, includeNone: true);
                    }
                    if (!string.IsNullOrEmpty(pin.TargetTagName) && TagCatalog != null)
                    {
                        pinVm.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pin.TargetTagName, StringComparison.OrdinalIgnoreCase));
                    }
                    else if (pin.TargetTagIndex.HasValue && TagCatalog != null)
                    {
                        pinVm.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == pin.TargetTagIndex.Value);
                    }
                    MqttCommandNode.Pins.Add(pinVm);
                }
            }
        }

        NotifyAllCapabilities();
        NotifyMqttPubSummary();
        NotifyMqttSubSummary();
        SelectFirstAvailableSection();
        var dtu = _metadata.DtuConfig;
        SelectedPortIndex = dtu.PortIndex;
        SerialMode = string.IsNullOrEmpty(dtu.SerialMode) || dtu.SerialMode == "Transference" ? "ModbusMaster" : dtu.SerialMode;
        if (dtu.PublishIntervalSec > 0) PublishIntervalSec = dtu.PublishIntervalSec;
        UplinkMode = dtu.UplinkMode;
        FrameTimeoutDelayMs = dtu.FrameTimeoutDelayMs > 0 ? dtu.FrameTimeoutDelayMs : (ushort)20;
        MaxPacketSize = dtu.MaxPacketSize > 0 ? dtu.MaxPacketSize : (ushort)512;
        JsonTemplate = string.IsNullOrWhiteSpace(dtu.CustomJsonTemplate) ? JsonTemplate : dtu.CustomJsonTemplate;

        // Payload mode & field inclusions
        PayloadMode = string.IsNullOrEmpty(dtu.PayloadMode) ? "Standard" : dtu.PayloadMode;
        IncludeDeviceId = dtu.IncludeDeviceId;
        IncludeTimestamp = dtu.IncludeTimestamp;
        IncludePort = dtu.IncludePort;
        IncludeSequence = dtu.IncludeSequence;
        IncludeHexStream = dtu.IncludeHexStream;
        IncludePayloadLength = dtu.IncludePayloadLength;

        // Wi-Fi from metadata
        WifiEnabled = _metadata.WifiEnabled;
        WifiAutoReconnect = _metadata.WifiAutoReconnect;
        WifiSsid = _metadata.WifiSsid ?? string.Empty;
        WifiPassword = _metadata.WifiPassword ?? string.Empty;
        WifiDhcpEnabled = _metadata.WifiDhcpEnabled;
        WifiStaticIp = string.IsNullOrWhiteSpace(_metadata.WifiStaticIp) ? "192.168.1.50" : _metadata.WifiStaticIp;
        WifiSubnetMask = string.IsNullOrWhiteSpace(_metadata.WifiSubnetMask) ? "255.255.255.0" : _metadata.WifiSubnetMask;
        WifiGateway = string.IsNullOrWhiteSpace(_metadata.WifiGateway) ? "192.168.1.1" : _metadata.WifiGateway;
        WifiDns = string.IsNullOrWhiteSpace(_metadata.WifiDns) ? "8.8.8.8" : _metadata.WifiDns;
        if (WifiVM != null)
        {
            WifiVM.IsEnabled = WifiEnabled;
            WifiVM.AutoReconnect = WifiAutoReconnect;
            WifiVM.Ssid = WifiSsid;
            WifiVM.Password = WifiPassword;
            WifiVM.IsDhcp = WifiDhcpEnabled;
            WifiVM.StaticIp = WifiStaticIp;
            WifiVM.StaticSubnet = WifiSubnetMask;
            WifiVM.StaticGateway = WifiGateway;
            WifiVM.StaticDns = WifiDns;
        }

        SlaveMapEntries.Clear();
        if (_metadata.ModbusSlaveMapEntries != null)
        {
            foreach (var e in _metadata.ModbusSlaveMapEntries)
            {
                SlaveMapEntries.Add(e);
            }
        }

        // Port specifics from ModbusPorts[0]
        if (_metadata.ModbusPorts != null && _metadata.ModbusPorts.Count > 0)
        {
            var p0 = _metadata.ModbusPorts[0];
            PortName = string.IsNullOrEmpty(p0.PortName) ? "RS-485 #1" : p0.PortName;
            BaudRate = p0.BaudRate;
            DataBits = p0.DataBits;
            Parity = p0.Parity;
            StopBits = p0.StopBits;
            ResponseTimeoutMs = p0.ResponseTimeoutMs;
            RetryCount = p0.RetryCount > 0 ? p0.RetryCount : (ushort)1;
            InterFrameDelayMs = p0.InterFrameDelayMs > 0 ? p0.InterFrameDelayMs : (ushort)20;
            SlaveId = p0.SlaveId;
        }

        // MQTT from metadata
        MqttHost = string.IsNullOrEmpty(_metadata.MqttBrokerHost) ? MqttHost : _metadata.MqttBrokerHost;
        MqttPort = _metadata.MqttBrokerPort > 0 ? _metadata.MqttBrokerPort : 1883;
        MqttClientId = string.IsNullOrEmpty(_metadata.MqttClientId) ? MqttClientId : _metadata.MqttClientId;
        MqttUsername = _metadata.MqttUsername;
        MqttPassword = _metadata.MqttPassword;
        MqttUseTls = _metadata.MqttTlsEnabled;
        MqttKeepAlive = _metadata.MqttKeepAliveSec > 0 ? _metadata.MqttKeepAliveSec : (ushort)60;
        MqttSubscribeTopic = string.IsNullOrEmpty(_metadata.MqttSubTopic) ? MqttSubscribeTopic : _metadata.MqttSubTopic;
        MqttWillEnabled = _metadata.MqttWillEnabled;
        MqttWillTopic = string.IsNullOrEmpty(_metadata.MqttWillTopic) ? MqttWillTopic : _metadata.MqttWillTopic;
        MqttWillPayload = string.IsNullOrEmpty(_metadata.MqttWillPayload) ? MqttWillPayload : _metadata.MqttWillPayload;
        MqttWillQos = _metadata.MqttWillQos;
        MqttWillRetain = _metadata.MqttWillRetain;

        // MQTT TLS & mTLS from metadata
        MqttTlsCaCertPath = _metadata.MqttTlsCaCertPath ?? string.Empty;
        MqttTlsCaCertPem = _metadata.MqttTlsCaCertPem ?? string.Empty;
        MqttTlsAllowSelfSigned = _metadata.MqttTlsAllowSelfSigned;
        MqttTlsIgnoreHostname = _metadata.MqttTlsIgnoreHostname;
        MqttTlsProtocolVersion = _metadata.MqttTlsProtocolVersion >= 0 && _metadata.MqttTlsProtocolVersion <= 2 ? _metadata.MqttTlsProtocolVersion : 0;
        MqttTlsSniHostname = _metadata.MqttTlsSniHostname ?? string.Empty;
        MqttTlsMtlsEnabled = _metadata.MqttTlsMtlsEnabled;
        MqttTlsClientCertPath = _metadata.MqttTlsClientCertPath ?? string.Empty;
        MqttTlsClientCertPem = _metadata.MqttTlsClientCertPem ?? string.Empty;
        MqttTlsClientKeyPath = _metadata.MqttTlsClientKeyPath ?? string.Empty;
        MqttTlsClientKeyPem = _metadata.MqttTlsClientKeyPem ?? string.Empty;
        MqttTlsClientKeyPassword = _metadata.MqttTlsClientKeyPassword ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(MqttTlsCaCertPath) || !string.IsNullOrWhiteSpace(MqttTlsCaCertPem))
        {
            ParseCaCert(MqttTlsCaCertPem, MqttTlsCaCertPath);
        }
        if (!string.IsNullOrWhiteSpace(MqttTlsClientCertPath) || !string.IsNullOrWhiteSpace(MqttTlsClientCertPem))
        {
            ParseClientCert(MqttTlsClientCertPem, MqttTlsClientCertPath);
        }

        // HTTP from metadata
        if (dtu.HttpSettings != null)
        {
            HttpEnabled = dtu.HttpSettings.Enabled || UplinkMode == "Http";
            HttpUrl = string.IsNullOrEmpty(dtu.HttpSettings.Url) ? HttpUrl : dtu.HttpSettings.Url;
            HttpMethod = string.IsNullOrEmpty(dtu.HttpSettings.Method) ? HttpMethod : dtu.HttpSettings.Method;
            HttpHeaders = string.IsNullOrEmpty(dtu.HttpSettings.Headers) ? HttpHeaders : dtu.HttpSettings.Headers;
            HttpTimeoutSec = dtu.HttpSettings.TimeoutSec > 0 ? dtu.HttpSettings.TimeoutSec : (ushort)10;
            HttpIntervalSec = dtu.HttpSettings.PeriodicIntervalSec > 0 ? dtu.HttpSettings.PeriodicIntervalSec : (ushort)5;
            HttpTriggerMode = string.IsNullOrEmpty(dtu.HttpSettings.TriggerMode) ? "OnDataReceived" : dtu.HttpSettings.TriggerMode;

            HttpFields.Clear();
            if (dtu.HttpSettings.Fields != null)
            {
                foreach (var f in dtu.HttpSettings.Fields)
                {
                    HttpFields.Add(new DtuHttpFieldModel { Key = f.Key, ValueSource = f.ValueSource, CustomValue = f.CustomValue });
                }
            }
            if (HttpFields.Count == 0)
            {
                HttpFields.Add(new DtuHttpFieldModel { Key = "device_id", ValueSource = "DeviceId" });
                HttpFields.Add(new DtuHttpFieldModel { Key = "payload", ValueSource = "Payload" });
                HttpFields.Add(new DtuHttpFieldModel { Key = "timestamp", ValueSource = "Timestamp" });
            }
        }

        // TCP from metadata
        if (dtu.TcpSettings != null)
        {
            TcpEnabled = dtu.TcpSettings.Enabled || UplinkMode is "TcpClient" or "TcpServer";
            TcpSocketMode = string.IsNullOrEmpty(dtu.TcpSettings.SocketMode) ? TcpSocketMode : dtu.TcpSettings.SocketMode;
            TcpRemoteHost = string.IsNullOrEmpty(dtu.TcpSettings.RemoteHost) ? TcpRemoteHost : dtu.TcpSettings.RemoteHost;
            TcpRemotePort = dtu.TcpSettings.RemotePort > 0 ? dtu.TcpSettings.RemotePort : (ushort)8888;
            TcpLocalPort = dtu.TcpSettings.LocalPort > 0 ? dtu.TcpSettings.LocalPort : (ushort)8888;
            TcpReconnectDelaySec = dtu.TcpSettings.ReconnectDelaySec > 0 ? dtu.TcpSettings.ReconnectDelaySec : (ushort)5;
            TcpHeartbeatIntervalSec = dtu.TcpSettings.HeartbeatIntervalSec;
            TcpHeartbeatPayload = dtu.TcpSettings.HeartbeatPayload ?? string.Empty;
            TcpRegistrationType = string.IsNullOrEmpty(dtu.TcpSettings.RegistrationType) ? "None" : dtu.TcpSettings.RegistrationType;
            TcpRegistrationPayload = string.IsNullOrEmpty(dtu.TcpSettings.RegistrationPayload) ? "{{DEVICE_ID}}" : dtu.TcpSettings.RegistrationPayload;
        }

        if (HttpVM != null)
        {
            HttpVM.Url = HttpUrl;
            HttpVM.Method = HttpMethod;
            HttpVM.CustomHeaders = HttpHeaders;
            HttpVM.TimeoutSec = HttpTimeoutSec;
            HttpVM.PeriodicIntervalSec = HttpIntervalSec;
            HttpVM.TriggerMode = HttpTriggerMode == "OnDataReceived" ? "Event" : "Cyclic";
        }

        if (TcpVM != null)
        {
            TcpVM.RemoteHost = TcpRemoteHost;
            TcpVM.RemotePort = TcpRemotePort;
            TcpVM.ReconnectDelaySec = TcpReconnectDelaySec;
            TcpVM.HeartbeatIntervalSec = TcpHeartbeatIntervalSec;
            TcpVM.HeartbeatPayload = TcpHeartbeatPayload;
            TcpVM.RegistrationType = TcpRegistrationType;
            TcpVM.RegistrationPayload = TcpRegistrationPayload;
        }

        if (_metadata.ZigbeeConfig != null && ZigbeeVM != null)
        {
            ZigbeeVM.LoadFromModel(_metadata.ZigbeeConfig);
        }
    }

    public void SaveToMetadata()
    {
        if (ModbusPortConfigVM?.SelectedPort != null)
        {
            var sel = ModbusPortConfigVM.SelectedPort;
            sel.PortName = PortName;
            sel.BaudRate = BaudRate;
            sel.DataBits = DataBits;
            sel.Parity = Parity;
            sel.StopBits = StopBits;
            sel.ResponseTimeoutMs = ResponseTimeoutMs;
            sel.RetryCount = RetryCount;
            sel.InterFrameDelayMs = InterFrameDelayMs;
            sel.SlaveId = SlaveId;
            sel.Role = SerialMode == "ModbusSlave" ? "Slave" : "Master";
        }
        ModbusPortConfigVM?.Apply();
        if (MqttBrokerConfigVM != null)
        {
            MqttBrokerConfigVM.Host = MqttHost;
            MqttBrokerConfigVM.Port = MqttPort;
            MqttBrokerConfigVM.ClientId = MqttClientId;
            MqttBrokerConfigVM.Username = MqttUsername;
            MqttBrokerConfigVM.Password = MqttPassword;
            MqttBrokerConfigVM.UseTls = MqttUseTls;
            MqttBrokerConfigVM.KeepAliveSec = MqttKeepAlive;
            MqttBrokerConfigVM.WillEnabled = MqttWillEnabled;
            MqttBrokerConfigVM.WillTopic = MqttWillTopic;
            MqttBrokerConfigVM.WillPayload = MqttWillPayload;
            MqttBrokerConfigVM.WillQos = (byte)MqttWillQos;
            MqttBrokerConfigVM.WillRetain = MqttWillRetain;
            MqttBrokerConfigVM.CaCertPath = MqttTlsCaCertPath;
            MqttBrokerConfigVM.CaCertPem = MqttTlsCaCertPem;
            MqttBrokerConfigVM.AllowSelfSigned = MqttTlsAllowSelfSigned;
            MqttBrokerConfigVM.IgnoreHostname = MqttTlsIgnoreHostname;
            MqttBrokerConfigVM.TlsVersionIndex = MqttTlsProtocolVersion;
            MqttBrokerConfigVM.SniHostname = MqttTlsSniHostname;
            MqttBrokerConfigVM.UseMtls = MqttTlsMtlsEnabled;
            MqttBrokerConfigVM.ClientCertPath = MqttTlsClientCertPath;
            MqttBrokerConfigVM.ClientCertPem = MqttTlsClientCertPem;
            MqttBrokerConfigVM.ClientKeyPath = MqttTlsClientKeyPath;
            MqttBrokerConfigVM.ClientKeyPem = MqttTlsClientKeyPem;
            MqttBrokerConfigVM.ClientKeyPassword = MqttTlsClientKeyPassword;
            MqttBrokerConfigVM.Apply();
        }

        // Save Telemetry Publish
        if (MqttTelemetryNode != null)
        {
            if (!string.IsNullOrEmpty(MqttPublishTopic))
                MqttTelemetryNode.Topic = MqttPublishTopic;
            _metadata.MqttTopic = MqttTelemetryNode.Topic;
            _metadata.MqttQos = MqttTelemetryNode.Qos;
            _metadata.MqttRetain = MqttTelemetryNode.Retain;
            _metadata.MqttPublishMode = MqttTelemetryNode.PublishMode;
            _metadata.MqttFields = MqttTelemetryNode.Fields.Select(f => new MqttPayloadFieldData
            {
                Key = f.Key,
                TagName = f.Tag?.Name ?? string.Empty,
                Unit = string.Empty
            }).ToList();
        }

        // Save Command Subscribe
        if (MqttCommandNode != null)
        {
            if (!string.IsNullOrEmpty(MqttSubscribeTopic))
                MqttCommandNode.Topic = MqttSubscribeTopic;
            _metadata.MqttSubTopic = MqttCommandNode.Topic;
            _metadata.MqttSubQos = MqttCommandNode.Qos;
            _metadata.MqttSubQueueMode = MqttCommandNode.QueueMode.ToString();
            _metadata.MqttSubPins = MqttCommandNode.Pins.Select(p => new MqttSubPinData
            {
                Id = p.Id,
                Label = p.Label,
                JsonPath = p.JsonPath,
                DataType = p.DataType.ToString(),
                TargetTagName = p.TargetTag?.Name ?? string.Empty,
                TargetTagIndex = p.TargetTagIndex
            }).ToList();
        }
        var dtu = _metadata.DtuConfig;
        dtu.Enabled = true;
        dtu.PortIndex = (byte)SelectedPortIndex;
        dtu.SerialMode = SerialMode;
        dtu.PublishIntervalSec = PublishIntervalSec;
        dtu.UplinkMode = UplinkMode;
        dtu.FrameTimeoutDelayMs = FrameTimeoutDelayMs;
        dtu.MaxPacketSize = MaxPacketSize;
        dtu.CustomJsonTemplate = JsonTemplate;
        dtu.PayloadMode = PayloadMode;
        dtu.IncludeDeviceId = IncludeDeviceId;
        dtu.IncludeTimestamp = IncludeTimestamp;
        dtu.IncludePort = IncludePort;
        dtu.IncludeSequence = IncludeSequence;
        dtu.IncludeHexStream = IncludeHexStream;
        dtu.IncludePayloadLength = IncludePayloadLength;

        // Save Wi-Fi
        _metadata.WifiEnabled = WifiEnabled;
        _metadata.WifiAutoReconnect = WifiAutoReconnect;
        _metadata.WifiSsid = WifiSsid;
        _metadata.WifiPassword = WifiPassword;
        _metadata.WifiDhcpEnabled = WifiDhcpEnabled;
        _metadata.WifiStaticIp = WifiStaticIp;
        _metadata.WifiSubnetMask = WifiSubnetMask;
        _metadata.WifiGateway = WifiGateway;
        _metadata.WifiDns = WifiDns;

        // Auto-register FastRegisters tags in TagCatalog
        if (TagCatalog != null)
        {
            foreach (var reg in FastRegisters)
            {
                if (!string.IsNullOrWhiteSpace(reg.TagName) && !TagCatalog.AllTags.Any(t => string.Equals(t.Name, reg.TagName, StringComparison.OrdinalIgnoreCase)))
                {
                    var tagType = reg.DataType switch
                    {
                        ModbusDataType.Bool => SimplePLC.Domain.Enums.TagDataType.Boolean,
                        ModbusDataType.Real => SimplePLC.Domain.Enums.TagDataType.Float32,
                        _ => SimplePLC.Domain.Enums.TagDataType.Int32
                    };
                    var newTag = new TagModel
                    {
                        Name = reg.TagName,
                        Kind = SimplePLC.Domain.Enums.TagKind.ModbusHolding,
                        DataType = tagType,
                        Alias = reg.Description
                    };
                    TagCatalog.RegisterTags.Add(newTag);
                }
            }
        }

        _metadata.ModbusSlaveMapEntries = new List<ModbusSlaveMapEntryModel>(SlaveMapEntries);

        // Save RS-485 fallback when ModbusPortConfigVM is null
        if (ModbusPortConfigVM == null && _metadata.ModbusPorts != null && _metadata.ModbusPorts.Count > 0)
        {
            var p0 = _metadata.ModbusPorts[0];
            p0.PortName = PortName;
            p0.BaudRate = BaudRate;
            p0.DataBits = DataBits;
            p0.Parity = Parity;
            p0.StopBits = StopBits;
            p0.ResponseTimeoutMs = ResponseTimeoutMs;
            p0.RetryCount = RetryCount;
            p0.InterFrameDelayMs = InterFrameDelayMs;
            p0.SlaveId = SlaveId;
            p0.Role = SerialMode == "ModbusSlave" ? "Slave" : "Master";
        }

        // Sync local presentation properties from _metadata (Single Source of Truth)
        MqttHost = _metadata.MqttBrokerHost;
        MqttPort = _metadata.MqttBrokerPort;
        MqttClientId = _metadata.MqttClientId;
        MqttUsername = _metadata.MqttUsername;
        MqttPassword = _metadata.MqttPassword;
        MqttUseTls = _metadata.MqttTlsEnabled;
        MqttKeepAlive = _metadata.MqttKeepAliveSec;
        MqttSubscribeTopic = _metadata.MqttSubTopic;
        MqttWillEnabled = _metadata.MqttWillEnabled;
        MqttWillTopic = _metadata.MqttWillTopic;
        MqttWillPayload = _metadata.MqttWillPayload;
        MqttWillQos = _metadata.MqttWillQos;
        MqttWillRetain = _metadata.MqttWillRetain;
        MqttTlsCaCertPath = _metadata.MqttTlsCaCertPath;
        MqttTlsCaCertPem = _metadata.MqttTlsCaCertPem;
        MqttTlsAllowSelfSigned = _metadata.MqttTlsAllowSelfSigned;
        MqttTlsIgnoreHostname = _metadata.MqttTlsIgnoreHostname;
        MqttTlsProtocolVersion = _metadata.MqttTlsProtocolVersion;
        MqttTlsSniHostname = _metadata.MqttTlsSniHostname;
        MqttTlsMtlsEnabled = _metadata.MqttTlsMtlsEnabled;
        MqttTlsClientCertPath = _metadata.MqttTlsClientCertPath;
        MqttTlsClientCertPem = _metadata.MqttTlsClientCertPem;
        MqttTlsClientKeyPath = _metadata.MqttTlsClientKeyPath;
        MqttTlsClientKeyPem = _metadata.MqttTlsClientKeyPem;
        MqttTlsClientKeyPassword = _metadata.MqttTlsClientKeyPassword;

        // Sync VM state before save
        if (HttpVM != null)
        {
            HttpVM.Url = HttpUrl;
            HttpVM.Method = HttpMethod;
            HttpVM.CustomHeaders = HttpHeaders;
            HttpVM.TimeoutSec = HttpTimeoutSec;
            HttpVM.PeriodicIntervalSec = HttpIntervalSec;
            HttpVM.TriggerMode = HttpTriggerMode == "Periodic" ? "Cyclic" : "Event";
            HttpVM.Apply();
        }
        if (TcpVM != null)
        {
            TcpVM.RemoteHost = TcpRemoteHost;
            TcpVM.RemotePort = TcpRemotePort;
            TcpVM.ReconnectDelaySec = TcpReconnectDelaySec;
            TcpVM.HeartbeatIntervalSec = TcpHeartbeatIntervalSec;
            TcpVM.HeartbeatPayload = TcpHeartbeatPayload;
            TcpVM.RegistrationType = TcpRegistrationType;
            TcpVM.RegistrationPayload = TcpRegistrationPayload;
            TcpVM.Apply();
        }

        // Save HTTP
        dtu.HttpSettings.Enabled = HttpEnabled || UplinkMode == "Http";
        dtu.HttpSettings.Url = HttpUrl;
        dtu.HttpSettings.Method = HttpMethod;
        dtu.HttpSettings.Headers = HttpHeaders;
        dtu.HttpSettings.TimeoutSec = HttpTimeoutSec;
        dtu.HttpSettings.PeriodicIntervalSec = HttpIntervalSec;
        dtu.HttpSettings.TriggerMode = HttpTriggerMode;
        dtu.HttpSettings.Fields = System.Linq.Enumerable.ToList(HttpFields);

        // Save TCP
        dtu.TcpSettings.Enabled = TcpEnabled || UplinkMode is "TcpClient" or "TcpServer";
        dtu.TcpSettings.SocketMode = TcpSocketMode;
        dtu.TcpSettings.RemoteHost = TcpRemoteHost;
        dtu.TcpSettings.RemotePort = TcpRemotePort;
        dtu.TcpSettings.LocalPort = TcpLocalPort;
        dtu.TcpSettings.ReconnectDelaySec = TcpReconnectDelaySec;
        dtu.TcpSettings.HeartbeatIntervalSec = TcpHeartbeatIntervalSec;
        dtu.TcpSettings.HeartbeatPayload = TcpHeartbeatPayload;
        dtu.TcpSettings.RegistrationType = TcpRegistrationType;
        dtu.TcpSettings.RegistrationPayload = TcpRegistrationPayload;

        if (_metadata.ZigbeeConfig == null) _metadata.ZigbeeConfig = new ZigbeeConfigModel();
        ZigbeeVM.SaveToModel(_metadata.ZigbeeConfig);
        _metadata.LastModified = DateTime.UtcNow;
    }

    partial void OnMqttUseTlsChanged(bool value)
    {
        if (value && MqttPort == 1883)
        {
            MqttPort = 8883;
        }
        else if (!value && MqttPort == 8883)
        {
            MqttPort = 1883;
        }

        if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.UseTls != value)
        {
            MqttBrokerConfigVM.UseTls = value;
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp chứng chỉ Root CA của máy chủ Broker (.crt, .cer, .pem).
    /// </summary>
    [RelayCommand]
    public void BrowseCaCert()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerCaCertLabel"],
            Filter = "Certificate Files (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                string pemContent = File.ReadAllText(dialog.FileName);
                ParseCaCert(pemContent, dialog.FileName);
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load CA cert: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa cấu hình chứng chỉ CA riêng và quay về Root CA hệ thống.
    /// </summary>
    [RelayCommand]
    public void ClearCaCert()
    {
        MqttTlsCaCertPath = string.Empty;
        MqttTlsCaCertPem = string.Empty;
        CaCertSubject = string.Empty;
        CaCertIssuer = string.Empty;
        CaCertExpiry = string.Empty;
        CaCertSha256 = string.Empty;
        OnPropertyChanged(nameof(HasCaCert));
    }

    /// <summary>
    /// Phân tích và nạp thông tin chứng chỉ máy chủ (CA Root Certificate).
    /// </summary>
    private void ParseCaCert(string pemContent, string filePath)
    {
        try
        {
            X509Certificate2 cert;
            if (File.Exists(filePath))
            {
                cert = new X509Certificate2(filePath);
            }
            else
            {
                cert = X509Certificate2.CreateFromPem(pemContent);
            }

            MqttTlsCaCertPath = filePath;
            MqttTlsCaCertPem = pemContent;
            CaCertSubject = cert.Subject;
            CaCertIssuer = cert.Issuer;
            CaCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            CaCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            OnPropertyChanged(nameof(HasCaCert));
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to parse CA cert: {ex.Message}");
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp chứng chỉ thiết bị Client (.crt, .cer, .pem, .pfx, .p12).
    /// </summary>
    [RelayCommand]
    public void BrowseClientCert()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerClientCertLabel"],
            Filter = "Certificate / Bundle Files (*.crt;*.cer;*.pem;*.pfx;*.p12)|*.crt;*.cer;*.pem;*.pfx;*.p12|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext is ".pfx" or ".p12")
                {
                    MqttTlsClientCertPath = dialog.FileName;
                    MqttTlsClientCertPem = string.Empty;
                    var cert = new X509Certificate2(dialog.FileName, MqttTlsClientKeyPassword, X509KeyStorageFlags.EphemeralKeySet);
                    ClientCertSubject = cert.Subject;
                    ClientCertIssuer = cert.Issuer;
                    ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");
                    byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
                    ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
                    MqttTlsClientKeyPath = dialog.FileName;
                    OnPropertyChanged(nameof(HasClientCert));
                    OnPropertyChanged(nameof(HasClientKey));
                }
                else
                {
                    string pemContent = File.ReadAllText(dialog.FileName);
                    ParseClientCert(pemContent, dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load Client cert: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa chứng chỉ Client Certificate.
    /// </summary>
    [RelayCommand]
    public void ClearClientCert()
    {
        MqttTlsClientCertPath = string.Empty;
        MqttTlsClientCertPem = string.Empty;
        ClientCertSubject = string.Empty;
        ClientCertIssuer = string.Empty;
        ClientCertExpiry = string.Empty;
        ClientCertSha256 = string.Empty;
        OnPropertyChanged(nameof(HasClientCert));
    }

    /// <summary>
    /// Phân tích và nạp thông tin chứng chỉ Client.
    /// </summary>
    private void ParseClientCert(string pemContent, string filePath)
    {
        try
        {
            X509Certificate2 cert;
            if (File.Exists(filePath))
            {
                cert = new X509Certificate2(filePath);
            }
            else
            {
                cert = X509Certificate2.CreateFromPem(pemContent);
            }

            MqttTlsClientCertPath = filePath;
            MqttTlsClientCertPem = pemContent;
            ClientCertSubject = cert.Subject;
            ClientCertIssuer = cert.Issuer;
            ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            OnPropertyChanged(nameof(HasClientCert));
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to parse Client cert: {ex.Message}");
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn tệp khóa bí mật Client Private Key (.key, .pem).
    /// </summary>
    [RelayCommand]
    public void BrowseClientKey()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.Instance["MqttBrokerClientKeyLabel"],
            Filter = "Key Files (*.key;*.pem)|*.key;*.pem|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                MqttTlsClientKeyPath = dialog.FileName;
                MqttTlsClientKeyPem = File.ReadAllText(dialog.FileName);
                OnPropertyChanged(nameof(HasClientKey));
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TLS ERROR] Failed to load Client Key: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xóa tệp khóa bí mật Client Private Key.
    /// </summary>
    [RelayCommand]
    public void ClearClientKey()
    {
        MqttTlsClientKeyPath = string.Empty;
        MqttTlsClientKeyPem = string.Empty;
        OnPropertyChanged(nameof(HasClientKey));
    }

    partial void OnJsonTemplateChanged(string value) => UpdateJsonPreview();
    partial void OnSampleSerialInputChanged(string value) => UpdateJsonPreview();
    partial void OnPayloadModeChanged(string value) => UpdateJsonPreview();
    partial void OnIncludeDeviceIdChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeTimestampChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludePortChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeSequenceChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludeHexStreamChanged(bool value) => UpdateJsonPreview();
    partial void OnIncludePayloadLengthChanged(bool value) => UpdateJsonPreview();
    partial void OnTerminalSendFormatChanged(string value) => OnPropertyChanged(nameof(TestPacketIsHex));
    partial void OnHttpTriggerModeChanged(string value) => OnPropertyChanged(nameof(IsHttpPeriodicTrigger));

    public void UpdateJsonPreview()
    {
        if (PayloadMode == "Raw")
        {
            JsonPreview = SampleSerialInput ?? string.Empty;
            IsJsonSyntaxValid = true;
            return;
        }

        if (PayloadMode == "Standard")
        {
            string stdSample = SampleSerialInput ?? string.Empty;
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            if (IncludeDeviceId) dict["device_id"] = string.IsNullOrEmpty(MqttClientId) ? "SYNX-DTU-01" : MqttClientId;
            if (IncludeTimestamp) dict["timestamp"] = DateTime.UtcNow.ToString("o");
            if (IncludePort) dict["port"] = SelectedPortIndex + 1;
            if (IncludeSequence) dict["seq"] = PacketSequenceCounter;
            if (IncludePayloadLength) dict["len"] = stdSample.Length;
            if (IncludeHexStream) dict["hex"] = ConvertToHexString(stdSample);
            dict["payload"] = stdSample;
            dict["status"] = "OK";

            try
            {
                JsonPreview = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                IsJsonSyntaxValid = true;
            }
            catch
            {
                JsonPreview = stdSample;
                IsJsonSyntaxValid = false;
            }
            return;
        }

        string raw = JsonTemplate;
        string sample = SampleSerialInput ?? string.Empty;
        string hex = ConvertToHexString(sample);

        string resolved = raw
            .Replace("{{DEVICE_ID}}", MqttClientId)

            .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
            .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
            .Replace("{{BAUDRATE}}", BaudRate.ToString())
            .Replace("{{SERIAL_PAYLOAD}}", sample)
            .Replace("{{HEX_STREAM}}", hex)
            .Replace("{{PAYLOAD_LEN}}", sample.Length.ToString())
            .Replace("{{PACKET_SEQ}}", PacketSequenceCounter.ToString());

        try
        {
            using var doc = JsonDocument.Parse(resolved);
            JsonPreview = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            IsJsonSyntaxValid = true;
        }
        catch
        {
            JsonPreview = resolved;
            IsJsonSyntaxValid = false;
        }
    }

    private static string ConvertToHexString(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    [RelayCommand]
    private void SendTestPacket()
    {
        if (string.IsNullOrWhiteSpace(TestPacketInput))
            return;

        string packet = TestPacketInput.Trim();
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");


        if (TerminalSendFormat == "Hex")
        {
            string cleanHex = packet.Replace(" ", "").Replace("-", "");
            if (cleanHex.Length == 0 || cleanHex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(cleanHex, @"\A\b[0-9a-fA-F]+\b\Z"))
            {
                AppendLog($"[{timestamp}] " + LocalizationService.Instance["DtuTerminalInvalidHexError"]);
                return;
            }

            AppendLog($"[{timestamp}] [TX-HEX RS-485 #{SelectedPortIndex + 1}] Hex Stream: {packet} ({cleanHex.Length / 2}B)");
        }
        else
        {
            AppendLog($"[{timestamp}] [TX-ASCII RS-485 #{SelectedPortIndex + 1}] Raw bytes: \"{packet}\" ({packet.Length}B, Hex: {ConvertToHexString(packet)})");
        }

        PacketSequenceCounter++;




        // Format to JSON
        string outgoingPayload;
        if (PayloadMode == "Raw")
        {
            outgoingPayload = packet;
        }
        else if (PayloadMode == "Standard")
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            if (IncludeDeviceId) dict["device_id"] = string.IsNullOrEmpty(MqttClientId) ? "SYNX-DTU-01" : MqttClientId;
            if (IncludeTimestamp) dict["timestamp"] = DateTime.UtcNow.ToString("o");
            if (IncludePort) dict["port"] = SelectedPortIndex + 1;
            if (IncludeSequence) dict["seq"] = PacketSequenceCounter;
            if (IncludePayloadLength) dict["len"] = packet.Length;
            if (IncludeHexStream) dict["hex"] = ConvertToHexString(packet);
            dict["payload"] = packet;
            dict["status"] = "OK";
            outgoingPayload = JsonSerializer.Serialize(dict);
        }
        else
        {
            outgoingPayload = JsonTemplate
                .Replace("{{DEVICE_ID}}", MqttClientId)
                .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("o"))
                .Replace("{{PORT}}", (SelectedPortIndex + 1).ToString())
                .Replace("{{BAUDRATE}}", BaudRate.ToString())
                .Replace("{{SERIAL_PAYLOAD}}", packet)
                .Replace("{{HEX_STREAM}}", ConvertToHexString(packet))
                .Replace("{{PAYLOAD_LEN}}", packet.Length.ToString())
                .Replace("{{PACKET_SEQ}}", PacketSequenceCounter.ToString());
    
        }

        string jsonPayload = outgoingPayload;




        // Simulate Uplink forwarding
        switch (UplinkMode)
        {
            case "Mqtt":
                AppendLog($"[{timestamp}] [UPLINK MQTT] Publishing {jsonPayload.Length}B to topic '{MqttPublishTopic}' (QoS {MqttQos}, TLS: {MqttUseTls})...");
                AppendLog($"[{timestamp}] [MQTT BROKER ACK] Message PUBACK received from {MqttHost}:{MqttPort}.");
                break;
            case "Http":
                AppendLog($"[{timestamp}] [UPLINK HTTP] {HttpMethod} {HttpUrl} ({jsonPayload.Length}B)...");
                AppendLog($"[{timestamp}] [HTTP RESPONSE] 200 OK (Response: {{\"status\":\"success\",\"records_inserted\":1}})");
                break;
            case "TcpClient":
                string prefixInfo = TcpRegistrationType == "PrependToEveryPacket" ? $" [RegPrefix: {TcpRegistrationPayload}]" : "";
                AppendLog($"[{timestamp}] [UPLINK TCP CLIENT] Streamed {jsonPayload.Length}B to {TcpRemoteHost}:{TcpRemotePort}{prefixInfo}...");
                AppendLog($"[{timestamp}] [TCP SERVER ECHO] ACK 0x06 (Frame processed).");
                break;
            case "TcpServer":
                AppendLog($"[{timestamp}] [UPLINK TCP SERVER] Pushed {jsonPayload.Length}B to connected client on local port {TcpLocalPort}...");
                AppendLog($"[{timestamp}] [CLIENT ACK] Received 0x06.");
                UpdateJsonPreview();
                break;
        }
    }

    [RelayCommand]
    private void ClearTerminal()
    {
        TerminalLog = $"--- SYNAPTIX DTU GATEWAY DIAGNOSTIC TERMINAL ---\n[{DateTime.Now:HH:mm:ss}] Terminal cleared.\n";
    }

    [RelayCommand]

    partial void OnMqttPublishTopicChanged(string value) 
    { 
        if (MqttTelemetryNode != null && MqttTelemetryNode.Topic != value) MqttTelemetryNode.Topic = value; 
        OnPropertyChanged(nameof(MqttTopic));
    }
    partial void OnMqttSubscribeTopicChanged(string value) { if (MqttCommandNode != null && MqttCommandNode.Topic != value) MqttCommandNode.Topic = value; }
    partial void OnMqttHostChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.Host != value) MqttBrokerConfigVM.Host = value; }
    partial void OnMqttPortChanged(int value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.Port != value) MqttBrokerConfigVM.Port = value; }
    partial void OnWifiEnabledChanged(bool value) { if (WifiVM != null && WifiVM.IsEnabled != value) WifiVM.IsEnabled = value; }
    partial void OnWifiAutoReconnectChanged(bool value) { if (WifiVM != null && WifiVM.AutoReconnect != value) WifiVM.AutoReconnect = value; }
    partial void OnWifiSsidChanged(string value) { if (WifiVM != null && WifiVM.Ssid != value) WifiVM.Ssid = value; }
    partial void OnWifiPasswordChanged(string value) { if (WifiVM != null && WifiVM.Password != value) WifiVM.Password = value; }
    partial void OnWifiDhcpEnabledChanged(bool value) { if (WifiVM != null && WifiVM.IsDhcp != value) WifiVM.IsDhcp = value; }
    partial void OnWifiStaticIpChanged(string value) { if (WifiVM != null && WifiVM.StaticIp != value) WifiVM.StaticIp = value; }
    partial void OnWifiSubnetMaskChanged(string value) { if (WifiVM != null && WifiVM.StaticSubnet != value) WifiVM.StaticSubnet = value; }
    partial void OnWifiGatewayChanged(string value) { if (WifiVM != null && WifiVM.StaticGateway != value) WifiVM.StaticGateway = value; }
    partial void OnWifiDnsChanged(string value) { if (WifiVM != null && WifiVM.StaticDns != value) WifiVM.StaticDns = value; }
    partial void OnMqttClientIdChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientId != value) MqttBrokerConfigVM.ClientId = value; }
    partial void OnMqttUsernameChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.Username != value) MqttBrokerConfigVM.Username = value; }
    partial void OnMqttPasswordChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.Password != value) MqttBrokerConfigVM.Password = value; }
    partial void OnMqttKeepAliveChanged(ushort value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.KeepAliveSec != value) MqttBrokerConfigVM.KeepAliveSec = value; }
    partial void OnMqttWillEnabledChanged(bool value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.WillEnabled != value) MqttBrokerConfigVM.WillEnabled = value; }
    partial void OnMqttWillTopicChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.WillTopic != value) MqttBrokerConfigVM.WillTopic = value; }
    partial void OnMqttWillPayloadChanged(string value)
    {
        if (value != null && (value.Contains('"') || value.Contains('\\')))
        {
            string sanitized = value.Replace("\"", string.Empty).Replace("\\", string.Empty);
            if (sanitized != value)
            {
                MqttWillPayload = sanitized;
                return;
            }
        }
        if (MqttBrokerConfigVM != null && value != null && MqttBrokerConfigVM.WillPayload != value) MqttBrokerConfigVM.WillPayload = value;
    }
    partial void OnMqttWillQosChanged(int value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.WillQos != (byte)value) MqttBrokerConfigVM.WillQos = (byte)value; }
    partial void OnMqttWillRetainChanged(bool value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.WillRetain != value) MqttBrokerConfigVM.WillRetain = value; }
    partial void OnMqttTlsCaCertPathChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.CaCertPath != value) MqttBrokerConfigVM.CaCertPath = value; }
    partial void OnMqttTlsAllowSelfSignedChanged(bool value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.AllowSelfSigned != value) MqttBrokerConfigVM.AllowSelfSigned = value; }
    partial void OnMqttTlsCaCertPemChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.CaCertPem != value) MqttBrokerConfigVM.CaCertPem = value; }
    partial void OnMqttTlsIgnoreHostnameChanged(bool value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.IgnoreHostname != value) MqttBrokerConfigVM.IgnoreHostname = value; }
    partial void OnMqttTlsProtocolVersionChanged(int value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.TlsVersionIndex != value) MqttBrokerConfigVM.TlsVersionIndex = value; }
    partial void OnMqttTlsSniHostnameChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.SniHostname != value) MqttBrokerConfigVM.SniHostname = value; }
    partial void OnMqttTlsMtlsEnabledChanged(bool value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.UseMtls != value) MqttBrokerConfigVM.UseMtls = value; }
    partial void OnMqttTlsClientCertPathChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientCertPath != value) MqttBrokerConfigVM.ClientCertPath = value; }
    partial void OnMqttTlsClientCertPemChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientCertPem != value) MqttBrokerConfigVM.ClientCertPem = value; }
    partial void OnMqttTlsClientKeyPathChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientKeyPath != value) MqttBrokerConfigVM.ClientKeyPath = value; }
    partial void OnMqttTlsClientKeyPemChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientKeyPem != value) MqttBrokerConfigVM.ClientKeyPem = value; }
    partial void OnMqttTlsClientKeyPasswordChanged(string value) { if (MqttBrokerConfigVM != null && MqttBrokerConfigVM.ClientKeyPassword != value) MqttBrokerConfigVM.ClientKeyPassword = value; }

    /// <summary>
    /// Lưu toàn bộ cấu hình thiết bị vào ProjectMetadata (RAM), đánh dấu dự án đã thay đổi và thông báo sẵn sàng nạp thiết bị.
    /// </summary>
    [RelayCommand]
    public void SaveConfiguration()
    {
        SaveToMetadata();
        NotifyConfigurationCommitted?.Invoke();
        RequestSaveProject?.Invoke();
        string statusText = LocalizationService.Instance["DeviceConfigAppliedStatus"];
        if (string.IsNullOrWhiteSpace(statusText))
        {
            statusText = LocalizationService.Instance.IsVietnamese
                ? "Đã áp dụng cấu hình dự án — sẵn sàng nạp thiết bị"
                : "Configuration committed to project — ready to deploy";
        }
        DeployStatusMessage = $"● {statusText} ({DateTime.Now:HH:mm:ss})";
    }

    /// <summary>
    /// Lưu cấu hình vào ProjectMetadata, đánh dấu dự án đã thay đổi và đóng hộp thoại nếu đang mở dưới dạng Modal Dialog.
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        SaveToMetadata();
        NotifyConfigurationCommitted?.Invoke();
        RequestSaveProject?.Invoke();
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Mở hộp thoại cấu hình MQTT Broker chuyên sâu (TLS/mTLS x509, diagnostics),
    /// đồng bộ dữ liệu hai chiều trước và sau khi hộp thoại hiển thị.
    /// </summary>
    [RelayCommand]
    private void OpenAdvancedMqttConfig()
    {
        SaveToMetadata();
        OpenMqttBrokerConfigRequested?.Invoke();
        ReloadFromMetadata();
    }

    [RelayCommand]
    private async Task DeployToDeviceAsync()
    {
        IsDeploying = true;
        DeployStatusMessage = LocalizationService.Tr("Deploying");
        AppendLog($"[{DateTime.Now:HH:mm:ss}] [DEPLOY] Commencing DTU hardware profile staging...");

        SaveToMetadata();

        try
        {
            var dtuDto = BuildDtuConfigDto();
            var willDto = BuildMqttWillDto();

            if (AppServices.Instance.RuleWriter != null)
            {
                var result = await AppServices.Instance.RuleWriter.DeployRulesAsync(
                    slaveId: 1,
                    rules: Array.Empty<RuleRecordDto>(),
                    dtuConfig: dtuDto,
                    mqttWillConfig: willDto,
                    cancellationToken: default,
                    progressLogger: (msg) => AppendLog($"[DEPLOY] {msg}"));

                if (result.IsSuccess)
                {
                    DeployStatusMessage = "✓ " + (LocalizationService.Instance.IsVietnamese ? "Nạp DTU thành công!" : "DTU Config Deployed!");
                    AppendLog($"[DEPLOY SUCCESS] Hardware version {result.ActiveVersion} verified. DTU Gateway operational.");
                }
                else
                {
                    DeployStatusMessage = "✗ " + result.ErrorMessage;
                    AppendLog($"[DEPLOY ERROR] Code: {result.ErrorCode}, Message: {result.ErrorMessage}");
                }
            }
            else
            {
                // Simulator fallback test
                await Task.Delay(300);
                DeployStatusMessage = "✓ " + (LocalizationService.Instance.IsVietnamese ? "Đã lưu cấu hình DTU (Offline/Mock)!" : "DTU Config Saved!");
                AppendLog("[DEPLOY] Offline configuration saved to project metadata successfully.");
            }
        }
        catch (Exception ex)
        {
            DeployStatusMessage = "✗ " + ex.Message;
            AppendLog($"[DEPLOY EXCEPTION] {ex.Message}");
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public DtuConfigDto BuildDtuConfigDto()
    {
        var dtu = new DtuConfigDto
        {
            Enabled = true,
            PortIndex = (byte)SelectedPortIndex,
            SerialMode = SerialMode switch
            {
                "ModbusMaster" => DtuSerialMode.ModbusMaster,
                "ModbusSlave" => DtuSerialMode.ModbusSlave,
                _ => DtuSerialMode.Transference
            },
            UplinkMode = UplinkMode switch
            {
                "Mqtt" => DtuUplinkMode.Mqtt,
                "Http" => DtuUplinkMode.Http,
                "TcpClient" => DtuUplinkMode.TcpClient,
                "TcpServer" => DtuUplinkMode.TcpServer,
                _ => DtuUplinkMode.None
            },
            FrameTimeoutDelayMs = FrameTimeoutDelayMs,
            MaxPacketSize = MaxPacketSize,
            PayloadMode = PayloadMode switch
            {
                "Custom" => DtuPayloadMode.Custom,
                "Raw" => DtuPayloadMode.Raw,
                _ => DtuPayloadMode.Standard
            },
            PayloadFlags = (byte)(
                (IncludeDeviceId ? 1 : 0) |
                (IncludeTimestamp ? 2 : 0) |
                (IncludePort ? 4 : 0) |
                (IncludeSequence ? 8 : 0) |
                (IncludeHexStream ? 16 : 0) |
                (IncludePayloadLength ? 32 : 0)
            ),
            HttpConfig = new DtuHttpConfigDto
            {
                Enabled = UplinkMode == "Http",
                Method = HttpMethod == "PUT" ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                Url = HttpUrl,
                CustomHeaders = HttpHeaders,
                TimeoutSec = HttpTimeoutSec,
                PeriodicIntervalSec = HttpIntervalSec,
                TriggerMode = HttpTriggerMode == "Periodic" ? DtuHttpTriggerMode.Periodic : DtuHttpTriggerMode.OnDataReceived
            },
            TcpConfig = new DtuTcpConfigDto
            {
                Enabled = UplinkMode is "TcpClient" or "TcpServer",
                SocketMode = TcpSocketMode == "Server" ? DtuTcpSocketMode.Server : DtuTcpSocketMode.Client,
                RemoteHost = TcpRemoteHost,
                RemotePort = TcpRemotePort,
                LocalPort = TcpLocalPort,
                ReconnectDelaySec = TcpReconnectDelaySec,
                HeartbeatIntervalSec = TcpHeartbeatIntervalSec,
                HeartbeatPayload = TcpHeartbeatPayload,
                RegistrationType = TcpRegistrationType switch
                {
                    "OnConnect" => DtuTcpRegistrationType.OnConnect,
                    "PrependToEveryPacket" => DtuTcpRegistrationType.PrependToEveryPacket,
                    _ => DtuTcpRegistrationType.None
                },
                RegistrationPayload = TcpRegistrationPayload
            },
            MqttWillConfig = BuildMqttWillDto()
        };

        return dtu;
    }

    public MqttWillConfigDto BuildMqttWillDto()
    {
        string effectiveClientId = string.IsNullOrWhiteSpace(MqttClientId) ? "SynaptiX_DTU" : MqttClientId.Trim();
        string rawTopic = MqttWillTopic ?? string.Empty;
        string resolvedTopic = rawTopic.Replace("{device}", effectiveClientId, StringComparison.OrdinalIgnoreCase).Trim();
        string rawPayload = string.IsNullOrWhiteSpace(MqttWillPayload) || MqttWillPayload == "{\"status\":\"offline\"}" ? "offline" : MqttWillPayload;
        string resolvedPayload = rawPayload.Replace("{device}", effectiveClientId, StringComparison.OrdinalIgnoreCase);

        resolvedPayload = resolvedPayload.Replace("\"", string.Empty).Replace("\\", string.Empty);
        if (resolvedPayload.Length > MqttWillConfigCodec.MaxPayloadChars)
        {
            resolvedPayload = resolvedPayload.Substring(0, MqttWillConfigCodec.MaxPayloadChars);
        }

        return new MqttWillConfigDto
        {
            Enabled = MqttWillEnabled,
            Topic = resolvedTopic,
            Payload = resolvedPayload,
            Qos = (byte)MqttWillQos,
            Retain = MqttWillRetain
        };
    }



    private void AppendLog(string message)
    {
        TerminalLog += message + "\n";
    }

    // =========================================================================
    // MULTI-STEP WIZARD NAVIGATION & FBD SYNTHESIS COMMANDS
    // =========================================================================

    [RelayCommand]
    public void NextStep()
    {
        if (CurrentStepIndex < 3 && IsCurrentStepValid)
        {
            CurrentStepIndex++;
            UpdateSummaryAndPreview();
        }
    }

    [RelayCommand]
    public void PreviousStep()
    {
        if (CurrentStepIndex > 1)
        {
            CurrentStepIndex--;
        }
    }

    [RelayCommand]
    public void AddFastRegister()
    {
        ushort nextAddr = 40001;
        if (FastRegisters.Count > 0)
        {
            nextAddr = (ushort)(FastRegisters.Max(r => r.StartAddress) + (FastRegisters.Last().DataType == ModbusDataType.Real ? 2 : 1));
        }
        string tag = $"VAL_{FastRegisters.Count + 1}";
        FastRegisters.Add(new DtuFastRegisterItem(1, nextAddr, ModbusDataType.Real, tag, $"Đo lường {tag}"));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(SummaryTopologyText));
        UpdateSummaryAndPreview();
    }

    [RelayCommand]
    public void RemoveFastRegister(DtuFastRegisterItem? item)
    {
        if (item != null)
        {
            FastRegisters.Remove(item);
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(SummaryTopologyText));
            UpdateSummaryAndPreview();
        }
    }

    [RelayCommand]
    public void FinishWizard()
    {
        SaveToMetadata();
        if (AutoGenerateFbd && LogicEditor != null)
        {
            GenerateFbdPipeline();
        }
        if (RequestCloseDialog != null)
        {
            RequestCloseDialog.Invoke(true);
        }
        else
        {
            RequestClose?.Invoke();
        }
    }

    [RelayCommand]
    public void CancelWizard()
    {
        if (RequestCloseDialog != null)
        {
            RequestCloseDialog.Invoke(false);
        }
        else
        {
            RequestClose?.Invoke();
        }
    }

    // --- LIVE INTELLIGENCE & CONTEXT INSPECTOR COMPUTED PROPERTIES ---

    /// <summary>
    /// Ước tính chu kỳ quét 1 vòng Modbus (ms) dựa trên số lượng thanh ghi và khoảng nghỉ khung.
    /// </summary>
    public int ActivePolledChannelsCount => ModbusPortConfigVM?.SelectedPort != null && ModbusPortConfigVM.SelectedPort.MasterChannels.Count > 0
        ? ModbusPortConfigVM.SelectedPort.ActivePolledChannelsCount
        : FastRegisters.Count;

    /// <summary>
    /// Chuỗi hiển thị số lượng kênh đọc tuần hoàn (song ngữ chuẩn, không lỗi '1 Tags').
    /// </summary>
    public string ActivePolledChannelsText => string.Format(
        LocalizationService.Instance["InspectorActiveChannelsFmt"],
        ActivePolledChannelsCount
    );

    /// <summary>
    /// Ước tính chu kỳ quét 1 vòng Modbus (ms) dựa trên số lượng thanh ghi và khoảng nghỉ khung.
    /// </summary>
    public int EstimatedScanCycleMs
    {
        get
        {
            if (ModbusPortConfigVM?.SelectedPort != null && ModbusPortConfigVM.SelectedPort.MasterChannels.Count > 0)
            {
                return ModbusPortConfigVM.SelectedPort.EstimatedScanCycleMs;
            }
            if (FastRegisters.Count == 0) return 0;
            int perRegMs = Math.Max(15, (int)(InterFrameDelayMs + 20));
            return FastRegisters.Count * perRegMs;
        }
    }

    /// <summary>
    /// Đánh giá tải đường truyền Bus Modbus RTU.
    /// </summary>
    public string EstimatedBusLoadText
    {
        get
        {
            if (FastRegisters.Count == 0)
                return LocalizationService.Instance.IsVietnamese ? "Không tải (0%)" : "Idle (0%)";

            int cycle = EstimatedScanCycleMs;
            int interval = Math.Max(100, (int)PollingIntervalMs);
            double ratio = (double)cycle / interval;

            if (ratio < 0.25)
                return LocalizationService.Instance.IsVietnamese ? "Rất thấp (< 25%)" : "Very Low (< 25%)";
            if (ratio < 0.65)
                return LocalizationService.Instance.IsVietnamese ? "Bình thường (25 - 65%)" : "Normal (25 - 65%)";
            return LocalizationService.Instance.IsVietnamese ? "Cao (> 65%)" : "High (> 65%)";
        }
    }

    /// <summary>
    /// Ước tính dung lượng dữ liệu tiêu thụ hàng tháng (MB/tháng).
    /// </summary>
    public double EstimatedMonthlyDataMb
    {
        get
        {
            int interval = Math.Max(1, (int)PublishIntervalSec);
            int payloadBytes = string.IsNullOrEmpty(LivePayloadPreview) 
                ? 150 
                : Math.Max(80, System.Text.Encoding.UTF8.GetByteCount(LivePayloadPreview));
            
            double packetsPerDay = 86400.0 / interval;
            double bytesPerMonth = packetsPerDay * 30.0 * payloadBytes;
            return Math.Round(bytesPerMonth / (1024.0 * 1024.0), 1);
        }
    }

    [RelayCommand]
    public void CopyMacAddress()
    {
        try
        {
            System.Windows.Clipboard.SetText(HardwareMacAddress);
            DeployStatusMessage = LocalizationService.Instance.IsVietnamese
                ? "Đã sao chép địa chỉ MAC vào bộ nhớ tạm."
                : "MAC address copied to clipboard.";
        }
        catch { }
    }

    public void UpdateSummaryAndPreview()
    {
        var dict = new Dictionary<string, object>
        {
            ["device"] = "SYNX-DTU-485",
            ["ts"] = 1714800000
        };

        foreach (var reg in FastRegisters)
        {
            string key = string.IsNullOrWhiteSpace(reg.TagName) ? $"reg_{reg.StartAddress}" : reg.TagName;
            if (reg.DataType == ModbusDataType.Real)
                dict[key] = 25.5;
            else if (reg.DataType == ModbusDataType.Bool)
                dict[key] = 1;
            else
                dict[key] = 100;
        }

        try
        {
            LivePayloadPreview = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            LivePayloadPreview = "{}";
        }
        OnPropertyChanged(nameof(SummaryTopologyText));
    }

    /// <summary>
    /// Tự động sinh cụm khối FBD (MB_READ -> Uplink) và nối dây trên Canvas FBD.
    /// </summary>
    public void GenerateFbdPipeline()
    {
        if (LogicEditor == null) return;

        // 1. Đăng ký các Tag mới vào TagCatalog nếu chưa tồn tại
        foreach (var reg in FastRegisters)
        {
            if (TagCatalog != null && !TagCatalog.AllTags.Any(t => string.Equals(t.Name, reg.TagName, StringComparison.OrdinalIgnoreCase)))
            {
                var tagType = reg.DataType switch
                {
                    ModbusDataType.Bool => TagDataType.Boolean,
                    ModbusDataType.Real => TagDataType.Float32,
                    _ => TagDataType.Int32
                };
                var newTag = new TagModel
                {
                    Name = reg.TagName,
                    Kind = TagKind.ModbusHolding,
                    DataType = tagType,
                    Alias = reg.Description
                };
                TagCatalog.RegisterTags.Add(newTag);
            }
        }

        // 2. Tạo khối MB_READ
        System.Windows.Point spawnPos = LogicEditor.GetNextRuleSpawnPosition();
        var mbNode = new ModbusReadNodeViewModel
        {
            PortIndex = (byte)SelectedPortIndex,
            SlaveId = FastRegisters.FirstOrDefault()?.SlaveId ?? 1,
            FunctionCode = FastRegisters.FirstOrDefault()?.FunctionCode ?? ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = FastRegisters.FirstOrDefault()?.StartAddress ?? 40001,
            Location = spawnPos
        };

        // Gán các chân Pin
        mbNode.Pins.Clear();
        foreach (var reg in FastRegisters)
        {
            var matchedTag = TagCatalog?.AllTags.FirstOrDefault(t => string.Equals(t.Name, reg.TagName, StringComparison.OrdinalIgnoreCase));
            var pin = new ModbusPinBindingViewModel
            {
                Label = reg.TagName,
                DataType = reg.DataType,
                TargetTag = matchedTag
            };
            mbNode.Pins.Add(pin);
        }

        // 3. Tạo khối Uplink tương ứng
        GraphNodeViewModel? uplinkNode = null;
        System.Windows.Point uplinkPos = new System.Windows.Point(spawnPos.X + 280, spawnPos.Y);

        if (UplinkMode == "Mqtt")
        {
            var mqttNode = new MqttPublishNodeViewModel
            {
                Topic = MqttPublishTopic,
                PayloadMode = "JSON",
                Location = uplinkPos
            };
            mqttNode.Fields.Clear();
            foreach (var reg in FastRegisters)
            {
                var matchedTag = TagCatalog?.AllTags.FirstOrDefault(t => string.Equals(t.Name, reg.TagName, StringComparison.OrdinalIgnoreCase));
                mqttNode.Fields.Add(new MqttPayloadFieldViewModel(reg.TagName, matchedTag));
            }
            uplinkNode = mqttNode;
        }
        else if (UplinkMode == "Http")
        {
            var httpNode = new HttpClientNodeViewModel
            {
                Url = HttpUrl,
                Method = HttpMethod,
                PeriodicIntervalSec = (ushort)HttpIntervalSec,
                Location = uplinkPos
            };
            httpNode.Fields.Clear();
            foreach (var reg in FastRegisters)
            {
                var matchedTag = TagCatalog?.AllTags.FirstOrDefault(t => string.Equals(t.Name, reg.TagName, StringComparison.OrdinalIgnoreCase));
                httpNode.Fields.Add(new HttpPayloadFieldViewModel(reg.TagName, matchedTag));
            }
            uplinkNode = httpNode;
        }
        else // TcpClient
        {
            var tcpNode = new TcpClientNodeViewModel
            {
                RemoteHost = TcpRemoteHost,
                RemotePort = TcpRemotePort,
                ReconnectDelaySec = TcpReconnectDelaySec,
                HeartbeatIntervalSec = TcpHeartbeatIntervalSec,
                HeartbeatPayload = TcpHeartbeatPayload,
                RegistrationType = TcpRegistrationType,
                DataTag = TagCatalog?.AllTags.FirstOrDefault(t => string.Equals(t.Name, FastRegisters.FirstOrDefault()?.TagName, StringComparison.OrdinalIgnoreCase)),
                Location = uplinkPos
            };
            uplinkNode = tcpNode;
        }

        LogicEditor.Nodes.Add(mbNode);
        LogicEditor.Nodes.Add(uplinkNode);

        // Nối dây tự động giữa chân ra MB_READ và chân vào Uplink
        if (mbNode.OutputConnectors.Count > 0 && uplinkNode.InputConnectors.Count > 0)
        {
            LogicEditor.CompleteConnection((mbNode.OutputConnectors[0], uplinkNode.InputConnectors[0]));
        }

        LogicEditor.SelectExclusive(uplinkNode);
        LogicEditor.CompileAndSaveRules(true);
    }
}

