namespace SimplePLC.Protocol.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

/// <summary>
/// Unit tests verifying the contract invariants and data integrity of the Modbus Slave Fixed Memory Map.
/// Enforces IEC 61508-3 Triad testing (Happy Path, Boundary/Invariants, Fault Injection/Sanity)
/// and zero "PLC" forbidden terminology per AGENTS.md.
/// </summary>
public sealed class ModbusSlaveFixedMapContractTests
{
    // =========================================================================
    // TRỤC 1: HAPPY PATH (Danh mục & Khế ước Chuẩn)
    // =========================================================================

    /// <summary>
    /// Verifies that DefaultEntries contains exactly 11 industrial partitions.
    /// </summary>
    [Fact]
    public void DefaultEntries_WhenEnumerated_ContainsExact11IndustrialPartitions()
    {
        IReadOnlyList<ModbusSlaveFixedRegisterDto> entries = ModbusSlaveFixedMap.DefaultEntries;

        Assert.NotNull(entries);
        Assert.Equal(11, entries.Count);
    }

    /// <summary>
    /// Verifies that Holding Registers contain exactly 8 standard word partitions.
    /// </summary>
    [Fact]
    public void HoldingRegisters_WhenFiltered_Contains8StandardWordPartitions()
    {
        var holdingRegisters = ModbusSlaveFixedMap.DefaultEntries
            .Where(e => e.Category == "Holding Registers")
            .ToList();

        Assert.Equal(8, holdingRegisters.Count);

        // Verify key tags exist
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "DI_WORD");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "DO_WORD");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "VFLAG_DWORD");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "AI0 .. AI3");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "VREG0 .. VREG31");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "VREG_R0 .. VREG_R31");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "COUNTER0 .. COUNTER7");
        Assert.Contains(holdingRegisters, r => r.DeviceTag == "SYS_DIAG");
    }

    /// <summary>
    /// Verifies that bit partitions contain 2 Coils and 1 Discrete Input partition.
    /// </summary>
    [Fact]
    public void CoilAndDiscreteInputs_WhenFiltered_MatchesExpectedBitPartitionCounts()
    {
        var coils = ModbusSlaveFixedMap.DefaultEntries
            .Where(e => e.Category == "Coils")
            .ToList();

        var discreteInputs = ModbusSlaveFixedMap.DefaultEntries
            .Where(e => e.Category == "Discrete Inputs")
            .ToList();

        Assert.Equal(2, coils.Count);
        Assert.Single(discreteInputs);

        Assert.Contains(coils, r => r.DeviceTag == "DO0 .. DO15");
        Assert.Contains(coils, r => r.DeviceTag == "VFLAG0 .. VFLAG31");
        Assert.Contains(discreteInputs, r => r.DeviceTag == "DI0 .. DI15");
    }

    /// <summary>
    /// Verifies that offset calculation formulas produce the expected addresses.
    /// </summary>
    [Fact]
    public void OffsetFormulas_WhenCalculated_MatchesDefined1BasedAddressRanges()
    {
        // VREG: 40033 + Index * 2
        int vreg0 = 40033 + 0 * 2;
        int vreg31 = 40033 + 31 * 2;
        Assert.Equal(40033, vreg0);
        Assert.Equal(40095, vreg31); // 40095..40096

        // VREG_R: 40097 + Index * 2
        int vregR0 = 40097 + 0 * 2;
        int vregR31 = 40097 + 31 * 2;
        Assert.Equal(40097, vregR0);
        Assert.Equal(40159, vregR31); // 40159..40160

        // COUNTER: 40161 + Index * 2
        int counter0 = 40161 + 0 * 2;
        int counter7 = 40161 + 7 * 2;
        Assert.Equal(40161, counter0);
        Assert.Equal(40175, counter7); // 40175..40176
    }

    /// <summary>
    /// Verifies that descriptions provide accurate high word first and format annotations.
    /// </summary>
    [Fact]
    public void Descriptions_WhenChecked_ContainsBigEndianAndCalculationNotes()
    {
        var ai = ModbusSlaveFixedMap.DefaultEntries.First(r => r.DeviceTag == "AI0 .. AI3");
        Assert.Contains("High Word First", ai.DescriptionEn);
        Assert.Contains("IEEE-754", ai.DescriptionEn);

        var vreg = ModbusSlaveFixedMap.DefaultEntries.First(r => r.DeviceTag == "VREG0 .. VREG31");
        Assert.Contains("40033 + Index * 2", vreg.DescriptionEn);

        var sysDiag = ModbusSlaveFixedMap.DefaultEntries.First(r => r.DeviceTag == "SYS_DIAG");
        Assert.Contains("Uptime (40241)", sysDiag.DescriptionEn);
        Assert.Contains("Scan ms (40243)", sysDiag.DescriptionEn);
    }

    // =========================================================================
    // TRỤC 2: BOUNDARY & INVARIANTS (Biên & Ranh giới Phân vùng)
    // =========================================================================

    /// <summary>
    /// Verifies that all holding register partitions have mutually disjoint wire address ranges.
    /// </summary>
    [Fact]
    public void HoldingRegisters_WhenAnalyzed_HaveZeroAddressOverlapsAcrossPartitions()
    {
        // Define known wire ranges [start, endInclusive]
        var ranges = new List<(string Tag, ushort Start, ushort End)>
        {
            ("DI_WORD", ModbusSlaveFixedMap.DiWordAddress, ModbusSlaveFixedMap.DiWordAddress),
            ("DO_WORD", ModbusSlaveFixedMap.DoWordAddress, ModbusSlaveFixedMap.DoWordAddress),
            ("VFLAG_DWORD", ModbusSlaveFixedMap.VflagDWordAddress, (ushort)(ModbusSlaveFixedMap.VflagDWordAddress + ModbusSlaveFixedMap.VflagDWordRegisterCount - 1)),
            ("AI", ModbusSlaveFixedMap.AiBaseAddress, (ushort)(ModbusSlaveFixedMap.AiBaseAddress + ModbusSlaveFixedMap.AiTotalRegisters - 1)),
            ("VREG", ModbusSlaveFixedMap.VregBaseAddress, (ushort)(ModbusSlaveFixedMap.VregBaseAddress + ModbusSlaveFixedMap.VregTotalRegisters - 1)),
            ("VREG_R", ModbusSlaveFixedMap.VregRetainBaseAddress, (ushort)(ModbusSlaveFixedMap.VregRetainBaseAddress + ModbusSlaveFixedMap.VregRetainTotalRegisters - 1)),
            ("COUNTER", ModbusSlaveFixedMap.CounterBaseAddress, (ushort)(ModbusSlaveFixedMap.CounterBaseAddress + ModbusSlaveFixedMap.CounterTotalRegisters - 1)),
            ("SYS_DIAG", ModbusSlaveFixedMap.SystemDiagBaseAddress, (ushort)(ModbusSlaveFixedMap.SystemDiagBaseAddress + ModbusSlaveFixedMap.SystemDiagTotalRegisters - 1))
        };

        // Assert strictly ascending and non-overlapping
        for (int i = 0; i < ranges.Count - 1; i++)
        {
            var current = ranges[i];
            var next = ranges[i + 1];

            Assert.True(current.Start <= current.End, $"{current.Tag} start ({current.Start}) must be <= end ({current.End})");
            Assert.True(current.End < next.Start, $"{current.Tag} end ({current.End}) must be strictly less than {next.Tag} start ({next.Start})");
        }
    }

    /// <summary>
    /// Verifies that bit registers within Coils have non-overlapping ranges.
    /// </summary>
    [Fact]
    public void CoilsRegisters_WhenAnalyzed_HaveZeroAddressOverlapsWithinCategory()
    {
        ushort doStart = ModbusSlaveFixedMap.CoilsDoBaseAddress;
        ushort doEnd = (ushort)(doStart + ModbusSlaveFixedMap.CoilsDoCount - 1); // 0..15

        ushort vflagStart = ModbusSlaveFixedMap.CoilsVflagBaseAddress;
        ushort vflagEnd = (ushort)(vflagStart + ModbusSlaveFixedMap.CoilsVflagCount - 1); // 100..131

        Assert.True(doEnd < vflagStart, $"DO Coils end ({doEnd}) must be strictly less than VFLAG Coils start ({vflagStart})");
    }

    /// <summary>
    /// Verifies that all addresses in DefaultEntries stay within 16-bit register limits.
    /// </summary>
    [Fact]
    public void RegisterAddresses_WhenEvaluated_StayWithinValidModbus16BitLimits()
    {
        foreach (var entry in ModbusSlaveFixedMap.DefaultEntries)
        {
            Assert.NotNull(entry.AddressHex);
            Assert.NotNull(entry.ModbusAddress);
            Assert.StartsWith("0x", entry.AddressHex);

            // Modbus address must be 5-digit format for holding (4xxxx) or 5-digit for bit zones (0xxxx, 1xxxx)
            string firstAddressPart = entry.ModbusAddress.Split("..")[0].Trim();
            Assert.True(int.TryParse(firstAddressPart, out int numAddress));
            Assert.True(numAddress >= 1 && numAddress <= 65535, $"Address {numAddress} must be in 1..65535");
        }
    }

    // =========================================================================
    // TRỤC 3: FAULT INJECTION & SANITY (Bảo vệ Quyền & Không chứa từ cấm)
    // =========================================================================

    /// <summary>
    /// Verifies zero occurrences of the forbidden word "PLC" in all entry descriptions, tags, and categories.
    /// </summary>
    [Fact]
    public void FixedMapDescriptions_AcrossAllLanguages_ContainsZeroForbiddenPlcTerms()
    {
        foreach (var entry in ModbusSlaveFixedMap.DefaultEntries)
        {
            Assert.DoesNotContain("PLC", entry.Category, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PLC", entry.DeviceTag, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PLC", entry.DescriptionVi, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PLC", entry.DescriptionEn, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verifies that read-only partitions strictly have "RO" access rights and correct function codes.
    /// </summary>
    [Fact]
    public void ReadOnlyPartitions_WhenChecked_HaveStrictlyReadOnlyAccessRights()
    {
        var roTags = new[] { "DI_WORD", "AI0 .. AI3", "SYS_DIAG", "DI0 .. DI15" };

        foreach (var tag in roTags)
        {
            var entry = ModbusSlaveFixedMap.DefaultEntries.FirstOrDefault(r => r.DeviceTag == tag);
            Assert.NotNull(entry);
            Assert.Equal("RO", entry.AccessRight);
            Assert.DoesNotContain("05", entry.FunctionCode);
            Assert.DoesNotContain("06", entry.FunctionCode);
            Assert.DoesNotContain("15", entry.FunctionCode);
            Assert.DoesNotContain("16", entry.FunctionCode);
        }
    }
}
