using System;
using System.Linq;
using SimplePLC.Domain.Enums;
using Xunit;

namespace SimplePLC.Domain.Tests;

/// <summary>
/// Khoa chat hop dong so (Platform TagKind Contract V1) theo dung dac ta ban dau (Spec 3.2).
/// Ngan chan numeric drift hoac tai cau truc enum vo tinh lam thay doi gia tri so.
/// </summary>
public class TagKindContractTests
{
    [Theory]
    [InlineData(TagKind.None, 0)]
    [InlineData(TagKind.DiscreteInput, 1)]
    [InlineData(TagKind.DiscreteOutput, 2)]
    [InlineData(TagKind.AnalogInput, 3)]
    [InlineData(TagKind.VirtualFlag, 4)]
    [InlineData(TagKind.VirtualRegister, 5)]
    [InlineData(TagKind.ModbusCoil, 6)]
    [InlineData(TagKind.ModbusHolding, 7)]
    [InlineData(TagKind.VirtualRegisterRetain, 8)]
    [InlineData(TagKind.Counter, 9)]
    [InlineData(TagKind.System, 10)]
    public void TagKind_NumericValue_MatchesPlatformContractV1(TagKind kind, byte expectedValue)
    {
        Assert.Equal(expectedValue, (byte)kind);
    }

    [Fact]
    public void TagKind_UnderlyingType_IsByte()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(TagKind)));
    }

    [Fact]
    public void TagKind_AllValues_AreUniqueAndTotal11()
    {
        var values = Enum.GetValues<TagKind>();
        Assert.Equal(11, values.Length);
        Assert.Equal(11, values.Distinct().Count());
    }
}
