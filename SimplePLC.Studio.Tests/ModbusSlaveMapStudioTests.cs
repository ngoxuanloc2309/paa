namespace SimplePLC.Studio.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

/// <summary>
/// Unit tests for Modbus Slave fixed addressing and memory map reference in SynaptiX Studio.
/// Ensures zero 'PLC' terminology, compliance with industrial fixed memory partitions,
/// and accurate bilingual export.
/// </summary>
public sealed class ModbusSlaveMapStudioTests
{
    [Fact]
    public void RoleSwitching_WhenToggledBetweenMasterAndSlave_UpdatesViewModelAndSlaveViewState()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 2,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Master" },
                new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Slave", SlaveId = 10 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        // Port 1 is Master
        vm.SelectedPort = vm.Ports[0];
        Assert.True(vm.SelectedPort.IsMasterMode);
        Assert.False(vm.SelectedPort.IsSlaveMode);
        Assert.False(vm.IsSlaveViewActive);
        Assert.Contains("Master", vm.SelectedPort.SummaryBadge);

        // Port 2 is Slave
        vm.SelectedPort = vm.Ports[1];
        Assert.False(vm.SelectedPort.IsMasterMode);
        Assert.True(vm.SelectedPort.IsSlaveMode);
        Assert.True(vm.IsSlaveViewActive);
        Assert.Equal((byte)10, vm.SelectedPort.SlaveId);
        Assert.Contains("Slave", vm.SelectedPort.SummaryBadge);
        Assert.Contains("ID: 10", vm.SelectedPort.SummaryBadge);

        // Switch Port 1 to Slave
        vm.SelectedPort = vm.Ports[0];
        vm.SetSlaveRoleCommand.Execute(null);
        Assert.True(vm.SelectedPort.IsSlaveMode);
        Assert.True(vm.IsSlaveViewActive);

