namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Vai trò vận hành của phân hệ Zigbee Modbus (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public enum ZigbeeRole : byte
{
    /// <summary>
    /// Master (Coordinator): Khởi tạo mạng Mesh, điều phối và chủ động gửi truy vấn Modbus.
    /// </summary>
    Master = 0,

    /// <summary>
    /// Slave (Router): Nút mạng chuyển tiếp thường trực, lắng nghe và phục vụ bản đồ Modbus Slave SLAVE-001.
    /// </summary>
    Slave = 1
}

/// <summary>
/// Định dạng khung truyền Modbus qua sóng Zigbee (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public enum ZigbeeFraming : byte
{
    /// <summary>
    /// Khung nhị phân Modbus RTU (Mặc định công nghiệp, bắt buộc).
    /// </summary>
    Rtu = 0,

    /// <summary>
    /// Khung ký tự Modbus ASCII (Dự phòng mở rộng, hiện firmware từ chối với CONFIG_ERROR_CODE = 2).
    /// </summary>
    Ascii = 1
}

/// <summary>
/// Loại ăng-ten vô tuyến được kích hoạt trên module Zigbee (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public enum ZigbeeAntenna : byte
{
    /// <summary>
    /// Ăng-ten tích hợp sẵn trên bo mạch (On-board PCB Antenna).
    /// </summary>
    Pcb = 0,

    /// <summary>
    /// Ăng-ten ngoài gắn qua đầu nối SMA / IPEX (External Antenna).
    /// </summary>
    External = 1
}

/// <summary>
/// Trạng thái liên kết vô tuyến của module Zigbee do Port báo qua thanh ghi +32 (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public enum ZigbeeLinkState : ushort
{
    /// <summary>
    /// Phân hệ Zigbee bị tắt (Disabled).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Đang quét và gia nhập mạng Mesh (Joining / Scanning).
    /// </summary>
    Joining = 1,

    /// <summary>
    /// Đã kết nối mạng thành công và sẵn sàng truyền thông (Up / Connected).
    /// </summary>
    Up = 2,

    /// <summary>
    /// Lỗi liên kết vô tuyến hoặc module không phản hồi (Error / Fault).
    /// </summary>
    Error = 3
}

/// <summary>
/// Danh mục mã lỗi chuẩn hóa của phân hệ Zigbee tại thanh ghi +33 (SPLC-SPEC-ZIGBEE-006).
/// </summary>
public enum ZigbeeErrorCode : ushort
{
    /// <summary>
    /// Không có lỗi.
    /// </summary>
    NoError = 0,

    /// <summary>
    /// Module UART không phản hồi lệnh từ MCU (Timeout).
    /// </summary>
    ErrModuleTimeout = 1,

    /// <summary>
    /// Kênh vô tuyến (Channel) hoặc PAN ID ngoài dải hỗ trợ của phần cứng.
    /// </summary>
    ErrInvalidRfParams = 2,

    /// <summary>
    /// Trùng Slave ID bị cấm (236 hoặc 237 trùng mã lệnh nội bộ của module DRF).
    /// </summary>
    ErrRestrictedSlaveId = 3,

    /// <summary>
    /// Hết thời gian chờ gia nhập mạng Mesh (Join Network Failed / No Coordinator).
    /// </summary>
    ErrJoinNetworkFailed = 4,

    /// <summary>
    /// Lỗi áp dụng khóa bảo mật AES-128.
    /// </summary>
    ErrSecurityKeyRejected = 5,

    /// <summary>
    /// Lỗi phần cứng vô tuyến hoặc cấu hình ăng-ten.
    /// </summary>
    ErrHardwareFault = 6
}
