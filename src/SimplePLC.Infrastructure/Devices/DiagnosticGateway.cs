using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực IDiagnosticGateway giao tiếp với phân vùng Diagnostic Block (0x0A20..0x0A24)
/// và ghi giá trị runtime tags (0x0900..0x09FF) qua giao thức Modbus RTU (Wire Profile V2).
/// </summary>
public sealed class DiagnosticGateway : IDiagnosticGateway
{
    private readonly IModbusClient _client;

    public DiagnosticGateway(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<DiagnosticStatusDto> ReadDiagnosticStatusAsync(
        byte slaveId = 1,
        CancellationToken cancellationToken = default)
    {
        var regs = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.DiagBlockBaseAddress,
            ModbusRegisterMap.DiagBlockLength,
            cancellationToken
        ).ConfigureAwait(false);

        if (regs.Length < ModbusRegisterMap.DiagBlockLength)
        {
            throw new InvalidOperationException($"Invalid diagnostic block response: expected {ModbusRegisterMap.DiagBlockLength} registers, received {regs.Length}.");
        }

        var state = (SPLC_DiagState)regs[1];
        var flags = (SPLC_DiagFlags)regs[2];
        var leaseRemainingMs = regs[3];
        var errorCode = (SPLC_DiagErrorCode)regs[4];

        return new DiagnosticStatusDto(state, flags, leaseRemainingMs, errorCode);
    }

    public async Task<DiagnosticStatusDto> SendDiagnosticCommandAsync(
        byte slaveId,
        SPLC_DiagCommand command,
        CancellationToken cancellationToken = default)
    {
        // Ghi lệnh vào thanh ghi DIAG_COMMAND (0x0A20)
        await _client.WriteSingleRegisterAsync(
            slaveId,
            ModbusRegisterMap.DiagCommandAddress,
            (ushort)command,
            cancellationToken
        ).ConfigureAwait(false);

        // Đọc lại trạng thái chẩn đoán ngay sau khi phát lệnh
        return await ReadDiagnosticStatusAsync(slaveId, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteTagValueAsync(
        byte slaveId,
        ushort tagIndex,
        int rawValue,
        CancellationToken cancellationToken = default)
    {
        ushort address = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + tagIndex * ModbusRegisterMap.RegistersPerRuntimeTag);

        ushort regHi = (ushort)((rawValue >> 16) & 0xFFFF);
        ushort regLo = (ushort)(rawValue & 0xFFFF);

        await _client.WriteMultipleRegistersAsync(
            slaveId,
            address,
            new[] { regHi, regLo },
            cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task WriteTagsBatchAsync(
        byte slaveId,
        ushort startTagIndex,
        IReadOnlyList<int> rawValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawValues);
        if (rawValues.Count == 0) return;

        ushort address = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + startTagIndex * ModbusRegisterMap.RegistersPerRuntimeTag);
        var registers = new ushort[rawValues.Count * ModbusRegisterMap.RegistersPerRuntimeTag];

        for (int i = 0; i < rawValues.Count; i++)
        {
            int val = rawValues[i];
            registers[i * 2] = (ushort)((val >> 16) & 0xFFFF);
            registers[i * 2 + 1] = (ushort)(val & 0xFFFF);
        }

        await _client.WriteMultipleRegistersAsync(
            slaveId,
            address,
            registers,
            cancellationToken
        ).ConfigureAwait(false);
    }
}
