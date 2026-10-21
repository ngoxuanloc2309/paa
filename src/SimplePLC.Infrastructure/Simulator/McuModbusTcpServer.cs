using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Máy chủ Modbus TCP Slave mô phỏng MCU STM32 / Smart PLC qua mạng IP.
/// Tiếp nhận kết nối Socket trên cổng chuẩn (mặc định 502), giải mã MBAP Header 7 bytes,
/// điều phối tới McuReferenceSimulator và xuất ra phản hồi Modbus TCP không có CRC-16.
/// </summary>
public sealed class McuModbusTcpServer : IAsyncDisposable
{
    private readonly McuReferenceSimulator _simulator;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isDisposed;

    public McuReferenceSimulator Simulator => _simulator;
    public int Port => _port;
    public bool IsRunning => _listener != null;

    public event Action<byte[]>? FrameReceived;
    public event Action<byte[]>? FrameSent;
    public event Action<string>? LogMessage;

    public McuModbusTcpServer(McuReferenceSimulator? simulator = null, int port = 502)
    {
        _simulator = simulator ?? new McuReferenceSimulator();
        _port = port;
    }

    /// <summary>
    /// Khởi động lắng nghe kết nối Modbus TCP từ các trạm client.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => AcceptClientsLoopAsync(_cts.Token));

