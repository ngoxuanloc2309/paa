using System.Buffers.Binary;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Máy chủ Modbus RTU Slave mô phỏng MCU STM32.
/// Tiếp nhận raw byte stream giao thức Modbus RTU, kiểm tra CRC-16,
/// điều phối tới McuReferenceSimulator và xuất ra phản hồi chuẩn Modbus RTU.
/// </summary>
public sealed class McuModbusRtuServer
{
    private readonly McuReferenceSimulator _simulator;
    private readonly byte _slaveId;

    public McuReferenceSimulator Simulator => _simulator;
    public byte SlaveId => _slaveId;

    public event Action<byte[]>? FrameReceived;
    public event Action<byte[]>? FrameSent;
    public event Action<string>? LogMessage;

    public McuModbusRtuServer(McuReferenceSimulator? simulator = null, byte slaveId = 1)
    {
        _simulator = simulator ?? new McuReferenceSimulator();
        _slaveId = slaveId;
    }

    /// <summary>
    /// Xử lý một khung dữ liệu Modbus RTU hoàn chỉnh dạng byte, trả về khung byte phản hồi.
    /// Trả về mảng byte rỗng nếu khung bị lỗi CRC hoặc không thuộc về Slave ID này.
    /// </summary>
    public async Task<byte[]> ProcessFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        if (frame.Length < 4)
        {
            LogMessage?.Invoke($"[MCU] Frame too short ({frame.Length} bytes). Ignored.");
            return Array.Empty<byte>();
        }

        // 1. Kiểm tra CRC và bóc tách header
        if (!ValidateIncomingCrc(frame, out byte rxSlave, out byte rxFunc))
        {
            LogMessage?.Invoke("[MCU] CRC mismatch or invalid frame. Dropping frame.");
            return Array.Empty<byte>();
        }

        // Bỏ qua nếu Slave ID không khớp (trừ broadcast 0x00)
        if (rxSlave != _slaveId && rxSlave != 0)
        {
            return Array.Empty<byte>();
        }

        FrameReceived?.Invoke(frame.ToArray());

