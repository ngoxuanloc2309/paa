namespace SimplePLC.Protocol.Dto;

using System.Collections.Generic;
using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO đóng gói toàn bộ thông số cấu hình một block MODBUS READ (Modbus RTU Master Poller).
/// Lưu trữ cùng file dự án (.synx) và dùng để nạp xuống MCU / Simulator.
/// </summary>
public sealed class ModbusMasterReadConfigDto
{
    public string NodeId { get; set; } = string.Empty;
    public int BlockIndex { get; set; } = 0;
    public byte PortIndex { get; set; } = 0;
    public bool Enabled { get; set; } = true;
    public byte SlaveId { get; set; } = 1;
    public ModbusMasterFunctionCode FunctionCode { get; set; } = ModbusMasterFunctionCode.ReadHoldingRegisters;
    public ushort StartAddress { get; set; } = 0;
    public ushort RegCount { get; set; } = 2;
    public uint PollIntervalMs { get; set; } = 500;
    public uint TimeoutMs { get; set; } = 300;

    // Chế độ đọc: Quét tuần hoàn (Cyclic) hoặc Kích hoạt theo sự kiện (OnTriggerRisingEdge)
    public ModbusReadMode ReadMode { get; set; } = ModbusReadMode.Cyclic;

    // Các chân trạng thái điều khiển
    public ushort? EnTagIndex { get; set; }
    public ushort? TrigTagIndex { get; set; }
    public ushort? ValidTagIndex { get; set; }
    public ushort? ErrorTagIndex { get; set; }
    public ushort? ErrCodeTagIndex { get; set; }

    // Danh sách các chân ngõ ra dữ liệu động (tối đa 4 chân mỗi block)
    public List<ModbusMasterPinBindingDto> Pins { get; set; } = new();
}
