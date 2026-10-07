namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Lệnh chẩn đoán & điều khiển cưỡng bức (0x0A20) theo Wire Contract V2.
/// </summary>
public enum SPLC_DiagCommand : ushort
{
    NONE = 0,
    ENTER_DIAG = 1,     /* Chiếm quyền điều khiển cưỡng bức thủ công */
    HEARTBEAT = 2,      /* Khởi tạo lại bộ đếm thời gian thuê (Lease Watchdog) */
    EXIT_DIAG = 3,      /* Nhả quyền chẩn đoán, khôi phục Rule Engine tự động */
    COMMIT_RETAIN = 4,  /* Ghi Flash nguyên tử bóng RAM Retain */
    DISCARD_RETAIN = 5  /* Nạp lại bóng RAM Retain từ Flash, xóa cờ dirty */
}

/// <summary>
/// Trạng thái hoạt động chẩn đoán của vi điều khiển (0x0A21).
/// </summary>
public enum SPLC_DiagState : ushort
{
    NONE = 0,
    ENGINE_RUNNING = 1, /* Chế độ tự động bình thường: Rule Engine đang hoạt động */
    DIAG_CONTROL = 2,   /* Chế độ chẩn đoán thủ công: Cho phép Host ghi giá trị */
    TRANSITIONING = 3,  /* Đang đồng bộ hóa tại ranh giới chu kỳ quét */
    FAULT = 4           /* Lỗi hệ thống: Khóa chức năng chẩn đoán */
}

/// <summary>
/// Cờ trạng thái chẩn đoán (0x0A22).
/// </summary>
[Flags]
public enum SPLC_DiagFlags : ushort
{
    NONE = 0,
    RETAIN_DIRTY = 1 << 0, /* 0x0001: RAM retain chưa được lưu vào Flash */
    LEASE_ACTIVE = 1 << 1  /* 0x0002: Bộ đếm thời gian thuê đang kích hoạt */
}

/// <summary>
/// Mã lỗi chẩn đoán chốt lưu (0x0A24).
/// </summary>
public enum SPLC_DiagErrorCode : ushort
{
    NONE = 0,
    DENIED_FAULT = 1,        /* Từ chối: Hệ thống đang ở trạng thái Fault */
    LEASE_EXPIRED = 2,       /* Hết hạn thời gian thuê (Watchdog trip) */
    FLASH_CRC_MISMATCH = 3,  /* Lỗi kiểm tra CRC khi commit Flash */
    INVALID_COMMAND = 4,     /* Mã lệnh không hợp lệ hoặc sai ngữ cảnh */
    RETAIN_DIRTY = 5         /* Từ chối thoát: Còn dữ liệu retain chưa commit trong RAM */
}