        try
        {
            byte[] response = rxFunc switch
            {
                0x03 => await HandleReadHoldingRegistersAsync(rxSlave, frame, ct).ConfigureAwait(false),
                0x06 => await HandleWriteSingleRegisterAsync(rxSlave, frame, ct).ConfigureAwait(false),
                0x10 => await HandleWriteMultipleRegistersAsync(rxSlave, frame, ct).ConfigureAwait(false),
                _ => BuildExceptionResponse(rxSlave, rxFunc, ModbusExceptionCode.IllegalFunction)
            };

            if (response.Length > 0)
            {
                FrameSent?.Invoke(response);
            }

            return response;
        }
        catch (ArgumentOutOfRangeException)
        {
            return BuildExceptionResponse(rxSlave, rxFunc, ModbusExceptionCode.IllegalDataAddress);
        }
        catch (InvalidOperationException)
        {
            return BuildExceptionResponse(rxSlave, rxFunc, ModbusExceptionCode.IllegalDataValue);
        }
        catch (TimeoutException)
        {
            return BuildExceptionResponse(rxSlave, rxFunc, ModbusExceptionCode.SlaveDeviceBusy);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[MCU] Exception during FC0x{rxFunc:X2} handling: {ex.Message}");
            return BuildExceptionResponse(rxSlave, rxFunc, ModbusExceptionCode.SlaveDeviceFailure);
        }
    }

    private static bool ValidateIncomingCrc(ReadOnlyMemory<byte> frame, out byte slaveId, out byte funcCode)
    {
        var span = frame.Span;
        slaveId = span[0];
        funcCode = span[1];

        ushort computedCrc = Crc16Modbus.Compute(span[..^2]);
        ushort receivedCrc = (ushort)(span[^2] | (span[^1] << 8));
        return computedCrc == receivedCrc;
    }

    private async Task<byte[]> HandleReadHoldingRegistersAsync(byte slaveId, ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (frame.Length != 8)
            return BuildExceptionResponse(slaveId, 0x03, ModbusExceptionCode.IllegalDataValue);

        if (!TryParseFc03Request(frame, out ushort startAddress, out ushort count))
            return BuildExceptionResponse(slaveId, 0x03, ModbusExceptionCode.IllegalDataValue);

        ushort[] registers = await _simulator.ReadHoldingRegistersAsync(slaveId, startAddress, count, ct).ConfigureAwait(false);
        return BuildFc03Response(slaveId, count, registers);
    }

    private static bool TryParseFc03Request(ReadOnlyMemory<byte> frame, out ushort startAddress, out ushort count)
    {
        var span = frame.Span;
        startAddress = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        count = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4, 2));
        return count > 0 && count <= 125;
    }

    private static byte[] BuildFc03Response(byte slaveId, ushort count, ushort[] registers)
    {
        int byteCount = count * 2;
        byte[] response = new byte[3 + byteCount + 2];
        response[0] = slaveId;
        response[1] = 0x03;
        response[2] = (byte)byteCount;

        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(3 + i * 2, 2), registers[i]);
        }

        AppendCrc(response);
        return response;
    }

    private async Task<byte[]> HandleWriteSingleRegisterAsync(byte slaveId, ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (frame.Length != 8)
            return BuildExceptionResponse(slaveId, 0x06, ModbusExceptionCode.IllegalDataValue);

        ParseFc06Request(frame, out ushort address, out ushort value);
        await _simulator.WriteSingleRegisterAsync(slaveId, address, value, ct).ConfigureAwait(false);

        return BuildFc06Response(frame);
    }

    private static void ParseFc06Request(ReadOnlyMemory<byte> frame, out ushort address, out ushort value)
    {
        var span = frame.Span;
        address = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        value = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4, 2));
    }

    private static byte[] BuildFc06Response(ReadOnlyMemory<byte> frame)
    {
        byte[] response = new byte[8];
        frame.Span[..6].CopyTo(response);
        AppendCrc(response);
        return response;
    }

    private async Task<byte[]> HandleWriteMultipleRegistersAsync(byte slaveId, ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (frame.Length < 9)
            return BuildExceptionResponse(slaveId, 0x10, ModbusExceptionCode.IllegalDataValue);

        if (!TryParseFc16Request(frame, out ushort startAddress, out ushort count, out ushort[] values))
            return BuildExceptionResponse(slaveId, 0x10, ModbusExceptionCode.IllegalDataValue);

        await _simulator.WriteMultipleRegistersAsync(slaveId, startAddress, values, ct).ConfigureAwait(false);
        return BuildFc16Response(slaveId, startAddress, count);
    }

    private static bool TryParseFc16Request(ReadOnlyMemory<byte> frame, out ushort startAddress, out ushort count, out ushort[] values)
    {
        var span = frame.Span;
        startAddress = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        count = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4, 2));
        byte byteCount = span[6];

        if (count == 0 || count > 123 || byteCount != count * 2 || frame.Length != 7 + byteCount + 2)
        {
            values = Array.Empty<ushort>();
            return false;
        }

        values = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(7 + i * 2, 2));
        }
        return true;
    }

    private static byte[] BuildFc16Response(byte slaveId, ushort startAddress, ushort count)
    {
        byte[] response = new byte[8];
        response[0] = slaveId;
        response[1] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), count);
        AppendCrc(response);
        return response;
    }

    private static byte[] BuildExceptionResponse(byte slaveId, byte funcCode, ModbusExceptionCode exceptionCode)
    {
        byte[] response = new byte[5];
        response[0] = slaveId;
        response[1] = (byte)(funcCode | 0x80);
        response[2] = (byte)exceptionCode;
        AppendCrc(response);
        return response;
    }

    private static void AppendCrc(Span<byte> frame)
    {
        ushort crc = Crc16Modbus.Compute(frame[..^2]);
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)((crc >> 8) & 0xFF);
    }

    /// <summary>
    /// Vòng lặp liên tục đọc khung Modbus RTU từ luồng vào (InputStream) và phản hồi qua luồng ra (OutputStream).
    /// </summary>
    public async Task RunAsync(Stream inputStream, Stream outputStream, CancellationToken ct)
    {
        byte[] headerBuffer = new byte[7];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                byte[] singleByte = new byte[1];
                int read = await inputStream.ReadAsync(singleByte.AsMemory(0, 1), ct).ConfigureAwait(false);
                if (read <= 0)
                {
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    continue;
                }

                headerBuffer[0] = singleByte[0];

                read = await inputStream.ReadAsync(singleByte.AsMemory(0, 1), ct).ConfigureAwait(false);
                if (read <= 0)
                    continue;

                headerBuffer[1] = singleByte[0];
                byte funcByte = headerBuffer[1];

                byte[] fullRequest;

                if (funcByte == 0x03 || funcByte == 0x06)
                {
                    byte[] rest = new byte[6];
                    await ReadExactAsync(inputStream, rest, ct).ConfigureAwait(false);

                    fullRequest = new byte[8];
                    fullRequest[0] = headerBuffer[0];
                    fullRequest[1] = headerBuffer[1];
                    rest.CopyTo(fullRequest, 2);
                }
                else if (funcByte == 0x10)
                {
                    byte[] fc16Meta = new byte[5];
                    await ReadExactAsync(inputStream, fc16Meta, ct).ConfigureAwait(false);

                    byte byteCount = fc16Meta[4];
                    byte[] payloadAndCrc = new byte[byteCount + 2];
                    await ReadExactAsync(inputStream, payloadAndCrc, ct).ConfigureAwait(false);

                    fullRequest = new byte[7 + byteCount + 2];
                    fullRequest[0] = headerBuffer[0];
                    fullRequest[1] = headerBuffer[1];
                    fc16Meta.CopyTo(fullRequest, 2);
                    payloadAndCrc.CopyTo(fullRequest, 7);
                }
                else
                {
                    continue;
                }

                byte[] response = await ProcessFrameAsync(fullRequest, ct).ConfigureAwait(false);
                if (response.Length > 0)
                {
                    await outputStream.WriteAsync(response, ct).ConfigureAwait(false);
                    await outputStream.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[MCU RunLoop] Error: {ex.Message}");
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int readTotal = 0;
        while (readTotal < buffer.Length)
        {
            int r = await stream.ReadAsync(buffer.Slice(readTotal), ct).ConfigureAwait(false);
            if (r <= 0)
            {
                await Task.Delay(5, ct).ConfigureAwait(false);
                continue;
            }
            readTotal += r;
        }
    }
}
