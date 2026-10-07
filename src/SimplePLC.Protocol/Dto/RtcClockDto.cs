namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 4 thanh ghi (8 bytes) của SPLC_RtcClock đọc/ghi tại địa chỉ 0x0810.
/// Đồng bộ thời gian thực theo chuẩn Unix Epoch UTC và múi giờ địa phương.
/// </summary>
public sealed class RtcClockDto
{
    /// <summary>
    /// Số giây từ Epoch UTC (01/01/1970 00:00:00 UTC).
    /// </summary>
    public uint EpochUtcSeconds { get; set; }

    /// <summary>
    /// Độ lệch múi giờ theo phút (Ví dụ: UTC+7 = +420, UTC-5 = -300).
    /// </summary>
    public short TimezoneOffsetMinutes { get; set; }

    /// <summary>
    /// Trạng thái đã được đồng bộ tin cậy (Bit 0).
    /// </summary>
    public bool IsSynced { get; set; }

    /// <summary>
    /// Thiết bị có chip RTC ngoại vi phần cứng rời (Bit 1).
    /// </summary>
    public bool HasHardwareRtc { get; set; }

    /// <summary>
    /// Cảnh báo pin nuôi RTC bị yếu (Bit 2).
    /// </summary>
    public bool IsBatteryLow { get; set; }

    /// <summary>
    /// Thời gian UTC tương ứng.
    /// </summary>
    public DateTimeOffset UtcDateTime => DateTimeOffset.FromUnixTimeSeconds(EpochUtcSeconds);

    /// <summary>
    /// Thời gian địa phương sau khi áp dụng múi giờ.
    /// </summary>
    public DateTimeOffset LocalDateTime => UtcDateTime.ToOffset(TimeSpan.FromMinutes(TimezoneOffsetMinutes));

    /// <summary>
    /// Giờ địa phương dạng HHMM (ví dụ 07:00 -> 700, 17:30 -> 1730).
    /// </summary>
    public int LocalHhmm => LocalDateTime.Hour * 100 + LocalDateTime.Minute;

    /// <summary>
    /// Tạo DTO RTC từ thời điểm hiện tại của máy tính.
    /// </summary>
    public static RtcClockDto Now()
    {
        var now = DateTimeOffset.Now;
        return new RtcClockDto
        {
            EpochUtcSeconds = (uint)now.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = (short)now.Offset.TotalMinutes,
            IsSynced = true,
            HasHardwareRtc = false,
            IsBatteryLow = false
        };
    }
}
