using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Application.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;

namespace SimplePLC.Infrastructure.Sessions;

/// <summary>
/// Nhà máy khởi tạo phiên kết nối vi điều khiển duy nhất (R2).
/// Xử lý phân nhánh giữa phần cứng thật (UsbCdcEndpoint) và môi trường giả lập (SimulatorEndpoint).
/// Thực hiện quy trình: Mở Transport -> Tạo Client -> Đọc Descriptor -> Thẩm định tương thích -> Tạo Session.
/// </summary>
public sealed class DeviceConnectionFactory : IDeviceConnectionFactory
{
    private readonly IDeviceCompatibilityValidator _validator;
    private readonly Func<UsbCdcOptions, IUsbCdcTransport>? _transportFactory;

    public DeviceConnectionFactory(
        IDeviceCompatibilityValidator? validator = null,
        Func<UsbCdcOptions, IUsbCdcTransport>? transportFactory = null)
    {
        _validator = validator ?? new StandardDeviceCompatibilityValidator();
        _transportFactory = transportFactory;
    }

    public async Task<DeviceConnectionResult> ConnectAsync(
        DeviceEndpoint endpoint,
        byte slaveId = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeCts.CancelAfter(TimeSpan.FromSeconds(3));
        var ct = handshakeCts.Token;

        IUsbCdcTransport? transport = null;
        IModbusClient? client = null;

        try
        {
            if (endpoint is UsbCdcEndpoint usbCdc)
            {
                var options = new UsbCdcOptions(usbCdc.PortName);
                var realTransport = _transportFactory != null ? _transportFactory(options) : new UsbCdcTransport();
                await realTransport.OpenAsync(options, ct).ConfigureAwait(false);
                transport = realTransport;
                client = new ModbusRtuClient(transport);
            }
            else if (endpoint is SimulatorEndpoint)
            {
                var fakeTransport = new FakeUsbCdcTransport { IsOpen = true };
                transport = fakeTransport;
                client = new FakeModbusClient(isConnected: true);
            }
            else
            {
                return DeviceConnectionResult.Failed($"Unsupported endpoint type: {endpoint.GetType().Name}");
            }

            // 1. Đọc DeviceDescriptor
            var descriptorReader = new DeviceDescriptorReader(client);
            var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, ct).ConfigureAwait(false);

            // 2. Thẩm định tính tương thích cơ bản của Descriptor
            var compatibility = _validator.Validate(descriptor);
            if (!compatibility.IsCompatible)
            {
                await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                await transport.DisposeAsync().ConfigureAwait(false);
                return DeviceConnectionResult.Incompatible(compatibility);
            }

            // 3. Đọc DeviceResourceInfo tại 0x0020
            var resourceInfo = await descriptorReader.ReadResourceInfoAsync(slaveId, ct).ConfigureAwait(false);

            // 4. Thẩm định Wire Profile V1 và dựng ProductDefinition tại runtime
            if (!DeviceProfileMapper.TryBuildProductDefinition(descriptor, resourceInfo, out var product, out var profileError))
            {
                await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                await transport.DisposeAsync().ConfigureAwait(false);
                return DeviceConnectionResult.Incompatible(
                    CompatibilityResult.Incompatible(CompatibilityStatus.UnsupportedDeviceVariant, profileError ?? "Invalid device profile."));
            }

            // 5. Khởi tạo Gateway hợp nhất (Rules) và các capabilities khác
            var ruleGateway = new RuleTableGateway(client);
            var tagReader = new RuntimeTagReader(client);
            var healthReader = new DeviceHealthReader(client);
            var commandClient = new SystemCommandClient(client);

            // 6. Đóng gói vào DeviceSession với ProductDefinition động
            var session = new DeviceSession(
                endpoint,
                descriptor,
                slaveId,
                transport,
                client,
                ruleGateway,
                tagReader,
                healthReader,
                commandClient,
                resourceInfo,
                product!);

            return DeviceConnectionResult.Success(session);
        }
        catch (OperationCanceledException)
        {
            if (transport != null)
            {
                try { await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                try { await transport.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            return DeviceConnectionResult.Failed("Connection timed out waiting for device handshake response.");
        }
        catch (Exception ex)
        {
            if (transport != null)
            {
                try { await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                try { await transport.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            return DeviceConnectionResult.Failed(ex.Message);
        }
    }
}
