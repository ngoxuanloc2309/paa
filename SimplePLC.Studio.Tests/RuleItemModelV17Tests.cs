using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class RuleItemModelV17Tests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void EncodeRuleV17_MustBeExactly32Bytes_AndReservedBytesZero()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true,
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0"),
            CompareOp = CompareOp.GT,
            ThresholdLo = 50,
            ThresholdHi = 100,
            ForMs = 500,
            ActionType = ActionType.SET_TAG,
            ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0"),
            ActionParam = 1,
            GuardTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI1"),
            GuardNegated = true
        };

        // Act
        byte[] bytes = RuleBinaryEncoder.EncodeRuleV17(rule);

        // Assert: 32 bytes (16 registers)
        Assert.Equal(32, bytes.Length);

        // Reserved bytes [26..31] must be 0x00
        for (int i = 26; i < 32; i++)
        {
            Assert.Equal(0, bytes[i]);
        }

        // Check Breakdown format
        string breakdown = RuleBinaryEncoder.FormatDetailedBreakdown(bytes);
        Assert.Contains("000000000000", breakdown); // reserved 6 bytes
    }

    [Fact]
    public void ToDomainRule_And_FromDomainRule_Roundtrip_PreservesAllData()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var original = new RuleItemModel
        {
            Id = "R5",
            Index = 4,
            Enabled = true,
            TriggerType = TriggerType.ON_FALL,
            TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI2"),
            CompareOp = CompareOp.BETWEEN,
            ThresholdLo = 10,
            ThresholdHi = 90,
            ForMs = 1200,
            ActionType = ActionType.INC_COUNTER,
            ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG_RETAIN0"),
            ActionParam = 3,
            GuardTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0"),
            GuardNegated = false
        };

        // Act
        var domainRule = original.ToDomainRule(_product);
        var restored = RuleItemModel.FromDomainRule(domainRule, tagCatalog.AllTags);

        // Assert
        Assert.Equal(original.Index, restored.Index);
        Assert.Equal(original.Enabled, restored.Enabled);
        Assert.Equal(original.TriggerType, restored.TriggerType);
        Assert.Equal(original.CompareOp, restored.CompareOp);
        Assert.Equal(original.ThresholdLo, restored.ThresholdLo);
        Assert.Equal(original.ThresholdHi, restored.ThresholdHi);
        Assert.Equal(original.ForMs, restored.ForMs);
        Assert.Equal(original.TriggerTag?.Name, restored.TriggerTag?.Name);
        Assert.Equal(original.ActionType, restored.ActionType);
        Assert.Equal(original.ActionParam, restored.ActionParam);
        Assert.Equal(original.ActionTag?.Name, restored.ActionTag?.Name);
        Assert.Equal(original.GuardTag?.Name, restored.GuardTag?.Name);
        Assert.Equal(original.GuardNegated, restored.GuardNegated);
    }

    [Fact]
    public void EncodeProgramV17_And_DecodeProgramV17_Roundtrip_PreservesData()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Id = "R1", Index = 0, Enabled = true,
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0"),
                ActionType = ActionType.SET_TAG,
                ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0"),
                ActionParam = 1
            },
            new()
            {
                Id = "R2", Index = 1, Enabled = true,
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI1"),
                ActionType = ActionType.INC_COUNTER,
                ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG_R0"),
                ActionParam = 5
            }
        };

        // Act
        byte[] payload = RuleBinaryEncoder.EncodeProgramV17(rules);
        var decoded = RuleBinaryEncoder.DecodeProgramV17(payload, tagCatalog.AllTags);

        // Assert
        Assert.Equal(64, payload.Length); // 2 rules * 32 bytes
        Assert.Equal(2, decoded.Count);
        Assert.Equal(rules[0].Index, decoded[0].Index);
        Assert.Equal(rules[0].TriggerTag?.Index, decoded[0].TriggerTag?.Index);
        Assert.Equal(rules[0].ActionTag?.Index, decoded[0].ActionTag?.Index);
        Assert.Equal(rules[1].ActionParam, decoded[1].ActionParam);
    }

    [Fact]
    public void DecodeProgram_AutoDetects_V17_32Byte_Payload()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Id = "R1", Index = 0, Enabled = true,
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0"),
                ActionType = ActionType.SET_TAG,
                ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0")
            }
        };

        // Act
        byte[] payload32 = RuleBinaryEncoder.EncodeProgramV17(rules);
        var decoded = RuleBinaryEncoder.DecodeProgram(payload32, tagCatalog.AllTags);

        // Assert
        Assert.Single(decoded);
        Assert.Equal("R1", decoded[0].Id);
        Assert.Equal(rules[0].TriggerType, decoded[0].TriggerType);
    }

    [Fact]
    public void Crc16Modbus_On_EncodeProgramV17_Matches_ComputeFromRegisters()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var rule = new RuleItemModel
        {
            Id = "R1", Index = 0, Enabled = true,
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0"),
            CompareOp = CompareOp.GT,
            ThresholdLo = 1234,
            ThresholdHi = 5678,
            ForMs = 1000,
            ActionType = ActionType.SET_TAG,
            ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0"),
            ActionParam = 42
        };

        // Act: 32 bytes
        byte[] bytes = RuleBinaryEncoder.EncodeProgramV17(new[] { rule });

        // Convert to registers
        ushort[] registers = new ushort[bytes.Length / 2];
        for (int i = 0; i < registers.Length; i++)
        {
            registers[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i * 2, 2));
        }

        ushort crcFromBytes = SimplePLC.Protocol.Cryptography.Crc16Modbus.Compute(bytes);
        ushort crcFromRegisters = SimplePLC.Protocol.Cryptography.Crc16Modbus.ComputeFromRegisters(registers);

        // Assert: CRC byte stream must match CRC register-based stream exactly!
        Assert.Equal(crcFromBytes, crcFromRegisters);
        Assert.NotEqual(0, crcFromBytes);
    }
}