        LogMessage?.Invoke($"[MCU TCP] Modbus TCP server listening on port {_port}...");
    }

    /// <summary>
    /// Dừng máy chủ lắng nghe kết nối.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            if (_listenerTask != null)
            {
                await _listenerTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // Bỏ qua lỗi dừng listener
        }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _listenerTask = null;
        }

        LogMessage?.Invoke("[MCU TCP] Modbus TCP server stopped.");
    }

    private async Task AcceptClientsLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                client.NoDelay = true;
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[MCU TCP] Accept client error: {ex.Message}");
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var prefixBuffer = new byte[6];

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // 1. Đọc 6 bytes MBAP prefix (TxId 2B, ProtoId 2B, RemainingLen 2B)
                    if (!await TryReadExactAsync(stream, prefixBuffer, ct).ConfigureAwait(false))
                        break;

                    ushort txId = BinaryPrimitives.ReadUInt16BigEndian(prefixBuffer.AsSpan(0, 2));
                    ushort protoId = BinaryPrimitives.ReadUInt16BigEndian(prefixBuffer.AsSpan(2, 2));
                    ushort remainingLen = BinaryPrimitives.ReadUInt16BigEndian(prefixBuffer.AsSpan(4, 2));

                    if (protoId != 0x0000 || remainingLen < 2)
                    {
                        LogMessage?.Invoke($"[MCU TCP] Invalid protocol {protoId} or length {remainingLen}. Disconnecting.");
                        break;
                    }

                    // 2. Đọc remainingLen bytes (UnitId 1B + PDU)
                    var payloadBuffer = new byte[remainingLen];
                    if (!await TryReadExactAsync(stream, payloadBuffer, ct).ConfigureAwait(false))
                        break;

                    byte unitId = payloadBuffer[0];
                    byte funcCode = payloadBuffer[1];

                    // Thông báo event FrameReceived (ghép full frame MBAP + Payload)
                    byte[] fullRx = new byte[6 + remainingLen];
                    prefixBuffer.CopyTo(fullRx, 0);
                    payloadBuffer.CopyTo(fullRx, 6);
                    FrameReceived?.Invoke(fullRx);

                    // 3. Xử lý PDU qua Simulator
                    byte[] responsePayload = funcCode switch
                    {
                        0x03 => await HandleReadHoldingRegistersAsync(unitId, payloadBuffer, ct).ConfigureAwait(false),
                        0x06 => await HandleWriteSingleRegisterAsync(unitId, payloadBuffer, ct).ConfigureAwait(false),
                        0x10 => await HandleWriteMultipleRegistersAsync(unitId, payloadBuffer, ct).ConfigureAwait(false),
                        _ => BuildExceptionPayload(funcCode, ModbusExceptionCode.IllegalFunction)
                    };

                    // 4. Lắp ráp MBAP Response:
                    // MBAP: [TxHi][TxLo][0x00][0x00][LenHi][LenLo][UnitId]
                    // Response: MBAP (6 bytes) + [UnitId] + responsePayload
                    ushort respLen = (ushort)(1 + responsePayload.Length);
                    byte[] response = new byte[6 + 1 + responsePayload.Length];

                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), txId);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), 0x0000);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), respLen);
                    response[6] = unitId;
                    responsePayload.CopyTo(response, 7);

                    await stream.WriteAsync(response.AsMemory(0, response.Length), ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);

                    FrameSent?.Invoke(response);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[MCU TCP] Client handle error: {ex.Message}");
                    break;
                }
            }
        }
    }

    private async Task<byte[]> HandleReadHoldingRegistersAsync(byte unitId, byte[] payload, CancellationToken ct)
    {
        if (payload.Length != 6) // UnitId(1) + FC(1) + Addr(2) + Count(2) = 6 bytes
            return BuildExceptionPayload(0x03, ModbusExceptionCode.IllegalDataValue);

        ushort startAddress = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2, 2));
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2));

        if (count == 0 || count > 125)
            return BuildExceptionPayload(0x03, ModbusExceptionCode.IllegalDataValue);

        try
        {
            ushort[] registers = await _simulator.ReadHoldingRegistersAsync(unitId, startAddress, count, ct).ConfigureAwait(false);
            int byteCount = count * 2;
            byte[] resp = new byte[2 + byteCount]; // FC(1) + ByteCount(1) + Data(2*N)
            resp[0] = 0x03;
            resp[1] = (byte)byteCount;

            for (int i = 0; i < count; i++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2 + i * 2, 2), registers[i]);
            }

            return resp;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BuildExceptionPayload(0x03, ModbusExceptionCode.IllegalDataAddress);
        }
        catch
        {
            return BuildExceptionPayload(0x03, ModbusExceptionCode.SlaveDeviceFailure);
        }
    }

    private async Task<byte[]> HandleWriteSingleRegisterAsync(byte unitId, byte[] payload, CancellationToken ct)
    {
        if (payload.Length != 6) // UnitId(1) + FC(1) + Addr(2) + Val(2) = 6 bytes
            return BuildExceptionPayload(0x06, ModbusExceptionCode.IllegalDataValue);

        ushort address = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2, 2));
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2));

        try
        {
            await _simulator.WriteSingleRegisterAsync(unitId, address, value, ct).ConfigureAwait(false);
            // FC06 Echo response: [0x06][AddrHi][AddrLo][ValHi][ValLo] (5 bytes)
            byte[] resp = new byte[5];
            payload.AsSpan(1, 5).CopyTo(resp);
            return resp;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BuildExceptionPayload(0x06, ModbusExceptionCode.IllegalDataAddress);
        }
        catch
        {
            return BuildExceptionPayload(0x06, ModbusExceptionCode.SlaveDeviceFailure);
        }
    }

    private async Task<byte[]> HandleWriteMultipleRegistersAsync(byte unitId, byte[] payload, CancellationToken ct)
    {
        if (payload.Length < 7)
            return BuildExceptionPayload(0x10, ModbusExceptionCode.IllegalDataValue);

        ushort startAddress = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2, 2));
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2));
        byte byteCount = payload[6];

        if (count == 0 || count > 123 || byteCount != count * 2 || payload.Length != 7 + byteCount)
            return BuildExceptionPayload(0x10, ModbusExceptionCode.IllegalDataValue);

        ushort[] values = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(7 + i * 2, 2));
        }

        try
        {
            await _simulator.WriteMultipleRegistersAsync(unitId, startAddress, values, ct).ConfigureAwait(false);
            // FC16 Echo response: [0x10][AddrHi][AddrLo][CountHi][CountLo] (5 bytes)
            byte[] resp = new byte[5];
            resp[0] = 0x10;
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(1, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(3, 2), count);
            return resp;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BuildExceptionPayload(0x10, ModbusExceptionCode.IllegalDataAddress);
        }
        catch
        {
            return BuildExceptionPayload(0x10, ModbusExceptionCode.SlaveDeviceFailure);
        }
    }

    private static byte[] BuildExceptionPayload(byte funcCode, ModbusExceptionCode code)
    {
        return new byte[] { (byte)(funcCode | 0x80), (byte)code };
    }

    private static async Task<bool> TryReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            ct.ThrowIfCancellationRequested();
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (read == 0) return false;
            totalRead += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
