using System.Linq;
using SimplePLC.Domain.Enums;
using Xunit;

namespace SimplePLC.Domain.Tests;

/// <summary>
/// Khóa ch?t h?p d?ng s? (Platform TagKind Contract V1) theo dúng d?c t? ban d?u (Spec 3.2).
/// Ngan ch?n numeric drift ho?c tái c?u trúc enum vô tình làm thay d?i giá tr? s?.
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
    public void TagKind_AllValues_AreUniqueAndTotal10()
    {
        var values = Enum.GetValues<TagKind>();
        Assert.Equal(10, values.Length);
        Assert.Equal(10, values.Distinct().Count());
    }
}
