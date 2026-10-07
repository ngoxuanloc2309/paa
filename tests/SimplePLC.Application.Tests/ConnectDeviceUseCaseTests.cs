using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class ConnectDeviceUseCaseTests
{
    [Fact]
    public async Task ExecuteConnectAsync_SimulatorEndpoint_ReturnsConnectedStateWithMetadata()
    {
        // Arrange
        var connectionFactory = new DeviceConnectionFactory();
        var sessionManager = new SessionManager();
        var useCase = new ConnectDeviceUseCase(connectionFactory, sessionManager);

        // Act
        var result = await useCase.ExecuteConnectAsync("SIMULATOR (VIRTUAL)");

        // Assert
        Assert.Equal(ConnectionStatus.Connected, result.Status);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, result.Descriptor.DeviceClass);
        Assert.NotNull(result.Health);
        Assert.True(sessionManager.HasActiveSession);
        Assert.NotNull(sessionManager.CurrentSession);
        Assert.True(sessionManager.CurrentSession.IsActive);
    }

    [Fact]
    public async Task ExecuteConnectAsync_WhenDeviceIncompatible_ReturnsIncompatibleStateWithoutThrowing()
    {
        // Arrange
        var fakeClient = new FakeModbusClient(isConnected: true);
        // Alter descriptor to an unsupported class
        fakeClient.Simulator.Control.Faults.OverrideDeviceClass = 0x9999; 

        var fakeTransport = new FakeUsbCdcTransport { IsOpen = true };
        var sessionManager = new SessionManager();

        var customFactory = new TestDeviceConnectionFactory(fakeClient, fakeTransport);
        var useCase = new ConnectDeviceUseCase(customFactory, sessionManager);

        // Act
        var result = await useCase.ExecuteConnectAsync(new UsbCdcEndpoint("COM5"));

        // Assert
        Assert.Equal(ConnectionStatus.Incompatible, result.Status);
        Assert.Equal(CompatibilityStatus.UnsupportedDeviceClass, result.Compatibility);
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task DisconnectAsync_ClosesSessionAndCleansUpSessionManager()
    {
        // Arrange
        var connectionFactory = new DeviceConnectionFactory();
        var sessionManager = new SessionManager();
        var useCase = new ConnectDeviceUseCase(connectionFactory, sessionManager);

        await useCase.ExecuteConnectAsync("SIMULATOR (VIRTUAL)");
        Assert.True(sessionManager.HasActiveSession);

        // Act
        await useCase.DisconnectAsync();

        // Assert
        Assert.False(sessionManager.HasActiveSession);
        Assert.Null(sessionManager.CurrentSession);
    }

    private sealed class TestDeviceConnectionFactory : IDeviceConnectionFactory
    {
        private readonly IModbusClient _client;
        private readonly IUsbCdcTransport _transport;
        private readonly IDeviceCompatibilityValidator _validator;

        public TestDeviceConnectionFactory(IModbusClient client, IUsbCdcTransport transport)
        {
            _client = client;
            _transport = transport;
            _validator = new StandardDeviceCompatibilityValidator();
        }

        public async Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            var descriptorReader = new DeviceDescriptorReader(_client);
            var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, cancellationToken);

            var compatibility = _validator.Validate(descriptor);
            if (!compatibility.IsCompatible)
            {
                return DeviceConnectionResult.Incompatible(compatibility);
            }

            var session = new DeviceSession(
                endpoint,
                descriptor,
                slaveId,
                _transport,
                _client,
                new SimplePLC.Infrastructure.Gateways.RuleTableGateway(_client),
                new RuntimeTagReader(_client),
                new DeviceHealthReader(_client),
                new SystemCommandClient(_client));

            return DeviceConnectionResult.Success(session);
        }
    }
}

public class ExecuteSystemCommandUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RebootCommand_ExecutesAndReturnsDone()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var cmdClient = new SystemCommandClient(fakeClient);
        var useCase = new ExecuteSystemCommandUseCase(cmdClient);

        // Act
        var result = await useCase.ExecuteAsync(SPLC_SystemCommand.REBOOT);

        // Assert
        Assert.Equal(SPLC_CommandStatus.DONE, result.Status);
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
    }
}
