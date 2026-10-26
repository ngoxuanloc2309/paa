using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public sealed class ModbusSlaveMapCodecTests
{
    [Fact]
    public void EncodeAndDecode_Roundtrip_PreservesDataIntegrity()
    {
        var entries = new List<ModbusSlaveMapEntryDto>
        {
            new ModbusSlaveMapEntryDto
            {
                ModbusAddress = 0,
                TargetTagIndex = 40,
                AccessRight = ModbusSlaveAccessRight.ReadOnly,
                DataType = 3, // REAL
                PortMask = 0x01,
                Description = "V_AVG"
            },
            new ModbusSlaveMapEntryDto
            {
                ModbusAddress = 2,
                TargetTagIndex = 72,
                AccessRight = ModbusSlaveAccessRight.ReadWrite,
                DataType = 3, // REAL
                PortMask = 0x03,
                Description = "TARGET_PRESSURE"
            },
            new ModbusSlaveMapEntryDto
            {
                ModbusAddress = 4,
                TargetTagIndex = 20,
                AccessRight = ModbusSlaveAccessRight.ReadWrite,
                DataType = 1, // BOOL
                PortMask = 0x0F,
                Description = "CMD_START"
            }
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusSlaveMapTableTotalLength];
        ModbusSlaveMapCodec.EncodeEntries(entries, buffer);

        // Assert Entry 0:
        Assert.Equal(0, buffer[0]);  // ModbusAddress
        Assert.Equal(40, buffer[1]); // TagIndex
        // Flags: Active (0x0100) | DataType 3 (0x0006) | PortMask 0x01 (0x0010) | RO (0) = 0x0116
        Assert.Equal(0x0116, buffer[2]);
        Assert.Equal(0, buffer[3]);

        // Decode back
        var decoded = ModbusSlaveMapCodec.DecodeEntries(buffer);
        Assert.Equal(3, decoded.Count);

        Assert.Equal(0, decoded[0].ModbusAddress);
        Assert.Equal(40, decoded[0].TargetTagIndex);
        Assert.Equal(ModbusSlaveAccessRight.ReadOnly, decoded[0].AccessRight);
        Assert.Equal(3, decoded[0].DataType);
        Assert.Equal(0x01, decoded[0].PortMask);

        Assert.Equal(2, decoded[1].ModbusAddress);
        Assert.Equal(72, decoded[1].TargetTagIndex);
        Assert.Equal(ModbusSlaveAccessRight.ReadWrite, decoded[1].AccessRight);
        Assert.Equal(3, decoded[1].DataType);
        Assert.Equal(0x03, decoded[1].PortMask);

        Assert.Equal(4, decoded[2].ModbusAddress);
        Assert.Equal(20, decoded[2].TargetTagIndex);
        Assert.Equal(ModbusSlaveAccessRight.ReadWrite, decoded[2].AccessRight);
        Assert.Equal(1, decoded[2].DataType);
    }

    [Fact]
    public void EncodePort_WithSlaveRoleAndId_EncodesCorrectly()
    {
        var dto = new ModbusPortConfigDto
        {
            PortIndex = 1, // Port 2
            PortName = "SCADA_SLAVE",
            Enabled = true,
            Role = ModbusPortRole.Slave,
            SlaveId = 5,
            BaudRate = 19200
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
        ModbusPortConfigCodec.EncodePort(dto, buffer);

        // Offset 0: Enabled (0x0100) | RoleSlave (0x0200) | Port 2 (0x0002) = 0x0302
        Assert.Equal(0x0302, buffer[0]);

        // Offset 14: Role = 1 (Slave)
        Assert.Equal(1, buffer[14]);

        // Offset 15: SlaveId = 5
        Assert.Equal(5, buffer[15]);

        // Decode back
        var decoded = ModbusPortConfigCodec.DecodePort(buffer);
        Assert.Equal(ModbusPortRole.Slave, decoded.Role);
        Assert.Equal(5, decoded.SlaveId);
        Assert.True(decoded.Enabled);
        Assert.Equal(1, decoded.PortIndex);
    }
}
