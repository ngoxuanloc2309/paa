using BenchmarkDotNet.Attributes;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class ModbusChunkPlannerBenchmarks
{
    private ushort[] _registers = null!;

    [GlobalSetup]
    public void Setup()
    {
        int regCount = ModbusRegisterMap.MaxRules * ModbusRegisterMap.RegistersPerRule; // 1600
        _registers = new ushort[regCount];
        for (int i = 0; i < regCount; i++)
        {
            _registers[i] = (ushort)i;
        }
    }

    [Benchmark(Description = "Plan Write Chunks for 1600 Registers (64/chunk)")]
    public IReadOnlyList<(ushort Address, ReadOnlyMemory<ushort> Chunk)> PlanWriteChunks_1600Regs()
    {
        return ModbusChunkPlanner.PlanWriteChunks<ushort>(ModbusRegisterMap.StagingRuleTableBaseAddress, _registers, 64);
    }

    [Benchmark(Description = "Plan Read Chunks for 1600 Registers (64/chunk)")]
    public IReadOnlyList<(ushort Address, ushort Count)> PlanReadChunks_1600Regs()
    {
        return ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.ActiveRuleTableBaseAddress, 1600, 64);
    }
}
