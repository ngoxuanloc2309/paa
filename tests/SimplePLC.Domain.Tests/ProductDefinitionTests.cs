using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Domain.Tests;

public class ProductDefinitionTests
{
    [Fact]
    public void CreateRemoteIo8Di8Do4Ai_HasExact124Tags()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        Assert.Equal(124, product.Tags.Count);
    }

    [Fact]
    public void CreateRemoteIo8Di8Do4Ai_DiAndAiAreReadOnly()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        // 8 DI
        for (ushort i = 0; i < 8; i++)
        {
            var di = product.FindTagByName($"DI{i}");
            Assert.NotNull(di);
            Assert.True(di.IsReadOnly);
            Assert.Equal(TagDataType.Boolean, di.DataType);
            Assert.Equal(TagKind.DiscreteInput, di.Kind);
        }

        // 4 AI
        for (ushort i = 0; i < 4; i++)
        {
            var ai = product.FindTagByName($"AI{i}");
            Assert.NotNull(ai);
            Assert.True(ai.IsReadOnly);
            Assert.Equal(TagDataType.Int32, ai.DataType);
            Assert.Equal(TagKind.AnalogInput, ai.Kind);
        }
    }

    [Fact]
    public void CreateRemoteIo8Di8Do4Ai_DoAndVflagAndVregAreWritable()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        // DO
        var do0 = product.FindTagByName("DO0");
        Assert.NotNull(do0);
        Assert.False(do0.IsReadOnly);

        // VFLAG
        var vflag0 = product.FindTagByName("VFLAG0");
        Assert.NotNull(vflag0);
        Assert.False(vflag0.IsReadOnly);

        // VREG
        var vreg0 = product.FindTagByName("VREG0");
        Assert.NotNull(vreg0);
        Assert.False(vreg0.IsReadOnly);

        // VREG_RETAIN
        var vregRetain0 = product.FindTagByName("VREG_RETAIN0");
        Assert.NotNull(vregRetain0);
        Assert.False(vregRetain0.IsReadOnly);
        Assert.Equal(TagKind.VirtualRegisterRetain, vregRetain0.Kind);

        // Counter COUNTER0
        var counter0 = product.FindTagByName("COUNTER0");
        Assert.NotNull(counter0);
        Assert.False(counter0.IsReadOnly);
        Assert.Equal(TagKind.Counter, counter0.Kind);
    }

    [Fact]
    public void FindTagByName_CaseInsensitive_FindsTag()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        var tagLower = product.FindTagByName("di0");
        var tagUpper = product.FindTagByName("DI0");

        Assert.NotNull(tagLower);
        Assert.Same(tagLower, tagUpper);
    }

    [Fact]
    public void FindTagByIndex_ValidIndex_FindsTag()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        var tag = product.FindTagByIndex(8);

        Assert.NotNull(tag);
        Assert.Equal("DO0", tag.Name);
    }

    [Fact]
    public void FindTagByIndex_InvalidIndex_ReturnsNull()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        var tag = product.FindTagByIndex(999);

        Assert.Null(tag);
    }
}
