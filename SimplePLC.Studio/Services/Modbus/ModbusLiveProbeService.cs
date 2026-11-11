using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Services.Modbus;

/// <summary>
/// Giao diện trừu tượng hóa tầng truyền dẫn nối tiếp SerialPort để phục vụ Unit Test không cần phần cứng COM thật.
/// </summary>
public interface IModbusSerialTransport : IAsyncDisposable
{
    bool IsOpen { get; }
    string? PortName { get; }
    Task OpenAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
    Task<int> ReadAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken = default);
    void DiscardBuffers();
}

/// <summary>
/// Triển khai thực tế IModbusSerialTransport sử dụng System.IO.Ports.SerialPort của .NET 8 trên Windows.
/// </summary>
public sealed class RealSerialPortTransport : IModbusSerialTransport
{
    private SerialPort? _port;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsOpen => _port != null && _port.IsOpen;
    public string? PortName => _port?.PortName;

    public async Task OpenAsync(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_port != null && _port.IsOpen)
            {
                if (string.Equals(_port.PortName, portName, StringComparison.OrdinalIgnoreCase) && _port.BaudRate == baudRate)
                {
                    return;
                }
                CloseInternal();
            }

            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            await Task.Run(() =>
            {
                _port.Open();
                try { _port.DtrEnable = true; } catch { /* ARCH_ALLOW_SWALLOW: Handshake fallback */ }
                try { _port.RtsEnable = true; } catch { /* ARCH_ALLOW_SWALLOW: Handshake fallback */ }
                try { _port.DiscardInBuffer(); } catch { /* ARCH_ALLOW_SWALLOW: Handshake fallback */ }
                try { _port.DiscardOutBuffer(); } catch { /* ARCH_ALLOW_SWALLOW: Handshake fallback */ }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CloseInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void DiscardBuffers()
    {
        if (_port != null && _port.IsOpen)
        {
            try { _port.DiscardInBuffer(); } catch { /* ARCH_ALLOW_SWALLOW: Best-effort buffer clear */ }
            try { _port.DiscardOutBuffer(); } catch { /* ARCH_ALLOW_SWALLOW: Best-effort buffer clear */ }
        }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not open.");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] arr = buffer.ToArray();
            await _port.BaseStream.WriteAsync(arr, 0, arr.Length, cancellationToken).ConfigureAwait(false);
            await _port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not open.");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int totalRead = 0;
            int targetLength = buffer.Length;
            var sw = Stopwatch.StartNew();

            while (totalRead < targetLength)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new TimeoutException($"Timed out waiting for serial data ({totalRead}/{targetLength} bytes received).");
                }

                if (_port.BytesToRead > 0)
                {
                    int toRead = Math.Min(targetLength - totalRead, _port.BytesToRead);
                    byte[] temp = new byte[toRead];
                    int read = _port.Read(temp, 0, toRead);
                    if (read > 0)
                    {
                        temp.AsSpan(0, read).CopyTo(buffer.Span.Slice(totalRead));
                        totalRead += read;
                    }
                }
                else
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }

            return totalRead;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void CloseInternal()
    {
        if (_port != null)
        {
            try { if (_port.IsOpen) _port.Close(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown */ }
            try { _port.Dispose(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown */ }
            _port = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Dịch vụ đọc thử và chẩn đoán Modbus RTU trực tiếp trên cổng COM phần cứng.
/// Tuân thủ học thuyết First Principles (Axiom 1, 2) và kiến trúc Clean Architecture.
/// </summary>
public sealed class ModbusLiveProbeService : IModbusLiveProbeService
{
    private readonly IModbusSerialTransport _transport;

    public bool IsPortOpen => _transport.IsOpen;
    public string? CurrentPortName => _transport.PortName;

    public ModbusLiveProbeService(IModbusSerialTransport? transport = null)
    {
        _transport = transport ?? new RealSerialPortTransport();
    }

    public Task OpenPortAsync(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One, CancellationToken cancellationToken = default)
    {
        return _transport.OpenAsync(portName, baudRate, dataBits, parity, stopBits, cancellationToken);
    }

    public Task ClosePortAsync(CancellationToken cancellationToken = default)
    {
        return _transport.CloseAsync(cancellationToken);
    }

    public async Task<ModbusProbeResult> ProbeAsync(
        byte slaveId,
        byte functionCode,
        ushort address,
        ModbusDataType dataType,
        ModbusEndianness endianness,
        double scale,
        int timeoutMs = 1000,
        CancellationToken cancellationToken = default)
    {
        if (!IsPortOpen)
        {
            return ModbusProbeResult.Failure(
                ModbusProbeFaultKind.PortNotOpen,
                LocalizationService.Instance["LiveTestNoPortSelected"],
                LocalizationService.Instance["LiveTestSelectPortTip"]);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            _transport.DiscardBuffers();

            // 1. Xác định số lượng thanh ghi cần đọc (Quantity)
            ushort quantity = (dataType is ModbusDataType.Int32 or ModbusDataType.UInt32 or ModbusDataType.Real) ? (ushort)2 : (ushort)1;
            ushort wireOffset = CalculateWireOffset(address);

            // 2. Chuẩn bị request Modbus RTU 8 bytes
            byte[] request = new byte[8];
            request[0] = slaveId;
            request[1] = functionCode;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), wireOffset);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), quantity);

            ushort crcReq = Crc16Modbus.Compute(request.AsSpan(0, 6));
            request[6] = (byte)(crcReq & 0xFF);
            request[7] = (byte)(crcReq >> 8);

            await _transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            // 3. Đọc phản hồi từ cảm biến
            var timeoutSpan = TimeSpan.FromMilliseconds(timeoutMs);

            // Đọc 2 byte header đầu tiên: [SlaveId] [FC]
            byte[] header = new byte[2];
            await _transport.ReadAsync(header, timeoutSpan, cancellationToken).ConfigureAwait(false);

            if (header[0] != slaveId)
            {
                sw.Stop();
                return ModbusProbeResult.Failure(
                    ModbusProbeFaultKind.GeneralFault,
                    $"Sai Slave ID (Nhận {header[0]}, kỳ vọng {slaveId})",
                    "Kiểm tra lại cấu hình địa chỉ trạm trên thiết bị.",
                    sw.ElapsedMilliseconds);
            }

            // Kiểm tra mã lỗi Modbus Exception (FC | 0x80)
            if (header[1] >= 0x80)
            {
                byte[] excBuf = new byte[3]; // [ErrorCode] [CRCLo] [CRCHi]
                await _transport.ReadAsync(excBuf, timeoutSpan, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                byte errCode = excBuf[0];
                string excDesc = errCode switch
                {
                    1 => "Mã hàm không được hỗ trợ (01 Illegal Function)",
                    2 => "Địa chỉ thanh ghi không tồn tại (02 Illegal Data Address)",
                    3 => "Giá trị dữ liệu không hợp lệ (03 Illegal Data Value)",
                    4 => "Lỗi nội bộ thiết bị Slave (04 Slave Device Failure)",
                    _ => $"Mã lỗi Modbus {errCode:X2}"
                };

                return ModbusProbeResult.Failure(
                    ModbusProbeFaultKind.ModbusException,
                    excDesc,
                    "Kiểm tra lại dải địa chỉ thanh ghi trong tài liệu kỹ thuật của thiết bị.",
                    sw.ElapsedMilliseconds);
            }

            // Đọc phần thân phản hồi bình thường
            int dataByteCount = (functionCode is 1 or 2) ? 1 : (quantity * 2);
            // Cấu trúc phản hồi: [ByteCount] + [DataBytes] + [CRCLo][CRCHi]
            int remainingLength = 1 + dataByteCount + 2;
            byte[] body = new byte[remainingLength];
            await _transport.ReadAsync(body, timeoutSpan, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            // Kiểm tra toàn vẹn CRC-16
            byte[] fullFrame = new byte[2 + remainingLength];
            header.CopyTo(fullFrame, 0);
            body.CopyTo(fullFrame, 2);

            ushort receivedCrc = (ushort)(fullFrame[^2] | (fullFrame[^1] << 8));
            ushort computedCrc = Crc16Modbus.Compute(fullFrame.AsSpan(0, fullFrame.Length - 2));

            if (receivedCrc != computedCrc)
            {
                return ModbusProbeResult.Failure(
                    ModbusProbeFaultKind.CrcError,
                    LocalizationService.Instance["LiveTestErrorCrc"],
                    LocalizationService.Instance["LiveTestErrorCrcHint"],
                    sw.ElapsedMilliseconds);
            }

            // 4. Giải mã dữ liệu theo kiểu dữ liệu và Endianness
            var (scaledVal, rawDisplay) = DecodeData(body, 1, dataByteCount, dataType, endianness, scale);

            return ModbusProbeResult.Success(scaledVal, rawDisplay, sw.ElapsedMilliseconds, LocalizationService.Instance["LiveTestStatusGood"]);
        }
        catch (TimeoutException)
        {
            sw.Stop();
            return ModbusProbeResult.Failure(
                ModbusProbeFaultKind.Timeout,
                LocalizationService.Instance["LiveTestErrorTimeout"],
                LocalizationService.Instance["LiveTestErrorTimeoutHint"],
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ModbusProbeResult.Failure(
                ModbusProbeFaultKind.GeneralFault,
                ex.Message,
                "Kiểm tra lại kết nối cáp vật lý và trạng thái nguồn thiết bị.",
                sw.ElapsedMilliseconds);
        }
    }

    private static (double scaled, string rawDisplay) DecodeData(byte[] buffer, int offset, int count, ModbusDataType dataType, ModbusEndianness endianness, double scale)
    {
        ReadOnlySpan<byte> data = buffer.AsSpan(offset, count);
        if (scale == 0) scale = 1.0;

        switch (dataType)
        {
            case ModbusDataType.Bool:
            {
                bool b = (data[0] & 0x01) != 0;
                double val = b ? 1.0 : 0.0;
                return (val, b ? "TRUE (1)" : "FALSE (0)");
            }

            case ModbusDataType.Int16:
            {
                short raw = BinaryPrimitives.ReadInt16BigEndian(data);
                double scaled = raw * scale;
                return (scaled, $"{raw} (0x{raw:X4})");
            }

            case ModbusDataType.UInt16:
            {
                ushort raw = BinaryPrimitives.ReadUInt16BigEndian(data);
                double scaled = raw * scale;
                return (scaled, $"{raw} (0x{raw:X4})");
            }

            case ModbusDataType.Int32:
            {
                Span<byte> reordered = stackalloc byte[4];
                ApplyEndianness(data, reordered, endianness);
                int raw = BinaryPrimitives.ReadInt32BigEndian(reordered);
                double scaled = raw * scale;
                return (scaled, $"{raw} (0x{raw:X8})");
            }

            case ModbusDataType.UInt32:
            {
                Span<byte> reordered = stackalloc byte[4];
                ApplyEndianness(data, reordered, endianness);
                uint raw = BinaryPrimitives.ReadUInt32BigEndian(reordered);
                double scaled = raw * scale;
                return (scaled, $"{raw} (0x{raw:X8})");
            }

            case ModbusDataType.Real:
            {
                Span<byte> reordered = stackalloc byte[4];
                ApplyEndianness(data, reordered, endianness);
                float raw = BinaryPrimitives.ReadSingleBigEndian(reordered);
                double scaled = raw * scale;
                return (scaled, $"{raw:F3} (0x{BinaryPrimitives.ReadUInt32BigEndian(reordered):X8})");
            }

            default:
            {
                short raw = BinaryPrimitives.ReadInt16BigEndian(data);
                return (raw * scale, $"{raw}");
            }
        }
    }

    private static void ApplyEndianness(ReadOnlySpan<byte> src, Span<byte> dst, ModbusEndianness endianness)
    {
        // 4 bytes: A, B, C, D từ 2 thanh ghi
        byte a = src[0];
        byte b = src[1];
        byte c = src[2];
        byte d = src[3];

        switch (endianness)
        {
            case ModbusEndianness.BigEndian_ABCD:
                dst[0] = a; dst[1] = b; dst[2] = c; dst[3] = d;
                break;
            case ModbusEndianness.MidBig_CDAB: // Word Swap
                dst[0] = c; dst[1] = d; dst[2] = a; dst[3] = b;
                break;
            case ModbusEndianness.ByteSwap_BADC: // Byte Swap
                dst[0] = b; dst[1] = a; dst[2] = d; dst[3] = c;
                break;
            case ModbusEndianness.LittleEndian_DCBA: // Pure Little Endian
                dst[0] = d; dst[1] = c; dst[2] = b; dst[3] = a;
                break;
        }
    }

    private static ushort CalculateWireOffset(ushort address) => address switch
    {
        >= 40001 and <= 49999 => (ushort)(address - 40001),
        >= 30001 and <= 39999 => (ushort)(address - 30001),
        >= 10001 and <= 19999 => (ushort)(address - 10001),
        >= 1 and <= 9999 => (ushort)(address - 1),
        _ => address
    };

    public ValueTask DisposeAsync()
    {
        return _transport.DisposeAsync();
    }
}
