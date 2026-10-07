using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Discovery;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Composition Root / Service Locator cho ứng dụng SimplePLC Studio.
/// Quản lý khởi tạo và vòng đời các Use Cases, Gateways, Sessions và Transport theo Clean Architecture.
/// </summary>
public sealed class AppServices
{
    private static readonly Lazy<AppServices> _instance = new(() => new AppServices());
    public static AppServices Instance => _instance.Value;

    public ProductDefinition CurrentProduct { get; set; }

    public IDeviceConnectionFactory ConnectionFactory { get; private set; }
    public ISessionManager SessionManager { get; private set; }
    public IDeviceOperationCoordinator Coordinator { get; private set; }
    public IDeviceLifecycleManager LifecycleManager { get; private set; }

    public IUsbCdcTransport Transport { get; private set; }
    public IModbusClient ModbusClient { get; private set; }

    public IDeviceDescriptorReader DescriptorReader { get; private set; } = null!;
    public IDeviceHealthReader HealthReader { get; private set; } = null!;
    public IRuleTableWriter RuleWriter { get; private set; } = null!;
    public IRuleTableReader RuleReader { get; private set; } = null!;
    public IRuntimeTagReader TagReader { get; private set; } = null!;
    public ISystemCommandClient CommandClient { get; private set; } = null!;
    public IDiagnosticGateway DiagnosticGateway { get; private set; } = null!;
    public DiagnosticControlService DiagnosticController { get; private set; } = null!;
    public IRtcClockClient RtcClockClient { get; private set; } = null!;

    public ConnectDeviceUseCase ConnectUseCase { get; private set; } = null!;
    public DeployRulesUseCase DeployUseCase { get; private set; } = null!;
    public LoadRulesUseCase LoadUseCase { get; private set; } = null!;
    public ExecuteSystemCommandUseCase CommandUseCase { get; private set; } = null!;
    public RuntimeMonitorService MonitorService { get; private set; } = null!;
    public RuntimeStateStore StateStore { get; } = new();

    public AppServices()
    {
        CurrentProduct = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        Transport = new FakeUsbCdcTransport();
        ModbusClient = new FakeModbusClient();

        ConnectionFactory = new DeviceConnectionFactory();
        SessionManager = new SessionManager();
        Coordinator = new DeviceOperationCoordinator();

        // Khởi tạo MonitorService trước khi LifecycleManager (cần truyền callback)
        MonitorService = new RuntimeMonitorService(
            new RuntimeTagReader(ModbusClient),
            new DeviceHealthReader(ModbusClient),
            Coordinator,
            StateStore);

        // D3: LifecycleManager là trung tâm điều phối kết nối
        var detector = new SerialPortDeviceDetector();
        LifecycleManager = new DeviceLifecycleManager(
            ConnectionFactory,
            SessionManager,
            deviceDetector: detector);

        // D3.1: SessionChanged → Attach/Detach monitor loop (đảm bảo 1 loop tại 1 thời điểm)
        SessionManager.SessionChanged += OnSessionChanged;

        // D3.2: Khi monitor phát hiện definitive transport error → LifecycleManager bắt đầu recovery
        MonitorService.OnTransportLost = ex =>
            LifecycleManager.NotifyUnexpectedDisconnectAsync(ex);

        RebuildServices();
    }

    private void OnSessionChanged(IDeviceSession? session)
    {
        if (session != null)
        {
            CurrentProduct = session.Product;
            RuleWriter = session.Rules;
            RuleReader = session.Rules;
            TagReader = session.RuntimeTags;
            HealthReader = session.Health;
            CommandClient = session.Commands;
            DiagnosticGateway = session.Diagnostics;
            DiagnosticController = new DiagnosticControlService(DiagnosticGateway, Coordinator);
            RtcClockClient = session.RtcClock;

            // D3.1: Attach session mới vào monitor (stop vòng lặp cũ → swap readers → start mới)
            _ = MonitorService.AttachSessionAsync(session, CurrentProduct);
        }
        else
        {
            DescriptorReader = new DeviceDescriptorReader(ModbusClient);
            HealthReader = new DeviceHealthReader(ModbusClient);
            RuleWriter = new RuleTableWriter(ModbusClient);
            RuleReader = new RuleTableReader(ModbusClient);
            TagReader = new RuntimeTagReader(ModbusClient);
            CommandClient = new SystemCommandClient(ModbusClient);
            DiagnosticGateway = new SimplePLC.Infrastructure.Devices.DiagnosticGateway(ModbusClient);
            DiagnosticController = new DiagnosticControlService(DiagnosticGateway, Coordinator);
            RtcClockClient = new SimplePLC.Infrastructure.Devices.RtcClockClient(ModbusClient);

            // D3.1: Detach monitor khi session bị đóng
            _ = MonitorService.DetachSessionAsync();
        }

        DeployUseCase = new DeployRulesUseCase(RuleWriter, Coordinator);
        LoadUseCase = new LoadRulesUseCase(RuleReader);

        // D3.2: CommandUseCase nhận LifecycleManager để REBOOT delegate đúng chỗ
        CommandUseCase = new ExecuteSystemCommandUseCase(CommandClient, Coordinator, SessionManager, LifecycleManager);
    }

