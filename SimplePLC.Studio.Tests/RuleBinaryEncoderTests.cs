using System.Buffers.Binary;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Tests;

public class RuleBinaryEncoderTests
{
    [Fact]
    public void WireSize_MustBeExactly32Bytes_REQ_RUL_001()
    {
        // Arrange
        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true,
            TriggerType = TriggerType.ON_RISE,
            ThresholdLo = 100,
            ThresholdHi = 200,
            ForMs = 5000,
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        // Act
        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);

        // Assert
        Assert.Equal(32, bytes.Length);
        Assert.Equal(32, RuleBinaryEncoder.RuleRecordSize);
    }

    [Fact]
    public void ReservedBytes_MustBeZero_REQ_RUL_002()
    {
        // Arrange
        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true
        };

        // Act
        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);

        // Assert: Offset 26-31 (6 reserved bytes) must be 0x00
        for (int i = 26; i < 32; i++)
        {
            Assert.Equal(0x00, bytes[i]);
        }
    }

    [Fact]
    public void GuardTag_NegateBitmask_MustSetBit15_REQ_RUL_003()
    {
        // Arrange: Guard index = 21 (VFLAG0), not negated
        var guardTag = new TagModel { Index = 21, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        var ruleNormal = new RuleItemModel
        {
            GuardTag = guardTag,
            GuardNegated = false
        };

        var ruleNegated = new RuleItemModel
        {
            GuardTag = guardTag,
            GuardNegated = true
        };

        // Act
        byte[] bytesNormal = RuleBinaryEncoder.EncodeRule(ruleNormal);
        byte[] bytesNegated = RuleBinaryEncoder.EncodeRule(ruleNegated);

        ushort guardNormal = BinaryPrimitives.ReadUInt16BigEndian(bytesNormal.AsSpan(20, 2));
        ushort guardNegated = BinaryPrimitives.ReadUInt16BigEndian(bytesNegated.AsSpan(20, 2));

        // Assert
        Assert.Equal(21, guardNormal);
        Assert.Equal(21 | 0x8000, guardNegated);
        Assert.True((guardNegated & 0x8000) != 0);
        Assert.Equal(21, guardNegated & 0x7FFF);
    }

    [Fact]
    public void Endianness_MustBeBigEndian_REQ_SER_001_002()
    {
        // Arrange
        var trigTag = new TagModel { Index = 0x0102, Name = "DI_TEST", Kind = TagKind.DiscreteInput };
        var actTag = new TagModel { Index = 0x0304, Name = "DO_TEST", Kind = TagKind.DiscreteOutput };

        var rule = new RuleItemModel
        {
            ThresholdLo = 0x12345678,
            ThresholdHi = unchecked((int)0x87654321),
            ForMs = 0xAABBCCDD,
            ActionParam = 0x11223344,
            TriggerTag = trigTag,
            ActionTag = actTag,
            Enabled = true,
            TriggerType = TriggerType.ON_RISE,
            CompareOp = CompareOp.GT,
            ActionType = ActionType.SET_TAG
        };

        // Act
        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);

        // Assert: Big-Endian wire order
        // 00-03: ThresholdLo
        Assert.Equal(0x12, bytes[0]);
        Assert.Equal(0x34, bytes[1]);
        Assert.Equal(0x56, bytes[2]);
        Assert.Equal(0x78, bytes[3]);

        // 04-07: ThresholdHi (0x87654321)
        Assert.Equal(0x87, bytes[4]);
        Assert.Equal(0x65, bytes[5]);
        Assert.Equal(0x43, bytes[6]);
        Assert.Equal(0x21, bytes[7]);

        // 08-11: ForMs (0xAABBCCDD)
        Assert.Equal(0xAA, bytes[8]);
        Assert.Equal(0xBB, bytes[9]);
        Assert.Equal(0xCC, bytes[10]);
        Assert.Equal(0xDD, bytes[11]);

        // 12-15: ActionParam (0x11223344)
        Assert.Equal(0x11, bytes[12]);
        Assert.Equal(0x22, bytes[13]);
        Assert.Equal(0x33, bytes[14]);
        Assert.Equal(0x44, bytes[15]);

        // 16-17: TriggerTag (0x0102)
        Assert.Equal(0x01, bytes[16]);
        Assert.Equal(0x02, bytes[17]);

        // 18-19: ActionTag (0x0304)
        Assert.Equal(0x03, bytes[18]);
        Assert.Equal(0x04, bytes[19]);

        // 22-25: Enabled, TriggerType, CompareOp, ActionType
        Assert.Equal(1, bytes[22]);
        Assert.Equal((byte)TriggerType.ON_RISE, bytes[23]);
        Assert.Equal((byte)CompareOp.GT, bytes[24]);
        Assert.Equal((byte)ActionType.SET_TAG, bytes[25]);

        // 26-31: Reserved[6]
        for (int i = 26; i < 32; i++)
        {
            Assert.Equal(0, bytes[i]);
        }
    }

    [Fact]
    public void Roundtrip_Encode_Decode_Integrity()
    {
        // Arrange
        var trigTag = new TagModel { Index = 5, Name = "DI4", Kind = TagKind.DiscreteInput };
        var actTag = new TagModel { Index = 12, Name = "DO3", Kind = TagKind.DiscreteOutput };
        var guardTag = new TagModel { Index = 22, Name = "VFLAG1", Kind = TagKind.VirtualFlag };

        var rule = new RuleItemModel
        {
            ThresholdLo = -150,
            ThresholdHi = 9999,
            ForMs = 15000,
            ActionParam = 42,
            TriggerTag = trigTag,
            ActionTag = actTag,
            GuardTag = guardTag,
            GuardNegated = true,
            Enabled = true,
            TriggerType = TriggerType.ON_FALL,
            CompareOp = CompareOp.BETWEEN,
            ActionType = ActionType.SEND_ALARM
        };

        // Act
        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);
        var decoded = RuleBinaryEncoder.DecodeRecord(bytes);

        // Assert
        Assert.Equal(-150, decoded.ThresholdLo);
        Assert.Equal(9999, decoded.ThresholdHi);
        Assert.Equal(15000u, decoded.ForMs);
        Assert.Equal(42, decoded.ActionParam);
        Assert.Equal(5, decoded.TriggerTag);
        Assert.Equal(12, decoded.ActionTag);
        Assert.Equal(22, decoded.GuardTag);
        Assert.True(decoded.GuardNegated);
        Assert.True(decoded.Enabled);
        Assert.Equal((byte)TriggerType.ON_FALL, decoded.TriggerType);
        Assert.Equal((byte)CompareOp.BETWEEN, decoded.CompareOp);
        Assert.Equal((byte)ActionType.SEND_ALARM, decoded.ActionType);
    }

    [Fact]
    public void ProgramEncoding_LengthMustBe_RuleCountTimes32()
    {
        // Arrange
        var rules = Enumerable.Range(0, 10).Select(i => new RuleItemModel
        {
            Id = $"R{i}",
            Index = i,
            Enabled = true
        }).ToList();

        // Act
        byte[] payload = RuleBinaryEncoder.EncodeProgram(rules);

        // Assert
        Assert.Equal(320, payload.Length);
    }

    [Fact]
    public void EncodeRule_NoGuard_PacksSentinel0x7FFF()
    {
        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            GuardTag = null
        };

        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);
        ushort guardPacked = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(20, 2));

        Assert.Equal(0x7FFF, guardPacked);
    }

    [Fact]
    public void EncodeRule_GuardIsDI0_PacksZero_AndDecodesAsDI0()
    {
        var di0 = new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            GuardTag = di0,
            GuardNegated = false
        };

        byte[] bytes = RuleBinaryEncoder.EncodeRule(rule);
        ushort guardPacked = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(20, 2));
        Assert.Equal(0x0000, guardPacked);

        var availableTags = new List<TagModel> { di0 };
        var decodedRules = RuleBinaryEncoder.DecodeProgram(bytes, availableTags);
        Assert.Single(decodedRules);
        Assert.NotNull(decodedRules[0].GuardTag);
        Assert.Equal(0, decodedRules[0].GuardTag!.Index);
        Assert.Equal("DI0", decodedRules[0].GuardTag!.Name);
    }
}
