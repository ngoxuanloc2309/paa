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
    private readonly IWifiClient? _wifiClient;
    private readonly Timer? _pollTimer;
    private bool _isDisposed;
    private int _isRefreshingTelemetry;

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
    private bool _isDeviceConnected;

    [ObservableProperty]
    private bool _isOperationRunning;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Scanning
    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatusMessage = string.Empty;

    public ObservableCollection<WifiApInfoDto> AvailableAps { get; } = new();

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
        if (IsDeviceConnected && _wifiClient != null)
        {
            _pollTimer = new Timer(async _ => await PollTelemetryAsync(), null, 500, 1500);
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
        if (_wifiClient == null || !IsDeviceConnected) return;

        IsScanning = true;
        ErrorMessage = string.Empty;
        ScanStatusMessage = LocalizationService.Instance["WifiScanningText"];

        try
        {
            await _wifiClient.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdStartScan);

            // Poll scan control status up to 10 times (500ms intervals = 5 seconds max)
            WifiScanControlDto? control = null;
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                control = await _wifiClient.ReadWifiScanStatusAsync(1);
                if (control.ScanState == ModbusRegisterMap.WifiScanStateCompleted || control.ScanState == ModbusRegisterMap.WifiScanStateFailed)
                {
                    break;
                }
            }

            if (control != null && control.ScanState == ModbusRegisterMap.WifiScanStateCompleted)
            {
                var aps = await _wifiClient.ReadWifiScanResultsAsync(1, control.ApFoundCount);
                AvailableAps.Clear();
                foreach (var ap in aps)
                {
                    AvailableAps.Add(ap);
                }

                ScanStatusMessage = AvailableAps.Count > 0
                    ? (LocalizationService.Instance.IsVietnamese ? $"Tìm thấy {AvailableAps.Count} mạng Wi-Fi" : $"Found {AvailableAps.Count} network(s)")
                    : LocalizationService.Instance["WifiNoApsFound"];
            }
            else
            {
                ScanStatusMessage = LocalizationService.Instance["WifiNoApsFound"];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ScanStatusMessage = string.Empty;
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (_wifiClient == null || !IsDeviceConnected) return;

        if (string.IsNullOrWhiteSpace(Ssid))
        {
            ErrorMessage = LocalizationService.Instance["WifiErrEmptySsid"];
            return;
        }

        IsOperationRunning = true;
        ErrorMessage = string.Empty;

        try
        {
            var config = BuildConfigDto();
            await _wifiClient.WriteWifiConfigAsync(1, config);
            await _wifiClient.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdConnect);
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
    public async Task DisconnectAsync()
    {
        if (_wifiClient == null || !IsDeviceConnected) return;

        IsOperationRunning = true;
        ErrorMessage = string.Empty;

        try
        {
            await _wifiClient.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdDisconnect);
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

        if (IsDeviceConnected && _wifiClient != null)
        {
            try
            {
                var config = BuildConfigDto();
                await _wifiClient.WriteWifiConfigAsync(1, config);
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
        if (_wifiClient == null) return;

        var config = await _wifiClient.ReadWifiConfigAsync(1);
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
