using BenchmarkDotNet.Attributes;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class Crc16Benchmarks
{
    private ushort[] _registers = null!;
    private byte[] _bytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 100 rules * 16 registers = 1600 registers (3200 bytes)
        int regCount = ModbusRegisterMap.MaxRules * ModbusRegisterMap.RegistersPerRule;
        _registers = new ushort[regCount];
        for (int i = 0; i < regCount; i++)
        {
            _registers[i] = (ushort)(i * 31 + 7);
        }

        _bytes = new byte[regCount * 2];
        for (int i = 0; i < _bytes.Length; i++)
        {
            _bytes[i] = (byte)(i & 0xFF);
        }
    }

    [Benchmark(Description = "CRC-16 from 1600 Registers (3.2 KB)")]
    public ushort Crc16FromRegisters_1600Regs()
    {
        return Crc16Modbus.ComputeFromRegisters(_registers);
    }

    [Benchmark(Description = "CRC-16 from 3200 Bytes Span")]
    public ushort Crc16FromBytes_3200Bytes()
    {
        return Crc16Modbus.Compute(_bytes);
    }
}
