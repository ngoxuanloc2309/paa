namespace SimplePLC.Protocol.Dto;

using System;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Đại diện cho cấu hình và viễn trắc của phân hệ truyền thông không dây Zigbee Modbus (SPLC-SPEC-ZIGBEE-006).
/// Ánh xạ 1:1 với phân vùng Modbus Holding Registers 0x1700 - 0x173F (64 registers = 128 bytes).
/// </summary>
public sealed class ZigbeeConfigDto
{
    // ==========================================
    // CẤU HÌNH GHI (Offset +0 .. +31)
    // ==========================================

    /// <summary>
    /// Offset +0 Bit 0: Cho phép kích hoạt phân hệ Zigbee.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Offset +0 Bit 1: Vai trò Modbus trên sóng Zigbee (0 = Master/Coordinator, 1 = Slave/Router).
    /// </summary>
    public ZigbeeRole Role { get; set; } = ZigbeeRole.Slave;

    /// <summary>
    /// Offset +0 Bit 2: Định dạng khung Modbus (0 = RTU, 1 = ASCII). Mặc định luôn là RTU.
    /// </summary>
    public ZigbeeFraming Framing { get; set; } = ZigbeeFraming.Rtu;

    /// <summary>
    /// Offset +0 Bit 3: Bật/tắt mã hóa bảo mật vô tuyến AES-128.
    /// </summary>
    public bool SecurityEnabled { get; set; } = false;

    /// <summary>
    /// Offset +0 Bit 4: Chọn loại ăng-ten vô tuyến (0 = PCB On-board, 1 = Ngoài SMA).
    /// </summary>
    public ZigbeeAntenna Antenna { get; set; } = ZigbeeAntenna.Pcb;

    /// <summary>
    /// Offset +0 Bit 5: Cờ lệnh giữ nguyên khóa mạng trong Flash (chỉ ghi, đọc lại luôn 0).
    /// </summary>
    public bool PreserveKey { get; set; } = false;

    /// <summary>
    /// Offset +1: Địa chỉ Modbus Slave (1..247) khi Role = Slave. Cấm 236 và 237 trên module DRF.
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Offset +2: Mã định danh mạng vô tuyến PAN ID (0x0001 .. 0xFFFE).
    /// </summary>
    public ushort PanId { get; set; } = 0x1234;

    /// <summary>
    /// Offset +3: Kênh tần số vô tuyến (11 .. 26, dải 2.4 GHz IEEE 802.15.4).
    /// </summary>
    public ushort Channel { get; set; } = 15;

    /// <summary>
    /// Offset +5..+12: Khóa mạng bảo mật AES-128 (16 bytes, big-endian byte order).
    /// Đọc từ thiết bị luôn trả về 0.
    /// </summary>
    public byte[] NetKey { get; set; } = new byte[16];

    // ==========================================
    // VIỄN TRẮC ĐỌC (Offset +32 .. +36)
    // ==========================================

    /// <summary>
    /// Offset +32: Trạng thái liên kết sóng vô tuyến (0=Off, 1=Joining, 2=Up, 3=Error).
    /// </summary>
    public ZigbeeLinkState LinkState { get; set; } = ZigbeeLinkState.Off;

    /// <summary>
    /// Offset +33: Mã lỗi chuẩn hóa do Port báo lên (0 = Không lỗi).
    /// </summary>
    public ZigbeeErrorCode ErrorCode => (ZigbeeErrorCode)RawErrorCode;

    /// <summary>
    /// Offset +33: Giá trị thô mã lỗi từ phần cứng.
    /// </summary>
    public ushort RawErrorCode { get; set; } = 0;

    /// <summary>
    /// Offset +34: Số lượng nút thấy được trong mạng Mesh (chỉ có khi là Coordinator; 0 = không có thông tin).
    /// </summary>
    public ushort NodeCount { get; set; } = 0;

    /// <summary>
    /// Offset +35: Cường độ tín hiệu gần nhất (int16 dBm; 0 = không có thông tin).
    /// </summary>
    public short Rssi { get; set; } = 0;

    /// <summary>
    /// Offset +36 Bit 0: Cho biết thiết bị đã có khóa bảo mật khác 0 đang lưu trong Flash.
    /// </summary>
    public bool IsKeySet { get; set; } = false;

    /// <summary>
    /// Tạo bản sao độc lập của đối tượng cấu hình.
    /// </summary>
    public ZigbeeConfigDto Clone()
    {
        var copy = new ZigbeeConfigDto
        {
            Enabled = this.Enabled,
            Role = this.Role,
            Framing = this.Framing,
            SecurityEnabled = this.SecurityEnabled,
            Antenna = this.Antenna,
            PreserveKey = this.PreserveKey,
            SlaveId = this.SlaveId,
            PanId = this.PanId,
            Channel = this.Channel,
            LinkState = this.LinkState,
            RawErrorCode = this.RawErrorCode,
            NodeCount = this.NodeCount,
            Rssi = this.Rssi,
            IsKeySet = this.IsKeySet
        };

        if (this.NetKey != null)
        {
            copy.NetKey = (byte[])this.NetKey.Clone();
        }

        return copy;
    }
}
