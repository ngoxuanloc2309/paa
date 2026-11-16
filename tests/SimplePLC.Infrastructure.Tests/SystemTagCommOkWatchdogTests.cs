namespace SimplePLC.Infrastructure.Tests;

using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using Xunit;

/// <summary>
/// Bộ kiểm thử tuân thủ chuẩn IEC 61508-3 Triad Testing cho cơ chế giám sát truyền thông SYS_COMM_OK (Tag 1007, 0x0827).
/// </summary>
public sealed class SystemTagCommOkWatchdogTests
{
    private const ushort CommOkRegisterAddress = ModbusRegisterMap.SystemRegistersBaseAddress + 7; // 0x0827

    // ==========================================
    // 1. HAPPY PATH TESTS
    // ==========================================

    [Fact]
    public void SysCommOk_OnStartupWithoutModbusOrMqtt_ReturnsZero()
    {
        // Arrange: Thiết bị vừa khởi động, chạy độc lập (Offline), chưa có Master poll và chưa có MQTT
        var simulator = new McuReferenceSimulator();
        simulator.ExecuteScanPass(10);

        // Act: Đọc trực tiếp ô nhớ nội bộ 0x0827
        ushort commOk = simulator.Memory.InternalRead(CommOkRegisterAddress);

        // Assert: Thể hiện đúng bản chất vật lý (The Physical Truth) là 0
        Assert.Equal(0, commOk);
    }

    [Fact]
    public async Task SysCommOk_AfterModbusRequestReceived_ReturnsOne()
    {
        // Arrange
        var simulator = new McuReferenceSimulator();

        // Act: Gửi 1 frame đọc hợp lệ tới simulator (Master đang hỏi)
        var regs = await simulator.ReadHoldingRegistersAsync(1, ModbusRegisterMap.SystemRegistersBaseAddress, 8);

        // Assert: Register thứ 7 (0x0827) phải bằng 1
        Assert.Equal(1, regs[7]);
        Assert.Equal(1, simulator.Memory.InternalRead(CommOkRegisterAddress));
    }

    [Fact]
    public void SysCommOk_WhenMqttConnectedWithoutModbus_ReturnsOne()
    {
        // Arrange: Không có Modbus Master nhưng MQTT đã kết nối tới Broker thành công
        var simulator = new McuReferenceSimulator();
        simulator.SetSimulatedMqttConnectionState(true);
        simulator.ExecuteScanPass(10);

        // Act & Assert
        ushort commOk = simulator.Memory.InternalRead(CommOkRegisterAddress);
        Assert.Equal(1, commOk);
    }

    // ==========================================
    // 2. BOUNDARY & TIMEOUT TESTS (2990ms vs 3010ms)
    // ==========================================

    [Fact]
    public async Task SysCommOk_WhenModbusSilenceBelow3000Ms_RemainsOne()
    {
        // Arrange
        var simulator = new McuReferenceSimulator();
        await simulator.ReadHoldingRegistersAsync(1, 0x0000, 1); // Kích hoạt frame đầu tiên -> COMM_OK = 1

        // Act: Thời gian trôi qua 2990ms (chưa vượt ngưỡng 3000ms)
        simulator.SimulateModbusSilence(2990);
        simulator.ExecuteScanPass(10);

        // Assert: Vẫn còn trong cửa sổ cho phép -> Bằng 1
        ushort commOk = simulator.Memory.InternalRead(CommOkRegisterAddress);
        Assert.Equal(1, commOk);
    }

    [Fact]
    public async Task SysCommOk_WhenModbusSilenceExceeds3000Ms_TransitionsToZero()
    {
        // Arrange
        var simulator = new McuReferenceSimulator();
        await simulator.ReadHoldingRegistersAsync(1, 0x0000, 1);

        // Act: Thời gian tĩnh lặng vượt qua 3010ms (> 3000ms watchdog timeout)
        simulator.SimulateModbusSilence(3010);
        simulator.ExecuteScanPass(10);

        // Assert: Watchdog trip chính xác -> Bằng 0
        ushort commOk = simulator.Memory.InternalRead(CommOkRegisterAddress);
        Assert.Equal(0, commOk);
    }

    // ==========================================
    // 3. FAULT INJECTION & RECOVERY TESTS
    // ==========================================

    [Fact]
    public async Task SysCommOk_WhenModbusResumesAfterTimeout_RecoversToOne()
    {
        // Arrange: Đã bị timeout về 0 do mất kết nối 5 giây
        var simulator = new McuReferenceSimulator();
        await simulator.ReadHoldingRegistersAsync(1, 0x0000, 1);
        simulator.SimulateModbusSilence(5000);
        simulator.ExecuteScanPass(10);
        Assert.Equal(0, simulator.Memory.InternalRead(CommOkRegisterAddress));

        // Act: Master gửi lại request mới (cắm lại cáp)
        var regs = await simulator.ReadHoldingRegistersAsync(1, ModbusRegisterMap.SystemRegistersBaseAddress, 8);

        // Assert: Hệ thống tự động phục hồi lên 1 ngay lập tức
        Assert.Equal(1, regs[7]);
        Assert.Equal(1, simulator.Memory.InternalRead(CommOkRegisterAddress));
    }

    [Fact]
    public async Task SysCommOk_PowerCycleReset_RevertsToZero()
    {
        // Arrange: Đang hoạt động bình thường với COMM_OK = 1
        var simulator = new McuReferenceSimulator();
        await simulator.ReadHoldingRegistersAsync(1, 0x0000, 1);
        Assert.Equal(1, simulator.Memory.InternalRead(CommOkRegisterAddress));

        // Act: Tắt nguồn bật lại (PowerCycle)
        simulator.PowerCycle();
        simulator.ExecuteScanPass(10);

        // Assert: Trở về trạng thái khởi động ban đầu (0)
        Assert.Equal(0, simulator.Memory.InternalRead(CommOkRegisterAddress));
    }

    [Fact]
    public async Task SysCommOk_CustomWatchdogTimeout_TripsAtConfiguredThreshold()
    {
        // Arrange: Cấu hình watchdog timeout ngắn hơn (ví dụ 1000ms)
        var simulator = new McuReferenceSimulator();
        simulator.SetSimulatedModbusWatchdogTimeoutMs(1000);
        await simulator.ReadHoldingRegistersAsync(1, 0x0000, 1);

        // Act 1: Sau 800ms -> Vẫn sống
        simulator.SimulateModbusSilence(800);
        simulator.ExecuteScanPass(10);
        Assert.Equal(1, simulator.Memory.InternalRead(CommOkRegisterAddress));

        // Act 2: Sau thêm 300ms (tổng 1100ms > 1000ms) -> Ngắt
        simulator.SimulateModbusSilence(300);
        simulator.ExecuteScanPass(10);
        Assert.Equal(0, simulator.Memory.InternalRead(CommOkRegisterAddress));
    }
}
