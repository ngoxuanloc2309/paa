using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public sealed partial class MqttBrokerConfigViewModel : ObservableObject
{
    private readonly ProjectMetadata _metadata;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 1883;

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _useTls;

    [ObservableProperty]
    private bool _cleanSession = true;

    [ObservableProperty]
    private ushort _keepAliveSec = 60;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _testStatusMessage = string.Empty;

    [ObservableProperty]
    private bool? _isTestSuccess;

    public event Action? RequestClose;

    public MqttBrokerConfigViewModel(ProjectMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        _isEnabled = metadata.MqttBrokerEnabled;
        _host = metadata.MqttBrokerHost ?? string.Empty;
        _port = metadata.MqttBrokerPort > 0 ? metadata.MqttBrokerPort : 1883;
        _clientId = metadata.MqttClientId;
        _username = metadata.MqttUsername;
        _password = metadata.MqttPassword;
        _useTls = metadata.MqttTlsEnabled;
        _cleanSession = metadata.MqttCleanSession;
        _keepAliveSec = metadata.MqttKeepAliveSec > 0 ? metadata.MqttKeepAliveSec : (ushort)60;
    }

    partial void OnUseTlsChanged(bool value)
    {
        if (value && Port == 1883)
        {
            Port = 8883;
        }
        else if (!value && Port == 8883)
        {
            Port = 1883;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            IsTestSuccess = false;
            TestStatusMessage = LocalizationService.Instance["MqttBrokerErrEmptyHost"];
            return;
        }

        if (Port <= 0 || Port > 65535)
        {
            IsTestSuccess = false;
            TestStatusMessage = LocalizationService.Instance["MqttBrokerErrInvalidPort"];
            return;
        }

        IsTesting = true;
        TestStatusMessage = string.Format(
            LocalizationService.Instance["MqttBrokerStatusChecking"],
            Host,
            Port);
        IsTestSuccess = null;

        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            
            await client.ConnectAsync(Host.Trim(), Port, cts.Token);

            IsTestSuccess = true;
            TestStatusMessage = LocalizationService.Instance["MqttBrokerTestSuccess"];
        }
        catch (OperationCanceledException)
        {
            IsTestSuccess = false;
            TestStatusMessage = string.Format(
                LocalizationService.Instance["MqttBrokerTestFailed"],
                LocalizationService.Instance["MqttBrokerErrTimeout"]);
        }
        catch (Exception ex)
        {
            IsTestSuccess = false;
            TestStatusMessage = string.Format(
                LocalizationService.Instance["MqttBrokerTestFailed"],
                ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        _metadata.MqttBrokerEnabled = IsEnabled;
        _metadata.MqttBrokerHost = Host?.Trim() ?? string.Empty;
        _metadata.MqttBrokerPort = Port > 0 ? Port : 1883;
        _metadata.MqttClientId = ClientId?.Trim() ?? string.Empty;
        _metadata.MqttUsername = Username?.Trim() ?? string.Empty;
        _metadata.MqttPassword = Password ?? string.Empty;
        _metadata.MqttTlsEnabled = UseTls;
        _metadata.MqttCleanSession = CleanSession;
        _metadata.MqttKeepAliveSec = KeepAliveSec > 0 ? KeepAliveSec : (ushort)60;
        _metadata.LastModified = DateTime.UtcNow;

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
