namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO cấu hình một chân ngõ ra động của khối MODBUS READ.
/// Ánh xạ từ dữ liệu thanh ghi giải mã sang Tag của PLC.
/// </summary>
public sealed class ModbusMasterPinBindingDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ushort RegOffset { get; set; } = 0;
    public ModbusDataType DataType { get; set; } = ModbusDataType.Int16;
    public ModbusEndianness Endianness { get; set; } = ModbusEndianness.BigEndian_ABCD;
    public float Multiplier { get; set; } = 1.0f;
    public ushort? TargetTagIndex { get; set; }
}
