using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services.Modbus;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Triển khai giả lập IModbusSerialTransport phục vụ hermetic unit test không cần COM port vật lý.
/// </summary>
public sealed class FakeModbusSerialTransport : IModbusSerialTransport
{
    public bool IsOpen { get; set; } = true;
    public string? PortName { get; set; } = "COM_TEST";

    public byte[]? NextResponseBytes { get; set; }
    public TimeSpan ReadDelay { get; set; } = TimeSpan.Zero;
    public byte[]? LastWrittenBytes { get; private set; }

    public Task OpenAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, CancellationToken cancellationToken = default)
    {
        IsOpen = true;
        PortName = portName;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        LastWrittenBytes = buffer.ToArray();
        return Task.CompletedTask;
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (ReadDelay > TimeSpan.Zero)
        {
            if (ReadDelay > timeout)
            {
                await Task.Delay(timeout, cancellationToken);
                throw new TimeoutException("Fake timeout exceeded.");
            }
            await Task.Delay(ReadDelay, cancellationToken);
        }

        if (NextResponseBytes == null || NextResponseBytes.Length == 0)
        {
            throw new TimeoutException("No fake response configured.");
        }

        int count = Math.Min(buffer.Length, NextResponseBytes.Length);
        NextResponseBytes.AsSpan(0, count).CopyTo(buffer.Span);
        
        // Remove delivered bytes
        if (count < NextResponseBytes.Length)
        {
            byte[] remaining = new byte[NextResponseBytes.Length - count];
            Array.Copy(NextResponseBytes, count, remaining, 0, remaining.Length);
            NextResponseBytes = remaining;
        }
        else
        {
            NextResponseBytes = null;
        }

        return count;
    }

    public void DiscardBuffers() { }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Unit tests cho dịch vụ đọc thử phần cứng <see cref="ModbusLiveProbeService"/>.
