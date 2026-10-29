using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Trình điều khiển giao thức Modbus TCP qua tầng mạng Ethernet / Wi-Fi (RFC / Modbus Application Protocol Specification V1.1b).
/// Sử dụng MBAP Header 7 bytes (Transaction ID, Protocol ID 0x0000, Length, Unit ID).
/// Không sử dụng CRC-16 (tầng TCP/IP checksum và Ethernet FCS đảm bảo toàn vẹn dữ liệu).
/// Hiện thực Deterministic Stream Reading an toàn, chống phân mảnh gói mạng và bảo đảm thread-safety.
/// </summary>
public sealed class ModbusTcpClient : IModbusClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _transactionId;
    private bool _isDisposed;

    public string Host => _host;
    public int Port => _port;
    public int TimeoutMs => _timeoutMs;

    public bool IsConnected => !_isDisposed && _tcpClient != null && _tcpClient.Connected;

    public ModbusTcpClient(string host, int port = 502, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentNullException(nameof(host));
        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "TCP port must be between 1 and 65535.");

        _host = host.Trim();
        _port = port;
        _timeoutMs = timeoutMs > 0 ? timeoutMs : 3000;
    }

    /// <summary>
    /// Thiết lập kết nối TCP Socket tới vi điều khiển / thiết bị PLC.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;

        var client = new TcpClient
        {
            NoDelay = true, // Tắt thuật toán Nagle để đạt độ trễ thời gian thực thấp nhất
            SendTimeout = _timeoutMs,
            ReceiveTimeout = _timeoutMs
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeoutMs);

        try
        {
            await client.ConnectAsync(_host, _port, cts.Token).ConfigureAwait(false);
            _tcpClient = client;
            _stream = client.GetStream();
        }
        catch
        {
            client.Dispose();
            throw;
        }
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
            ushort txId = unchecked(++_transactionId);

            // 1. Chuẩn bị Request: 7 bytes MBAP + 5 bytes PDU = 12 bytes
            // MBAP: [TxHi][TxLo][0x00][0x00][LenHi][LenLo][UnitId]
            // PDU:  [0x03][AddrHi][AddrLo][CountHi][CountLo]
            byte[] request = new byte[12];
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0x0000); // Protocol ID: Modbus
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6);      // Length: 1 (UnitId) + 5 (PDU)
            request[6] = slaveId;
            request[7] = 0x03; // FC03
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), count);

            await _stream!.WriteAsync(request.AsMemory(0, request.Length), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // 2. Deterministic Read MBAP Header (6 bytes: TxId, ProtoId, Length)
            byte[] mbapPrefix = new byte[6];
            await ReadExactAsync(mbapPrefix, cancellationToken).ConfigureAwait(false);

            ushort rxTxId = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(0, 2));
            ushort rxProto = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(2, 2));
            ushort rxRemainingLen = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(4, 2));

            if (rxTxId != txId)
            {
                throw new InvalidDataException($"Modbus TCP transaction ID mismatch: expected {txId}, got {rxTxId}.");
            }
            if (rxProto != 0x0000)
            {
                throw new InvalidDataException($"Invalid Modbus TCP protocol identifier: 0x{rxProto:X04} (expected 0x0000).");
            }
            if (rxRemainingLen < 2)
            {
                throw new InvalidDataException($"Modbus TCP response payload length too short: {rxRemainingLen} bytes.");
            }

            // 3. Đọc rxRemainingLen bytes còn lại (Unit ID + PDU)
            byte[] remaining = new byte[rxRemainingLen];
            await ReadExactAsync(remaining, cancellationToken).ConfigureAwait(false);

            byte rxUnitId = remaining[0];
            byte rxFc = remaining[1];

            // Kiểm tra phản hồi lỗi Modbus (FC | 0x80)
            if (rxFc == (0x03 | 0x80))
            {
                byte exCode = remaining.Length > 2 ? remaining[2] : (byte)0x04;
                throw new ModbusProtocolException(rxUnitId, 0x03, (ModbusExceptionCode)exCode);
            }

            if (rxFc != 0x03)
            {
                throw new InvalidDataException($"Unexpected Modbus function code in TCP response: 0x{rxFc:X02} (expected 0x03).");
            }

            int expectedDataBytes = count * 2;
            int actualDataBytes = remaining[2];
            if (actualDataBytes != expectedDataBytes || remaining.Length < 3 + expectedDataBytes)
            {
                throw new InvalidDataException($"Modbus FC03 byte count mismatch: expected {expectedDataBytes} bytes, got {actualDataBytes} bytes.");
            }

            // 4. Giải mã thanh ghi Big-Endian
            ushort[] result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(3 + i * 2, 2));
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
            ushort txId = unchecked(++_transactionId);

            // Request FC06: 7 bytes MBAP + 5 bytes PDU = 12 bytes
            // PDU: [0x06][AddrHi][AddrLo][ValHi][ValLo]
            byte[] request = new byte[12];
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0x0000);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6); // Length = 6 (UnitId + PDU)
            request[6] = slaveId;
            request[7] = 0x06;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), value);

            await _stream!.WriteAsync(request.AsMemory(0, request.Length), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Đọc 6 bytes MBAP prefix
            byte[] mbapPrefix = new byte[6];
            await ReadExactAsync(mbapPrefix, cancellationToken).ConfigureAwait(false);

            ushort rxTxId = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(0, 2));
            ushort rxRemainingLen = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(4, 2));

            if (rxTxId != txId)
            {
                throw new InvalidDataException($"Modbus TCP transaction ID mismatch: expected {txId}, got {rxTxId}.");
            }

            byte[] remaining = new byte[rxRemainingLen];
            await ReadExactAsync(remaining, cancellationToken).ConfigureAwait(false);

            byte rxUnitId = remaining[0];
            byte rxFc = remaining[1];

            if (rxFc == (0x06 | 0x80))
            {
                byte exCode = remaining.Length > 2 ? remaining[2] : (byte)0x04;
                throw new ModbusProtocolException(rxUnitId, 0x06, (ModbusExceptionCode)exCode);
            }

            if (rxFc != 0x06)
            {
                throw new InvalidDataException($"Unexpected Modbus function code in TCP response: 0x{rxFc:X02} (expected 0x06).");
            }

            ushort echoAddr = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(2, 2));
            ushort echoVal = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(4, 2));
            if (echoAddr != address || echoVal != value)
            {
                throw new InvalidDataException("Modbus FC06 TCP response echo did not match the request.");
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
            throw new ArgumentOutOfRangeException(nameof(values), "Modbus FC16 register count must be between 1 and 123.");

        ushort count = (ushort)values.Length;
        byte byteCount = (byte)(count * 2);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ushort txId = unchecked(++_transactionId);

            // Request FC16: 7 bytes MBAP + 6 bytes PDU Header + 2*N data bytes
            // Total bytes = 7 + 6 + 2*N = 13 + 2*N
            // MBAP Length = 1 (UnitId) + 6 (FC16 + Addr 2B + Count 2B + ByteCount 1B) + 2*N = 7 + 2*N
            int totalBytes = 13 + byteCount;
            byte[] request = new byte[totalBytes];

            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0x0000);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), (ushort)(7 + byteCount));
            request[6] = slaveId;
            request[7] = 0x10; // FC16
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), count);
            request[12] = byteCount;

            CopyValuesToPayload(request, values);

            await _stream!.WriteAsync(request.AsMemory(0, request.Length), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Đọc 6 bytes MBAP prefix
            byte[] mbapPrefix = new byte[6];
            await ReadExactAsync(mbapPrefix, cancellationToken).ConfigureAwait(false);

            ushort rxTxId = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(0, 2));
            ushort rxRemainingLen = BinaryPrimitives.ReadUInt16BigEndian(mbapPrefix.AsSpan(4, 2));

            if (rxTxId != txId)
            {
                throw new InvalidDataException($"Modbus TCP transaction ID mismatch: expected {txId}, got {rxTxId}.");
            }

            byte[] remaining = new byte[rxRemainingLen];
            await ReadExactAsync(remaining, cancellationToken).ConfigureAwait(false);

            byte rxUnitId = remaining[0];
            byte rxFc = remaining[1];

            if (rxFc == (0x10 | 0x80))
            {
                byte exCode = remaining.Length > 2 ? remaining[2] : (byte)0x04;
                throw new ModbusProtocolException(rxUnitId, 0x10, (ModbusExceptionCode)exCode);
            }

            if (rxFc != 0x10)
            {
                throw new InvalidDataException($"Unexpected Modbus function code in TCP response: 0x{rxFc:X02} (expected 0x10).");
            }

            ushort echoAddr = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(2, 2));
            ushort echoCount = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(4, 2));
            if (echoAddr != startAddress || echoCount != count)
            {
                throw new InvalidDataException("Modbus FC16 TCP response echo did not match the request.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void CopyValuesToPayload(byte[] request, ReadOnlyMemory<ushort> values)
    {
        var span = values.Span;
        for (int i = 0; i < span.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(13 + i * 2, 2), span[i]);
        }
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int bytesReadTotal = 0;
        while (bytesReadTotal < buffer.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = await _stream!.ReadAsync(
                buffer.AsMemory(bytesReadTotal, buffer.Length - bytesReadTotal),
                cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException($"Modbus TCP connection closed by peer while reading (expected {buffer.Length} bytes, received {bytesReadTotal} bytes).");
            }

            bytesReadTotal += read;
        }
    }

    private void EnsureConnected()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ModbusTcpClient));
        if (!IsConnected || _stream == null)
            throw new InvalidOperationException($"Modbus TCP client is not connected to {_host}:{_port}.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_stream != null)
        {
            try { await _stream.DisposeAsync().ConfigureAwait(false); } catch { /* ARCH_ALLOW_SWALLOW: Teardown cleanup */ }
            _stream = null;
        }

        if (_tcpClient != null)
        {
            try { _tcpClient.Close(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown cleanup */ }
            try { _tcpClient.Dispose(); } catch { /* ARCH_ALLOW_SWALLOW: Teardown cleanup */ }
            _tcpClient = null;
        }

        _lock.Dispose();
    }
}
