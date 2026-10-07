using SimplePLC.Infrastructure.Transport;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class ModbusChunkPlannerTests
{
    [Fact]
    public void DefaultMaxChunkSize_IsConfiguredTo16Registers()
    {
        Assert.Equal(16, ModbusChunkPlanner.DefaultMaxChunkSize);
    }

    [Fact]
    public void PlanReadChunks_DefaultMaxChunkSize_SplitsInto16RegisterChunks()
    {
        // 1600 registers (100 rules), mặc định 16 regs/chunk -> 100 chunks (1 rule per chunk)
        var chunks = ModbusChunkPlanner.PlanReadChunks(0x0100, 1600);

        Assert.Equal(100, chunks.Count);
        Assert.Equal(0x0100, chunks[0].Address);
        Assert.Equal(16, chunks[0].Count);
        Assert.Equal((ushort)(0x0100 + 99 * 16), chunks[99].Address);
        Assert.Equal(16, chunks[99].Count);
        Assert.Equal(1600, chunks.Sum(c => c.Count));
    }

    [Fact]
    public void PlanWriteChunks_DefaultMaxChunkSize_SplitsInto16RegisterChunks()
    {
        var data = new ushort[32]; // 2 rules
        var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(0x9010, data.AsMemory());

        Assert.Equal(2, chunks.Count);
        Assert.Equal(16, chunks[0].Chunk.Length);
        Assert.Equal(16, chunks[1].Chunk.Length);
    }

    [Fact]
    public void PlanReadChunks_WithinMaxChunkSize_ReturnsSingleChunk()
    {
        var chunks = ModbusChunkPlanner.PlanReadChunks(0x0100, 32, maxChunkSize: 64);

        Assert.Single(chunks);
        Assert.Equal(0x0100, chunks[0].Address);
        Assert.Equal(32, chunks[0].Count);
    }

    [Fact]
    public void PlanReadChunks_LargeRegisterCount_SplitsIntoExpectedChunks()
    {
        // 1600 registers (100 rules), chunk size 64 -> 25 chunks (25 * 64 = 1600)
        var chunks = ModbusChunkPlanner.PlanReadChunks(0x0100, 1600, maxChunkSize: 64);

        Assert.Equal(25, chunks.Count);

        // Chunk đầu tiên
        Assert.Equal(0x0100, chunks[0].Address);
        Assert.Equal(64, chunks[0].Count);

        // Chunk cuối cùng
        Assert.Equal((ushort)(0x0100 + 24 * 64), chunks[24].Address);
        Assert.Equal(64, chunks[24].Count);

        // Tổng số thanh ghi được đọc đúng bằng 1600
        Assert.Equal(1600, chunks.Sum(c => c.Count));
    }

    [Fact]
    public void PlanReadChunks_NotExactMultiple_LastChunkHasRemainder()
    {
        // 70 registers, chunk size 64 -> 2 chunks (64 + 6)
        var chunks = ModbusChunkPlanner.PlanReadChunks(0x9010, 70, maxChunkSize: 64);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0x9010, chunks[0].Address);
        Assert.Equal(64, chunks[0].Count);

        Assert.Equal((ushort)(0x9010 + 64), chunks[1].Address);
        Assert.Equal(6, chunks[1].Count);
    }

    [Fact]
    public void PlanWriteChunks_LargeData_SplitsAndPreservesSlices()
    {
        var data = new ushort[150];
        for (int i = 0; i < data.Length; i++) data[i] = (ushort)(i + 1);

        var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(0x9010, data.AsMemory(), maxChunkSize: 64);

        Assert.Equal(3, chunks.Count); // 64 + 64 + 22
        Assert.Equal(64, chunks[0].Chunk.Length);
        Assert.Equal(64, chunks[1].Chunk.Length);
        Assert.Equal(22, chunks[2].Chunk.Length);

        // Kiểm tra dữ liệu slice chính xác
        Assert.Equal(1, chunks[0].Chunk.Span[0]);
        Assert.Equal(65, chunks[1].Chunk.Span[0]);
        Assert.Equal(129, chunks[2].Chunk.Span[0]);
    }

    [Fact]
    public void PlanReadChunks_ZeroTotalCount_ReturnsEmpty()
    {
        var chunks = ModbusChunkPlanner.PlanReadChunks(0x0100, 0, maxChunkSize: 64);

        Assert.Empty(chunks);
    }

    [Fact]
    public void PlanReadChunks_InvalidMaxChunkSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusChunkPlanner.PlanReadChunks(0x0100, 10, maxChunkSize: 0));
    }
}
