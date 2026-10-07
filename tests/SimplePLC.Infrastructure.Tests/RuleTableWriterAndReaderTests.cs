using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class RuleTableWriterAndReaderTests
{
    private static RuleRecordDto CreateTestRule(int index)
    {
        return new RuleRecordDto
        {
            ThresholdLo = index * 10,
            ThresholdHi = index * 20 + 5,
            ForMs = (uint)(index * 1000),
            ActionParam = index + 1,
            TriggerTag = (ushort)(index % 8),
            ActionTag = (ushort)(8 + (index % 8)),
            GuardTag = (ushort)(20 + (index % 16)),
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.GT,
            ActionType = SPLC_ActionType.SET_TAG
        };
    }

    [Fact]
    public async Task DeployRulesAsync_SingleRule_TransfersAndCommitsSuccessfully()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        var rules = new List<RuleRecordDto> { CreateTestRule(0) };

        // Act
        var result = await writer.DeployRulesAsync(1, rules);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ConfigStatus); // READY
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
        Assert.True(result.ActiveVersion >= 1);

        // Đọc lại qua RuleTableReader
        var readBack = await reader.ReadActiveRulesAsync(1);
        Assert.Single(readBack);
        Assert.Equal(rules[0].ThresholdLo, readBack[0].ThresholdLo);
        Assert.Equal(rules[0].TriggerTag, readBack[0].TriggerTag);
        Assert.Equal(rules[0].ActionTag, readBack[0].ActionTag);
    }

    [Fact]
    public async Task DeployRulesAsync_37Rules_TransfersThroughChunksAndCommits()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        var rules = Enumerable.Range(0, 37).Select(CreateTestRule).ToList();

        // Act
        var result = await writer.DeployRulesAsync(1, rules);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ConfigStatus);

        // Đọc lại toàn bộ 37 rules
        var readBack = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(37, readBack.Count);
        for (int i = 0; i < 37; i++)
        {
            Assert.Equal(rules[i].ThresholdLo, readBack[i].ThresholdLo);
            Assert.Equal(rules[i].ThresholdHi, readBack[i].ThresholdHi);
            Assert.Equal(rules[i].ActionParam, readBack[i].ActionParam);
        }
    }

    [Fact]
    public async Task DeployRulesAsync_100Rules_MaxCapacitySuccessfullyDeployed()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        var rules = Enumerable.Range(0, 100).Select(CreateTestRule).ToList();

        var result = await writer.DeployRulesAsync(1, rules);

        Assert.True(result.IsSuccess);

        var readBack = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(100, readBack.Count);
    }

    [Fact]
    public async Task DeployRulesAsync_ExceedingMaxRules_ReturnsInvalidParameterWithoutSending()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = Enumerable.Range(0, 101).Select(CreateTestRule).ToList();

        var result = await writer.DeployRulesAsync(1, rules);

        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.INVALID_PARAMETER, result.ErrorCode);
    }

    [Fact]
    public async Task DeployRulesAsync_CrcMismatchOnMcu_FailsAndProtectsActiveTable()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);

        // 1. Deploy 2 rules hợp lệ trước
        var initialRules = new List<RuleRecordDto> { CreateTestRule(0), CreateTestRule(1) };
        var initialResult = await writer.DeployRulesAsync(1, initialRules);
        Assert.True(initialResult.IsSuccess);

        // 2. Bật cờ gây sai lệch CRC trên Fake MCU
        client.CorruptStagingCrcOnCommit = true;

        var newRules = new List<RuleRecordDto> { CreateTestRule(5), CreateTestRule(6), CreateTestRule(7) };
        var failResult = await writer.DeployRulesAsync(1, newRules);

        // Assert: Deploy phải thất bại với lỗi CRC_MISMATCH
        Assert.False(failResult.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.CRC_MISMATCH, failResult.ErrorCode);
        Assert.Equal(4, failResult.ConfigStatus); // ERROR

        // Bảng Active Table cũ phải được bảo vệ nguyên vẹn (vẫn là 2 rules ban đầu)
        var currentActive = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(2, currentActive.Count);
        Assert.Equal(initialRules[0].ThresholdLo, currentActive[0].ThresholdLo);
        Assert.Equal(initialRules[1].ThresholdLo, currentActive[1].ThresholdLo);
    }

    [Fact]
    public async Task ReadActiveRulesAsync_EmptyTable_ReturnsEmptyList()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var reader = new RuleTableReader(client);

        var rules = await reader.ReadActiveRulesAsync(1);

        Assert.Empty(rules);
    }
}
