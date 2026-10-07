using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Exceptions;

/// <summary>
/// Ngoại lệ đại diện cho phản hồi lỗi từ thiết bị Modbus (Function Code | 0x80 kèm Exception Code).
/// Giúp phân biệt lỗi nghiệp vụ MCU từ chối với lỗi vật lý/timeout (R7, R10).
/// </summary>
public sealed class ModbusProtocolException : Exception
{
    public byte SlaveId { get; }
    public byte FunctionCode { get; }
    public ModbusExceptionCode ExceptionCode { get; }

    public ModbusProtocolException(byte slaveId, byte functionCode, ModbusExceptionCode exceptionCode)
        : base(FormatMessage(slaveId, functionCode, exceptionCode))
    {
        SlaveId = slaveId;
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }

    public ModbusProtocolException(byte slaveId, byte functionCode, ModbusExceptionCode exceptionCode, string message)
        : base(message)
    {
        SlaveId = slaveId;
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }

    private static string FormatMessage(byte slaveId, byte functionCode, ModbusExceptionCode exceptionCode)
    {
        string desc = exceptionCode switch
        {
            ModbusExceptionCode.IllegalFunction => "Illegal Function (0x01) - Function code not supported by device",
            ModbusExceptionCode.IllegalDataAddress => "Illegal Data Address (0x02) - Register address out of range",
            ModbusExceptionCode.IllegalDataValue => "Illegal Data Value (0x03) - Invalid value or register count",
            ModbusExceptionCode.SlaveDeviceFailure => "Slave Device Failure (0x04) - Unrecoverable error in device",
            ModbusExceptionCode.Acknowledge => "Acknowledge (0x05) - Request accepted and executing",
            ModbusExceptionCode.SlaveDeviceBusy => "Slave Device Busy (0x06) - Device busy, retry later",
            ModbusExceptionCode.NegativeAcknowledge => "Negative Acknowledge (0x07) - Request cannot be performed",
            ModbusExceptionCode.MemoryParityError => "Memory Parity Error (0x08) - Parity error in memory read",
            ModbusExceptionCode.GatewayPathUnavailable => "Gateway Path Unavailable (0x0A) - Gateway path not found",
            ModbusExceptionCode.GatewayTargetDeviceFailedToRespond => "Gateway Target Failed (0x0B) - No response from target",
            _ => $"Unknown Modbus exception code (0x{(byte)exceptionCode:X2})"
        };

        return $"Modbus error from Slave {slaveId} on FC 0x{functionCode:X2}: {desc}.";
    }
}
