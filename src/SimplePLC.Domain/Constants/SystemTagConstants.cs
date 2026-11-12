namespace SimplePLC.Domain.Constants;

/// <summary>
/// Các hằng số định nghĩa chỉ số và số lượng System Tags theo chuẩn công nghiệp (IEC 61131-3).
/// Được ánh xạ cố định trong vùng nhớ Tag hệ thống (1000..1007).
/// </summary>
public static class SystemTagConstants
{
    /// <summary>
    /// Chỉ số cơ sở của vùng System Tags.
    /// </summary>
    public const ushort SystemTagBaseIndex = 1000;

    /// <summary>
    /// Tổng số lượng System Tags được hỗ trợ.
    /// </summary>
    public const ushort SystemTagCount = 8;

    /// <summary>
    /// Tag 1000: Xung chu kỳ quét đầu tiên (SYS_FIRST_SCAN: 1 ở scan 1, sau đó 0).
    /// </summary>
    public const ushort SysIndexFirstScan = 1000;

    /// <summary>
    /// Tag 1001: Mức logic 1 vĩnh viễn (SYS_ON / TRUE).
    /// </summary>
    public const ushort SysIndexAlwaysOn = 1001;

    /// <summary>
    /// Tag 1002: Mức logic 0 vĩnh viễn (SYS_OFF / FALSE).
    /// </summary>
    public const ushort SysIndexAlwaysOff = 1002;

    /// <summary>
    /// Tag 1003: Xung vuông chu kỳ 1.0 Giây (SYS_CLOCK_1S: 500ms ON / 500ms OFF).
    /// </summary>
    public const ushort SysIndexClock1S = 1003;

    /// <summary>
    /// Tag 1004: Xung vuông chu kỳ 100 Mili-giây (SYS_CLOCK_100MS: 50ms ON / 50ms OFF).
    /// </summary>
    public const ushort SysIndexClock100Ms = 1004;

    /// <summary>
    /// Tag 1005: Giờ hiện tại của đồng hồ thời gian thực RTC (SYS_RTC_HOUR: 0..23).
    /// </summary>
    public const ushort SysIndexRtcHour = 1005;

    /// <summary>
    /// Tag 1006: Phút hiện tại của đồng hồ thời gian thực RTC (SYS_RTC_MINUTE: 0..59).
    /// </summary>
    public const ushort SysIndexRtcMinute = 1006;

    /// <summary>
    /// Tag 1007: Trạng thái truyền thông khả dụng (SYS_COMM_OK: 1 khi online).
    /// </summary>
    public const ushort SysIndexCommOk = 1007;
}
