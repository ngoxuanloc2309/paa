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

    // Mutual TLS (mTLS) Client Certificate & Key Properties (IEC 62443-4-2)
    [ObservableProperty]
    private bool _useMtls;

    [ObservableProperty]
    private string _clientCertPath = string.Empty;

    [ObservableProperty]
    private string _clientCertPem = string.Empty;

    [ObservableProperty]
    private string _clientCertSubject = string.Empty;

    [ObservableProperty]
    private string _clientCertIssuer = string.Empty;

    [ObservableProperty]
    private string _clientCertExpiry = string.Empty;

    [ObservableProperty]
    private string _clientCertSha256 = string.Empty;

    [ObservableProperty]
    private bool _hasClientCert;

    [ObservableProperty]
    private string _clientKeyPath = string.Empty;

    [ObservableProperty]
    private string _clientKeyPem = string.Empty;

    [ObservableProperty]
    private string _clientKeyPassword = string.Empty;

    [ObservableProperty]
    private bool _hasClientKey;

    // Advanced TLS Protocol Parameters
    [ObservableProperty]
    private bool _ignoreHostname;

    [ObservableProperty]
    private int _tlsVersionIndex; // 0: Auto (TLS 1.2/1.3), 1: TLS 1.2, 2: TLS 1.3

    [ObservableProperty]
    private string _sniHostname = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    /// <summary>
    /// Cho biết nút kiểm tra kết nối có thể thực thi hay không (chỉ khi dịch vụ được kích hoạt và không trong quá trình test).
    /// </summary>
    public bool CanTestConnection => IsEnabled && !IsTesting;

    [ObservableProperty]
    private string _testStatusMessage = string.Empty;

    [ObservableProperty]
    private bool? _isTestSuccess;

    // Last Will & Testament (LWT) Properties (SPLC-SPEC-MQTT-004)
    [ObservableProperty]
    private bool _willEnabled;

    [ObservableProperty]
    private string _willTopic = string.Empty;

    [ObservableProperty]
    private string _willPayload = "{\"status\":\"offline\"}";

    [ObservableProperty]
    private byte _willQos = 1;

    [ObservableProperty]
    private bool _willRetain = true;

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

        // Load mTLS & Advanced Settings
        _useMtls = metadata.MqttTlsMtlsEnabled;
        _clientCertPath = metadata.MqttTlsClientCertPath ?? string.Empty;
        _clientCertPem = metadata.MqttTlsClientCertPem ?? string.Empty;
        _clientKeyPath = metadata.MqttTlsClientKeyPath ?? string.Empty;
        _clientKeyPem = metadata.MqttTlsClientKeyPem ?? string.Empty;
        _clientKeyPassword = metadata.MqttTlsClientKeyPassword ?? string.Empty;
        _ignoreHostname = metadata.MqttTlsIgnoreHostname;
        _tlsVersionIndex = metadata.MqttTlsProtocolVersion >= 0 && metadata.MqttTlsProtocolVersion <= 2 ? metadata.MqttTlsProtocolVersion : 0;
        _sniHostname = metadata.MqttTlsSniHostname ?? string.Empty;

        _willEnabled = metadata.MqttWillEnabled;
        _willTopic = metadata.MqttWillTopic ?? string.Empty;
        _willPayload = string.IsNullOrEmpty(metadata.MqttWillPayload) ? "{\"status\":\"offline\"}" : metadata.MqttWillPayload;
        _willQos = metadata.MqttWillQos;
        _willRetain = metadata.MqttWillRetain;

        // Tự động phân tích chứng chỉ CA nếu đã có sẵn trong metadata
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

        // Tự động phân tích chứng chỉ Client nếu đã có sẵn trong metadata
        if (!string.IsNullOrWhiteSpace(_clientCertPem))
        {
            ParseClientCert(_clientCertPem, _clientCertPath);
        }
        else if (!string.IsNullOrWhiteSpace(_clientCertPath) && File.Exists(_clientCertPath))
        {
            try
            {
                string pem = File.ReadAllText(_clientCertPath);
                ParseClientCert(pem, _clientCertPath);
            }
            catch
            {
                // Bỏ qua nếu file không đọc được lúc khởi tạo
            }
        }

        if (!string.IsNullOrWhiteSpace(_clientKeyPath))
        {
            HasClientKey = true;
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTestConnection));
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsTestingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTestConnection));
        TestConnectionCommand.NotifyCanExecuteChanged();
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
            LoadClientCertificateFile(dialog.FileName);
        }
    }

    public void LoadClientCertificateFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".pfx" || ext == ".p12")
            {
                // PKCS#12 bundle
                ClientCertPath = filePath;
                ClientCertPem = string.Empty;
                var cert = new X509Certificate2(filePath, ClientKeyPassword, X509KeyStorageFlags.EphemeralKeySet);
                ClientCertSubject = cert.Subject;
                ClientCertIssuer = cert.Issuer;
                ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");
                byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
                ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
                HasClientCert = true;
                HasClientKey = true; // PKCS#12 contains private key
                ClientKeyPath = filePath;
            }
            else
            {
                string pemContent = File.ReadAllText(filePath);
                ParseClientCert(pemContent, filePath);
            }
        }
        catch (Exception ex)
        {
            ClearClientCert();
            TestStatusMessage = string.Format(LocalizationService.Instance["MqttBrokerTlsCertInvalid"], ex.Message);
            IsTestSuccess = false;
        }
    }

    public void ParseClientCert(string pemContent, string filePath)
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

            ClientCertPath = filePath;
            ClientCertPem = pemContent;
            ClientCertSubject = cert.Subject;
            ClientCertIssuer = cert.Issuer;
            ClientCertExpiry = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss");

            byte[] hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            ClientCertSha256 = BitConverter.ToString(hash).Replace("-", ":");
            HasClientCert = true;
        }
        catch (Exception)
        {
            ClientCertPath = filePath;
            ClientCertPem = pemContent;
            ClientCertSubject = Path.GetFileName(filePath);
            ClientCertIssuer = "Client Certificate";
            ClientCertExpiry = "Loaded";
            ClientCertSha256 = "N/A";
            HasClientCert = true;
        }
    }

    [RelayCommand]
    public void ClearClientCert()
    {
        ClientCertPath = string.Empty;
        ClientCertPem = string.Empty;
        ClientCertSubject = string.Empty;
        ClientCertIssuer = string.Empty;
        ClientCertExpiry = string.Empty;
        ClientCertSha256 = string.Empty;
        HasClientCert = false;
    }

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
            LoadClientKeyFile(dialog.FileName);
        }
    }

    public void LoadClientKeyFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            ClientKeyPath = filePath;
            ClientKeyPem = File.ReadAllText(filePath);
            HasClientKey = true;
        }
        catch (Exception ex)
        {
            ClearClientKey();
            TestStatusMessage = string.Format(LocalizationService.Instance["MqttBrokerTlsCertInvalid"], ex.Message);
            IsTestSuccess = false;
        }
    }

    [RelayCommand]
    public void ClearClientKey()
    {
        ClientKeyPath = string.Empty;
        ClientKeyPem = string.Empty;
        HasClientKey = false;
    }

    /// <summary>
    /// Tạo đối tượng X509Certificate2 kết hợp chứng chỉ công khai và khóa bí mật cho xác thực mTLS.
    /// </summary>
    public X509Certificate2? CreateClientCertificate()
    {
        if (!HasClientCert) return null;

        try
        {
            string ext = Path.GetExtension(ClientCertPath).ToLowerInvariant();
            if ((ext == ".pfx" || ext == ".p12") && File.Exists(ClientCertPath))
            {
                return new X509Certificate2(ClientCertPath, ClientKeyPassword, X509KeyStorageFlags.EphemeralKeySet);
            }

            if (!string.IsNullOrWhiteSpace(ClientCertPem) && !string.IsNullOrWhiteSpace(ClientKeyPem))
            {
                return X509Certificate2.CreateFromPem(ClientCertPem, ClientKeyPem);
            }

            if (File.Exists(ClientCertPath) && File.Exists(ClientKeyPath))
            {
                string certText = File.ReadAllText(ClientCertPath);
                string keyText = File.ReadAllText(ClientKeyPath);
                return X509Certificate2.CreateFromPem(certText, keyText);
            }

            if (File.Exists(ClientCertPath))
            {
                return new X509Certificate2(ClientCertPath);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            
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

                        // Bỏ qua lỗi tên miền khi cấu hình IP trong nhà máy
                        if (IgnoreHostname && sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                        {
                            return true;
                        }

                        // Nếu có tệp CA riêng, kiểm tra khớp Fingerprint
                        if (HasCaCert && !string.IsNullOrWhiteSpace(CaCertSha256) && certificate != null)
                        {
                            byte[] serverHash = certificate.GetCertHash(HashAlgorithmName.SHA256);
                            string serverSha = BitConverter.ToString(serverHash).Replace("-", ":");
                            if (serverSha.Equals(CaCertSha256, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        return sslPolicyErrors == SslPolicyErrors.None;
                    });

                var targetHost = !string.IsNullOrWhiteSpace(SniHostname) ? SniHostname.Trim() : Host.Trim();

                var sslProtocols = TlsVersionIndex switch
                {
                    1 => SslProtocols.Tls12,
                    2 => SslProtocols.Tls13,
                    _ => SslProtocols.Tls12 | SslProtocols.Tls13
                };

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                    EnabledSslProtocols = sslProtocols
                };

                X509Certificate2? clientCert = null;
                if (UseMtls)
                {
                    clientCert = CreateClientCertificate();
                    if (clientCert != null)
                    {
                        sslOptions.ClientCertificates = new X509CertificateCollection { clientCert };
                        sslOptions.LocalCertificateSelectionCallback = (s, t, localCerts, remoteCert, issuers) => clientCert;
                    }
                }

                await sslStream.AuthenticateAsClientAsync(sslOptions, cts.Token).ConfigureAwait(false);

                IsTestSuccess = true;
                if (UseMtls && clientCert != null)
                {
                    TestStatusMessage = string.Format(
                        LocalizationService.Instance["MqttBrokerMtlsTestSuccess"],
                        sslStream.SslProtocol,
                        sslStream.CipherAlgorithm);
                }
                else
                {
                    TestStatusMessage = string.Format(
                        LocalizationService.Instance["MqttBrokerTlsTestSuccess"],
                        sslStream.SslProtocol,
                        sslStream.CipherAlgorithm);
                }
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

    /// <summary>
    /// Lưu cấu hình MQTT Broker vào ProjectMetadata (Single Source of Truth).
    /// </summary>
    public void Apply()
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

        // Lưu thông số chứng chỉ Server CA
        _metadata.MqttTlsCaCertPath = CaCertPath ?? string.Empty;
        _metadata.MqttTlsCaCertPem = CaCertPem ?? string.Empty;
        _metadata.MqttTlsAllowSelfSigned = AllowSelfSigned;

        // Lưu thông số mTLS và cấu hình TLS nâng cao
        _metadata.MqttTlsMtlsEnabled = UseMtls;
        _metadata.MqttTlsClientCertPath = ClientCertPath ?? string.Empty;
        _metadata.MqttTlsClientCertPem = ClientCertPem ?? string.Empty;
        _metadata.MqttTlsClientKeyPath = ClientKeyPath ?? string.Empty;
        _metadata.MqttTlsClientKeyPem = ClientKeyPem ?? string.Empty;
        _metadata.MqttTlsClientKeyPassword = ClientKeyPassword ?? string.Empty;
        _metadata.MqttTlsIgnoreHostname = IgnoreHostname;
        _metadata.MqttTlsProtocolVersion = TlsVersionIndex;
        _metadata.MqttTlsSniHostname = SniHostname?.Trim() ?? string.Empty;

        _metadata.MqttWillEnabled = WillEnabled;
        _metadata.MqttWillTopic = WillTopic?.Trim() ?? string.Empty;
        _metadata.MqttWillPayload = WillPayload ?? string.Empty;
        _metadata.MqttWillQos = WillQos;
        _metadata.MqttWillRetain = WillRetain;

        _metadata.LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Nạp lại cấu hình MQTT Broker từ ProjectMetadata.
    /// </summary>
    public void ReloadFromMetadata()
    {
        IsEnabled = _metadata.MqttBrokerEnabled;
        Host = _metadata.MqttBrokerHost ?? string.Empty;
        Port = _metadata.MqttBrokerPort > 0 ? _metadata.MqttBrokerPort : 1883;
        ClientId = _metadata.MqttClientId ?? string.Empty;
        Username = _metadata.MqttUsername ?? string.Empty;
        Password = _metadata.MqttPassword ?? string.Empty;
        UseTls = _metadata.MqttTlsEnabled;
        CleanSession = _metadata.MqttCleanSession;
        KeepAliveSec = _metadata.MqttKeepAliveSec > 0 ? _metadata.MqttKeepAliveSec : (ushort)60;

        CaCertPath = _metadata.MqttTlsCaCertPath ?? string.Empty;
        CaCertPem = _metadata.MqttTlsCaCertPem ?? string.Empty;
        AllowSelfSigned = _metadata.MqttTlsAllowSelfSigned;

        UseMtls = _metadata.MqttTlsMtlsEnabled;
        ClientCertPath = _metadata.MqttTlsClientCertPath ?? string.Empty;
        ClientCertPem = _metadata.MqttTlsClientCertPem ?? string.Empty;
        ClientKeyPath = _metadata.MqttTlsClientKeyPath ?? string.Empty;
        ClientKeyPem = _metadata.MqttTlsClientKeyPem ?? string.Empty;
        ClientKeyPassword = _metadata.MqttTlsClientKeyPassword ?? string.Empty;
        IgnoreHostname = _metadata.MqttTlsIgnoreHostname;
        TlsVersionIndex = _metadata.MqttTlsProtocolVersion >= 0 && _metadata.MqttTlsProtocolVersion <= 2 ? _metadata.MqttTlsProtocolVersion : 0;
        SniHostname = _metadata.MqttTlsSniHostname ?? string.Empty;

        WillEnabled = _metadata.MqttWillEnabled;
        WillTopic = _metadata.MqttWillTopic ?? string.Empty;
        WillPayload = string.IsNullOrEmpty(_metadata.MqttWillPayload) ? "{\"status\":\"offline\"}" : _metadata.MqttWillPayload;
        WillQos = _metadata.MqttWillQos;
        WillRetain = _metadata.MqttWillRetain;

        if (!string.IsNullOrWhiteSpace(CaCertPem))
        {
            ParseCertPem(CaCertPem, CaCertPath);
        }
    }

    [RelayCommand]
    private void Save()
    {
        Apply();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
