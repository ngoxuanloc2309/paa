using System.Buffers.Binary;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class McuModbusRtuServerTests
{
    private readonly McuReferenceSimulator _simulator;
    private readonly McuModbusRtuServer _server;

    public McuModbusRtuServerTests()
    {
        _simulator = new McuReferenceSimulator();
        _server = new McuModbusRtuServer(_simulator, slaveId: 1);
    }

    private static byte[] BuildFc03Request(byte slaveId, ushort startAddress, ushort count)
    {
        byte[] request = new byte[8];
        request[0] = slaveId;
        request[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), count);
        ushort crc = Crc16Modbus.Compute(request.AsSpan(0, 6));
        request[6] = (byte)(crc & 0xFF);
        request[7] = (byte)((crc >> 8) & 0xFF);
        return request;
    }

    private static byte[] BuildFc06Request(byte slaveId, ushort address, ushort value)
    {
        byte[] request = new byte[8];
        request[0] = slaveId;
        request[1] = 0x06;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), value);
        ushort crc = Crc16Modbus.Compute(request.AsSpan(0, 6));
        request[6] = (byte)(crc & 0xFF);
        request[7] = (byte)((crc >> 8) & 0xFF);
        return request;
    }

    private static byte[] BuildFc16Request(byte slaveId, ushort startAddress, ushort[] values)
    {
        ushort count = (ushort)values.Length;
        byte byteCount = (byte)(count * 2);
        byte[] request = new byte[7 + byteCount + 2];
        request[0] = slaveId;
        request[1] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), count);
        request[6] = byteCount;

        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(7 + i * 2, 2), values[i]);
        }

        ushort crc = Crc16Modbus.Compute(request.AsSpan(0, 7 + byteCount));
        request[7 + byteCount] = (byte)(crc & 0xFF);
        request[7 + byteCount + 1] = (byte)((crc >> 8) & 0xFF);
        return request;
    }

    [Fact]
    public async Task ProcessFrame_Fc03ReadDescriptors_ReturnsValidModbusResponse()
    {
        // Request: Read 10 registers from 0x0000 (Hardware Descriptors)
        byte[] request = BuildFc03Request(1, 0x0000, 10);

        byte[] response = await _server.ProcessFrameAsync(request);

        Assert.NotEmpty(response);
        Assert.Equal(1, response[0]); // SlaveId
        Assert.Equal(0x03, response[1]); // FC03
        Assert.Equal(20, response[2]); // 10 registers * 2 bytes = 20

        // Check CRC of the response
        ushort computedCrc = Crc16Modbus.Compute(response.AsSpan(0, response.Length - 2));
        ushort receivedCrc = (ushort)(response[^2] | (response[^1] << 8));
        Assert.Equal(computedCrc, receivedCrc);

        // First register is Device Class (REMOTE_IO = 1)
        ushort deviceClass = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(3, 2));
        Assert.Equal((ushort)SPLC_DeviceClass.REMOTE_IO, deviceClass);
    }

    [Fact]
    public async Task ProcessFrame_Fc06WriteRegister_EchoesBackAndUpdatesState()
    {
        // Ghi RULE_COUNT_STAGED = 3 (địa chỉ 0x9002)
        byte[] request = BuildFc06Request(1, ModbusRegisterMap.RuleCountStagedAddress, 3);

        byte[] response = await _server.ProcessFrameAsync(request);

        // FC06 echo response phải giống 100% request
        Assert.Equal(request, response);

        // Đọc lại qua FC03 để xác nhận giá trị đã ghi vào simulator
        byte[] readReq = BuildFc03Request(1, ModbusRegisterMap.RuleCountStagedAddress, 1);
        byte[] readResp = await _server.ProcessFrameAsync(readReq);

        ushort val = BinaryPrimitives.ReadUInt16BigEndian(readResp.AsSpan(3, 2));
        Assert.Equal(3, val);
    }

    [Fact]
    public async Task ProcessFrame_Fc16WriteMultipleRegisters_WritesAndReturnsEchoParameters()
    {
        // Khởi tạo Staging session với 1 rule (16 registers)
        await _server.ProcessFrameAsync(BuildFc06Request(1, ModbusRegisterMap.RuleCountStagedAddress, 1));

        ushort[] registers = new ushort[16];
        for (int i = 0; i < 16; i++) registers[i] = (ushort)(0x1000 + i);

        byte[] request = BuildFc16Request(1, ModbusRegisterMap.StagingRuleTableBaseAddress, registers);
        byte[] response = await _server.ProcessFrameAsync(request);

        Assert.Equal(8, response.Length);
        Assert.Equal(1, response[0]);
        Assert.Equal(0x10, response[1]);
        Assert.Equal(ModbusRegisterMap.StagingRuleTableBaseAddress, BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4, 2)));

        // Verify CRC
        ushort computedCrc = Crc16Modbus.Compute(response.AsSpan(0, 6));
        ushort receivedCrc = (ushort)(response[6] | (response[7] << 8));
        Assert.Equal(computedCrc, receivedCrc);
    }

    [Fact]
    public async Task ProcessFrame_CorruptRequestCrc_DropsFrameSilently()
    {
        byte[] request = BuildFc03Request(1, 0x0000, 5);
        request[^1] ^= 0xFF; // Corrupt CRC

        byte[] response = await _server.ProcessFrameAsync(request);

        Assert.Empty(response);
    }

    [Fact]
    public async Task ProcessFrame_UnsupportedFunctionCode_ReturnsExceptionResponse()
    {
        byte[] request = { 0x01, 0x07, 0x00, 0x00, 0x00, 0x00 };
        ushort crc = Crc16Modbus.Compute(request);
        byte[] fullReq = new byte[8];
        request.CopyTo(fullReq, 0);
        fullReq[6] = (byte)(crc & 0xFF);
        fullReq[7] = (byte)((crc >> 8) & 0xFF);

        byte[] response = await _server.ProcessFrameAsync(fullReq);

        Assert.Equal(5, response.Length);
        Assert.Equal(0x01, response[0]);
        Assert.Equal(0x87, response[1]); // 0x07 | 0x80
        Assert.Equal((byte)ModbusExceptionCode.IllegalFunction, response[2]);
    }

    [Fact]
    public async Task VirtualComTransport_ModbusRtuClientIntegration_PerformsTransactionsThroughVirtualCom()
    {
        // Tích hợp ModbusRtuClient thật giao tiếp với McuModbusRtuServer qua VirtualComMcuTransport
        var transport = new VirtualComMcuTransport(_server);
        await transport.OpenAsync(new UsbCdcOptions("COM_VIRTUAL"));

        var client = new ModbusRtuClient(transport);

        // 1. Đọc Descriptors qua ModbusRtuClient thật
        var descriptors = await client.ReadHoldingRegistersAsync(1, 0x0000, 10);
        Assert.Equal(10, descriptors.Length);
        Assert.Equal((ushort)SPLC_DeviceClass.REMOTE_IO, descriptors[0]);

        // 2. Ghi thanh ghi qua FC06
        await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.RuleCountStagedAddress, 2);

        // 3. Đọc lại xác nhận
        var staged = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, 1);
        Assert.Equal(2, staged[0]);

        await client.DisposeAsync();
    }
}
