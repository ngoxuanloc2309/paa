using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Studio.Services.Modbus;

/// <summary>
/// Giao diện dịch vụ đọc thử &amp; chẩn đoán thiết bị Modbus RTU qua cổng COM phần cứng.
/// </summary>
public interface IModbusLiveProbeService : IAsyncDisposable
{
    /// <summary>
    /// Cho biết cổng COM hiện tại có đang mở hay không.
    /// </summary>
    bool IsPortOpen { get; }

    /// <summary>
    /// Tên cổng COM đang mở (ví dụ "COM3").
    /// </summary>
    string? CurrentPortName { get; }

    /// <summary>
    /// Mở cổng COM với các thông số truyền thông vật lý.
    /// </summary>
    Task OpenPortAsync(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đóng cổng COM an toàn.
    /// </summary>
    Task ClosePortAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực thi một truy vấn đọc thử đối với kênh Modbus mục tiêu.
    /// </summary>
    Task<ModbusProbeResult> ProbeAsync(byte slaveId, byte functionCode, ushort address, ModbusDataType dataType, ModbusEndianness endianness, double scale, int timeoutMs = 1000, CancellationToken cancellationToken = default);
}
