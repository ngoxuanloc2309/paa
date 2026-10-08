namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Cấu hình cấy lỗi tập trung (Fault Injection Profile) cho MCU Reference Simulator.
/// Cho phép các kịch bản kiểm thử độc lập mà không làm ô nhiễm API chính của simulator.
/// </summary>
public sealed class SimulatorFaultProfile
{
    /// <summary>
    /// Gây sai lệch CRC có chủ đích khi commit để kiểm thử nhánh lỗi CRC_MISMATCH.
    /// </summary>
    public bool CorruptCommitCrc { get; set; }

    /// <summary>
    /// Gây timeout có chủ đích tại lượt ghi (transaction index) thứ N.
    /// Ví dụ: bằng 7 thì transaction ghi thứ 7 sẽ ném TimeoutException.
    /// </summary>
    public int? TimeoutOnWriteNumber { get; set; }

    /// <summary>
    /// Gây timeout khi có thao tác đọc hoặc ghi tại địa chỉ cụ thể.
    /// </summary>
    public ushort? TimeoutOnAddress { get; set; }

    /// <summary>
    /// Đưa MCU vào trạng thái BUSY: từ chối các tác vụ cấu hình với mã lỗi BUSY.
    /// </summary>
    public bool DeviceBusy { get; set; }

    /// <summary>
    /// Giả lập phần cứng trả về Descriptor không tương thích (RuleFormatVersion = 0x0200).
    /// </summary>
    public bool UnsupportedDescriptor { get; set; }

    /// <summary>
    /// Ghi đè DeviceDescriptor để kiểm thử các biến thể hoặc phiên bản giao thức khác nhau.
    /// </summary>
    public SimplePLC.Protocol.Dto.DeviceDescriptorDto? OverrideDescriptor { get; set; }

    /// <summary>
    /// Ghi đè DeviceClass để kiểm thử từ chối tương thích (ví dụ 0xEEEE).
    /// </summary>
    public ushort? OverrideDeviceClass { get; set; }

    /// <summary>
    /// Ghi đè DeviceVariant để kiểm thử (ví dụ biến thể lạ).
    /// </summary>
    public ushort? OverrideDeviceVariant { get; set; }

    /// <summary>
    /// Ghi đè DeviceResourceInfo để kiểm thử các biến thể hoặc vi phạm Wire Profile V1.
    /// </summary>
    public SimplePLC.Protocol.Dto.DeviceResourceInfoDto? OverrideResourceInfo { get; set; }

    /// <summary>
    /// Giả lập cáp USB bị rút đột ngột: ném IOException khi truy cập Modbus.
    /// </summary>
    public bool CableDisconnected { get; set; }

    /// <summary>
    /// Đặt lại toàn bộ cờ lỗi về trạng thái bình thường.
    /// </summary>
    public void Reset()
    {
        CorruptCommitCrc = false;
        TimeoutOnWriteNumber = null;
        TimeoutOnAddress = null;
        DeviceBusy = false;
        UnsupportedDescriptor = false;
        OverrideDescriptor = null;
        OverrideDeviceClass = null;
        OverrideDeviceVariant = null;
        OverrideResourceInfo = null;
        CableDisconnected = false;
    }
}
