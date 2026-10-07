using System.Runtime.InteropServices;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Dual-level Golden Tests validating roundtrip fidelity:
/// Level 1: 3200 canonical bytes wire equality (zero-allocation serialization/deserialization across 1600 registers).
/// Level 2: Domain semantic equality across full Deploy -> Staging -> Commit -> Read Active -> Domain mapping.
/// </summary>
public class RuleTableRoundtripGoldenTests
{
    private const int MaxRules = 100;
    private const int RegistersPerRule = 16;
    private const int TotalCanonicalRegisters = MaxRules * RegistersPerRule; // 1600 registers
    private const int TotalCanonicalBytes = TotalCanonicalRegisters * 2;      // Exactly 3200 bytes

    [Fact]
    public void Level1_WireEquality_100Rules_Exact3200BytesCanonicalWireRoundtrip()
    {
        // Arrange: Generate 100 diverse RuleRecordDtos covering all operators, comparators, and trigger types
        var originalDtos = new RuleRecordDto[MaxRules];
        for (int i = 0; i < MaxRules; i++)
        {
            originalDtos[i] = new RuleRecordDto
            {
                ThresholdLo = (short)(-500 + i * 10),
                ThresholdHi = (short)(500 + i * 15),
                ForMs = (ushort)(100 + (i * 25) % 5000),
                ActionParam = (short)(i % 2 == 0 ? 1 : -i),
                TriggerTag = (ushort)(i % 20),
                ActionTag = (ushort)(8 + (i % 8)),
                GuardTag = (ushort)(i % 3 == 0 ? 0 : (i % 2 == 0 ? (ushort)(i % 8) : (ushort)((i % 8) | ModbusRegisterMap.GuardTagNegateMask))),
                Enabled = i % 7 != 0,
                TriggerType = (SPLC_TriggerType)((i % 6) + 1), // 1..6
                CompareOp = (SPLC_CompareOp)(i % 7),          // 0..6
                ActionType = (SPLC_ActionType)((i % 8) + 1)   // 1..8
            };
        }

        // Act 1: Encode into canonical wire registers (100 * 16 = 1600 registers = 3200 bytes)
        ushort[] wireRegisters = new ushort[TotalCanonicalRegisters];
        RegisterCodec.EncodeRuleRecords(originalDtos, wireRegisters);

        // Convert to Big-Endian Wire Bytes
        byte[] wireBytes = new byte[TotalCanonicalBytes];
        for (int i = 0; i < TotalCanonicalRegisters; i++)
        {
            wireBytes[i * 2] = (byte)(wireRegisters[i] >> 8);
            wireBytes[i * 2 + 1] = (byte)(wireRegisters[i] & 0xFF);
        }

        // Assert 1: Exactly 3200 bytes / 1600 registers
        Assert.Equal(1600, wireRegisters.Length);
        Assert.Equal(3200, wireBytes.Length);

        // Act 2: Calculate CRC16 on registers and wire bytes
        ushort regCrc = Crc16Modbus.ComputeFromRegisters(wireRegisters);
        ushort byteCrc = Crc16Modbus.Compute(wireBytes);
        Assert.Equal(regCrc, byteCrc);
        Assert.NotEqual(0, regCrc);

        // Act 3: Decode back to DTOs from the wire registers
        var decodedDtos = RegisterCodec.DecodeRuleRecords(wireRegisters, MaxRules);

        // Assert 3: Compare each decoded DTO with the original DTO (Field-for-field equality)
        for (int i = 0; i < MaxRules; i++)
        {
            var orig = originalDtos[i];
            var dec = decodedDtos[i];

            Assert.Equal(orig.ThresholdLo, dec.ThresholdLo);
            Assert.Equal(orig.ThresholdHi, dec.ThresholdHi);
            Assert.Equal(orig.ForMs, dec.ForMs);
            Assert.Equal(orig.ActionParam, dec.ActionParam);
            Assert.Equal(orig.TriggerTag, dec.TriggerTag);
            Assert.Equal(orig.ActionTag, dec.ActionTag);
            Assert.Equal(orig.GuardTag, dec.GuardTag);
            Assert.Equal(orig.GuardTagIndex, dec.GuardTagIndex);
            Assert.Equal(orig.GuardNegated, dec.GuardNegated);
            Assert.Equal(orig.Enabled, dec.Enabled);
            Assert.Equal(orig.TriggerType, dec.TriggerType);
            Assert.Equal(orig.CompareOp, dec.CompareOp);
            Assert.Equal(orig.ActionType, dec.ActionType);
        }

        // Act 4: Re-encode decoded DTOs to a second buffer and verify 100% byte-for-byte equality
        ushort[] reEncodedRegisters = new ushort[TotalCanonicalRegisters];
        RegisterCodec.EncodeRuleRecords(decodedDtos, reEncodedRegisters);

        Assert.True(wireRegisters.AsSpan().SequenceEqual(reEncodedRegisters.AsSpan()), "Wire registers must match word-for-word (zero drift across 1600 registers).");
        Assert.Equal(regCrc, Crc16Modbus.ComputeFromRegisters(reEncodedRegisters));
    }

