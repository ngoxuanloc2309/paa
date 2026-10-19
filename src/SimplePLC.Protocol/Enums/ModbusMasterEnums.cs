namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Mã hàm đọc Modbus Master RTU chuẩn công nghiệp.
/// </summary>
public enum ModbusMasterFunctionCode : byte
{
    ReadCoils = 1,              // FC01: Đọc trạng thái Coil (0x)
    ReadDiscreteInputs = 2,     // FC02: Đọc trạng thái tiếp điểm ngõ vào số (1x)
    ReadHoldingRegisters = 3,   // FC03: Đọc thanh ghi lưu trữ Holding Registers (4x)
    ReadInputRegisters = 4      // FC04: Đọc thanh ghi ngõ vào Input Registers (3x)
}

/// <summary>
/// Kiểu dữ liệu giải mã từ thanh ghi Modbus.
/// </summary>
public enum ModbusDataType : ushort
{
    Bool = 1,       // 1-bit hoặc 1 register (0/1)
    Int16 = 2,      // Có dấu 16-bit (-32768 .. 32767) - 1 register
    UInt16 = 3,     // Không dấu 16-bit (0 .. 65535) - 1 register
    Int32 = 4,      // Có dấu 32-bit - 2 registers
    UInt32 = 5,     // Không dấu 32-bit - 2 registers
    Real = 6        // Số thực 32-bit IEEE 754 Float - 2 registers
}

/// <summary>
/// Thứ tự Byte và Word (Endianness) khi đọc dữ liệu 32-bit từ thanh ghi Modbus.
/// </summary>
public enum ModbusEndianness : ushort
{
    BigEndian_ABCD = 0, // Big-Endian chuẩn (High word first, High byte first)
    MidBig_CDAB = 1,    // Word-Swap (Low word first, High byte first) - Phổ biến nhất trong cảm biến công nghiệp
    ByteSwap_BADC = 2,  // Byte-Swap (High word first, Low byte first)
    LittleEndian_DCBA = 3 // Little-Endian thuần (Low word first, Low byte first)
}

/// <summary>
/// Chế độ hoạt động của khối ghi Modbus Master.
/// </summary>
public enum ModbusWriteMode : byte
{
    DirectRegisters = 0,    // Ghi trực tiếp theo địa chỉ thanh ghi cố định
    ChannelIndexed = 1      // Ghi theo chỉ số kênh mục tiêu (Sort-to-Light, Cụm van, Tháp đèn)
}

/// <summary>
/// Mã hàm ghi Modbus Master RTU chuẩn công nghiệp.
/// </summary>
public enum ModbusMasterWriteFunctionCode : byte
{
    WriteSingleCoil = 5,            // FC05: Ghi 1 Coil đơn (0x)
    WriteSingleRegister = 6,        // FC06: Ghi 1 Holding Register đơn (4x)
    WriteMultipleCoils = 15,        // FC15 (0x0F): Ghi nhiều Coils (0x)
    WriteMultipleRegisters = 16     // FC16 (0x10): Ghi nhiều Holding Registers (4x)
}

/// <summary>
/// Chế độ kích hoạt phát lệnh ghi Modbus Master.
/// </summary>
public enum ModbusWriteTriggerMode : byte
{
    OnTriggerRisingEdge = 0,    // Kích hoạt khi chân Trig chuyển từ 0 sang 1
    OnDataChange = 1,           // Tự động phát khi bất kỳ dữ liệu ngõ vào nào thay đổi
    Cyclic = 2                  // Ghi tuần hoàn định kỳ theo thời gian cài đặt
}

