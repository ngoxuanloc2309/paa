namespace SimplePLC.Studio.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Đại diện cho một mục thanh ghi cảm biến cần đọc trong Trình cấu hình nhanh DTU Gateway (Quick Setup Wizard).
/// </summary>
public partial class DtuFastRegisterItem : ObservableObject
{
    [ObservableProperty]
    private byte _slaveId = 1;

    [ObservableProperty]
    private ushort _startAddress = 40001;

    [ObservableProperty]
    private ModbusMasterFunctionCode _functionCode = ModbusMasterFunctionCode.ReadHoldingRegisters;

    [ObservableProperty]
    private ModbusDataType _dataType = ModbusDataType.Real;

    [ObservableProperty]
    private string _tagName = "NhietDo_C";

    [ObservableProperty]
    private string _description = "Nhiệt độ cảm biến / Sensor temperature";

    /// <summary>
    /// Khởi tạo mục thanh ghi đọc nhanh với giá trị mặc định.
    /// </summary>
    public DtuFastRegisterItem() { }

    /// <summary>
    /// Khởi tạo mục thanh ghi đọc nhanh với các thông số cấu hình cụ thể.
    /// </summary>
    public DtuFastRegisterItem(byte slaveId, ushort address, ModbusDataType dataType, string tagName, string desc = "", ModbusMasterFunctionCode functionCode = ModbusMasterFunctionCode.ReadHoldingRegisters)
    {
        _slaveId = slaveId;
        _startAddress = address;
        _dataType = dataType;
        _tagName = tagName;
        _description = desc;
        _functionCode = functionCode;
    }
}
