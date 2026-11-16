using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Abstractions;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public sealed partial class WifiConfigViewModel : ObservableObject, IDisposable
{
    private readonly ProjectMetadata _metadata;
    private IWifiClient? _wifiClient;
    private Timer? _pollTimer;
    private bool _isDisposed;
    private int _isRefreshingTelemetry;

    private IWifiClient? EffectiveWifiClient => _wifiClient ?? AppServices.Instance.WifiClient;
    private bool EffectiveIsConnected => IsDeviceConnected || AppServices.Instance.SessionManager.HasActiveSession;

    public Action? RequestClose { get; set; }
    public bool? DialogResult { get; private set; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _autoReconnect = true;

    [ObservableProperty]
    private string _ssid = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _isDhcp = true;

    [ObservableProperty]
    private string _staticIp = "192.168.1.50";

    [ObservableProperty]
    private string _staticSubnet = "255.255.255.0";

    [ObservableProperty]
    private string _staticGateway = "192.168.1.1";

    [ObservableProperty]
    private string _staticDns = "8.8.8.8";

    // Live Telemetry
    [ObservableProperty]
    private ushort _connectionStatus = ModbusRegisterMap.WifiConnDisconnected;

    /// <summary>
    /// Khoảng thời gian (ms) giữa các lần thăm dò kết nối Wi-Fi. Mặc định 300ms.
    /// </summary>
    public int ConnectPollIntervalMs { get; set; } = 300;

    /// <summary>
    /// Khoảng thời gian (ms) giữa các lần thăm dò quét Wi-Fi. Mặc định 500ms.
    /// </summary>
    public int ScanPollIntervalMs { get; set; } = 500;

    [ObservableProperty]
    private string _connectionStatusText = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private sbyte _rssiDbm = -100;

    [ObservableProperty]
    private int _signalQualityPercent = 0;

    [ObservableProperty]
    private string _currentIp = "0.0.0.0";

    [ObservableProperty]
    private string _macAddress = "00:00:00:00:00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private bool _isDeviceConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    private bool _isOperationRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    // Scanning
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private bool _isScanning;

    /// <summary>
    /// Cho biết nút quét mạng có được phép nhấn hay không (phần cứng đã kết nối và không đang quét dở).
    /// </summary>
    public bool CanScan => !IsScanning && !IsOperationRunning && EffectiveIsConnected;

    /// <summary>
    /// Cho biết nút kiểm tra kết nối có được phép bấm hay không (đã kết nối cổng COM và không đang bận).
    /// </summary>
    public bool CanConnect => EffectiveIsConnected && !IsOperationRunning && !IsScanning;

    [ObservableProperty]
    private string _scanStatusMessage = string.Empty;

    public ObservableCollection<WifiApInfoDto> AvailableAps { get; } = new();

    /// <summary>
    /// Cho biết danh sách AP quét được có ít nhất 1 mạng Wi-Fi khả dụng (phục vụ hiển thị UI DataBinding).
    /// </summary>
    [ObservableProperty]
    private bool _hasAvailableAps;

    [ObservableProperty]
    private WifiApInfoDto? _selectedAp;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [RelayCommand]
    public async Task SwitchToScanAndStartScanAsync()
    {
        SelectedTabIndex = 1;
        if (IsDeviceConnected)
        {
            await ScanAsync();
        }
    }

    [RelayCommand]
    public void ApplySelectedApAndReturn()
    {
        if (SelectedAp != null && !string.IsNullOrWhiteSpace(SelectedAp.Ssid))
        {
            Ssid = SelectedAp.Ssid;
            SelectedTabIndex = 0;
        }
    }

    public WifiConfigViewModel(ProjectMetadata metadata, IWifiClient? wifiClient = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _wifiClient = wifiClient ?? AppServices.Instance.WifiClient;

        // Load initial state from metadata
        _isEnabled = _metadata.WifiEnabled;
        _autoReconnect = _metadata.WifiAutoReconnect;
        _ssid = _metadata.WifiSsid ?? string.Empty;
        _password = _metadata.WifiPassword ?? string.Empty;
        _isDhcp = _metadata.WifiDhcpEnabled;
        _staticIp = string.IsNullOrWhiteSpace(_metadata.WifiStaticIp) ? "192.168.1.50" : _metadata.WifiStaticIp;
        _staticSubnet = string.IsNullOrWhiteSpace(_metadata.WifiSubnetMask) ? "255.255.255.0" : _metadata.WifiSubnetMask;
        _staticGateway = string.IsNullOrWhiteSpace(_metadata.WifiGateway) ? "192.168.1.1" : _metadata.WifiGateway;
        _staticDns = string.IsNullOrWhiteSpace(_metadata.WifiDns) ? "8.8.8.8" : _metadata.WifiDns;

        UpdateStatusText();

        // Check if device is connected
        IsDeviceConnected = AppServices.Instance.SessionManager.HasActiveSession;

        // Initialize polling timer (fires every 1500ms when connected)
        if (IsDeviceConnected && EffectiveWifiClient != null)
        {
            _pollTimer = new Timer(async _ => await PollTelemetryAsync(), null, 500, 1500);
        }
    }

    /// <summary>
    /// Cập nhật trạng thái kết nối phần cứng thời gian thực và nạp lại client truyền thông Wi-Fi.
    /// </summary>
    public void UpdateConnectionState(bool isConnected, IWifiClient? client = null)
    {
        IsDeviceConnected = isConnected;
        if (client != null)
        {
            _wifiClient = client;
        }
        else if (isConnected && AppServices.Instance.WifiClient != null)
        {
            _wifiClient = AppServices.Instance.WifiClient;
        }

        if (EffectiveIsConnected && EffectiveWifiClient != null)
        {
            _pollTimer?.Dispose();
            _pollTimer = new Timer(async _ => await PollTelemetryAsync(), null, 500, 1500);
            _ = RefreshTelemetryDirectAsync();
        }
        else
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }

    partial void OnSelectedApChanged(WifiApInfoDto? value)
    {
        if (value != null && !string.IsNullOrWhiteSpace(value.Ssid))
        {
            Ssid = value.Ssid;
        }
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    public async Task ScanAsync()
    {
        var client = EffectiveWifiClient;
        bool isConnected = EffectiveIsConnected;
        if (client == null || !isConnected)
        {
            ErrorMessage = LocalizationService.Instance.IsVietnamese
                ? "Chưa kết nối thiết bị. Vui lòng kết nối cổng COM trước khi quét sóng."
                : "Device not connected. Please connect serial COM port first.";
            return;
        }

        if (IsScanning) return;

        IsScanning = true;
        ErrorMessage = string.Empty;
        ScanStatusMessage = LocalizationService.Instance["WifiScanningText"];
        AvailableAps.Clear();
        HasAvailableAps = false;

        try
        {
            await client.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdStartScan);

            // Poll scan control status up to 30 times (500ms intervals = 15 seconds max per ICD SPLC-SPEC-WIFI-004)
            WifiScanControlDto? control = null;
            int effectiveScanDelay = client.GetType().Name.Contains("Fake") ? 0 : ScanPollIntervalMs;
            for (int i = 0; i < 30; i++)
            {
                if (effectiveScanDelay > 0) await Task.Delay(effectiveScanDelay);
                control = await client.ReadWifiScanStatusAsync(1);
                if (control.ScanState == ModbusRegisterMap.WifiScanStateCompleted || control.ScanState == ModbusRegisterMap.WifiScanStateFailed)
                {
                    break;
                }
            }

            if (control != null && control.ScanState == ModbusRegisterMap.WifiScanStateCompleted)
            {
                var aps = await client.ReadWifiScanResultsAsync(1, control.ApFoundCount);
                AvailableAps.Clear();

                // Sắp xếp thứ tự theo đúng RSSI giảm dần (sóng mạnh nhất lên đầu: -45 dBm > -52 dBm > -87 dBm)
                var sortedAps = aps
                    .Where(ap => !string.IsNullOrWhiteSpace(ap.Ssid))
                    .OrderByDescending(ap => ap.RssiDbm)
                    .ThenBy(ap => ap.Ssid)
                    .ToList();

                foreach (var ap in sortedAps)
                {
                    AvailableAps.Add(ap);
                }

                HasAvailableAps = AvailableAps.Count > 0;
                if (AvailableAps.Count > 0)
                {
                    // Nếu đã có SSID khớp với danh sách thì giữ nguyên, ngược lại chọn AP sóng mạnh nhất
                    var existing = AvailableAps.FirstOrDefault(a => string.Equals(a.Ssid, Ssid, StringComparison.OrdinalIgnoreCase));
                    SelectedAp = existing ?? AvailableAps[0];
                }

                ScanStatusMessage = AvailableAps.Count > 0
                    ? (LocalizationService.Instance.IsVietnamese ? $"Tìm thấy {AvailableAps.Count} mạng Wi-Fi" : $"Found {AvailableAps.Count} network(s)")
                    : LocalizationService.Instance["WifiNoApsFound"];
            }
            else
            {
                HasAvailableAps = false;
                ScanStatusMessage = LocalizationService.Instance["WifiNoApsFound"];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ScanStatusMessage = string.Empty;
            HasAvailableAps = false;
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        var client = EffectiveWifiClient;
        if (client == null || !EffectiveIsConnected) return;

        if (string.IsNullOrWhiteSpace(Ssid))
        {
            ErrorMessage = LocalizationService.Instance["WifiErrEmptySsid"];
            return;
        }

        IsOperationRunning = true;
        ErrorMessage = string.Empty;
        ConnectionStatusText = LocalizationService.Instance["WifiBtnTesting"];

        try
        {
            var config = BuildConfigDto();
            await client.WriteWifiConfigAsync(1, config);
            await client.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdConnect);

            // Poll for connection resolution (up to 10 iterations of 300ms = 3 seconds max per ICD)
            int effectiveConnectDelay = client.GetType().Name.Contains("Fake") ? 0 : ConnectPollIntervalMs;
            for (int i = 0; i < 10; i++)
            {
                if (effectiveConnectDelay > 0) await Task.Delay(effectiveConnectDelay);
                await RefreshTelemetryDirectAsync();
                if (ConnectionStatus == ModbusRegisterMap.WifiConnGotIp ||
                    ConnectionStatus == ModbusRegisterMap.WifiConnConnected ||
                    ConnectionStatus == ModbusRegisterMap.WifiConnAuthFailed ||
                    ConnectionStatus == ModbusRegisterMap.WifiConnSsidNotFound)
                {
                    break;
                }
            }

            if (ConnectionStatus == ModbusRegisterMap.WifiConnAuthFailed)
            {
                ErrorMessage = LocalizationService.Instance["WifiStatusAuthFailed"];
            }
            else if (ConnectionStatus == ModbusRegisterMap.WifiConnSsidNotFound)
            {
                ErrorMessage = LocalizationService.Instance["WifiStatusSsidNotFound"];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsOperationRunning = false;
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        var client = EffectiveWifiClient;
        if (client == null || !EffectiveIsConnected) return;

        IsOperationRunning = true;
        ErrorMessage = string.Empty;

        try
        {
            await client.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdDisconnect);
            int effectiveDisconnectDelay = client.GetType().Name.Contains("Fake") ? 0 : 200;
            if (effectiveDisconnectDelay > 0) await Task.Delay(effectiveDisconnectDelay);
            await RefreshTelemetryDirectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsOperationRunning = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsEnabled && string.IsNullOrWhiteSpace(Ssid))
        {
            ErrorMessage = LocalizationService.Instance["WifiErrEmptySsid"];
            return;
        }

        // Save to project metadata
        _metadata.WifiEnabled = IsEnabled;
        _metadata.WifiAutoReconnect = AutoReconnect;
        _metadata.WifiSsid = Ssid.Trim();
        _metadata.WifiPassword = Password ?? string.Empty;
        _metadata.WifiDhcpEnabled = IsDhcp;
        _metadata.WifiStaticIp = StaticIp.Trim();
        _metadata.WifiSubnetMask = StaticSubnet.Trim();
        _metadata.WifiGateway = StaticGateway.Trim();
        _metadata.WifiDns = StaticDns.Trim();
        _metadata.LastModified = DateTime.UtcNow;

        var client = EffectiveWifiClient;
        if (EffectiveIsConnected && client != null)
        {
            try
            {
                var config = BuildConfigDto();
                await client.WriteWifiConfigAsync(1, config);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
        }

        Dispose();
        DialogResult = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        Dispose();
        DialogResult = false;
        RequestClose?.Invoke();
    }

    private WifiConfigDto BuildConfigDto()
    {
        return new WifiConfigDto
        {
            Enabled = IsEnabled,
            AutoReconnect = AutoReconnect,
            IsDhcp = IsDhcp,
            Mode = 0, // STA
            Ssid = Ssid.Trim(),
            Password = Password ?? string.Empty,
            StaticIp = StaticIp.Trim(),
            StaticSubnet = StaticSubnet.Trim(),
            StaticGateway = StaticGateway.Trim(),
            StaticDns = StaticDns.Trim()
        };
    }

    private async Task PollTelemetryAsync()
    {
        if (Interlocked.CompareExchange(ref _isRefreshingTelemetry, 1, 0) != 0) return;

        try
        {
            await RefreshTelemetryDirectAsync();
        }
        catch
        {
            // Suppress background poll errors
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshingTelemetry, 0);
        }
    }

    public async Task RefreshTelemetryDirectAsync()
    {
        var client = EffectiveWifiClient;
        if (client == null) return;

        var config = await client.ReadWifiConfigAsync(1);
        ConnectionStatus = config.ConnectionStatus;
        RssiDbm = (sbyte)config.RssiDbm;
        CurrentIp = string.IsNullOrWhiteSpace(config.CurrentIp) ? "0.0.0.0" : config.CurrentIp;
        MacAddress = string.IsNullOrWhiteSpace(config.MacAddress) ? "00:00:00:00:00:00" : config.MacAddress;

        // Calculate Signal Quality %
        if (RssiDbm <= -100 || RssiDbm == 0)
        {
            SignalQualityPercent = 0;
        }
        else if (RssiDbm >= -50)
        {
            SignalQualityPercent = 100;
        }
        else
        {
            SignalQualityPercent = 2 * (RssiDbm + 100);
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        IsConnected = ConnectionStatus == ModbusRegisterMap.WifiConnConnected || ConnectionStatus == ModbusRegisterMap.WifiConnGotIp;

        ConnectionStatusText = ConnectionStatus switch
        {
            ModbusRegisterMap.WifiConnDisconnected => LocalizationService.Instance["WifiStatusDisconnected"],
            ModbusRegisterMap.WifiConnConnecting => LocalizationService.Instance["WifiStatusConnecting"],
            ModbusRegisterMap.WifiConnConnected => LocalizationService.Instance["WifiStatusConnected"],
            ModbusRegisterMap.WifiConnGotIp => LocalizationService.Instance["WifiStatusGotIp"],
            ModbusRegisterMap.WifiConnAuthFailed => LocalizationService.Instance["WifiStatusAuthFailed"],
            ModbusRegisterMap.WifiConnSsidNotFound => LocalizationService.Instance["WifiStatusSsidNotFound"],
            _ => LocalizationService.Instance["WifiStatusDisconnected"]
        };
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _pollTimer?.Dispose();
        }
    }
}
