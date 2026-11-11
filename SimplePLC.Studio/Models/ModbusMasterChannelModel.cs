using System;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Model cấu hình cho một kênh thu thập dữ liệu (Read) hoặc lệnh điều khiển (Write) trên cổng Modbus Master.
/// Tuân thủ nguyên tắc Single Source of Truth và ánh xạ trực tiếp với khối MB_READ / MB_WRITE.
/// </summary>
public class ModbusMasterChannelModel
{
    /// <summary>
    /// Địa chỉ trạm ngoại vi Modbus Slave (1 đến 247).
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Mã hàm Modbus: FC01-04 (Đọc tuần hoàn), FC05-16 (Ghi khi đổi giá trị).
    /// </summary>
    public byte FunctionCode { get; set; } = 3; // FC03: Read Holding Registers

    /// <summary>
    /// Địa chỉ thanh ghi Modbus (1-based: 40001, 30001, 00001...).
    /// </summary>
    public ushort Address { get; set; } = 40001;

    /// <summary>
    /// Định dạng kiểu dữ liệu nhị phân: INT16, REAL, BOOL, INT32...
    /// </summary>
    public ModbusDataType DataType { get; set; } = ModbusDataType.Real;

    /// <summary>
    /// Thứ tự sắp xếp byte và word (ABCD, CDAB, BADC, DCBA).
    /// </summary>
    public ModbusEndianness Endianness { get; set; } = ModbusEndianness.BigEndian_ABCD;

    /// <summary>
    /// Hệ số nhân tỷ lệ chuyển đổi đơn vị kỹ thuật (ví dụ 0.1 để chia 10).
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Tên biến Tag trong Tag Catalog nhận dữ liệu đọc hoặc cung cấp dữ liệu để ghi.
    /// </summary>
    public string TargetTagName { get; set; } = string.Empty;

    /// <summary>
    /// Mô tả hoặc bí danh ghi chú cho kênh truyền thông này.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tạo bản sao độc lập của đối tượng cấu hình kênh Modbus Master.
    /// </summary>
    /// <returns>Bản sao đối tượng ModbusMasterChannelModel.</returns>
    public ModbusMasterChannelModel Clone()
    {
        return new ModbusMasterChannelModel
        {
            SlaveId = this.SlaveId,
            FunctionCode = this.FunctionCode,
            Address = this.Address,
            DataType = this.DataType,
            Endianness = this.Endianness,
            Scale = this.Scale,
            TargetTagName = this.TargetTagName,
            Description = this.Description
        };
    }
}
