namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Vai trò của cổng nối tiếp RS-485 trên SynaptiX Controller.
/// Master: Chủ động truy vấn thiết bị ngoại vi qua khối MB_READ / MB_WRITE.
/// Slave: Lắng nghe và phản hồi lệnh truy vấn từ SCADA / HMI ngoài.
/// </summary>
public enum ModbusPortRole : byte
{
    Master = 0,
    Slave = 1
}
