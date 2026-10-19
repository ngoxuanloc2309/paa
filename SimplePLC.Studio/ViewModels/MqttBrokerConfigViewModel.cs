using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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

    // TLS Certificate & Security Properties (SPLC-SPEC-MQTT-003)
    [ObservableProperty]
    private string _caCertPath = string.Empty;

    [ObservableProperty]
    private string _caCertPem = string.Empty;

    [ObservableProperty]
    private string _caCertSubject = string.Empty;

    [ObservableProperty]
    private string _caCertIssuer = string.Empty;

    [ObservableProperty]
    private string _caCertExpiry = string.Empty;

    [ObservableProperty]
    private string _caCertSha256 = string.Empty;

    [ObservableProperty]
    private bool _hasCaCert;

    [ObservableProperty]
    private bool _allowSelfSigned;

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

        _caCertPath = metadata.MqttTlsCaCertPath ?? string.Empty;
        _caCertPem = metadata.MqttTlsCaCertPem ?? string.Empty;
        _allowSelfSigned = metadata.MqttTlsAllowSelfSigned;

        // Tự động phân tích chứng chỉ nếu đã có sẵn trong metadata
        if (!string.IsNullOrWhiteSpace(_caCertPem))
        {
            ParseCertPem(_caCertPem, _caCertPath);
        }
        else if (!string.IsNullOrWhiteSpace(_caCertPath) && File.Exists(_caCertPath))
        {
            try
            {
                string pem = File.ReadAllText(_caCertPath);
                ParseCertPem(pem, _caCertPath);
            }
            catch
            {
                // Bỏ qua nếu file không đọc được lúc khởi tạo
            }
        }
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
            LoadCertificateFile(dialog.FileName);
        }
    }

    public void LoadCertificateFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            string pemContent = File.ReadAllText(filePath);
            ParseCertPem(pemContent, filePath);
        }
        catch (Exception ex)
        {
            ClearCaCert();
            TestStatusMessage = string.Format(LocalizationService.Instance["MqttBrokerTlsCertInvalid"], ex.Message);
            IsTestSuccess = false;
        }
    }

    public void ParseCertPem(string pemContent, string filePath)
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

            CaCertPath = filePath;
            CaCertPem = pemContent;
            CaCertSubject = cert.Subject;
            CaCertIssuer = cert.Issuer;
            CaCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            CaCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            HasCaCert = true;
        }
        catch (Exception)
        {
            // Dự phòng nếu định dạng DER nhị phân hoặc chuỗi đặc biệt
            CaCertPath = filePath;
            CaCertPem = pemContent;
            CaCertSubject = Path.GetFileName(filePath);
            CaCertIssuer = "Custom Certificate";
            CaCertExpiry = "Loaded";
            CaCertSha256 = "N/A";
            HasCaCert = true;
        }
    }

    [RelayCommand]
    public void ClearCaCert()
    {
        CaCertPath = string.Empty;
        CaCertPem = string.Empty;
        CaCertSubject = string.Empty;
        CaCertIssuer = string.Empty;
        CaCertExpiry = string.Empty;
        CaCertSha256 = string.Empty;
        HasCaCert = false;
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            await client.ConnectAsync(Host.Trim(), Port, cts.Token).ConfigureAwait(false);

            if (UseTls)
            {
                // Kiểm tra bắt tay TLS / SSL thực tế bằng SslStream
                using var sslStream = new SslStream(
                    client.GetStream(),
                    false,
                    (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if (AllowSelfSigned) return true;
                        if (sslPolicyErrors == SslPolicyErrors.None) return true;

                        // Nếu có tệp CA riêng, kiểm tra khớp Issuer hoặc Cert
                        if (HasCaCert && !string.IsNullOrWhiteSpace(CaCertSha256) && certificate != null)
                        {
                            byte[] serverHash = certificate.GetCertHash();
                            string serverSha = BitConverter.ToString(serverHash).Replace("-", ":");
                            if (serverSha.Equals(CaCertSha256, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        // Nếu người dùng chọn bỏ qua lỗi
                        return sslPolicyErrors == SslPolicyErrors.None;
                    });

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = Host.Trim(),
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                };

                await sslStream.AuthenticateAsClientAsync(sslOptions, cts.Token).ConfigureAwait(false);

                IsTestSuccess = true;
                TestStatusMessage = string.Format(
                    LocalizationService.Instance["MqttBrokerTlsTestSuccess"],
                    sslStream.SslProtocol,
                    sslStream.CipherAlgorithm);
            }
            else
            {
                IsTestSuccess = true;
                TestStatusMessage = LocalizationService.Instance["MqttBrokerTestSuccess"];
            }
        }
        catch (OperationCanceledException)
        {
            IsTestSuccess = false;
            TestStatusMessage = string.Format(
                LocalizationService.Instance["MqttBrokerTestFailed"],
                LocalizationService.Instance["MqttBrokerErrTimeout"]);
        }
        catch (AuthenticationException authEx)
        {
            IsTestSuccess = false;
            TestStatusMessage = string.Format(
                LocalizationService.Instance["MqttBrokerTlsCertInvalid"],
                authEx.Message);
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

        // Lưu thông số chứng chỉ TLS (SPLC-SPEC-MQTT-003)
        _metadata.MqttTlsCaCertPath = CaCertPath ?? string.Empty;
        _metadata.MqttTlsCaCertPem = CaCertPem ?? string.Empty;
        _metadata.MqttTlsAllowSelfSigned = AllowSelfSigned;

        _metadata.LastModified = DateTime.UtcNow;

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
