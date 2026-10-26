namespace SimplePLC.Studio.Tests;



using System.Collections.Generic;

using System.Linq;

using SimplePLC.Studio.Models;

using SimplePLC.Studio.ViewModels;

using Xunit;



public sealed class ModbusSlaveMapStudioTests

{

    [Fact]

    public void RoleSwitching_UpdatesViewModelState()

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

    public void SmartPopulateDefault_CorrectlyEnforcesSandboxAccessControl()

    {

        var meta = new ProjectMetadata

        {

            PhysicalRs485PortCount = 2,

            ModbusPorts = new List<ModbusPortConfigModel>

            {

                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 2 }

            }

        };



        var vm = new ModbusPortConfigViewModel(meta);

        vm.SmartPopulateDefaultCommand.Execute(null);



        Assert.NotEmpty(vm.SlaveMapEntries);



        // DI tags must be Read-Only and hard-locked

        var diEntries = vm.SlaveMapEntries.Where(e => e.TargetTagName.StartsWith("DI")).ToList();

        Assert.NotEmpty(diEntries);

        foreach (var di in diEntries)

        {

            Assert.True(di.IsReadOnly);

            Assert.True(di.IsAccessRightLocked);



            // Attempting to toggle or force ReadWrite on locked entry must fail

            di.ToggleAccessRight();

            Assert.True(di.IsReadOnly);



            di.SetAccessRight("ReadWrite");

            Assert.True(di.IsReadOnly);

        }



        // AI tags must be Read-Only and hard-locked

        var aiEntries = vm.SlaveMapEntries.Where(e => e.TargetTagName.StartsWith("AI")).ToList();

        Assert.NotEmpty(aiEntries);

        foreach (var ai in aiEntries)

        {

            Assert.True(ai.IsReadOnly);

            Assert.True(ai.IsAccessRightLocked);

            Assert.Equal((byte)3, ai.DataType); // REAL

        }



        // VREG and VFLAG must be ReadWrite and unlocked

        var vregEntries = vm.SlaveMapEntries.Where(e => e.TargetTagName.StartsWith("VREG")).ToList();

        Assert.NotEmpty(vregEntries);

        foreach (var vreg in vregEntries)

        {

            Assert.True(vreg.IsReadWrite);

            Assert.False(vreg.IsAccessRightLocked);



            // Toggling should successfully change to ReadOnly and back

            vreg.ToggleAccessRight();

            Assert.True(vreg.IsReadOnly);

            vreg.ToggleAccessRight();

            Assert.True(vreg.IsReadWrite);

        }

    }



    [Fact]

    public void ExportModbusMap_GeneratesAccurateMarkdownAndCsv()

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

        vm.SmartPopulateDefaultCommand.Execute(null);



        string csv = vm.GenerateExportModbusMapCsv();

        string md = vm.GenerateExportModbusMapMarkdown();



        Assert.Contains("Modbus Address,PLC Tag,Data Type,Access Right", csv);

        Assert.Contains("DI0", csv);

        Assert.Contains("ReadOnly", csv);

        Assert.Contains("VREG0", csv);

        Assert.Contains("ReadWrite", csv);



        Assert.Contains("# Modbus RTU Slave Memory Address Map", md);

        Assert.Contains("| `0` | **DI0** |", md);

        Assert.Contains("🔒 Read-Only", md);

        Assert.Contains("✏️ Read/Write", md);

    }

    [Fact]
    public async System.Threading.Tasks.Task ExportModbusMapCommand_WhenEmpty_SetsWarningMessage()
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
        vm.SlaveMapEntries.Clear();

        await vm.ExportModbusMapCommand.ExecuteAsync(null);

        Assert.True(vm.HasExportNoticeMessage);
        Assert.False(vm.HasLastExportedFilePath);
    }

    [Fact]
    public async System.Threading.Tasks.Task ExportModbusMapCommand_WhenCancelled_DoesNotWriteFile()
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
        vm.SmartPopulateDefaultCommand.Execute(null);
        vm.SaveFilePicker = (filter, defaultName) => null; // Simulate Cancel

        await vm.ExportModbusMapCommand.ExecuteAsync(null);

        Assert.False(vm.HasLastExportedFilePath);
    }

    [Fact]
    public async System.Threading.Tasks.Task ExportModbusMapCommand_WhenSavedAsCsv_WritesFileProperly()
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
        vm.SmartPopulateDefaultCommand.Execute(null);

        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_slave_map_{System.Guid.NewGuid():N}.csv");
        try
        {
            vm.SaveFilePicker = (filter, defaultName) => tempFile;

            await vm.ExportModbusMapCommand.ExecuteAsync(null);

            Assert.True(System.IO.File.Exists(tempFile));
            Assert.True(vm.HasLastExportedFilePath);
            Assert.Equal(tempFile, vm.LastExportedFilePath);
            Assert.Contains(System.IO.Path.GetFileName(tempFile), vm.ExportNoticeMessage);

            string fileContent = System.IO.File.ReadAllText(tempFile);
            Assert.Contains("Modbus Address,PLC Tag,Data Type,Access Right", fileContent);
            Assert.Contains("DI0", fileContent);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
            {
                System.IO.File.Delete(tempFile);
            }
        }
    }



    [Fact]

    public void ToggleAccessRightCommand_CanBeExecutedDirectly_ViaRelayCommand()

    {

        var entry = new ModbusSlaveMapItemViewModel

        {

            TargetTagName = "VFLAG0",

            AccessRight = "ReadWrite",

            IsAccessRightLocked = false

        };



        // Test ToggleAccessRightCommand generated by RelayCommand

        Assert.True(entry.IsReadWrite);

        entry.ToggleAccessRightCommand.Execute(null);

        Assert.True(entry.IsReadOnly);

        entry.ToggleAccessRightCommand.Execute(null);

        Assert.True(entry.IsReadWrite);



        // If locked, command must not change it to ReadWrite

        entry.IsAccessRightLocked = true;

        entry.AccessRight = "ReadOnly";

        entry.ToggleAccessRightCommand.Execute(null);

        Assert.True(entry.IsReadOnly);

    }



    [Fact]

    public void TargetTag_Assignment_UpdatesProperties_AndEnforcesPhysicalInputLock()

    {

        var tagCatalog = new TagCatalogViewModel();




        var diTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.DiscreteInput);

        var vregTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.VirtualRegister);



        Assert.NotNull(diTag);

        Assert.NotNull(vregTag);



        var entry = new ModbusSlaveMapItemViewModel();



        // Assign DI tag -> must lock to ReadOnly

        entry.TargetTag = diTag;

        Assert.Equal(diTag.Name, entry.TargetTagName);

        Assert.True(entry.IsReadOnly);

        Assert.True(entry.IsAccessRightLocked);



        // Assign VREG tag -> must allow ReadWrite

        entry.TargetTag = vregTag;

        Assert.Equal(vregTag.Name, entry.TargetTagName);

        Assert.False(entry.IsAccessRightLocked);

    }



    [Fact]

    public void HasExportNoticeMessage_ReflectsExportState()

    {

        var meta = new ProjectMetadata { PhysicalRs485PortCount = 1 };

        var vm = new ModbusPortConfigViewModel(meta);



        Assert.False(vm.HasExportNoticeMessage);

        vm.ExportNoticeMessage = "Export complete";

        Assert.True(vm.HasExportNoticeMessage);

        vm.ExportNoticeMessage = string.Empty;

        Assert.False(vm.HasExportNoticeMessage);

    }

}