    [Fact]
    public async Task Level2_DomainSemanticEquality_100Rules_FullDeployAndReadbackRoundtrip()
    {
        // Arrange: Create ProductDefinition with 60 tags
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var domainTable = new RuleTable();

        // Populate 100 diverse and valid Domain Rules satisfying all invariants
        for (ushort i = 0; i < MaxRules; i++)
        {
            // Pick tags:
            // Trigger tags: DI0..DI7 (0..7), AI0..AI3 (16..19), VFLAG0..VFLAG15 (20..35)
            ushort trigTagIdx = (ushort)(i % 2 == 0 ? (i % 8) : (16 + (i % 4)));
            var trigTag = product.FindTagByIndex(trigTagIdx)!;

            // Action target tags: DO0..DO7 (8..15) for boolean, VREG0..VREG15 (36..51) for int
            bool targetIsInt = i % 4 == 0;
            ushort actionTagIdx = targetIsInt ? (ushort)(36 + (i % 16)) : (ushort)(8 + (i % 8));
            var actionTag = product.FindTagByIndex(actionTagIdx)!;

            // Trigger Kind & Compare Operator
            var trigKind = (TriggerKind)((i % 4) + 1); // 1..4 (OnRise, OnFall, TimeWindow, Interval)
            var trig = new TriggerModel(trigTag, trigKind)
            {
                CompareOp = (CompareOperator)(i % 7),
                ThresholdLo = (short)(-100 + i),
                ThresholdHi = (short)(100 + i * 2),
                ForMs = (ushort)(50 + (i * 10) % 2000)
            };

            if (trig.CompareOp == CompareOperator.Between && trig.ThresholdLo > trig.ThresholdHi)
            {
                (trig.ThresholdLo, trig.ThresholdHi) = (trig.ThresholdHi, trig.ThresholdLo);
            }

            // Action Kind & Parameter
            ActionKind actionKind;
            if (targetIsInt)
            {
                actionKind = (i % 2 == 0) ? ActionKind.AddTag : ActionKind.IncrementCounter;
            }
            else
            {
                actionKind = (i % 2 == 0) ? ActionKind.SetTag : ActionKind.ToggleTag;
            }
            var act = new ActionModel(actionTag, actionKind, (short)(i % 100));

            // Guard Model: 1/3 no guard, 1/3 DI guard normal, 1/3 DI guard negated
            GuardModel guard = GuardModel.Empty;
            if (i % 3 != 0)
            {
                var guardTag = product.FindTagByIndex((ushort)(1 + (i % 7)))!; // DI1..DI7
                guard = new GuardModel(guardTag, negated: i % 2 == 1);
            }

            var rule = new Rule(
                ruleIndex: i,
                name: $"GoldenRule_{i:D3}",
                trigger: trig,
                action: act,
                guard: guard,
                enabled: i % 5 != 0
            );

            domainTable.AddRule(rule);
        }

        // Verify domain table validity before deployment
        var validation = RuleTableValidator.Validate(domainTable);
        Assert.True(validation.IsValid, string.Join(", ", validation.Errors.Select(e => e.Message)));
        Assert.Equal(100, domainTable.Rules.Count);

        // Act 1: Domain -> Wire DTOs
        var dtos = RuleMapper.ToDtoArray(domainTable);
        Assert.Equal(100, dtos.Length);

        // Act 2: Deploy to MCU via RuleTableWriter
        var fakeClient = new FakeModbusClient(isConnected: true);
        var writer = new RuleTableWriter(fakeClient);
        var deployResult = await writer.DeployRulesAsync(slaveId: 1, dtos);

        // Assert 2: Deploy successfully transitioned Staging -> Commit -> Active
        Assert.True(deployResult.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.NONE, deployResult.ErrorCode);
        Assert.True(deployResult.ActiveVersion >= 1);

        // Act 3: Read back from MCU via RuleTableReader
        var reader = new RuleTableReader(fakeClient);
        var readBackDtos = await reader.ReadActiveRulesAsync(slaveId: 1);

        // Assert 3: Exactly 100 rules read back
        Assert.Equal(100, readBackDtos.Count);

        // Act 4: Map back to Domain RuleTable
        var readBackTable = RuleMapper.ToDomainTable(readBackDtos, product);
        Assert.Equal(100, readBackTable.Rules.Count);

        // Assert 4: Domain Semantic Equality for all 100 rules
        for (int i = 0; i < 100; i++)
        {
            var orig = domainTable.Rules[i];
            var restored = readBackTable.Rules[i];

            Assert.Equal(orig.RuleIndex, restored.RuleIndex);
            Assert.Equal(orig.Enabled, restored.Enabled);

            // Trigger comparison
            Assert.Equal(orig.Trigger.Type, restored.Trigger.Type);
            Assert.Equal(orig.Trigger.Tag.TagIndex, restored.Trigger.Tag.TagIndex);
            Assert.Equal(orig.Trigger.CompareOp, restored.Trigger.CompareOp);
            Assert.Equal(orig.Trigger.ThresholdLo, restored.Trigger.ThresholdLo);
            Assert.Equal(orig.Trigger.ThresholdHi, restored.Trigger.ThresholdHi);
            Assert.Equal(orig.Trigger.ForMs, restored.Trigger.ForMs);

            // Action comparison
            Assert.Equal(orig.Action.Type, restored.Action.Type);
            Assert.Equal(orig.Action.TargetTag.TagIndex, restored.Action.TargetTag.TagIndex);
            Assert.Equal(orig.Action.Parameter, restored.Action.Parameter);

            // Guard comparison
            Assert.Equal(orig.Guard.HasGuard, restored.Guard.HasGuard);
            if (orig.Guard.HasGuard)
            {
                Assert.Equal(orig.Guard.Negated, restored.Guard.Negated);
                Assert.Equal(orig.Guard.Tag!.TagIndex, restored.Guard.Tag!.TagIndex);
            }
        }
    }
}
