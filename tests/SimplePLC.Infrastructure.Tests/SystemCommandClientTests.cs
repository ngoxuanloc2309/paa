using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class SystemCommandClientTests
{
    [Fact]
    public async Task ExecuteCommandAsync_ClearRules_ExecutesSuccessfullyAndClearsActiveRules()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // 1. Nạp trước 2 rules
        var writer = new RuleTableWriter(client);
        var reader = new RuleTableReader(client);
        await writer.DeployRulesAsync(1, new List<RuleRecordDto> { new(), new() });

        var beforeClear = await reader.ReadActiveRulesAsync(1);
        Assert.Equal(2, beforeClear.Count);

        // 2. Gửi lệnh CLEAR_RULES
        var cmdClient = new SystemCommandClient(client);
        var result = await cmdClient.ExecuteCommandAsync(1, SPLC_SystemCommand.CLEAR_RULES);

        Assert.True(result.IsSuccess);
        Assert.Equal(SPLC_CommandStatus.DONE, result.Status);
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);

        // 3. Kiểm tra active rules đã bị xóa
        var afterClear = await reader.ReadActiveRulesAsync(1);
        Assert.Empty(afterClear);
    }
}
