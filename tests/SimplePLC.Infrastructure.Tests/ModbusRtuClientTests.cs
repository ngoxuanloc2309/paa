using System.Buffers.Binary;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;

namespace SimplePLC.Infrastructure.Tests;

public class ModbusRtuClientTests
{
    private readonly FakeUsbCdcTransport _transport;
    private readonly ModbusRtuClient _client;

    public ModbusRtuClientTests()
    {
        _transport = new FakeUsbCdcTransport { IsOpen = true };
        _client = new ModbusRtuClient(_transport);
    }

    [Fact]
    public async Task ReadHoldingRegisters_Success_EncodesRequestAndDecodesResponse()
    {
        // Arrange: MCU trả về 2 thanh ghi [0x1234, 0x5678]
        // Frame: [Slave=1][FC=3][Bytes=4][0x12, 0x34][0x56, 0x78][CRCLo, CRCHi]
        byte[] payload = { 0x01, 0x03, 0x04, 0x12, 0x34, 0x56, 0x78 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = new byte[payload.Length + 2];
        payload.CopyTo(response, 0);
        response[^2] = (byte)(crc & 0xFF);
        response[^1] = (byte)((crc >> 8) & 0xFF);

        _transport.EnqueueResponse(response);

        // Act
        var registers = await _client.ReadHoldingRegistersAsync(1, 0x0100, 2);

        // Assert
        Assert.Equal(2, registers.Length);
        Assert.Equal(0x1234, registers[0]);
        Assert.Equal(0x5678, registers[1]);

        // Kiểm tra request gửi đi đúng chuẩn Modbus RTU FC03
        Assert.Single(_transport.SentRequests);
        var request = _transport.SentRequests[0];
        Assert.Equal(8, request.Length);
        Assert.Equal(1, request[0]); // Slave
        Assert.Equal(3, request[1]); // FC03
        Assert.Equal(0x0100, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(2, 2))); // Addr
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(4, 2))); // Count
    }

    [Fact]
    public async Task ReadHoldingRegisters_64ByteChunking_HandlesTinyUsbPacketSlicing()
    {
        // Arrange: Đọc 50 thanh ghi (100 bytes data, tổng frame = 3 + 100 + 2 = 105 bytes)
        // Transport xé thành các gói tối đa 64 bytes (mô phỏng TinyUSB CDC FS endpoint)
        _transport.SimulateChunkSize = 64;

        ushort[] expected = new ushort[50];
        byte[] payload = new byte[3 + 100];
        payload[0] = 0x01; // SlaveId
        payload[1] = 0x03; // FC03
        payload[2] = 100;  // ByteCount
        for (int i = 0; i < 50; i++)
        {
            expected[i] = (ushort)(1000 + i);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3 + i * 2, 2), expected[i]);
        }

        ushort crc = Crc16Modbus.Compute(payload);
        byte[] fullResponse = new byte[payload.Length + 2];
        payload.CopyTo(fullResponse, 0);
        fullResponse[^2] = (byte)(crc & 0xFF);
        fullResponse[^1] = (byte)((crc >> 8) & 0xFF);

        _transport.EnqueueResponse(fullResponse);

        // Act
        var result = await _client.ReadHoldingRegistersAsync(1, 0x0000, 50);

        // Assert
        Assert.Equal(50, result.Length);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }
    }

    [Fact]
    public async Task ReadHoldingRegisters_CrcMismatch_ThrowsInvalidDataException()
    {
        // Arrange: Phản hồi có CRC sai
        byte[] corruptResponse = { 0x01, 0x03, 0x02, 0x00, 0x05, 0x00, 0x00 };
        _transport.EnqueueResponse(corruptResponse);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));
    }

    [Fact]
    public async Task ReadHoldingRegisters_ModbusException_ThrowsModbusProtocolException()
    {
        // Arrange: MCU phản hồi lỗi IllegalDataAddress (0x02)
        // Frame: [Slave=1][FC=0x83][ExCode=0x02][CRCLo][CRCHi]
        byte[] exPayload = { 0x01, 0x83, 0x02 };
        ushort crc = Crc16Modbus.Compute(exPayload);
        byte[] exResponse = { 0x01, 0x83, 0x02, (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };

        _transport.EnqueueResponse(exResponse);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModbusProtocolException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x9999, 1));

        Assert.Equal(1, ex.SlaveId);
        Assert.Equal(0x03, ex.FunctionCode);
        Assert.Equal(ModbusExceptionCode.IllegalDataAddress, ex.ExceptionCode);
    }

    [Fact]
    public async Task ReadHoldingRegisters_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        _transport.SimulateTimeout = true;

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(
            () => _client.ReadHoldingRegistersAsync(1, 0x0000, 1));
    }

    [Fact]
    public async Task WriteSingleRegister_Success_EncodesRequestAndValidatesEcho()
    {
        // Arrange: Request ghi thanh ghi 0x0040 giá trị 0x0001 (Reboot)
        // Echo response: [Slave=1][FC=6][0x00, 0x40][0x00, 0x01][CRCLo, CRCHi]
        byte[] payload = { 0x01, 0x06, 0x00, 0x40, 0x00, 0x01 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = new byte[8];
        payload.CopyTo(response, 0);
        response[6] = (byte)(crc & 0xFF);
        response[7] = (byte)((crc >> 8) & 0xFF);

        _transport.EnqueueResponse(response);

        // Act
        await _client.WriteSingleRegisterAsync(1, 0x0040, 0x0001);

        // Assert
        Assert.Single(_transport.SentRequests);
        var req = _transport.SentRequests[0];
        Assert.Equal(8, req.Length);
        Assert.Equal(0x06, req[1]);
        Assert.Equal(0x0040, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(2, 2)));
        Assert.Equal(0x0001, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(4, 2)));
    }

    [Fact]
    public async Task WriteSingleRegister_Exception_ThrowsModbusProtocolException()
    {
        // Arrange: MCU từ chối ghi với IllegalDataValue (0x03)
        byte[] exPayload = { 0x01, 0x86, 0x03 };
        ushort crc = Crc16Modbus.Compute(exPayload);
        byte[] exResponse = { 0x01, 0x86, 0x03, (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };

        _transport.EnqueueResponse(exResponse);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModbusProtocolException>(
            () => _client.WriteSingleRegisterAsync(1, 0x0040, 0xFFFF));

        Assert.Equal(1, ex.SlaveId);
        Assert.Equal(0x06, ex.FunctionCode);
        Assert.Equal(ModbusExceptionCode.IllegalDataValue, ex.ExceptionCode);
    }

    [Fact]
    public async Task WriteMultipleRegisters_Success_EncodesRequestAndValidatesEcho()
    {
        // Arrange: Ghi 2 thanh ghi tại 0x9000
        // Echo response: [Slave=1][FC=0x10][0x90, 0x00][0x00, 0x02][CRCLo, CRCHi]
        byte[] payload = { 0x01, 0x10, 0x90, 0x00, 0x00, 0x02 };
        ushort crc = Crc16Modbus.Compute(payload);
        byte[] response = new byte[8];
        payload.CopyTo(response, 0);
        response[6] = (byte)(crc & 0xFF);
        response[7] = (byte)((crc >> 8) & 0xFF);

        _transport.EnqueueResponse(response);

        // Act
        ushort[] values = { 0xA5A5, 0x1234 };
        await _client.WriteMultipleRegistersAsync(1, 0x9000, values);

        // Assert
        Assert.Single(_transport.SentRequests);
        var req = _transport.SentRequests[0];
        Assert.Equal(13, req.Length); // 7 header + 4 data + 2 CRC = 13
        Assert.Equal(0x10, req[1]);
        Assert.Equal(0x9000, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(2, 2)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(4, 2)));
        Assert.Equal(4, req[6]); // byte count
        Assert.Equal(0xA5A5, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(7, 2)));
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16BigEndian(req.AsSpan(9, 2)));
    }

    [Fact]
    public async Task WriteMultipleRegisters_Exception_ThrowsModbusProtocolException()
    {
        // Arrange
        byte[] exPayload = { 0x01, 0x90, 0x04 }; // Slave Device Failure
        ushort crc = Crc16Modbus.Compute(exPayload);
        byte[] exResponse = { 0x01, 0x90, 0x04, (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };

        _transport.EnqueueResponse(exResponse);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModbusProtocolException>(
            () => _client.WriteMultipleRegistersAsync(1, 0x9000, new ushort[] { 1, 2 }));

        Assert.Equal(ModbusExceptionCode.SlaveDeviceFailure, ex.ExceptionCode);
    }

    [Fact]
    public async Task Operations_WhenTransportNotOpen_ThrowInvalidOperationException()
    {
        _transport.IsOpen = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.ReadHoldingRegistersAsync(1, 0, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.WriteSingleRegisterAsync(1, 0, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.WriteMultipleRegistersAsync(1, 0, new ushort[] { 1 }));
    }
}
