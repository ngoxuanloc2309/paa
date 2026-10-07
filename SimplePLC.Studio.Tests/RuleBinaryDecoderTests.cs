using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class RuleBinaryDecoderTests
{
    [Fact]
    public void EncodeAndDecode_Roundtrip_Preserves32BytesIntegrity()
    {
        // Arrange: Test rules covering different triggers, guards, comparisons, and actions
        var tagDI0 = new TagModel { Index = 1, Name = "DI0", Kind = TagKind.DiscreteInput };
        var tagDO0 = new TagModel { Index = 9, Name = "DO0", Kind = TagKind.DiscreteOutput };
        var tagAI0 = new TagModel { Index = 17, Name = "AI0", Kind = TagKind.AnalogInput };
        var tagVFLAG0 = new TagModel { Index = 21, Name = "VFLAG0", Kind = TagKind.VirtualFlag };

        var allTags = new List<TagModel> { tagDI0, tagDO0, tagAI0, tagVFLAG0 };

        var originalRules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Enabled = true,
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = tagDI0,
                ForMs = 15000,
                GuardTag = tagVFLAG0,
                GuardNegated = false,
                CompareOp = CompareOp.EQ,
                ThresholdLo = 1,
                ActionType = ActionType.SET_TAG,
                ActionTag = tagDO0,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Enabled = true,
                TriggerType = TriggerType.ON_CHANGE,
                TriggerTag = tagAI0,
                ForMs = 0,
                GuardTag = tagAI0,
                GuardNegated = true,
                CompareOp = CompareOp.BETWEEN,
                ThresholdLo = 20,
                ThresholdHi = 80,
                ActionType = ActionType.INC_COUNTER,
                ActionTag = tagDO0,
                ActionParam = 5
            }
        };

        // Act: Encode to 32-byte payload
        byte[] payload = RuleBinaryEncoder.EncodeProgram(originalRules);
        Assert.Equal(2 * 32, payload.Length);

        // Act: Decode payload back to RuleItemModel
        var decodedRules = RuleBinaryEncoder.DecodeProgram(payload, allTags);
        Assert.Equal(2, decodedRules.Count);

        // Assert: Rule 0
        var r0 = decodedRules[0];
        Assert.Equal(0, r0.Index);
        Assert.True(r0.Enabled);
        Assert.Equal(TriggerType.ON_FALL, r0.TriggerType);
        Assert.Equal(1, r0.TriggerTag?.Index);
        Assert.Equal(15000u, r0.ForMs);
        Assert.Equal(21, r0.GuardTag?.Index);
        Assert.False(r0.GuardNegated);
        Assert.Equal(CompareOp.EQ, r0.CompareOp);
        Assert.Equal(1, r0.ThresholdLo);
        Assert.Equal(ActionType.SET_TAG, r0.ActionType);
        Assert.Equal(9, r0.ActionTag?.Index);
        Assert.Equal(1, r0.ActionParam);

        // Assert: Rule 1
        var r1 = decodedRules[1];
        Assert.Equal(1, r1.Index);
        Assert.True(r1.Enabled);
        Assert.Equal(TriggerType.ON_CHANGE, r1.TriggerType);
        Assert.Equal(17, r1.TriggerTag?.Index);
        Assert.Equal(0u, r1.ForMs);
        Assert.Equal(17, r1.GuardTag?.Index);
        Assert.True(r1.GuardNegated);
        Assert.Equal(CompareOp.BETWEEN, r1.CompareOp);
        Assert.Equal(20, r1.ThresholdLo);
        Assert.Equal(80, r1.ThresholdHi);
        Assert.Equal(ActionType.INC_COUNTER, r1.ActionType);
        Assert.Equal(9, r1.ActionTag?.Index);
        Assert.Equal(5, r1.ActionParam);
    }

    [Fact]
    public void Decode_InvalidBufferLength_ThrowsArgumentException()
    {
        byte[] oddBuffer = new byte[35]; // Not a multiple of 32
        Assert.Throws<ArgumentException>(() =>
        {
            RuleBinaryEncoder.DecodeProgram(oddBuffer);
        });
    }

    [Fact]
    public void Decode_EmptyBuffer_ReturnsEmptyList()
    {
        var result = RuleBinaryEncoder.DecodeProgram(Array.Empty<byte>());
        Assert.Empty(result);
    }
}
