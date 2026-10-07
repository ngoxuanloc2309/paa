using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;

namespace SimplePLC.Application.UseCases;

public sealed class ConnectDeviceUseCase
{
    private readonly IDeviceConnectionFactory _connectionFactory;
    private readonly ISessionManager _sessionManager;

    public ConnectDeviceUseCase(
        IDeviceConnectionFactory connectionFactory,
        ISessionManager sessionManager)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public async Task<DeviceConnectionInfo> ExecuteConnectAsync(
        DeviceEndpoint endpoint,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        try
        {
            var connResult = await _connectionFactory.ConnectAsync(endpoint, slaveId, ct).ConfigureAwait(false);

            if (!connResult.IsSuccess)
            {
                if (!connResult.Compatibility.IsCompatible)
                {
                    return DeviceConnectionInfo.IncompatibleState(
                        endpoint,
                        connResult.Compatibility.Status,
                        connResult.FailureReason ?? connResult.Compatibility.Reason ?? "Device is incompatible.");
                }

                return DeviceConnectionInfo.FaultedState(
                    endpoint,
                    connResult.FailureReason ?? "Failed to connect to device.");
            }

            var session = connResult.Session!;
            var health = await session.Health.ReadHealthAsync(slaveId, ct).ConfigureAwait(false);

            await _sessionManager.SetCurrentSessionAsync(session, ct).ConfigureAwait(false);

            return DeviceConnectionInfo.ConnectedState(endpoint, session.Descriptor, health);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DeviceConnectionInfo.FaultedState(endpoint, ex.Message);
        }
    }

    public Task<DeviceConnectionInfo> ExecuteConnectAsync(
        string portName,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        DeviceEndpoint endpoint = string.Equals(portName, "SIMULATOR (VIRTUAL)", StringComparison.OrdinalIgnoreCase)
            ? new SimulatorEndpoint()
            : new UsbCdcEndpoint(portName);

        return ExecuteConnectAsync(endpoint, slaveId, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _sessionManager.CloseCurrentSessionAsync(ct).ConfigureAwait(false);
    }
}