    public void ConfigureForPort(string portName)
    {
        // Phân giải và cấu hình endpoint được xử lý tự động bởi DeviceConnectionFactory
    }

    public void SetModbusClient(IModbusClient client, IUsbCdcTransport? transport = null)
    {
        ModbusClient = client ?? throw new ArgumentNullException(nameof(client));
        if (transport != null)
        {
            Transport = transport;
        }

        ConnectionFactory = new SingleClientConnectionFactory(ModbusClient, Transport);

        // D3: Rebuild LifecycleManager với ConnectionFactory mới (cần thiết cho test injection)
        // Unsubscribe cũ trước để tránh double-subscription
        SessionManager.SessionChanged -= OnSessionChanged;

        LifecycleManager = new DeviceLifecycleManager(
            ConnectionFactory,
            SessionManager,
            deviceDetector: new SerialPortDeviceDetector());

        SessionManager.SessionChanged += OnSessionChanged;

        MonitorService.OnTransportLost = ex =>
            LifecycleManager.NotifyUnexpectedDisconnectAsync(ex);

        RebuildServices();
    }

    private void RebuildServices()
    {
        DescriptorReader = new DeviceDescriptorReader(ModbusClient);
        HealthReader = new DeviceHealthReader(ModbusClient);
        RuleWriter = new RuleTableWriter(ModbusClient);
        RuleReader = new RuleTableReader(ModbusClient);
        TagReader = new RuntimeTagReader(ModbusClient);
        CommandClient = new SystemCommandClient(ModbusClient);
        DiagnosticGateway = new SimplePLC.Infrastructure.Devices.DiagnosticGateway(ModbusClient);
        DiagnosticController = new DiagnosticControlService(DiagnosticGateway, Coordinator);
        RtcClockClient = new SimplePLC.Infrastructure.Devices.RtcClockClient(ModbusClient);

        // D3.3: ConnectUseCase vẫn giữ — nhưng ToggleConnect trong MainViewModel sẽ
        // dùng LifecycleManager.ConnectAsync thay vì ConnectUseCase trực tiếp
        ConnectUseCase = new ConnectDeviceUseCase(ConnectionFactory, SessionManager);
        DeployUseCase = new DeployRulesUseCase(RuleWriter, Coordinator);
        LoadUseCase = new LoadRulesUseCase(RuleReader);
        CommandUseCase = new ExecuteSystemCommandUseCase(CommandClient, Coordinator, SessionManager, LifecycleManager);
    }

    private sealed class SingleClientConnectionFactory : IDeviceConnectionFactory
    {
        private readonly IModbusClient _client;
        private readonly IUsbCdcTransport? _transport;
        private readonly IDeviceCompatibilityValidator _validator;

        public SingleClientConnectionFactory(IModbusClient client, IUsbCdcTransport? transport, IDeviceCompatibilityValidator? validator = null)
        {
            _client = client;
            _transport = transport;
            _validator = validator ?? new StandardDeviceCompatibilityValidator();
        }

        public async Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client is FakeModbusClient fake && !fake.IsConnected)
                {
                    await fake.ConnectAsync(endpoint is UsbCdcEndpoint u ? u.PortName : "SIMULATOR", 115200, cancellationToken).ConfigureAwait(false);
                }
                else if (_transport != null && !_transport.IsOpen && endpoint is UsbCdcEndpoint usb)
                {
                    await _transport.OpenAsync(new UsbCdcOptions(usb.PortName), cancellationToken).ConfigureAwait(false);
                }

                var descriptorReader = new DeviceDescriptorReader(_client);
                var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, cancellationToken).ConfigureAwait(false);

                var compatibility = _validator.Validate(descriptor);
                if (!compatibility.IsCompatible)
                {
                    return DeviceConnectionResult.Incompatible(compatibility);
                }

                var resourceInfo = await descriptorReader.ReadResourceInfoAsync(slaveId, cancellationToken).ConfigureAwait(false);
                if (!DeviceProfileMapper.TryBuildProductDefinition(descriptor, resourceInfo, out var product, out var profileError))
                {
                    return DeviceConnectionResult.Incompatible(
                        CompatibilityResult.Incompatible(CompatibilityStatus.UnsupportedDeviceVariant, profileError ?? "Invalid device profile."));
                }

                var ruleGateway = new SimplePLC.Infrastructure.Gateways.RuleTableGateway(_client);
                var tagReader = new RuntimeTagReader(_client);
                var healthReader = new DeviceHealthReader(_client);
                var commandClient = new SystemCommandClient(_client);

                var session = new DeviceSession(
                    endpoint,
                    descriptor,
                    slaveId,
                    _transport ?? new FakeUsbCdcTransport(),
                    _client,
                    ruleGateway,
                    tagReader,
                    healthReader,
                    commandClient,
                    resourceInfo,
                    product!);

                return DeviceConnectionResult.Success(session);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return DeviceConnectionResult.Failed(ex.Message);
            }
        }
    }
}
