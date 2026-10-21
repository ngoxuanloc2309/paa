namespace SimplePLC.Protocol.Dto;

using System.Collections.Generic;
using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO đóng gói toàn bộ thông số cấu hình một block MODBUS WRITE (Modbus RTU Master Writer / Actuator).
/// Hỗ trợ cả 2 chế độ: DirectRegisters và ChannelIndexed (độc quyền kênh).
/// </summary>
public sealed class ModbusMasterWriteConfigDto
{
    public string NodeId { get; set; } = string.Empty;
    public int BlockIndex { get; set; } = 0;
    public byte PortIndex { get; set; } = 0;
    public bool Enabled { get; set; } = true;
    public byte SlaveId { get; set; } = 1;
    public ModbusWriteMode WriteMode { get; set; } = ModbusWriteMode.ChannelIndexed;
    public ModbusMasterWriteFunctionCode FunctionCode { get; set; } = ModbusMasterWriteFunctionCode.WriteSingleRegister;

    // Direct Mode settings
    public ushort StartAddress { get; set; } = 0;
    public ushort PointCount { get; set; } = 1;

    // Channel-Indexed Mode settings
    public ushort BaseAddressCommand { get; set; } = 0x0100;
    public ushort BaseAddressParam { get; set; } = 0x0120;
    public ushort ChannelCount { get; set; } = 9;
    public bool IsExclusiveChannel { get; set; } = true;

    // Trigger settings
    public ModbusWriteTriggerMode TriggerMode { get; set; } = ModbusWriteTriggerMode.OnTriggerRisingEdge;
    public uint CyclicIntervalMs { get; set; } = 500;
    public uint TimeoutMs { get; set; } = 300;

    /// <summary>
    /// Ngưỡng COV Deadband cho chế độ OnDataChange. Ghi Modbus chỉ kích hoạt khi giá trị thay đổi ≥ Deadband.
    /// Ngăn bão hòa bus RS485 do nhiễu ADC. Mặc định 0.5 đơn vị.
    /// </summary>
    public float Deadband { get; set; } = 0.5f;

    // Các chân trạng thái điều khiển
    public ushort? TrigTagIndex { get; set; }
    public ushort? DoneTagIndex { get; set; }
    public ushort? BusyTagIndex { get; set; }
    public ushort? ErrorTagIndex { get; set; }
    public ushort? ErrCodeTagIndex { get; set; }

    // Channel-Indexed pins (Tag indexes if bound to catalog)
    public ushort? ChannelTagIndex { get; set; }
    public ushort? CommandTagIndex { get; set; }
    public ushort? ParamTagIndex { get; set; }

    // Danh sách các chân ngõ vào dữ liệu động (cho Direct Mode)
    public List<ModbusMasterPinBindingDto> Pins { get; set; } = new();
}
