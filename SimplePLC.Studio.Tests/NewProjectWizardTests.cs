using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class NewProjectWizardTests
{
    [Fact]
    public void NewProjectViewModel_DefaultState_LoadsStandardRemoteIoPreset()
    {
        // Arrange & Act
        var vm = new NewProjectViewModel();

        // Assert
        Assert.Equal("Project_1", vm.ProjectName);
        Assert.Equal("SimplePLC Engineer", vm.Author);
        Assert.NotNull(vm.SelectedPreset);
        Assert.Equal("REMOTE_IO_8DI_8DO_4AI", vm.SelectedPreset.Id);
        Assert.Equal(8, vm.PreviewDigitalInputs);
        Assert.Equal(8, vm.PreviewDigitalOutputs);
        Assert.Equal(4, vm.PreviewAnalogInputs);
        Assert.Equal(32, vm.PreviewVirtualFlags);
        Assert.Equal(32, vm.PreviewVirtualRegs);
        Assert.Equal(32, vm.PreviewRetainRegs);
        Assert.Equal(8, vm.PreviewCounters);
        Assert.Equal(100, vm.PreviewMaxRules);
        Assert.Equal(124, vm.PreviewTotalTags);
        Assert.Contains("DI0 – DI7", vm.PreviewDiText);
        Assert.Contains("DO0 – DO7", vm.PreviewDoText);
        Assert.Contains("AI0 – AI3", vm.PreviewAiText);
    }

    [Fact]
    public void NewProjectViewModel_SwitchingPresets_UpdatesPreviewProperly()
    {
        // Arrange
        var vm = new NewProjectViewModel();

        // Act: Chọn Compact 4DI/4DO
        var compact = vm.Presets.First(p => p.Id == "REMOTE_IO_4DI_4DO");
        vm.SelectStandardPreset(compact);

        // Assert Compact
        Assert.Equal(4, vm.PreviewDigitalInputs);
        Assert.Equal(4, vm.PreviewDigitalOutputs);
        Assert.Equal(0, vm.PreviewAnalogInputs);
        Assert.Equal(50, vm.PreviewMaxRules);
        Assert.Equal(60, vm.PreviewTotalTags);
        Assert.Equal("0", vm.PreviewAiText);
        Assert.Contains("DI0 – DI3", vm.PreviewDiText);

        // Act: Chọn Datalogger 4DI/2DO/4AI
        var datalogger = vm.Presets.First(p => p.Id == "DATALOGGER_4DI_2DO_4AI");
        vm.SelectStandardPreset(datalogger);

        // Assert Datalogger
        Assert.Equal(4, vm.PreviewDigitalInputs);
        Assert.Equal(2, vm.PreviewDigitalOutputs);
        Assert.Equal(4, vm.PreviewAnalogInputs);
        Assert.Equal(50, vm.PreviewMaxRules);
        Assert.Equal(94, vm.PreviewTotalTags);
    }

    [Fact]
    public void NewProjectViewModel_CustomMode_GeneratesValidProductDefinition()
    {
        // Arrange
        var vm = new NewProjectViewModel();
        vm.EnableCustomMode();

        // Act
        vm.CustomDi = 6;
        vm.CustomDo = 4;
        vm.CustomAi = 2;
        vm.CustomVflag = 20;
        vm.CustomVreg = 10;
        vm.CustomRetain = 10;
        vm.CustomCounters = 2;
        vm.CustomMaxRules = 40;

        // Assert Preview
        Assert.Equal(6, vm.PreviewDigitalInputs);
        Assert.Equal(4, vm.PreviewDigitalOutputs);
        Assert.Equal(2, vm.PreviewAnalogInputs);
        Assert.Equal(40, vm.PreviewMaxRules);
        Assert.Equal(54, vm.PreviewTotalTags);

        // Act: Build ProductDefinition
        var product = vm.BuildProductDefinition();
        Assert.NotNull(product);
        Assert.Equal(6, product.Resources.DigitalInputs);
        Assert.Equal(4, product.Resources.DigitalOutputs);
        Assert.Equal(2, product.Resources.AnalogInputs);
        Assert.Equal(40, product.MaxRules);
        Assert.Equal(54, product.Tags.Count);
    }

    [Fact]
    public void NewProjectViewModel_Validation_DetectsEmptyOrInvalidNames()
    {
        // Arrange
        var vm = new NewProjectViewModel();

        // Empty name
        vm.ProjectName = "   ";
        Assert.False(vm.Validate(out var err1));
        Assert.NotEmpty(err1);

        // Invalid characters
        vm.ProjectName = "Project/Invalid*Name?";
        Assert.False(vm.Validate(out var err2));
        Assert.NotEmpty(err2);

        // Valid name
        vm.ProjectName = "Valid_Project_01";
        Assert.True(vm.Validate(out var err3));
        Assert.Empty(err3);
    }

    [Fact]
    public void MainViewModel_NewProject_WithCustomPreset_SyncsTagCatalogViewModel()
    {
        // Arrange
        var mainVM = new MainViewModel();

        // Mock dialog handler to select Compact 4DI/4DO
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "Compact_Substation_01";
            var compact = wizardVm.Presets.First(p => p.Id == "REMOTE_IO_4DI_4DO");
            wizardVm.SelectStandardPreset(compact);
            return true;
        };

        // Act
        mainVM.NewProject();

        // Assert
        Assert.Equal("Compact_Substation_01", mainVM.CurrentProjectName);
        Assert.False(mainVM.IsProjectDirty);

        // Tag Catalog should have exactly 4 DI (DI0..DI3) and 4 DO (DO0..DO3), and NO AI
        var diTags = mainVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.DiscreteInput).ToList();
        var doTags = mainVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput).ToList();
        var aiTags = mainVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.AnalogInput).ToList();

        Assert.Equal(4, diTags.Count);
        Assert.Equal(4, doTags.Count);
        Assert.Empty(aiTags);
        Assert.Equal(60, mainVM.TagCatalogVM.TotalTagCount);
    }

    [Fact]
    public void SaveAndReloadProject_WithCustomHardware_RestoresExactHardwareProfile()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "Datalogger_Station";
            var dl = wizardVm.Presets.First(p => p.Id == "DATALOGGER_4DI_2DO_4AI");
            wizardVm.SelectStandardPreset(dl);
            return true;
        };
        mainVM.NewProject();

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_hw_proj_{Guid.NewGuid():N}.splc");
        try
        {
            // Set an alias to verify tag preservation
            var di0 = mainVM.TagCatalogVM.AllTags.First(t => t.Name == "DI0");
            di0.Alias = "Sensor Phao";

            // Act 1: Save
            // Invoke SaveProjectInternal via reflection or SaveProject
            var method = typeof(MainViewModel).GetMethod("SaveProjectInternal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(mainVM, new object[] { tempPath });

            Assert.True(File.Exists(tempPath));

            // Act 2: Create a fresh MainViewModel and load
            var freshVM = new MainViewModel();
            freshVM.LoadProjectFile(tempPath);

            // Assert
            Assert.Equal("Datalogger_Station", freshVM.CurrentProjectName);
            Assert.Equal(4, freshVM.CurrentProjectMetadata.DigitalInputs);
            Assert.Equal(2, freshVM.CurrentProjectMetadata.DigitalOutputs);
            Assert.Equal(4, freshVM.CurrentProjectMetadata.AnalogInputs);

            var diTags = freshVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.DiscreteInput).ToList();
            var doTags = freshVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput).ToList();
            var aiTags = freshVM.TagCatalogVM.AllTags.Where(t => t.Kind == TagKind.AnalogInput).ToList();

            Assert.Equal(4, diTags.Count);
            Assert.Equal(2, doTags.Count);
            Assert.Equal(4, aiTags.Count);

            var loadedDi0 = freshVM.TagCatalogVM.AllTags.First(t => t.Name == "DI0");
            Assert.Equal("Sensor Phao", loadedDi0.Alias);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
