namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Quyền truy cập của Master bên ngoài (SCADA / HMI) đối với thanh ghi Modbus Slave.
/// ReadOnly: Chỉ cho phép đọc (FC03/FC04), từ chối khi ghi với Exception 0x03.
/// ReadWrite: Cho phép đọc (FC03/FC04) và can thiệp ghi (FC06/FC16).
/// </summary>
public enum ModbusSlaveAccessRight : byte
{
    ReadOnly = 0,
    ReadWrite = 1
}
