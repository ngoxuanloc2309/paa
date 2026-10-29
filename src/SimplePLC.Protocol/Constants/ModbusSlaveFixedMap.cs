namespace SimplePLC.Protocol.Constants;

using System.Collections.Generic;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Định nghĩa bản đồ địa chỉ Modbus Slave cố định của thiết bị SynaptiX Controller.
/// Tuân thủ quy chuẩn quốc tế, hỗ trợ truy xuất trực tiếp không qua bảng ánh xạ trung gian.
/// </summary>
public static class ModbusSlaveFixedMap
{
    // =========================================================================
    // 1. THANH GHI GIỮ (HOLDING REGISTERS - FC 03 / FC 06 / FC 16) - 0-BASED
    // =========================================================================

    /// <summary>
    /// Địa chỉ thanh ghi gom 16 ngõ vào số DI0..DI15 (16-bit bitmask, Read-Only).
    /// Địa chỉ Modbus 1-based: 40001.
    /// </summary>
    public const ushort DiWordAddress = 0x0000;

    /// <summary>
    /// Địa chỉ thanh ghi gom 16 ngõ ra Relay DO0..DO15 (16-bit bitmask, Read/Write).
    /// Địa chỉ Modbus 1-based: 40002.
    /// </summary>
    public const ushort DoWordAddress = 0x0001;

    /// <summary>
    /// Địa chỉ bắt đầu gom 32 cờ logic VFLAG0..VFLAG31 (32-bit bitmask, 2 thanh ghi, Read/Write).
    /// Địa chỉ Modbus 1-based: 40003 .. 40004.
    /// </summary>
    public const ushort VflagDWordAddress = 0x0002;
    public const ushort VflagDWordRegisterCount = 2;

    /// <summary>
    /// Địa chỉ bắt đầu của các ngõ vào tương tự AI0..AI3 (Float32 IEEE-754, 2 thanh ghi/kênh, Read-Only).
    /// Địa chỉ Modbus 1-based: 40017 .. 40024.
    /// </summary>
    public const ushort AiBaseAddress = 0x0010;
    public const ushort AiChannelCount = 4;
    public const ushort AiRegistersPerChannel = 2;
    public const ushort AiTotalRegisters = AiChannelCount * AiRegistersPerChannel; // 8 registers (0x0010..0x0017)

    /// <summary>
    /// Địa chỉ bắt đầu của 32 biến tính toán nội vi VREG0..VREG31 (32-bit Int32/Float32, 2 thanh ghi/biến, Read/Write).
    /// Địa chỉ Modbus 1-based: 40033 .. 40096.
    /// </summary>
    public const ushort VregBaseAddress = 0x0020;
    public const ushort VregVariableCount = 32;
    public const ushort VregRegistersPerVariable = 2;
    public const ushort VregTotalRegisters = VregVariableCount * VregRegistersPerVariable; // 64 registers (0x0020..0x005F)

    /// <summary>
    /// Địa chỉ bắt đầu của 32 biến lưu giữ Retentive VREG_R0..VREG_R31 (32-bit Int32/Float32, 2 thanh ghi/biến, Read/Write).
    /// Không mất dữ liệu khi cúp điện.
    /// Địa chỉ Modbus 1-based: 40097 .. 40160.
    /// </summary>
    public const ushort VregRetainBaseAddress = 0x0060;
    public const ushort VregRetainVariableCount = 32;
    public const ushort VregRetainRegistersPerVariable = 2;
    public const ushort VregRetainTotalRegisters = VregRetainVariableCount * VregRetainRegistersPerVariable; // 64 registers (0x0060..0x009F)

    /// <summary>
    /// Địa chỉ bắt đầu của 8 bộ đếm COUNTER0..COUNTER7 (Int32 PV/CV, 2 thanh ghi/bộ, Read/Write).
    /// Địa chỉ Modbus 1-based: 40161 .. 40176.
    /// </summary>
    public const ushort CounterBaseAddress = 0x00A0;
    public const ushort CounterUnitCount = 8;
    public const ushort CounterRegistersPerUnit = 2;
    public const ushort CounterTotalRegisters = CounterUnitCount * CounterRegistersPerUnit; // 16 registers (0x00A0..0x00AF)

    /// <summary>
    /// Địa chỉ bắt đầu vùng chẩn đoán thiết bị (Uptime, Scan time ms, RTC, Mã lỗi, Read-Only).
    /// Địa chỉ Modbus 1-based: 40241 .. 40256.
    /// </summary>
    public const ushort SystemDiagBaseAddress = 0x00F0;
    public const ushort SystemDiagTotalRegisters = 16; // 0x00F0..0x00FF

    // =========================================================================
    // 2. TRUY XUẤT BIT (COILS & DISCRETE INPUTS) - 0-BASED
    // =========================================================================

    /// <summary>
    /// Địa chỉ bắt đầu đọc trạng thái chân ngõ vào số DI0..DI15 (FC 02, Read-Only).
    /// Địa chỉ Modbus 1-based: 10001 .. 10016.
    /// </summary>
    public const ushort DiscreteInputsDiBaseAddress = 0x0000;
    public const ushort DiscreteInputsDiCount = 16;

    /// <summary>
    /// Địa chỉ bắt đầu điều khiển ngõ ra Relay DO0..DO15 (FC 01 / FC 05 / FC 15, Read/Write).
    /// Địa chỉ Modbus 1-based: 00001 .. 00016.
    /// </summary>
    public const ushort CoilsDoBaseAddress = 0x0000;
    public const ushort CoilsDoCount = 16;

