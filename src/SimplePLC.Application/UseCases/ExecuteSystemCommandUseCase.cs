namespace SimplePLC.Application.UseCases;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

public sealed class ExecuteSystemCommandUseCase
{
    private readonly ISystemCommandClient _commandClient;
    private readonly IDeviceOperationCoordinator? _coordinator;
    private readonly ISessionManager? _sessionManager;
    private readonly IDeviceLifecycleManager? _lifecycleManager;

    public ExecuteSystemCommandUseCase(
        ISystemCommandClient commandClient,
        IDeviceOperationCoordinator? coordinator = null,
        ISessionManager? sessionManager = null,
        IDeviceLifecycleManager? lifecycleManager = null)
    {
        _commandClient = commandClient ?? throw new ArgumentNullException(nameof(commandClient));
        _coordinator = coordinator;
        _sessionManager = sessionManager;
        _lifecycleManager = lifecycleManager;
    }

    public async Task<SystemCommandResultDto> ExecuteAsync(
        SPLC_SystemCommand command,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        var op = command switch
        {
            SPLC_SystemCommand.REBOOT => DeviceOperation.Reboot,
            SPLC_SystemCommand.FACTORY_RESET => DeviceOperation.FactoryReset,
            _ => DeviceOperation.SystemCommand
        };

        IAsyncDisposable? lease = null;
        if (_coordinator != null)
        {
            lease = await _coordinator.AcquireExclusiveAsync(op, ct).ConfigureAwait(false);
        }

        try
        {
            var result = await _commandClient.ExecuteCommandAsync(slaveId, command, ct).ConfigureAwait(false);

            // Flow chuyên biệt cho REBOOT & FACTORY_RESET:
            // Thiết bị khởi động lại và ngắt kết nối USB vật lý (Expected Disconnect).
            // Ủy quyền cho DeviceLifecycleManager đóng session và kích hoạt expected restart (không gây race condition).
            if (command is SPLC_SystemCommand.REBOOT or SPLC_SystemCommand.FACTORY_RESET)
            {
                if (_lifecycleManager != null)
                {
                    await _lifecycleManager.NotifyExpectedRestartAsync(ct).ConfigureAwait(false);
                }
                else if (_sessionManager != null)
                {
                    await _sessionManager.CloseCurrentSessionAsync(ct).ConfigureAwait(false);
                }
            }

            return result;
        }
        finally
        {
            if (lease != null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
