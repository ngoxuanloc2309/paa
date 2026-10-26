namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO đại diện cho một mục ánh xạ trong Bảng Ánh Xạ Modbus Slave (0x1500 - 0x157F).
/// Cho phép Master ngoài (SCADA/HMI) đọc hoặc can thiệp ghi vào Tag nội bộ của PLC.
/// </summary>
public sealed class ModbusSlaveMapEntryDto
{
    /// <summary>
    /// Địa chỉ Modbus mà Master ngoài sẽ truy vấn (0..65535).
    /// </summary>
    public ushort ModbusAddress { get; set; }

    /// <summary>
    /// Chỉ số Tag nội bộ trong Tag Store của PLC (0..127).
    /// </summary>
    public ushort TargetTagIndex { get; set; }

    /// <summary>
    /// Quyền hạn truy cập: ReadOnly hoặc ReadWrite.
    /// </summary>
    public ModbusSlaveAccessRight AccessRight { get; set; } = ModbusSlaveAccessRight.ReadOnly;

    /// <summary>
    /// Kiểu dữ liệu ánh xạ: 1=BOOL, 2=INT32, 3=REAL, 4=UINT16, 5=INT16.
    /// </summary>
    public byte DataType { get; set; } = 2;

    /// <summary>
    /// Mặt nạ cổng áp dụng (Bit 0 = Port 1, Bit 1 = Port 2...). Mặc định 0x0F (áp dụng cho tất cả cổng Slave).
    /// </summary>
    public byte PortMask { get; set; } = 0x0F;

    /// <summary>
    /// Nhãn mô tả gợi nhớ cho kỹ sư SCADA/HMI (ví dụ: "V_AVG_SENSOR", "TARGET_PRESSURE").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public ModbusSlaveMapEntryDto Clone()
    {
        return new ModbusSlaveMapEntryDto
        {
            ModbusAddress = this.ModbusAddress,
            TargetTagIndex = this.TargetTagIndex,
            AccessRight = this.AccessRight,
            DataType = this.DataType,
            PortMask = this.PortMask,
            Description = this.Description
        };
    }
}
