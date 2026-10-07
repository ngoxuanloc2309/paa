using SimplePLC.Application.Models;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Nhà máy khởi tạo kết nối và phiên làm việc (Session) với vi điều khiển.
/// </summary>
public interface IDeviceConnectionFactory
{
    Task<DeviceConnectionResult> ConnectAsync(
        DeviceEndpoint endpoint,
        byte slaveId = 1,
        CancellationToken cancellationToken = default);
}
