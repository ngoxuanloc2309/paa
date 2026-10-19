using System;
using System.Text;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class TlsCertificateCodecTests
{
    [Fact]
    public void EncodeAndDecodeChunk_Roundtrip_PreservesAllFieldsAndPayload()
    {
        // Arrange
        byte[] samplePayload = Encoding.UTF8.GetBytes("-----BEGIN CERTIFICATE-----\nMIIFazCCA1OgAwIBAgIUW");
        ushort crc = Crc16Modbus.Compute(samplePayload);

        var original = new TlsCertChunkDto
        {
            CertType = ModbusRegisterMap.MqttCertTypeRootCa,
            ChunkIndex = 2,
            TotalChunks = 15,
            ChunkLen = (byte)samplePayload.Length,
            TotalCertSize = 1840,
            ChunkCrc16 = crc,
            Payload = samplePayload
        };

        var destination = new ushort[ModbusRegisterMap.MqttCertChunkRegisters];

        // Act
        TlsCertificateCodec.EncodeChunk(original, destination);
        var decoded = TlsCertificateCodec.DecodeChunk(destination);

        // Assert
        Assert.Equal(original.CertType, decoded.CertType);
        Assert.Equal(original.ChunkIndex, decoded.ChunkIndex);
        Assert.Equal(original.TotalChunks, decoded.TotalChunks);
        Assert.Equal(original.ChunkLen, decoded.ChunkLen);
        Assert.Equal(original.TotalCertSize, decoded.TotalCertSize);
        Assert.Equal(original.ChunkCrc16, decoded.ChunkCrc16);
        Assert.Equal(original.Payload, decoded.Payload);
    }

    [Fact]
    public void SliceCertificate_CorrectlyChunksLargeCertificate()
    {
        // Arrange: Generate 1850 bytes of dummy certificate data
        byte[] certData = new byte[1850];
        for (int i = 0; i < certData.Length; i++)
        {
            certData[i] = (byte)(i % 256);
        }

        // Act
        var chunks = TlsCertificateCodec.SliceCertificate(ModbusRegisterMap.MqttCertTypeRootCa, certData);

        // Assert: 1850 / 120 = 15.41 -> 16 chunks
        Assert.Equal(16, chunks.Count);

        int accumulatedBytes = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            Assert.Equal(ModbusRegisterMap.MqttCertTypeRootCa, chunk.CertType);
            Assert.Equal(i, chunk.ChunkIndex);
            Assert.Equal(16, chunk.TotalChunks);
            Assert.Equal(1850, chunk.TotalCertSize);

            // Payload size
            int expectedLen = (i == 15) ? (1850 - 15 * 120) : 120;
            Assert.Equal(expectedLen, chunk.ChunkLen);
            Assert.Equal(expectedLen, chunk.Payload.Length);

            // CRC-16 check
            ushort expectedCrc = Crc16Modbus.Compute(chunk.Payload);
            Assert.Equal(expectedCrc, chunk.ChunkCrc16);

            accumulatedBytes += chunk.ChunkLen;
        }

        Assert.Equal(1850, accumulatedBytes);
    }

    [Fact]
    public void SliceCertificate_EmptyData_ReturnsEmptyList()
    {
        var chunks = TlsCertificateCodec.SliceCertificate(ModbusRegisterMap.MqttCertTypeRootCa, Array.Empty<byte>());
        Assert.Empty(chunks);
    }

    [Fact]
    public void EncodeChunk_BufferTooSmall_ThrowsArgumentException()
    {
        var chunk = new TlsCertChunkDto();
        var destination = new ushort[30]; // Smaller than 64

        Assert.Throws<ArgumentException>(() => TlsCertificateCodec.EncodeChunk(chunk, destination));
    }

    [Fact]
    public void DecodeChunk_WithCorruptedPayload_CrcMismatchDetectable()
    {
        // Arrange
        byte[] payload = Encoding.UTF8.GetBytes("AUTHENTIC_TLS_CA_PAYLOAD_DATA");
        var chunk = new TlsCertChunkDto
        {
            CertType = ModbusRegisterMap.MqttCertTypeRootCa,
            ChunkIndex = 0,
            TotalChunks = 1,
            ChunkLen = (byte)payload.Length,
            TotalCertSize = (ushort)payload.Length,
            ChunkCrc16 = Crc16Modbus.Compute(payload),
            Payload = payload
        };

        var registers = new ushort[ModbusRegisterMap.MqttCertChunkRegisters];
        TlsCertificateCodec.EncodeChunk(chunk, registers);

        // Act: Corrupt a register in the payload area (register index 4 onwards)
        registers[4] ^= 0xFFFF;
        var decoded = TlsCertificateCodec.DecodeChunk(registers);

        // Assert: CRC of altered payload differs from original ChunkCrc16
        ushort computedCrc = Crc16Modbus.Compute(decoded.Payload);
        Assert.NotEqual(decoded.ChunkCrc16, computedCrc);
    }

    [Fact]
    public void EncodeChunk_MatchesModbusRegisterPackingRules()
    {
        // Arrange
        byte[] payload = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        ushort crc = Crc16Modbus.Compute(payload);
        var chunk = new TlsCertChunkDto
        {
            CertType = ModbusRegisterMap.MqttCertTypeRootCa, // 0x01
            ChunkIndex = 5,
            TotalChunks = 10,
            ChunkLen = 4,
            TotalCertSize = 1000,
            ChunkCrc16 = crc,
            Payload = payload
        };

        var regs = new ushort[64];

        // Act
        TlsCertificateCodec.EncodeChunk(chunk, regs);

        // Reg 0: High = CertType (0x01), Low = ChunkIndex (0x05) => 0x0105
        Assert.Equal((ushort)0x0105, regs[0]);

        // Reg 1: High = TotalChunks (10 = 0x0A), Low = ChunkLen (4 = 0x04) => 0x0A04
        Assert.Equal((ushort)0x0A04, regs[1]);

        // Reg 2: TotalCertSize (1000)
        Assert.Equal((ushort)1000, regs[2]);

        // Reg 3: ChunkCrc16
        Assert.Equal(crc, regs[3]);

        // Reg 4: Payload bytes [0] (0x11) high, [1] (0x22) low => 0x1122
        Assert.Equal((ushort)0x1122, regs[4]);

        // Reg 5: Payload bytes [2] (0x33) high, [3] (0x44) low => 0x3344
        Assert.Equal((ushort)0x3344, regs[5]);
    }
}
