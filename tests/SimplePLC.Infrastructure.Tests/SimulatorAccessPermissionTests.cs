using System;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class SimulatorAccessPermissionTests
{
    [Fact]
    public void IsReadableRange_SystemCommand_0x0A00_IsWriteOnly_ReturnsFalse()
    {
        // 0x0A00 (SystemCommand) theo khế ước là Write-Only, không được phép đọc
        bool readable = SimulatorRegisterMemory.IsReadableRange(ModbusRegisterMap.SystemCommandAddress, 1);
        Assert.False(readable, "0x0A00 (SystemCommand) must be Write-Only (RO = false)");
    }

    [Fact]
    public void IsReadableRange_SystemCommandResult_0x0A01_IsReadOnly_ReturnsTrue()
    {
        // 0x0A01..0x0A02 (SystemCommandResult) là Read-Only, được phép đọc
        bool readable = SimulatorRegisterMemory.IsReadableRange(ModbusRegisterMap.SystemCommandResultAddress, ModbusRegisterMap.SystemCommandResultLength);
        Assert.True(readable, "0x0A01..0x0A02 (SystemCommandResult) must be Readable");
    }

    [Fact]
    public void IsWritableRange_RuntimeTagValues_0x0900_IsReadOnly_ReturnsFalse()
    {
        // 0x0900..0x09FF (RuntimeTagValues) là Read-Only do MCU telemetry cập nhật, Host cấm ghi
        bool writableSingle = SimulatorRegisterMemory.IsWritableRange(ModbusRegisterMap.RuntimeTagValuesBaseAddress, 1);
        Assert.False(writableSingle, "0x0900 (RuntimeTagValues) must be Read-Only (WO = false)");

        bool writableMulti = SimulatorRegisterMemory.IsWritableRange(ModbusRegisterMap.RuntimeTagValuesBaseAddress, 20);
        Assert.False(writableMulti, "0x0900..0x0913 range must be Read-Only");
    }

    [Fact]
    public void IsWritableRange_SystemCommand_0x0A00_IsWritable_ReturnsTrue()
    {
        // 0x0A00 (SystemCommand) cho phép ghi lệnh (Reboot, Reset)
        bool writable = SimulatorRegisterMemory.IsWritableRange(ModbusRegisterMap.SystemCommandAddress, 1);
        Assert.True(writable, "0x0A00 (SystemCommand) must be Writable");
    }

    [Fact]
    public async Task McuReferenceSimulator_ReadSystemCommand_ThrowsIllegalDataAddress()
    {
        var simulator = new McuReferenceSimulator();

        // Cố gắng đọc 0x0A00 phải ném ngoại lệ ILLEGAL_DATA_ADDRESS
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            simulator.ReadHoldingRegistersAsync(1, ModbusRegisterMap.SystemCommandAddress, 1));

        Assert.Contains("ILLEGAL_DATA_ADDRESS", ex.Message);
    }

    [Fact]
    public async Task McuReferenceSimulator_WriteRuntimeTagValues_ThrowsIllegalDataAddress()
    {
        var simulator = new McuReferenceSimulator();

        // Cố gắng ghi vào 0x0900 phải ném ngoại lệ ILLEGAL_DATA_ADDRESS
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            simulator.WriteSingleRegisterAsync(1, ModbusRegisterMap.RuntimeTagValuesBaseAddress, 123));

        Assert.Contains("ILLEGAL_DATA_ADDRESS", ex.Message);
    }

    [Fact]
    public async Task McuReferenceSimulator_WriteSystemCommand_And_ReadResult_Succeeds()
    {
        var simulator = new McuReferenceSimulator();

        // Ghi lệnh hợp lệ vào 0x0A00 (ví dụ CLEAR_FAULTS = 2)
        await simulator.WriteSingleRegisterAsync(1, ModbusRegisterMap.SystemCommandAddress, 2);

        // Đọc kết quả từ 0x0A01..0x0A02 phải thành công
        var resultRegs = await simulator.ReadHoldingRegistersAsync(1, ModbusRegisterMap.SystemCommandResultAddress, 2);
        Assert.Equal(2, resultRegs.Length);
    }
}
