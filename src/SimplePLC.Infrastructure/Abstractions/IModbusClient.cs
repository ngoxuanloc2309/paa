namespace SimplePLC.Infrastructure.Abstractions;

/// <summary>
/// Trừu tượng giao tiếp Modbus client (R2, R5, R6).
/// Quản lý kết nối transport và các hàm đọc/ghi thanh ghi Modbus RTU.
/// </summary>
public interface IModbusClient : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>
    /// Đọc mảng thanh ghi holding registers (Modbus FC03).
    /// </summary>
    Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhiều thanh ghi holding registers (Modbus FC16).
    /// </summary>
    Task WriteMultipleRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi một thanh ghi đơn (Modbus FC06 hoặc FC16 1 thanh ghi).
    /// </summary>
    Task WriteSingleRegisterAsync(
        byte slaveId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default);
}