        // Switch back to Master
        vm.SetMasterRoleCommand.Execute(null);
        Assert.True(vm.SelectedPort.IsMasterMode);
        Assert.False(vm.IsSlaveViewActive);
    }

    [Fact]
    public void FixedRegisterMap_WhenInitialized_ContainsStandardIndustrialPartitions()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 1,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 2 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        Assert.NotNull(vm.FixedRegisterMap);
        Assert.NotEmpty(vm.FixedRegisterMap);

        // Holding Registers Partitions
        var diWord = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "DI_WORD");
        Assert.NotNull(diWord);
        Assert.Equal("0x0000", diWord.AddressHex);
        Assert.Equal("40001", diWord.ModbusAddress);
        Assert.Equal("RO", diWord.AccessRight);
        Assert.True(diWord.IsReadOnly);

        var doWord = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "DO_WORD");
        Assert.NotNull(doWord);
        Assert.Equal("0x0001", doWord.AddressHex);
        Assert.Equal("40002", doWord.ModbusAddress);
        Assert.Equal("RW", doWord.AccessRight);
        Assert.False(doWord.IsReadOnly);

        var vflag = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "VFLAG_DWORD");
        Assert.NotNull(vflag);
        Assert.Equal("RW", vflag.AccessRight);

        var ai = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "AI0 .. AI3");
        Assert.NotNull(ai);
        Assert.Equal("RO", ai.AccessRight);
        Assert.True(ai.IsReadOnly);
        Assert.Equal("FLOAT32", ai.DataType);

        var vreg = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "VREG0 .. VREG31");
        Assert.NotNull(vreg);
        Assert.Equal("RW", vreg.AccessRight);

        var vregR = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "VREG_R0 .. VREG_R31");
        Assert.NotNull(vregR);
        Assert.Equal("RW", vregR.AccessRight);

        var counter = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "COUNTER0 .. COUNTER7");
        Assert.NotNull(counter);
        Assert.Equal("RW", counter.AccessRight);

        var sysDiag = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "SYS_DIAG");
        Assert.NotNull(sysDiag);
        Assert.Equal("RO", sysDiag.AccessRight);
        Assert.True(sysDiag.IsReadOnly);

        // Bit zone check
        var diBits = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "DI0 .. DI15");
        Assert.NotNull(diBits);
        Assert.Equal("Discrete Inputs", diBits.Category);
        Assert.Equal("FC 02", diBits.FunctionCode);
        Assert.Equal("RO", diBits.AccessRight);

        var doBits = vm.FixedRegisterMap.FirstOrDefault(r => r.DeviceTag == "DO0 .. DO15");
        Assert.NotNull(doBits);
        Assert.Equal("Coils", doBits.Category);
        Assert.Equal("FC 01, 05, 15", doBits.FunctionCode);
        Assert.Equal("RW", doBits.AccessRight);
    }

    [Fact]
    public void ExportModbusMap_WhenInvoked_GeneratesAccurateMarkdownAndCsvWithoutPlcTerminology()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 1,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        string csv = vm.GenerateExportModbusMapCsv();
        string md = vm.GenerateExportModbusMapMarkdown();

        // Must NOT contain "PLC" anywhere
        Assert.DoesNotContain("PLC", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PLC", md, StringComparison.OrdinalIgnoreCase);

        // CSV assertions
        Assert.Contains("Category,Hex Address,Modbus Address (1-based),Device Tag,Data Type,Access Right,Modbus Function,Description", csv);
        Assert.Contains("DI_WORD", csv);
        Assert.Contains("DO_WORD", csv);
        Assert.Contains("VREG0 .. VREG31", csv);
        Assert.Contains("RO", csv);
        Assert.Contains("RW", csv);

        // Markdown assertions
        Assert.Contains("# SynaptiX Controller - Modbus Slave Fixed Memory Map", md);
        Assert.Contains("| Category | Hex Address | Modbus Address | Device Tag | Data Type | Access | Function | Description |", md);
        Assert.Contains("`0x0000`", md);
        Assert.Contains("`40001`", md);
        Assert.Contains("**DI_WORD**", md);
    }

    [Fact]
    public async Task ExportModbusMapCommand_WhenCancelled_DoesNotWriteFile()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 1,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);
        vm.SaveFilePicker = (filter, defaultName) => null; // Simulate Cancel

        await vm.ExportModbusMapCommand.ExecuteAsync(null);

        Assert.False(vm.HasLastExportedFilePath);
    }

    [Fact]
    public async Task ExportModbusMapCommand_WhenSavedAsCsv_WritesFileProperly()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 1,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_slave_fixed_map_{Guid.NewGuid():N}.csv");
        try
        {
            vm.SaveFilePicker = (filter, defaultName) => tempFile;

            await vm.ExportModbusMapCommand.ExecuteAsync(null);

            Assert.True(File.Exists(tempFile));
            Assert.True(vm.HasLastExportedFilePath);
            Assert.Equal(tempFile, vm.LastExportedFilePath);
            Assert.Contains(Path.GetFileName(tempFile), vm.ExportNoticeMessage);

            string fileContent = File.ReadAllText(tempFile);
            Assert.Contains("Device Tag", fileContent);
            Assert.Contains("DI_WORD", fileContent);
            Assert.DoesNotContain("PLC", fileContent, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ExportNoticeMessage_WhenSetAndCleared_ReflectsAccurateState()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };
        var vm = new ModbusPortConfigViewModel(meta);

        Assert.False(vm.HasExportNoticeMessage);
        vm.ExportNoticeMessage = "Export complete";
        Assert.True(vm.HasExportNoticeMessage);
        vm.ExportNoticeMessage = string.Empty;
        Assert.False(vm.HasExportNoticeMessage);
    }

    [Fact]
    public void FixedRegisterDescription_WhenLanguageSwitched_UpdatesBetweenVietnameseAndEnglish()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };
        var vm = new ModbusPortConfigViewModel(meta);

        var diWord = vm.FixedRegisterMap.First(r => r.DeviceTag == "DI_WORD");

        LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.Contains("ngõ vào số", diWord.Description);

        LocalizationService.Instance.CurrentLanguage = "en";
        Assert.Contains("Digital Inputs", diWord.Description);

        // Reset back to English default
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    [Fact]
    public void SelectMemoryTab_WhenExecuted_FiltersRegistersCorrectly()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };
        var vm = new ModbusPortConfigViewModel(meta);

        // 1. Initial State: All
        Assert.True(vm.IsTabAllSelected);
        Assert.False(vm.IsTabHoldingSelected);
        Assert.False(vm.IsTabCoilsSelected);
        Assert.False(vm.IsTabDiscreteInputsSelected);
        Assert.Equal(vm.FixedRegisterMap.Count, vm.FilteredFixedRegisterMap.Count);
        Assert.Equal(11, vm.CountAll);
        Assert.Equal(8, vm.CountHolding);
        Assert.Equal(2, vm.CountCoils);
        Assert.Equal(1, vm.CountDiscreteInputs);

        // 2. Select Holding Registers
        vm.SelectMemoryTabCommand.Execute("Holding");
        Assert.False(vm.IsTabAllSelected);
        Assert.True(vm.IsTabHoldingSelected);
        Assert.Equal(8, vm.FilteredFixedRegisterMap.Count);
        Assert.All(vm.FilteredFixedRegisterMap, r => Assert.Equal("Holding Registers", r.Category));

        // 3. Select Coils
        vm.SelectMemoryTabCommand.Execute("Coils");
        Assert.True(vm.IsTabCoilsSelected);
        Assert.Equal(2, vm.FilteredFixedRegisterMap.Count);
        Assert.All(vm.FilteredFixedRegisterMap, r => Assert.Equal("Coils", r.Category));

        // 4. Select Discrete Inputs
        vm.SelectMemoryTabCommand.Execute("Discrete");
        Assert.True(vm.IsTabDiscreteInputsSelected);
        Assert.Single(vm.FilteredFixedRegisterMap);
        Assert.Equal("Discrete Inputs", vm.FilteredFixedRegisterMap[0].Category);

        // 5. Back to All
        vm.SelectMemoryTabCommand.Execute("All");
        Assert.True(vm.IsTabAllSelected);
        Assert.Equal(11, vm.FilteredFixedRegisterMap.Count);
    }

    [Fact]
    public void ProtocolWireTooltip_WhenEvaluated_ContainsHexAndFc()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };
        var vm = new ModbusPortConfigViewModel(meta);

        var diWord = vm.FixedRegisterMap.First(r => r.DeviceTag == "DI_WORD");
        Assert.Contains("0x0000", diWord.ProtocolWireTooltip);
        Assert.Contains("FC 03", diWord.ProtocolWireTooltip);

        var doCoils = vm.FixedRegisterMap.First(r => r.DeviceTag == "DO0 .. DO15");
        Assert.Contains("FC 01, 05, 15", doCoils.ProtocolWireTooltip);
    }

    [Fact]
    public void SelectMemoryTab_WhenGivenInvalidOrNullTabName_DefaultsToTabAllSafely()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };
        var vm = new ModbusPortConfigViewModel(meta);

        // Select Coils first
        vm.SelectMemoryTabCommand.Execute("Coils");
        Assert.True(vm.IsTabCoilsSelected);

        // Pass invalid tab string
        vm.SelectMemoryTabCommand.Execute("UnknownTabName");
        Assert.True(vm.IsTabAllSelected);
        Assert.Equal(11, vm.FilteredFixedRegisterMap.Count);

        // Select Holding first
        vm.SelectMemoryTabCommand.Execute("Holding");
        Assert.True(vm.IsTabHoldingSelected);

        // Pass null
        vm.SelectMemoryTabCommand.Execute(null);
        Assert.True(vm.IsTabAllSelected);
        Assert.Equal(11, vm.FilteredFixedRegisterMap.Count);
    }

    [Fact]
    public void ExportModbusMap_WhenFilteredByTab_ExportsOnlyActiveTabRegisters()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 1,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        // Select Coils tab (only 2 entries)
        vm.SelectMemoryTabCommand.Execute("Coils");

        string csv = vm.GenerateExportModbusMapCsv();
        string md = vm.GenerateExportModbusMapMarkdown();

        // Must contain Coils items
        Assert.Contains("DO0 .. DO15", csv);
        Assert.Contains("VFLAG0 .. VFLAG31", csv);

        // Must NOT contain Holding Registers items like DI_WORD or VREG
        Assert.DoesNotContain("DI_WORD", csv);
        Assert.DoesNotContain("VREG0 .. VREG31", csv);

        // Markdown table check
        Assert.Contains("DO0 .. DO15", md);
        Assert.Contains("VFLAG0 .. VFLAG31", md);
        Assert.DoesNotContain("DI_WORD", md);
    }

    [Fact]
    public void FixedRegisterViewModel_WhenConstructed_ExposesFormattedPropertiesAndTooltip()
    {
        var dto = new SimplePLC.Protocol.Dto.ModbusSlaveFixedRegisterDto(
            Category: "Holding Registers",
            AddressHex: "0x0020 .. 0x005F",
            ModbusAddress: "40033 .. 40096",
            DeviceTag: "VREG0 .. VREG31",
            DataType: "INT32 / FLOAT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "32 biến tính toán",
            DescriptionEn: "32 Virtual Registers"
        );

        var vm = new ModbusSlaveFixedRegisterViewModel(dto);

        Assert.Equal("Holding Registers", vm.Category);
        Assert.Equal("0x0020 .. 0x005F", vm.AddressHex);
        Assert.Equal("40033 .. 40096", vm.ModbusAddress);
        Assert.Equal("VREG0 .. VREG31", vm.DeviceTag);
        Assert.Equal("INT32 / FLOAT32", vm.DataType);
        Assert.Equal("RW", vm.AccessRight);
        Assert.False(vm.IsReadOnly);
        Assert.Equal("FC 03, 06, 16", vm.FunctionCode);
        Assert.Contains("0x0020 .. 0x005F", vm.ProtocolWireTooltip);
        Assert.Contains("FC 03, 06, 16", vm.ProtocolWireTooltip);
    }

    [Fact]
    public void ModbusPortConfigViewModel_WhenAllPortsAreMaster_SlaveViewRemainsInactive()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 2,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Master" },
                new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Master" }
            }
        };

        var vm = new ModbusPortConfigViewModel(meta);

        vm.SelectedPort = vm.Ports[0];
        Assert.False(vm.IsSlaveViewActive);
        Assert.True(vm.SelectedPort.IsMasterMode);

        vm.SelectedPort = vm.Ports[1];
        Assert.False(vm.IsSlaveViewActive);
        Assert.True(vm.SelectedPort.IsMasterMode);
    }
}
