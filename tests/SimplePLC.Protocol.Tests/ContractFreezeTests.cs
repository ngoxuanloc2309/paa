using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class ContractFreezeTests
{
    [Fact]
    public void SPLC_ErrorCode_EnumValues_AreUniqueAndMatchContract()
    {
        // Freeze numeric values: distinct and immutable
        Assert.Equal(0, (ushort)SPLC_ErrorCode.NONE);
        Assert.Equal(1, (ushort)SPLC_ErrorCode.INVALID_COMMAND);
        Assert.Equal(2, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
        Assert.Equal(3, (ushort)SPLC_ErrorCode.BUSY);
        Assert.Equal(4, (ushort)SPLC_ErrorCode.CRC_MISMATCH);
        Assert.Equal(5, (ushort)SPLC_ErrorCode.UNSUPPORTED);
        Assert.Equal(6, (ushort)SPLC_ErrorCode.FLASH);

        // Ensure all enum values are strictly unique
        var values = Enum.GetValues<SPLC_ErrorCode>();
        var distinctValues = values.Select(v => (ushort)v).Distinct().ToList();
        Assert.Equal(values.Length, distinctValues.Count);
    }

    [Fact]
    public void SPLC_ResetReason_EnumValues_AreUniqueAndMatchContract()
    {
        Assert.Equal(0, (ushort)SPLC_ResetReason.UNKNOWN);
        Assert.Equal(1, (ushort)SPLC_ResetReason.POWER_ON);
        Assert.Equal(2, (ushort)SPLC_ResetReason.SOFTWARE);
        Assert.Equal(3, (ushort)SPLC_ResetReason.WATCHDOG);
        Assert.Equal(4, (ushort)SPLC_ResetReason.BROWNOUT);
        Assert.Equal(5, (ushort)SPLC_ResetReason.EXTERNAL);
    }

    [Fact]
    public void SPLC_SystemCommand_EnumValues_AreUniqueAndMatchContract()
    {
        Assert.Equal(0, (ushort)SPLC_SystemCommand.NONE);
        Assert.Equal(1, (ushort)SPLC_SystemCommand.REBOOT);
        Assert.Equal(2, (ushort)SPLC_SystemCommand.FACTORY_RESET);
        Assert.Equal(3, (ushort)SPLC_SystemCommand.CLEAR_RULES);
        Assert.Equal(4, (ushort)SPLC_SystemCommand.CLEAR_RETAIN);
    }
}
