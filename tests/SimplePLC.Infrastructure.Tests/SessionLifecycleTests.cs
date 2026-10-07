using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Kiểm thử chuyên biệt về vòng đời phiên kết nối (Session Lifecycle) và rò rỉ tài nguyên COM/USB:
/// - Đảm bảo giải phóng phiên cũ khi thay thế
/// - Đóng transport khi kiểm tra tương thích thất bại hoặc hủy kết nối
/// - Xử lý ngắt kết nối dự kiến sau lệnh Reboot
/// - Đảm bảo session không còn Active khi rút cáp USB vật lý
/// </summary>
public class SessionLifecycleTests
{
    private static DeviceSession CreateTestSession(DeviceEndpoint endpoint, IUsbCdcTransport transport, IModbusClient client)
    {
        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            ProtocolVersion = 1,
            RuleFormatVersion = 1
        };

        return new DeviceSession(
            endpoint,
            descriptor,
            slaveId: 1,
            transport,
            client,
            new RuleTableGateway(client),
            new RuntimeTagReader(client),
            new DeviceHealthReader(client),
            new SystemCommandClient(client));
    }

    [Fact]
    public async Task ReplaceSession_DisposesOldSession()
    {
        // Arrange
        var sessionManager = new SessionManager();
        var transport1 = new FakeUsbCdcTransport { IsOpen = true };
        var client1 = new FakeModbusClient(isConnected: true);
        var session1 = CreateTestSession(new UsbCdcEndpoint("COM3"), transport1, client1);

        var transport2 = new FakeUsbCdcTransport { IsOpen = true };
        var client2 = new FakeModbusClient(isConnected: true);
        var session2 = CreateTestSession(new UsbCdcEndpoint("COM4"), transport2, client2);

        // Act 1: Thiết lập session 1
        await sessionManager.SetCurrentSessionAsync(session1);
        Assert.Same(session1, sessionManager.CurrentSession);
        Assert.True(session1.IsActive);
        Assert.True(transport1.IsOpen);

        // Act 2: Thay thế bằng session 2
        await sessionManager.SetCurrentSessionAsync(session2);

        // Assert: Session 1 phải được giải phóng và đóng transport
        Assert.Same(session2, sessionManager.CurrentSession);
        Assert.False(session1.IsActive);
        Assert.False(transport1.IsOpen);
        Assert.True(session2.IsActive);
        Assert.True(transport2.IsOpen);
    }

    [Fact]
    public async Task FailedCompatibility_ClosesTransport()
    {
        // Arrange
        var fakeTransport = new FakeUsbCdcTransport { IsOpen = true };
        var fakeClient = new FakeModbusClient(isConnected: true);
        // Thiết lập descriptor không tương thích qua Control Faults
        fakeClient.Simulator.Control.Faults.OverrideDeviceClass = 0xEEEE;

        var validator = new StandardDeviceCompatibilityValidator();
        var factory = new TestConnectingFactory(fakeTransport, fakeClient, validator);

        // Act
        var result = await factory.ConnectAsync(new UsbCdcEndpoint("COM10"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CompatibilityStatus.UnsupportedDeviceClass, result.Compatibility.Status);
        // Transport phải được đóng an toàn, không rò rỉ cổng COM
        Assert.False(fakeTransport.IsOpen);
    }

    [Fact]
    public async Task ConnectCancelled_ClosesTransport()
    {
        // Arrange
        var fakeTransport = new FakeUsbCdcTransport { IsOpen = true };
        var fakeClient = new FakeModbusClient(isConnected: true)
        {
            LatencyMs = 2000 // Giả lập độ trễ bắt tay mạng
        };

        var factory = new TestConnectingFactory(fakeTransport, fakeClient);
        using var cts = new CancellationTokenSource(50); // Hủy sau 50ms

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await factory.ConnectAsync(new UsbCdcEndpoint("COM3"), slaveId: 1, cancellationToken: cts.Token);
        });

        // Transport phải được giải phóng ngay khi bị hủy
        Assert.False(fakeTransport.IsOpen);
    }

    [Fact]
    public async Task Reboot_DisposesSessionAfterExpectedDisconnect()
    {
        // Arrange
        var sessionManager = new SessionManager();
        var coordinator = new DeviceOperationCoordinator();
        var transport = new FakeUsbCdcTransport { IsOpen = true };
        var client = new FakeModbusClient(isConnected: true);
        var session = CreateTestSession(new UsbCdcEndpoint("COM3"), transport, client);

        await sessionManager.SetCurrentSessionAsync(session);
        Assert.True(sessionManager.HasActiveSession);

        var cmdClient = new SystemCommandClient(client);
        var useCase = new ExecuteSystemCommandUseCase(cmdClient, coordinator, sessionManager);

        // Act
        var result = await useCase.ExecuteAsync(SPLC_SystemCommand.REBOOT);

        // Assert
        Assert.Equal(SPLC_CommandStatus.DONE, result.Status);
        // Session hiện tại phải được tự động đóng vì vi điều khiển đã reset
        Assert.False(sessionManager.HasActiveSession);
        Assert.Null(sessionManager.CurrentSession);
        Assert.False(session.IsActive);
        Assert.False(coordinator.IsExclusiveOperationActive);
    }

    [Fact]
    public async Task Unplug_DoesNotLeaveSessionActive()
    {
        // Arrange
        var transport = new FakeUsbCdcTransport { IsOpen = true };
        var client = new FakeModbusClient(isConnected: true);
        var session = CreateTestSession(new UsbCdcEndpoint("COM3"), transport, client);

        Assert.True(session.IsActive);

        // Act: Rút cáp USB vật lý (cổng COM biến mất)
        await transport.CloseAsync();

        // Assert: Session phải phản ánh trạng thái inactive ngay lập tức
        Assert.False(session.IsActive);
    }

    private sealed class TestConnectingFactory : IDeviceConnectionFactory
    {
        private readonly IUsbCdcTransport _transport;
        private readonly IModbusClient _client;
        private readonly IDeviceCompatibilityValidator _validator;

        public TestConnectingFactory(
            IUsbCdcTransport transport,
            IModbusClient client,
            IDeviceCompatibilityValidator? validator = null)
        {
            _transport = transport;
            _client = client;
            _validator = validator ?? new StandardDeviceCompatibilityValidator();
        }

        public async Task<DeviceConnectionResult> ConnectAsync(
            DeviceEndpoint endpoint,
            byte slaveId = 1,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var descriptorReader = new DeviceDescriptorReader(_client);
                var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, cancellationToken).ConfigureAwait(false);

                var compatibility = _validator.Validate(descriptor);
                if (!compatibility.IsCompatible)
                {
                    await _transport.CloseAsync(CancellationToken.None);
                    await _transport.DisposeAsync();
                    return DeviceConnectionResult.Incompatible(compatibility);
                }

                var session = new DeviceSession(
                    endpoint,
                    descriptor,
                    slaveId,
                    _transport,
                    _client,
                    new RuleTableGateway(_client),
                    new RuntimeTagReader(_client),
                    new DeviceHealthReader(_client),
                    new SystemCommandClient(_client));

                return DeviceConnectionResult.Success(session);
            }
            catch (OperationCanceledException)
            {
                await _transport.CloseAsync(CancellationToken.None);
                await _transport.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                await _transport.CloseAsync(CancellationToken.None);
                await _transport.DisposeAsync();
                return DeviceConnectionResult.Failed(ex.Message);
            }
        }
    }
}
