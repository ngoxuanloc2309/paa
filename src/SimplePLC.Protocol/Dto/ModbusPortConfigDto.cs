namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO mang thông số cấu hình vật lý của một cổng nối tiếp RS-485 Modbus Master.
/// Quy hoạch tại phân vùng MCU 0x1400..0x143F (4 cổng x 16 thanh ghi = 64 thanh ghi).
/// </summary>
public sealed class ModbusPortConfigDto
{
    /// <summary>
    /// Chỉ số cổng (0 = Cổng 1, 1 = Cổng 2, 2 = Cổng 3, 3 = Cổng 4).
    /// </summary>
    public byte PortIndex { get; set; } = 0;

    /// <summary>
    /// Tên đại diện hiển thị cho cổng (ví dụ: "RS-485 #1", "RS-485 #2"). Tối đa 12 ký tự ASCII.
    /// </summary>
    public string PortName { get; set; } = "RS-485 #1";

    /// <summary>
    /// Cho phép cổng hoạt động. Nếu tắt, MCU sẽ không cấp phát driver UART cho cổng này.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Tốc độ truyền baud rate (ví dụ: 9600, 19200, 38400, 57600, 115200).
    /// </summary>
    public uint BaudRate { get; set; } = 9600;

    /// <summary>
    /// Số bit dữ liệu (Data bits): thường là 8 (hoặc 7 khi dùng ASCII).
    /// </summary>
    public byte DataBits { get; set; } = 8;

    /// <summary>
    /// Chế độ Parity (0 = None, 1 = Even, 2 = Odd).
    /// </summary>
    public ModbusParity Parity { get; set; } = ModbusParity.None;

    /// <summary>
    /// Số bit dừng (Stop bits): 1 hoặc 2.
    /// </summary>
    public byte StopBits { get; set; } = 1;

    /// <summary>
    /// Thời gian chờ phản hồi tối đa từ Slave (ms). Mặc định 200ms.
    /// </summary>
    public ushort ResponseTimeoutMs { get; set; } = 200;

    /// <summary>
    /// Thời gian trễ giữa các khung truyền liên tiếp (Inter-frame delay, ms) để giải phóng bus RS-485. Mặc định 20ms.
    /// </summary>
    public ushort InterFrameDelayMs { get; set; } = 20;

    /// <summary>
    /// Số lần thử lại tối đa khi giao dịch bị timeout trước khi báo lỗi (0..5). Mặc định 1.
    /// </summary>
    public ushort RetryCount { get; set; } = 1;

    /// <summary>
    /// Tạo bản sao độc lập (Deep Clone).
    /// </summary>
    public ModbusPortConfigDto Clone()
    {
        return new ModbusPortConfigDto
        {
            PortIndex = this.PortIndex,
            PortName = this.PortName,
            Enabled = this.Enabled,
            BaudRate = this.BaudRate,
            DataBits = this.DataBits,
            Parity = this.Parity,
            StopBits = this.StopBits,
            ResponseTimeoutMs = this.ResponseTimeoutMs,
            InterFrameDelayMs = this.InterFrameDelayMs,
            RetryCount = this.RetryCount
        };
    }
}
