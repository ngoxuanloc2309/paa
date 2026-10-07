using System.Buffers.Binary;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Trình điều khiển giao thức Modbus RTU qua tầng truyền dẫn USB CDC (STM32 TinyUSB CDC / NanoModbus).
/// Hiện thực hoá Deterministic Length-Based Reading giải quyết triệt để vấn đề xé gói 64 bytes của USB FS.
/// Đảm bảo an toàn luồng (Thread-safe qua SemaphoreSlim) và kiểm tra toàn vẹn CRC-16 (R1, R7).
/// </summary>
public sealed class ModbusRtuClient : IModbusClient
{
    private readonly IUsbCdcTransport _transport;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDisposed;

    public bool IsConnected => !_isDisposed && _transport.IsOpen;

    public ModbusRtuClient(IUsbCdcTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    public async Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (count == 0 || count > 125)
            throw new ArgumentOutOfRangeException(nameof(count), "Modbus FC03 register count must be between 1 and 125.");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _transport.DiscardBuffers();

            // 1. Chuẩn bị request FC03 (8 bytes: [Slave][0x03][AddrHi][AddrLo][CountHi][CountLo][CRCLo][CRCHi])
            byte[] request = new byte[8];
            request[0] = slaveId;
            request[1] = 0x03;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), count);

            ushort requestCrc = Crc16Modbus.Compute(request.AsSpan(0, 6));
            request[6] = (byte)(requestCrc & 0xFF);        // CRC Low byte first
            request[7] = (byte)((requestCrc >> 8) & 0xFF); // CRC High byte second

            await _transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            // 2. Deterministic Read: Đọc trước 3 bytes header [Slave][Func][ByteCountOrExCode]
            byte[] header = new byte[3];
            await ReadExactAsync(header, cancellationToken).ConfigureAwait(false);

            // Kiểm tra phản hồi lỗi Modbus (FC | 0x80)
            if (header[1] == (0x03 | 0x80))
            {
                byte[] exCrc = new byte[2];
                await ReadExactAsync(exCrc, cancellationToken).ConfigureAwait(false);

                byte[] fullExFrame = new byte[5];
                header.CopyTo(fullExFrame, 0);
                exCrc.CopyTo(fullExFrame, 3);

                ValidateCrc(fullExFrame);
                throw new ModbusProtocolException(header[0], 0x03, (ModbusExceptionCode)header[2]);
            }

            if (header[0] != slaveId || header[1] != 0x03)
            {
                throw new InvalidDataException(
                    $"Unexpected Modbus response header: Slave=0x{header[0]:X2} (expected 0x{slaveId:X2}), FC=0x{header[1]:X2} (expected 0x03).");
            }

            int expectedDataBytes = count * 2;
            int actualDataBytes = header[2];
            if (actualDataBytes != expectedDataBytes)
            {
                throw new InvalidDataException(
                    $"Modbus FC03 byte count mismatch: expected {expectedDataBytes} bytes, got {actualDataBytes} bytes.");
            }

            // 3. Đọc dữ liệu + 2 bytes CRC (xử lý an toàn khi USB CDC cắt thành các gói 64-byte)
            int remainingBytes = actualDataBytes + 2;
            byte[] restOfFrame = new byte[remainingBytes];
            await ReadExactAsync(restOfFrame, cancellationToken).ConfigureAwait(false);

            // 4. Lắp ráp toàn bộ khung để thẩm định CRC-16
            byte[] fullFrame = new byte[3 + remainingBytes];
            header.CopyTo(fullFrame, 0);
            restOfFrame.CopyTo(fullFrame, 3);

            ValidateCrc(fullFrame);

            // 5. Giải mã Big-Endian registers
            ushort[] result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = BinaryPrimitives.ReadUInt16BigEndian(fullFrame.AsSpan(3 + i * 2, 2));
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteSingleRegisterAsync(
        byte slaveId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _transport.DiscardBuffers();

            // Request FC06: 8 bytes [Slave][0x06][AddrHi][AddrLo][ValHi][ValLo][CRCLo][CRCHi]
            byte[] request = new byte[8];
            request[0] = slaveId;
            request[1] = 0x06;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), value);

            ushort requestCrc = Crc16Modbus.Compute(request.AsSpan(0, 6));
            request[6] = (byte)(requestCrc & 0xFF);
            request[7] = (byte)((requestCrc >> 8) & 0xFF);

            await _transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            // Đọc 3 bytes header
            byte[] header = new byte[3];
            await ReadExactAsync(header, cancellationToken).ConfigureAwait(false);

            if (header[1] == (0x06 | 0x80))
            {
                byte[] exCrc = new byte[2];
                await ReadExactAsync(exCrc, cancellationToken).ConfigureAwait(false);

                byte[] fullExFrame = new byte[5];
                header.CopyTo(fullExFrame, 0);
                exCrc.CopyTo(fullExFrame, 3);

                ValidateCrc(fullExFrame);
                throw new ModbusProtocolException(header[0], 0x06, (ModbusExceptionCode)header[2]);
            }

            // Normal echo response là 8 bytes
            byte[] restOfFrame = new byte[5]; // AddrLo, ValHi, ValLo, CRCLo, CRCHi
            await ReadExactAsync(restOfFrame, cancellationToken).ConfigureAwait(false);

            byte[] fullFrame = new byte[8];
            header.CopyTo(fullFrame, 0);
            restOfFrame.CopyTo(fullFrame, 3);

            ValidateCrc(fullFrame);

            // Kiểm tra echo khớp với request
            ushort echoAddr = BinaryPrimitives.ReadUInt16BigEndian(fullFrame.AsSpan(2, 2));
            ushort echoVal = BinaryPrimitives.ReadUInt16BigEndian(fullFrame.AsSpan(4, 2));
            if (header[0] != slaveId || header[1] != 0x06 || echoAddr != address || echoVal != value)
            {
                throw new InvalidDataException("Modbus FC06 response echo did not match the request.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteMultipleRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (values.Length == 0 || values.Length > 123)
            throw new ArgumentOutOfRangeException(nameof(values), "Modbus FC16 values length must be between 1 and 123.");

        ushort count = (ushort)values.Length;
        byte byteCount = (byte)(count * 2);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] request = BuildWriteMultipleRegistersRequest(slaveId, startAddress, values);
            await _transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            // Đọc 3 bytes header
            byte[] header = new byte[3];
            await ReadExactAsync(header, cancellationToken).ConfigureAwait(false);

            if (header[1] == (0x10 | 0x80))
            {
                byte[] exCrc = new byte[2];
                await ReadExactAsync(exCrc, cancellationToken).ConfigureAwait(false);

                byte[] fullExFrame = new byte[5];
                header.CopyTo(fullExFrame, 0);
                exCrc.CopyTo(fullExFrame, 3);

                ValidateCrc(fullExFrame);
                throw new ModbusProtocolException(header[0], 0x10, (ModbusExceptionCode)header[2]);
            }

            // Normal FC16 response là 8 bytes: [Slave][0x10][AddrHi][AddrLo][CountHi][CountLo][CRCLo][CRCHi]
            byte[] restOfFrame = new byte[5];
            await ReadExactAsync(restOfFrame, cancellationToken).ConfigureAwait(false);

            byte[] fullFrame = new byte[8];
            header.CopyTo(fullFrame, 0);
            restOfFrame.CopyTo(fullFrame, 3);

            ValidateCrc(fullFrame);

            ushort echoAddr = BinaryPrimitives.ReadUInt16BigEndian(fullFrame.AsSpan(2, 2));
            ushort echoCount = BinaryPrimitives.ReadUInt16BigEndian(fullFrame.AsSpan(4, 2));
            if (header[0] != slaveId || header[1] != 0x10 || echoAddr != startAddress || echoCount != count)
            {
                throw new InvalidDataException("Modbus FC16 response parameters did not match the request.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ReadExactAsync(Memory<byte> target, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < target.Length)
        {
            int read = await _transport.ReadAsync(target.Slice(totalRead), ct).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new TimeoutException(
                    $"Timeout or connection closed while reading from USB CDC. Expected {target.Length} bytes, received {totalRead} bytes.");
            }
            totalRead += read;
        }
    }

    private static byte[] BuildWriteMultipleRegistersRequest(
        byte slaveId,
        ushort startAddress,
        ReadOnlyMemory<ushort> values)
    {
        ushort count = (ushort)values.Length;
        byte byteCount = (byte)(count * 2);
        int requestLength = 7 + byteCount + 2;
        byte[] request = new byte[requestLength];
        request[0] = slaveId;
        request[1] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), count);
        request[6] = byteCount;

        var spanValues = values.Span;
        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(7 + i * 2, 2), spanValues[i]);
        }

        ushort requestCrc = Crc16Modbus.Compute(request.AsSpan(0, 7 + byteCount));
        request[7 + byteCount] = (byte)(requestCrc & 0xFF);
        request[7 + byteCount + 1] = (byte)((requestCrc >> 8) & 0xFF);

        return request;
    }

    private static void ValidateCrc(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3)
            throw new InvalidDataException("Frame too short for Modbus CRC validation.");

        int payloadLength = frame.Length - 2;
        ushort computedCrc = Crc16Modbus.Compute(frame.Slice(0, payloadLength));
        ushort receivedCrc = (ushort)(frame[payloadLength] | (frame[payloadLength + 1] << 8));

        if (computedCrc != receivedCrc)
        {
            throw new InvalidDataException(
                $"Modbus CRC-16 mismatch: received 0x{receivedCrc:X4}, computed 0x{computedCrc:X4}.");
        }
    }

    private void EnsureConnected()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ModbusRtuClient));

        if (!_transport.IsOpen)
            throw new InvalidOperationException("USB CDC transport is not open.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        await _transport.DisposeAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
