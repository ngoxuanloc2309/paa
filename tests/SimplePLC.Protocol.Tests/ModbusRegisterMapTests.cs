using SimplePLC.Protocol.Constants;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class ModbusRegisterMapTests
{
    [Theory]
    [InlineData(0, 0x0100)]
    [InlineData(1, 0x0110)]
    [InlineData(2, 0x0120)]
    [InlineData(36, 0x0340)]
    [InlineData(99, 0x0730)]
    public void GetActiveRuleAddress_ValidIndices_ComputesCorrectBaseAddress(int ruleIndex, ushort expectedAddress)
    {
        ushort actualAddress = ModbusRegisterMap.GetActiveRuleAddress(ruleIndex);
        Assert.Equal(expectedAddress, actualAddress);
    }

    [Theory]
    [InlineData(0, 0x9010)]
    [InlineData(1, 0x9020)]
    [InlineData(2, 0x9030)]
    [InlineData(36, 0x9250)]
    [InlineData(99, 0x9640)]
    public void GetStagingRuleAddress_ValidIndices_ComputesCorrectBaseAddress(int ruleIndex, ushort expectedAddress)
    {
        ushort actualAddress = ModbusRegisterMap.GetStagingRuleAddress(ruleIndex);
        Assert.Equal(expectedAddress, actualAddress);
    }

    [Theory]
    [InlineData(0, 0x0900)]
    [InlineData(1, 0x0902)]
    [InlineData(10, 0x0914)]
    [InlineData(127, 0x09FE)]
    public void GetRuntimeTagAddress_ValidIndices_ComputesCorrectAddress(int tagIndex, ushort expectedAddress)
    {
        ushort actualAddress = ModbusRegisterMap.GetRuntimeTagAddress(tagIndex);
        Assert.Equal(expectedAddress, actualAddress);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void GetActiveRuleAddress_OutOfRange_ThrowsArgumentOutOfRangeException(int ruleIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetActiveRuleAddress(ruleIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void GetStagingRuleAddress_OutOfRange_ThrowsArgumentOutOfRangeException(int ruleIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetStagingRuleAddress(ruleIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void GetRuntimeTagAddress_OutOfRange_ThrowsArgumentOutOfRangeException(int tagIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetRuntimeTagAddress(tagIndex));
    }

    [Fact]
    public void RegisterMap_ConstantValues_MatchSpecV17()
    {
        Assert.Equal(16, ModbusRegisterMap.RegistersPerRule);
        Assert.Equal(32, ModbusRegisterMap.BytesPerRule);
        Assert.Equal(100, ModbusRegisterMap.MaxRules);
        Assert.Equal(0xA5A5, ModbusRegisterMap.CommitMagic);
        Assert.Equal(0x8000, ModbusRegisterMap.GuardTagNegateMask);
        Assert.Equal(0x7FFF, ModbusRegisterMap.GuardTagIndexMask);
    }
}