    /// <summary>
    /// Địa chỉ bắt đầu điều khiển cờ logic nội vi VFLAG0..VFLAG31 (FC 01 / FC 05 / FC 15, Read/Write).
    /// Địa chỉ Modbus 1-based: 00101 .. 00132.
    /// </summary>
    public const ushort CoilsVflagBaseAddress = 0x0064; // 100 (0-based) -> 101 (1-based)
    public const ushort CoilsVflagCount = 32;

    // =========================================================================
    // 3. DANH SÁCH MÔ TẢ ĐỊNH NGHĨA THAM CHIẾU TĨNH (STATIC REFERENCE TABLE)
    // =========================================================================

    /// <summary>
    /// Danh sách bảng tra cứu cố định phục vụ hiển thị UI và tài liệu kỹ thuật datasheet.
    /// </summary>
    public static readonly IReadOnlyList<ModbusSlaveFixedRegisterDto> DefaultEntries = new List<ModbusSlaveFixedRegisterDto>
    {
        // Holding Registers
        new(
            Category: "Holding Registers",
            AddressHex: "0x0000",
            ModbusAddress: "40001",
            DeviceTag: "DI_WORD",
            DataType: "UINT16",
            AccessRight: "RO",
            FunctionCode: "FC 03",
            DescriptionVi: "Gom 16 ngõ vào số DI0..DI15 (Bitmask 16-bit)",
            DescriptionEn: "Packed Digital Inputs DI0..DI15 (16-bit bitmask)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x0001",
            ModbusAddress: "40002",
            DeviceTag: "DO_WORD",
            DataType: "UINT16",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "Gom 16 ngõ ra Relay DO0..DO15 (Bitmask 16-bit)",
            DescriptionEn: "Packed Digital Outputs DO0..DO15 (16-bit bitmask)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x0002 .. 0x0003",
            ModbusAddress: "40003 .. 40004",
            DeviceTag: "VFLAG_DWORD",
            DataType: "UINT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "Gom 32 cờ logic VFLAG0..VFLAG31 (Bitmask 32-bit)",
            DescriptionEn: "Packed Virtual Flags VFLAG0..VFLAG31 (32-bit bitmask)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x0010 .. 0x0017",
            ModbusAddress: "40017 .. 40024",
            DeviceTag: "AI0 .. AI3",
            DataType: "FLOAT32",
            AccessRight: "RO",
            FunctionCode: "FC 03",
            DescriptionVi: "4 ngõ vào tương tự (2 thanh ghi/kênh, High Word First, IEEE-754)",
            DescriptionEn: "4 Analog Inputs (2 regs/ch, High Word First, IEEE-754)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x0020 .. 0x005F",
            ModbusAddress: "40033 .. 40096",
            DeviceTag: "VREG0 .. VREG31",
            DataType: "INT32 / FLOAT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "32 biến tính toán nội vi (Offset: 40033 + Index * 2, High Word First)",
            DescriptionEn: "32 Virtual Registers (Offset: 40033 + Index * 2, High Word First)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x0060 .. 0x009F",
            ModbusAddress: "40097 .. 40160",
            DeviceTag: "VREG_R0 .. VREG_R31",
            DataType: "INT32 / FLOAT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "32 biến lưu giữ Retentive không mất khi cúp điện (Offset: 40097 + Index * 2)",
            DescriptionEn: "32 Retentive Non-Volatile Registers, power-off safe (Offset: 40097 + Index * 2)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x00A0 .. 0x00AF",
            ModbusAddress: "40161 .. 40176",
            DeviceTag: "COUNTER0 .. COUNTER7",
            DataType: "INT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "8 bộ đếm sự kiện / xung (Offset: 40161 + Index * 2, High Word First)",
            DescriptionEn: "8 Event/Pulse Counters (Offset: 40161 + Index * 2, High Word First)"
        ),
        new(
            Category: "Holding Registers",
            AddressHex: "0x00F0 .. 0x00FF",
            ModbusAddress: "40241 .. 40256",
            DeviceTag: "SYS_DIAG",
            DataType: "UINT16",
            AccessRight: "RO",
            FunctionCode: "FC 03",
            DescriptionVi: "Chẩn đoán hệ thống: Uptime (40241), Scan ms (40243), Status (40244), RTC (40245)",
            DescriptionEn: "System Diagnostics: Uptime (40241), Scan ms (40243), Status (40244), RTC (40245)"
        ),

        // Bit Zones
        new(
            Category: "Discrete Inputs",
            AddressHex: "0x0000 .. 0x000F",
            ModbusAddress: "10001 .. 10016",
            DeviceTag: "DI0 .. DI15",
            DataType: "BOOL",
            AccessRight: "RO",
            FunctionCode: "FC 02",
            DescriptionVi: "Trạng thái các chân ngõ vào số thực tế",
            DescriptionEn: "Physical Digital Inputs Real-Time Status"
        ),
        new(
            Category: "Coils",
            AddressHex: "0x0000 .. 0x000F",
            ModbusAddress: "00001 .. 00016",
            DeviceTag: "DO0 .. DO15",
            DataType: "BOOL",
            AccessRight: "RW",
            FunctionCode: "FC 01, 05, 15",
            DescriptionVi: "Điều khiển đóng/ngắt các Relay ngõ ra vật lý",
            DescriptionEn: "Physical Relay Outputs Control (Energize/De-energize)"
        ),
        new(
            Category: "Coils",
            AddressHex: "0x0064 .. 0x0083",
            ModbusAddress: "00101 .. 00132",
            DeviceTag: "VFLAG0 .. VFLAG31",
            DataType: "BOOL",
            AccessRight: "RW",
            FunctionCode: "FC 01, 05, 15",
            DescriptionVi: "Cờ logic trung gian nội vi của thiết bị",
            DescriptionEn: "Internal Logic Flags for Sequence and State Control"
        )
    };
}
