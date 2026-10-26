namespace SimplePLC.Domain.Models;

using SimplePLC.Protocol.Enums;

/// <summary>
/// Thực thể Domain đại diện cho một mục ánh xạ thanh ghi Modbus Slave.
/// </summary>
public sealed class ModbusSlaveMapEntry
{
    public ushort ModbusAddress { get; set; }
    public ushort TargetTagIndex { get; set; }
    public ModbusSlaveAccessRight AccessRight { get; set; } = ModbusSlaveAccessRight.ReadOnly;
    public byte DataType { get; set; } = 2; // 1=BOOL, 2=INT32, 3=REAL, 4=UINT16, 5=INT16
    public byte PortMask { get; set; } = 0x0F;
    public string Description { get; set; } = string.Empty;

    public ModbusSlaveMapEntry() { }

    public ModbusSlaveMapEntry(ushort modbusAddress, ushort targetTagIndex, ModbusSlaveAccessRight accessRight, byte dataType = 2, byte portMask = 0x0F, string description = "")
    {
        ModbusAddress = modbusAddress;
        TargetTagIndex = targetTagIndex;
        AccessRight = accessRight;
        DataType = dataType;
        PortMask = portMask;
        Description = description;
    }

    public ModbusSlaveMapEntry Clone()
    {
        return new ModbusSlaveMapEntry
        {
            ModbusAddress = this.ModbusAddress,
            TargetTagIndex = this.TargetTagIndex,
            AccessRight = this.AccessRight,
            DataType = this.DataType,
            PortMask = this.PortMask,
            Description = this.Description
        };
    }
}