/// Tuân thủ quy tắc IEC 61508-3 Triad Testing (Happy Path, Boundary, Fault Injection).
/// </summary>
public class ModbusLiveProbeServiceTests
{
    private static byte[] BuildModbusResponse(byte slaveId, byte fc, byte[] dataBytes)
    {
        byte[] frame = new byte[2 + 1 + dataBytes.Length + 2];
        frame[0] = slaveId;
        frame[1] = fc;
        frame[2] = (byte)dataBytes.Length;
        dataBytes.CopyTo(frame, 3);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, frame.Length - 2));
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    [Fact]
    public async Task Probe_HappyPath_FC03_Int16_ReturnsCorrectScaledAndRawValue()
    {
        // Arrange: Sensor returns value 250 (0x00FA) for Slave 1, FC03, Scale 0.1 -> Expect 25.0
        var fakeTransport = new FakeModbusSerialTransport();
        fakeTransport.NextResponseBytes = BuildModbusResponse(1, 3, new byte[] { 0x00, 0xFA });

        var service = new ModbusLiveProbeService(fakeTransport);

        // Act
        var result = await service.ProbeAsync(
            slaveId: 1,
            functionCode: 3,
            address: 40001,
            dataType: ModbusDataType.Int16,
            endianness: ModbusEndianness.BigEndian_ABCD,
            scale: 0.1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ScaledValue);
        Assert.Equal(25.0, result.ScaledValue.Value, precision: 2);
        Assert.Contains("250", result.RawValueDisplay);
        Assert.Contains("0x00FA", result.RawValueDisplay);
        Assert.Equal(ModbusProbeFaultKind.None, result.FaultKind);
    }

    [Fact]
    public async Task Probe_HappyPath_FC03_RealFloat_ReturnsCorrectFloatValue()
    {
        // Arrange: Float 25.5f in IEEE 754 Big-Endian ABCD is 0x41CC0000 -> bytes [41, CC, 00, 00]
        var fakeTransport = new FakeModbusSerialTransport();
        fakeTransport.NextResponseBytes = BuildModbusResponse(1, 3, new byte[] { 0x41, 0xCC, 0x00, 0x00 });

        var service = new ModbusLiveProbeService(fakeTransport);

        // Act
        var result = await service.ProbeAsync(
            slaveId: 1,
            functionCode: 3,
            address: 40001,
            dataType: ModbusDataType.Real,
            endianness: ModbusEndianness.BigEndian_ABCD,
            scale: 1.0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ScaledValue);
        Assert.Equal(25.5, result.ScaledValue.Value, precision: 1);
        Assert.Contains("25.500", result.RawValueDisplay);
    }

    [Fact]
    public async Task Probe_FaultInjection_Timeout_ReturnsTimeoutFaultAndTroubleshootingHint()
    {
        // Arrange: Fake transport times out
        var fakeTransport = new FakeModbusSerialTransport
        {
            ReadDelay = TimeSpan.FromMilliseconds(500)
        };

        var service = new ModbusLiveProbeService(fakeTransport);

        // Act
        var result = await service.ProbeAsync(
            slaveId: 1,
            functionCode: 3,
            address: 40001,
            dataType: ModbusDataType.Int16,
            endianness: ModbusEndianness.BigEndian_ABCD,
            scale: 1.0,
            timeoutMs: 100);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusProbeFaultKind.Timeout, result.FaultKind);
        Assert.False(string.IsNullOrWhiteSpace(result.TroubleshootingHint));
    }

    [Fact]
    public async Task Probe_FaultInjection_CrcCorrupted_ReturnsCrcFault()
    {
        // Arrange: Frame with corrupted CRC
        var validFrame = BuildModbusResponse(1, 3, new byte[] { 0x00, 0x64 });
        validFrame[^1] ^= 0xFF; // Invert CRC byte

        var fakeTransport = new FakeModbusSerialTransport
        {
            NextResponseBytes = validFrame
        };

        var service = new ModbusLiveProbeService(fakeTransport);

        // Act
        var result = await service.ProbeAsync(
            slaveId: 1,
            functionCode: 3,
            address: 40001,
            dataType: ModbusDataType.Int16,
            endianness: ModbusEndianness.BigEndian_ABCD,
            scale: 1.0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusProbeFaultKind.CrcError, result.FaultKind);
    }

    [Fact]
    public async Task Probe_FaultInjection_ModbusException_ReturnsExceptionFaultAndDescription()
    {
        // Arrange: Slave returns Exception 0x02 (Illegal Data Address)
        // [Slave: 01] [FC: 0x83] [Code: 0x02] [CRCLo] [CRCHi]
        byte[] excFrame = new byte[5];
        excFrame[0] = 1;
        excFrame[1] = 0x83;
        excFrame[2] = 0x02;
        ushort crc = Crc16Modbus.Compute(excFrame.AsSpan(0, 3));
        excFrame[3] = (byte)(crc & 0xFF);
        excFrame[4] = (byte)(crc >> 8);

        var fakeTransport = new FakeModbusSerialTransport
        {
            NextResponseBytes = excFrame
        };

        var service = new ModbusLiveProbeService(fakeTransport);

        // Act
        var result = await service.ProbeAsync(
            slaveId: 1,
            functionCode: 3,
            address: 40001,
            dataType: ModbusDataType.Int16,
            endianness: ModbusEndianness.BigEndian_ABCD,
            scale: 1.0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusProbeFaultKind.ModbusException, result.FaultKind);
        Assert.Contains("02 Illegal Data Address", result.DiagnosticMessage);
    }

    [Fact]
    public async Task Probe_WhenPortClosed_ReturnsPortNotOpenFault()
    {
        var fakeTransport = new FakeModbusSerialTransport { IsOpen = false };
        var service = new ModbusLiveProbeService(fakeTransport);

        var result = await service.ProbeAsync(1, 3, 40001, ModbusDataType.Int16, ModbusEndianness.BigEndian_ABCD, 1.0);

        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusProbeFaultKind.PortNotOpen, result.FaultKind);
    }
}
