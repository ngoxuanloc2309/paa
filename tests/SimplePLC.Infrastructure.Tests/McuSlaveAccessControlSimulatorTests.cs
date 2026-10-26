namespace SimplePLC.Infrastructure.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

public sealed class McuSlaveAccessControlSimulatorTests
{
    [Fact]
    public async Task SlaveSimulator_EnforcesSandboxAccessControl()
    {
        var sim = new McuReferenceSimulator();
        byte slavePortIndex = 1; // Port 2
        byte slaveId = 5;

        // 1. Cấu hình Cổng 2 là Modbus Slave với Slave ID = 5
        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master },
            new() { PortIndex = slavePortIndex, PortName = "RS-485 #2", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = slaveId }
        };

        var portBuffer = new ushort[ModbusRegisterMap.ModbusPortConfigTableTotalLength];
        ModbusPortConfigCodec.EncodeAllPorts(portConfigs, portBuffer);
        sim.InternalWriteRegisters(ModbusRegisterMap.ModbusPortConfigBaseAddress, portBuffer);

        // 2. Thiết lập Bảng Ánh Xạ Modbus Slave tại 0x1500
        // Address 0  -> DI0 (Tag 0), Read-Only
        // Address 10 -> VREG0 (Tag 52), Read/Write
        var entries = new List<ModbusSlaveMapEntryDto>
        {
            new()
            {
                ModbusAddress = 0,
                TargetTagIndex = 0,
                AccessRight = ModbusSlaveAccessRight.ReadOnly,
                DataType = 1,
                PortMask = 0x0F,
                Description = "DI0 Sensor"
            },
            new()
            {
                ModbusAddress = 10,
                TargetTagIndex = 52,
                AccessRight = ModbusSlaveAccessRight.ReadWrite,
                DataType = 2,
                PortMask = 0x0F,
                Description = "VREG0 Setpoint"
            }
        };

        var mapBuffer = new ushort[ModbusRegisterMap.ModbusSlaveMapTableTotalLength];
        ModbusSlaveMapCodec.EncodeEntries(entries, mapBuffer);
        sim.InternalWriteRegisters(ModbusRegisterMap.ModbusSlaveMapBaseAddress, mapBuffer);

        // Giả lập giá trị runtime ban đầu: DI0 = 1, VREG0 = 1234
        sim.InternalWriteRegisters((ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + 0 * 2), new ushort[] { 1, 0 });
        sim.InternalWriteRegisters((ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + 52 * 2), new ushort[] { 1234, 0 });

        // TC 1: External SCADA đọc FC03 tại địa chỉ 0 (DI0) -> Nhận đúng giá trị 1
        var di0Values = await sim.ExecuteSlaveReadHoldingRegistersAsync(slavePortIndex, slaveId, 0, 1);
        Assert.Single(di0Values);
        Assert.Equal((ushort)1, di0Values[0]);

        // TC 2: External SCADA đọc FC03 tại địa chỉ 10 (VREG0) -> Nhận đúng 1234
        var vregValues = await sim.ExecuteSlaveReadHoldingRegistersAsync(slavePortIndex, slaveId, 10, 1);
        Assert.Single(vregValues);
        Assert.Equal((ushort)1234, vregValues[0]);

        // TC 3: External SCADA cố đọc địa chỉ ngoài bảng (e.g. 99) -> Bị từ chối Exception 0x02
        var exUnmappedRead = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(slavePortIndex, slaveId, 99, 1));
        Assert.Contains("Modbus Exception 0x02", exUnmappedRead.Message);

        // TC 4: External SCADA cố ghi vào ô Read-Only (Địa chỉ 0 - DI0) -> Bị từ chối Exception 0x03
        var exReadOnlyWrite = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(slavePortIndex, slaveId, 0, new ushort[] { 0 }));
        Assert.Contains("Modbus Exception 0x03", exReadOnlyWrite.Message);
        Assert.Contains("Read-Only sandbox", exReadOnlyWrite.Message);

        // TC 5: External SCADA ghi vào ô Read/Write (Địa chỉ 10 - VREG0) -> Thành công
        await sim.ExecuteSlaveWriteHoldingRegistersAsync(slavePortIndex, slaveId, 10, new ushort[] { 5678, 0 });
        var updatedVreg = await sim.ExecuteSlaveReadHoldingRegistersAsync(slavePortIndex, slaveId, 10, 1);
        Assert.Equal((ushort)5678, updatedVreg[0]);

        // TC 6: External SCADA cố ghi vào vùng cấm không nằm trong bảng ánh xạ (e.g. 0x9000) -> Exception 0x02
        var exForbiddenWrite = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(slavePortIndex, slaveId, 0x9000, new ushort[] { 0x0001 }));
        Assert.Contains("Modbus Exception 0x02", exForbiddenWrite.Message);
    }
}
