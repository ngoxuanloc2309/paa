namespace SimplePLC.Application.Enums;

/// <summary>
/// Trạng thái chất lượng của giá trị Runtime Tag tại tầng ứng dụng (Host-side quality).
/// Được suy diễn từ tần suất cập nhật dữ liệu qua Modbus polling, không làm thay đổi contract MCU.
/// </summary>
public enum TagQuality
{
    /// <summary>
    /// Chưa từng đọc được giá trị hoặc vừa mất kết nối / ngắt phiên.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Dữ liệu mới, vừa được cập nhật thành công từ chu kỳ polling hợp lệ gần nhất.
    /// </summary>
    Good = 1,

    /// <summary>
    /// Dữ liệu cũ / trễ chu kỳ (bị gián đoạn hoặc quá hạn cập nhật từ 3 chu kỳ polling trở lên).
    /// </summary>
    Stale = 2
}
