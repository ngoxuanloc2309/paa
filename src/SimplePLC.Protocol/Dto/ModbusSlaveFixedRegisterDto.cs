namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO mô tả thông tin thanh ghi cố định trong bản đồ địa chỉ Modbus Slave của thiết bị SynaptiX Controller.
/// </summary>
/// <param name="Category">Nhóm bộ nhớ (Holding Registers, Discrete Inputs, Coils).</param>
/// <param name="AddressHex">Địa chỉ Hex (0-based) hiển thị trên tài liệu kỹ thuật.</param>
/// <param name="ModbusAddress">Địa chỉ Modbus chuẩn công nghiệp 1-based (4x, 1x, 0x).</param>
/// <param name="DeviceTag">Tên biến hoặc Tag thiết bị tương ứng.</param>
/// <param name="DataType">Kiểu dữ liệu (UINT16, UINT32, FLOAT32, INT32, BOOL).</param>
/// <param name="AccessRight">Quyền truy cập (RO = Read-Only, RW = Read/Write).</param>
/// <param name="FunctionCode">Mã hàm Modbus hỗ trợ (FC 01, FC 02, FC 03, FC 05, FC 06, FC 15, FC 16).</param>
/// <param name="DescriptionVi">Mô tả công năng chi tiết bằng tiếng Việt.</param>
/// <param name="DescriptionEn">Detailed functional description in English.</param>
public sealed record ModbusSlaveFixedRegisterDto(
    string Category,
    string AddressHex,
    string ModbusAddress,
    string DeviceTag,
    string DataType,
    string AccessRight,
    string FunctionCode,
    string DescriptionVi,
    string DescriptionEn
);
