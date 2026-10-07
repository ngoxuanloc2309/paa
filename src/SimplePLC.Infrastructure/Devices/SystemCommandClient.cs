using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực gửi System Command xuống MCU và đợi kết quả thực thi.
/// </summary>
public sealed class SystemCommandClient : ISystemCommandClient
{
    private readonly IModbusClient _client;

    public SystemCommandClient(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<SystemCommandResultDto> ExecuteCommandAsync(
        byte slaveId,
        SPLC_SystemCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Ghi command vào SYSTEM_COMMAND (0x0A00)
        try
        {
            await _client.WriteSingleRegisterAsync(
                slaveId,
                ModbusRegisterMap.SystemCommandAddress,
                (ushort)command,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch when (command == SPLC_SystemCommand.REBOOT)
        {
            // Bỏ qua lỗi ngắt kết nối vật lý tức thì khi MCU reboot
        }

        // Với lệnh REBOOT: MCU khởi động lại ngay lập tức, ngắt kết nối USB.
        // Không chờ thanh ghi SYSTEM_COMMAND_RESULT.
        if (command == SPLC_SystemCommand.REBOOT)
        {
            return new SystemCommandResultDto
            {
                Status = SPLC_CommandStatus.DONE,
                ErrorCode = SPLC_ErrorCode.NONE
            };
        }

        // 2. Chờ kết quả từ SYSTEM_COMMAND_RESULT (0x0A01 - 0x0A02) cho các lệnh thông thường
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultRegs = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.SystemCommandResultAddress,
                ModbusRegisterMap.SystemCommandResultLength,
                cancellationToken
            );

            var status = (SPLC_CommandStatus)resultRegs[0];
            var error = (SPLC_ErrorCode)resultRegs[1];

            if (status == SPLC_CommandStatus.DONE || status == SPLC_CommandStatus.ERROR)
            {
                return new SystemCommandResultDto
                {
                    Status = status,
                    ErrorCode = error
                };
            }

            await Task.Delay(25, cancellationToken);
        }

        return new SystemCommandResultDto
        {
            Status = SPLC_CommandStatus.ERROR,
            ErrorCode = SPLC_ErrorCode.BUSY
        };
    }
}
