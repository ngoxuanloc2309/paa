namespace SimplePLC.Domain.Enums;

/// <summary>
/// Quyền truy cập các thanh ghi Modbus Slave tùy biến.
/// </summary>
public enum ModbusSlaveAccessRight : byte
{
    ReadOnly  = 0,
    ReadWrite = 1
}
