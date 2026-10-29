namespace SimplePLC.Studio.ViewModels;

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Services;

/// <summary>
/// ViewModel hiển thị từng dòng thanh ghi trong bản đồ địa chỉ Modbus Slave cố định của thiết bị.
/// Hỗ trợ song ngữ Anh - Việt tự động cập nhật khi đổi ngôn ngữ.
/// </summary>
public sealed partial class ModbusSlaveFixedRegisterViewModel : ObservableObject
{
    private readonly ModbusSlaveFixedRegisterDto _dto;

    /// <summary>
    /// Khởi tạo ViewModel từ DTO bản đồ cố định.
    /// </summary>
    /// <param name="dto">Dữ liệu DTO thanh ghi Modbus Slave cố định.</param>
    public ModbusSlaveFixedRegisterViewModel(ModbusSlaveFixedRegisterDto dto)
    {
        _dto = dto ?? throw new ArgumentNullException(nameof(dto));
    }

    /// <summary>
    /// Phân nhóm bộ nhớ (Holding Registers, Discrete Inputs, Coils).
    /// </summary>
    public string Category => _dto.Category;

    /// <summary>
    /// Địa chỉ Hex (0-based) hiển thị trong tài liệu kỹ thuật.
    /// </summary>
    public string AddressHex => _dto.AddressHex;

    /// <summary>
    /// Địa chỉ Modbus 1-based (4x, 1x, 0x).
    /// </summary>
    public string ModbusAddress => _dto.ModbusAddress;

    /// <summary>
    /// Tag hoặc nhóm biến của thiết bị.
    /// </summary>
    public string DeviceTag => _dto.DeviceTag;

    /// <summary>
    /// Kiểu dữ liệu (UINT16, UINT32, FLOAT32, INT32, BOOL).
    /// </summary>
    public string DataType => _dto.DataType;

    /// <summary>
    /// Quyền truy cập (RO = Read-Only, RW = Read/Write).
    /// </summary>
    public string AccessRight => _dto.AccessRight;

    /// <summary>
    /// Mã hàm Modbus hỗ trợ (FC 01, 02, 03, 05, 06, 15, 16).
    /// </summary>
    public string FunctionCode => _dto.FunctionCode;

    /// <summary>
    /// Cờ xác định thanh ghi chỉ đọc (Read-Only).
    /// </summary>
    public bool IsReadOnly => string.Equals(_dto.AccessRight, "RO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mô tả công năng tự động bản địa hóa theo ngôn ngữ đang chọn.
    /// </summary>
    public string Description => LocalizationService.Instance.IsVietnamese 
        ? _dto.DescriptionVi 
        : _dto.DescriptionEn;

    /// <summary>
    /// Chuỗi tooltip hiển thị chi tiết địa chỉ gói tin thực tế trên đường truyền và mã hàm hỗ trợ.
    /// </summary>
    public string ProtocolWireTooltip =>
        $"{LocalizationService.Instance["ModbusSlaveTooltipProtocolOffset"]} {AddressHex} (Hex)\n" +
        $"{LocalizationService.Instance["ModbusSlaveTooltipFunctionCode"]} {FunctionCode}";

    /// <summary>
    /// Thông báo giao diện cập nhật lại mô tả và tooltip khi chuyển đổi ngôn ngữ.
    /// </summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ProtocolWireTooltip));
    }
}
