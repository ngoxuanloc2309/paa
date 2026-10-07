using System.Buffers.Binary;
using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Phase F1 — Communication Fault Injection:
/// Kiểm tra failure semantics rõ ràng của tầng Modbus RTU và Runtime Monitor khi gặp các sự cố:
/// - Timeout
/// - Frame CRC lỗi
/// - Malformed response (sai Slave, sai FC, sai ByteCount)
/// - Short / Truncated frame
/// - USB Disconnect (read / write)
/// - Device Busy / Modbus Exceptions (0x01, 0x02, 0x04, 0x06)
/// - Chống rò rỉ Lock / Task khi có exception
/// </summary>
public sealed class CommunicationFaultInjectionTests
{
    private readonly FakeUsbCdcTransport _transport;
    private readonly ModbusRtuClient _client;

    public CommunicationFaultInjectionTests()
    {
        _transport = new FakeUsbCdcTransport { IsOpen = true };
        _client = new ModbusRtuClient(_transport);
    }

    [Fact]
    public async Task F1_01_ReadHoldingRegisters_Timeout_ThrowsTimeoutException_AndReleasesLock()
    {
        _transport.SimulateTimeout = true;

        await Assert.ThrowsAsync<TimeoutException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));

        // Kiểm tra sau khi timeout, lock đã được release: request tiếp theo vẫn vào được
        _transport.SimulateTimeout = false;
        byte[] payload = { 0x01, 0x03, 0x02, 0x00, 0x10 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = { 0x01, 0x03, 0x02, 0x00, 0x10, (byte)(crc & 0xFF), (byte)(crc >> 8) };
        _transport.EnqueueResponse(response);

        var regs = await _client.ReadHoldingRegistersAsync(1, 0x0000, 1);
        Assert.Single(regs);
        Assert.Equal(0x0010, regs[0]);
    }

    [Fact]
    public async Task F1_02_ReadHoldingRegisters_CorruptedFrameCrc_ThrowsInvalidDataException()
    {
        // Frame hợp lệ ngoại trừ 2 bytes CRC bị đảo ngược
        byte[] payload = { 0x01, 0x03, 0x02, 0x12, 0x34 };
        ushort correctCrc = Crc16Modbus.Compute(payload);
        byte[] corruptResponse = { 0x01, 0x03, 0x02, 0x12, 0x34, (byte)(~correctCrc & 0xFF), (byte)(~correctCrc >> 8) };

        _transport.EnqueueResponse(corruptResponse);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));

        Assert.Contains("CRC-16 mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task F1_03_ReadHoldingRegisters_MalformedSlaveId_ThrowsInvalidDataException()
    {
        // Slave ID là 2 nhưng client gửi tới slave 1
        byte[] payload = { 0x02, 0x03, 0x02, 0x12, 0x34 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = { 0x02, 0x03, 0x02, 0x12, 0x34, (byte)(crc & 0xFF), (byte)(crc >> 8) };

        _transport.EnqueueResponse(response);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));

        Assert.Contains("Unexpected Modbus response header", ex.Message);
    }

    [Fact]
    public async Task F1_04_ReadHoldingRegisters_MalformedFunctionCode_ThrowsInvalidDataException()
    {
        // FC là 0x04 thay vì 0x03
        byte[] payload = { 0x01, 0x04, 0x02, 0x12, 0x34 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = { 0x01, 0x04, 0x02, 0x12, 0x34, (byte)(crc & 0xFF), (byte)(crc >> 8) };

        _transport.EnqueueResponse(response);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));

        Assert.Contains("Unexpected Modbus response header", ex.Message);
    }

    [Fact]
    public async Task F1_05_ReadHoldingRegisters_ByteCountMismatch_ThrowsInvalidDataException()
    {
        // Yêu cầu đọc 2 thanh ghi (4 bytes data) nhưng byteCount trả về là 2
        byte[] payload = { 0x01, 0x03, 0x02, 0x12, 0x34 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = { 0x01, 0x03, 0x02, 0x12, 0x34, (byte)(crc & 0xFF), (byte)(crc >> 8) };

        _transport.EnqueueResponse(response);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 2));

        Assert.Contains("byte count mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task F1_06_ReadHoldingRegisters_ShortTruncatedFrame_ThrowsTimeoutException()
    {
        // Gửi thiếu data (chỉ gửi 1 byte header [0x01] rồi không còn byte nào)
        byte[] shortFrame = { 0x01 };
        _transport.EnqueueResponse(shortFrame);

        // ReadExactAsync sẽ chờ và timeout khi không đủ bytes
        await Assert.ThrowsAsync<TimeoutException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));
    }

    [Fact]
    public async Task F1_07_WriteSingleRegister_DisconnectOnWrite_ThrowsIOException()
    {
        _transport.SimulateDisconnectOnWrite = true;

        await Assert.ThrowsAsync<IOException>(
            () => _client.WriteSingleRegisterAsync(1, 0xA000, 0xA5A5));

        Assert.False(_client.IsConnected);
    }

    [Fact]
    public async Task F1_08_WriteSingleRegister_DisconnectOnReadEcho_ThrowsIOException()
    {
        _transport.SimulateDisconnectOnRead = true;

        await Assert.ThrowsAsync<IOException>(
            () => _client.WriteSingleRegisterAsync(1, 0xA000, 0xA5A5));

        Assert.False(_client.IsConnected);
    }

    [Fact]
    public async Task F1_09_ModbusExceptionResponses_MappedCorrectly()
    {
        // Test Illegal Function (0x01)
        byte[] ex01Payload = { 0x01, 0x83, 0x01 };
        ushort crc01 = Crc16Modbus.Compute(ex01Payload);
        _transport.EnqueueResponse(new byte[] { 0x01, 0x83, 0x01, (byte)(crc01 & 0xFF), (byte)(crc01 >> 8) });

        var ex1 = await Assert.ThrowsAsync<ModbusProtocolException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));
        Assert.Equal(ModbusExceptionCode.IllegalFunction, ex1.ExceptionCode);

        // Test Slave Device Busy (0x06)
        byte[] ex06Payload = { 0x01, 0x86, 0x06 };
        ushort crc06 = Crc16Modbus.Compute(ex06Payload);
        _transport.EnqueueResponse(new byte[] { 0x01, 0x86, 0x06, (byte)(crc06 & 0xFF), (byte)(crc06 >> 8) });

        var ex6 = await Assert.ThrowsAsync<ModbusProtocolException>(
            () => _client.WriteSingleRegisterAsync(1, 0xA000, 0xA5A5));
        Assert.Equal(ModbusExceptionCode.SlaveDeviceBusy, ex6.ExceptionCode);
    }

    [Fact]
    public async Task F1_10_RuntimeMonitor_SingleTimeout_DoesNotTriggerReconnect_KeepsGoodUntilStale()
    {
        var sim = new McuReferenceSimulator();
        var fakeClient = new FakeModbusClient(sim, isConnected: true);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();

        var tagReader = new RuntimeTagReader(fakeClient);
        var healthReader = new DeviceHealthReader(fakeClient);
        var monitor = new RuntimeMonitorService(tagReader, healthReader, coordinator: null, stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(50),
            HealthCheckDivisor = 2
        };

        bool transportLostFired = false;
        monitor.OnTransportLost = _ =>
        {
            transportLostFired = true;
            return Task.CompletedTask;
        };

        await monitor.StartAsync(product, slaveId: 1);

        // Đợi 2 chu kỳ poll đầu thành công -> Tags phải là Good
        await Task.Delay(130);
        var initialSnapshot = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Connected, initialSnapshot.ConnectionStatus);
        Assert.True(initialSnapshot.Tags.Count > 0);
        Assert.All(initialSnapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        // Inject 1 lần Timeout tạm thời trên địa chỉ Tag
        sim.Control.Faults.TimeoutOnAddress = 0x0100;

        // Chờ 1 chu kỳ polling (50ms)
        await Task.Delay(80);

        // Gỡ lỗi timeout ngay sau đó
        sim.Control.Faults.TimeoutOnAddress = null;

        // Bất biến F1: Timeout tạm thời không được kích hoạt OnTransportLost
        Assert.False(transportLostFired, "Single timeout must NOT trigger OnTransportLost or reconnect");
        Assert.True(monitor.IsRunning, "Monitor must continue running through transient timeout");

        // Chờ thêm 1 chu kỳ để polling phục hồi dữ liệu Good
        await Task.Delay(100);
        var recoveredSnapshot = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Connected, recoveredSnapshot.ConnectionStatus);
        Assert.All(recoveredSnapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        await monitor.StopAsync();
    }

    [Fact]
    public async Task F1_11_RuntimeMonitor_PhysicalDisconnect_TerminatesLoop_MarksDisconnected_FiresOnTransportLost()
    {
        var sim = new McuReferenceSimulator();
        var fakeClient = new FakeModbusClient(sim, isConnected: true);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();

        var tagReader = new RuntimeTagReader(fakeClient);
        var healthReader = new DeviceHealthReader(fakeClient);
        var monitor = new RuntimeMonitorService(tagReader, healthReader, coordinator: null, stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(50)
        };

        var tcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.OnTransportLost = ex =>
        {
            tcs.TrySetResult(ex);
            return Task.CompletedTask;
        };

        await monitor.StartAsync(product, slaveId: 1);
        await Task.Delay(80);

        // Mô phỏng rút cáp USB vật lý (ném IOException)
        fakeClient.SimulateDisconnect = true;

        // Chờ callback OnTransportLost được kích hoạt
        var lostEx = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.Same(tcs.Task, lostEx);

        var caughtEx = await tcs.Task;
        Assert.IsAssignableFrom<IOException>(caughtEx);

        // Chờ monitor dọn dẹp kết thúc vòng lặp
        await Task.Delay(60);
        Assert.False(monitor.IsRunning, "Monitor loop must terminate when physical transport is lost");

        var finalSnapshot = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Disconnected, finalSnapshot.ConnectionStatus);

        await monitor.StopAsync();
    }
}
