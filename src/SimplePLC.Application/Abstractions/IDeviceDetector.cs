using SimplePLC.Application.Models;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Trừu tượng hóa việc phát hiện các endpoint tiềm năng trong hệ thống (Discovery only).
/// Chỉ liệt kê các endpoint có khả năng tồn tại, không thực hiện mở port, probe hay handshake.
/// </summary>
public interface IDeviceDetector
{
    /// <summary>
    /// Tìm kiếm danh sách các endpoint ứng viên hiện có trên hệ thống.
    /// </summary>
    /// <param name="ct">Token hủy thao tác.</param>
    /// <returns>Danh sách các endpoint tiềm năng.</returns>
    Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default);
}
