using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec mã hóa, giải mã và phân mảnh cấu trúc chứng chỉ bảo mật TLS/SSL (0x0F00, 64 thanh ghi = 128 bytes).
/// Tuân thủ chuẩn SPLC-SPEC-MQTT-003.
/// </summary>
public static class TlsCertificateCodec
{
    /// <summary>
    /// Mã hóa một DTO khối chứng chỉ thành 64 thanh ghi Modbus (128 bytes).
    /// </summary>
    public static void EncodeChunk(TlsCertChunkDto chunk, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (destination.Length < ModbusRegisterMap.MqttCertChunkRegisters)
        {
            throw new ArgumentException(
                $"Destination span must have at least {ModbusRegisterMap.MqttCertChunkRegisters} registers.",
                nameof(destination));
        }

        destination.Slice(0, ModbusRegisterMap.MqttCertChunkRegisters).Fill(0);

        byte chunkLen = (byte)Math.Min((int)chunk.ChunkLen, ModbusRegisterMap.MqttCertPayloadMaxBytes);
        if (chunk.Payload != null && chunk.Payload.Length > 0)
        {
            chunkLen = (byte)Math.Min(chunk.Payload.Length, ModbusRegisterMap.MqttCertPayloadMaxBytes);
        }

        // Header: 4 thanh ghi (8 bytes)
        // Offset +0: High=CertType, Low=ChunkIndex
        destination[0] = (ushort)((chunk.CertType << 8) | (chunk.ChunkIndex & 0xFF));

        // Offset +1: High=TotalChunks, Low=ChunkLen
        destination[1] = (ushort)((chunk.TotalChunks << 8) | (chunkLen & 0xFF));

        // Offset +2: TotalCertSize (16-bit Big-Endian)
        destination[2] = chunk.TotalCertSize;

        // Offset +3: ChunkCrc16 (16-bit Big-Endian)
        destination[3] = chunk.ChunkCrc16;

        // Payload: 60 thanh ghi (120 bytes) bắt đầu từ Offset +4..+63
        if (chunk.Payload != null && chunkLen > 0)
        {
            int regCount = (chunkLen + 1) / 2;
            for (int k = 0; k < regCount; k++)
            {
                int b0 = 2 * k;
                int b1 = 2 * k + 1;
                byte high = b0 < chunkLen ? chunk.Payload[b0] : (byte)0;
                byte low = b1 < chunkLen ? chunk.Payload[b1] : (byte)0;
                destination[4 + k] = (ushort)((high << 8) | low);
            }
        }
    }

    /// <summary>
    /// Giải mã 64 thanh ghi Modbus thành đối tượng TlsCertChunkDto.
    /// </summary>
    public static TlsCertChunkDto DecodeChunk(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.MqttCertChunkRegisters)
        {
            throw new ArgumentException(
                $"Source span must have at least {ModbusRegisterMap.MqttCertChunkRegisters} registers.",
                nameof(source));
        }

        // Header
        byte certType = (byte)(source[0] >> 8);
        byte chunkIndex = (byte)(source[0] & 0xFF);
        byte totalChunks = (byte)(source[1] >> 8);
        byte chunkLen = (byte)(source[1] & 0xFF);
        ushort totalCertSize = source[2];
        ushort chunkCrc16 = source[3];

        int actualLen = Math.Min((int)chunkLen, ModbusRegisterMap.MqttCertPayloadMaxBytes);
        byte[] payload = new byte[actualLen];

        int regCount = (actualLen + 1) / 2;
        for (int k = 0; k < regCount; k++)
        {
            ushort reg = source[4 + k];
            int b0 = 2 * k;
            int b1 = 2 * k + 1;
            if (b0 < actualLen) payload[b0] = (byte)(reg >> 8);
            if (b1 < actualLen) payload[b1] = (byte)(reg & 0xFF);
        }

        return new TlsCertChunkDto
        {
            CertType = certType,
            ChunkIndex = chunkIndex,
            TotalChunks = totalChunks,
            ChunkLen = (byte)actualLen,
            TotalCertSize = totalCertSize,
            ChunkCrc16 = chunkCrc16,
            Payload = payload
        };
    }

    /// <summary>
    /// Phân mảnh toàn bộ mảng byte chứng chỉ thành danh sách các TlsCertChunkDto (120 bytes payload/khối).
    /// Tự động tính toán CRC-16 cho từng khối payload.
    /// </summary>
    public static IReadOnlyList<TlsCertChunkDto> SliceCertificate(byte certType, ReadOnlySpan<byte> certBytes)
    {
        if (certBytes.Length == 0)
        {
            return Array.Empty<TlsCertChunkDto>();
        }

        int maxChunkSize = ModbusRegisterMap.MqttCertPayloadMaxBytes;
        int totalChunks = (certBytes.Length + maxChunkSize - 1) / maxChunkSize;
        if (totalChunks > 255)
        {
            throw new ArgumentException($"Certificate size {certBytes.Length} exceeds maximum supported chunks (255).", nameof(certBytes));
        }

        var chunks = new List<TlsCertChunkDto>(totalChunks);
        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * maxChunkSize;
            int length = Math.Min(maxChunkSize, certBytes.Length - offset);
            ReadOnlySpan<byte> slice = certBytes.Slice(offset, length);

            byte[] payload = slice.ToArray();
            ushort crc = Crc16Modbus.Compute(payload);

            chunks.Add(new TlsCertChunkDto
            {
                CertType = certType,
                ChunkIndex = (byte)i,
                TotalChunks = (byte)totalChunks,
                ChunkLen = (byte)length,
                TotalCertSize = (ushort)certBytes.Length,
                ChunkCrc16 = crc,
                Payload = payload
            });
        }

        return chunks;
    }
}
