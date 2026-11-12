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

/// <summary>
/// Kiểm thử hành vi của wizard tạo dự án mới và đồng bộ phần cứng.
/// </summary>
[Collection("AppServicesDeployTests")]
public class NewProjectWizardTests : IAsyncLifetime
{
    [Fact]
    public void NewProjectViewModel_DefaultState_LoadsStandardRemoteIoPreset()
    {
        // Arrange & Act
        var vm = new NewProjectViewModel();

        // Assert
        Assert.Equal("Project_1", vm.ProjectName);
        Assert.Equal("SynaptiX Engineer", vm.Author);
        Assert.NotNull(vm.SelectedPreset);
        Assert.Equal("SYNX_RIO_884", vm.SelectedPreset.Id);
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
        var compact = vm.Presets.First(p => p.Id == "SYNX_RIO_440");
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
        var datalogger = vm.Presets.First(p => p.Id == "SYNX_DL_424");
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
            var compact = wizardVm.Presets.First(p => p.Id == "SYNX_RIO_440");
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
        Assert.Equal(68, mainVM.TagCatalogVM.TotalTagCount); // 60 user tags + 8 system tags
    }

    [Fact]
    public void SaveAndReloadProject_WithCustomHardware_RestoresExactHardwareProfile()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "Datalogger_Station";
            var dl = wizardVm.Presets.First(p => p.Id == "SYNX_DL_424");
            wizardVm.SelectStandardPreset(dl);
            return true;
        };
        mainVM.NewProject();

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_hw_proj_{Guid.NewGuid():N}.synx");
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

    [Fact]
    public void MainViewModel_NewProject_WithDtuPreset_SelectsSystemTabAndInitializesAllHardwareParameters()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "DTU_Station_01";
            var dtuPreset = wizardVm.Presets.First(p => p.Id == "SYNX_DTU_485");
            wizardVm.SelectStandardPreset(dtuPreset);
            return true;
        };

        // Act
        mainVM.NewProject();

        // Assert
        Assert.Equal("DTU_Station_01", mainVM.CurrentProjectName);
        Assert.True(mainVM.IsDtuProject);
        Assert.True(mainVM.HasSystemTab);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex); // All projects default to LogicGraph (Hero Screen)
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
        Assert.Equal(4, mainVM.CurrentProjectMetadata.PhysicalRs485PortCount);
        Assert.NotEqual(0u, mainVM.CurrentProjectMetadata.HardwareCapabilities);
        Assert.True(mainVM.CurrentProjectMetadata.DtuConfig.Enabled);

        // Capabilities on DtuGatewayStudioVM
        Assert.NotNull(mainVM.DtuGatewayStudioVM);
        Assert.True(mainVM.DtuGatewayStudioVM.HasRs485Capability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasWifiCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasMqttCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasHttpCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasTcpSocketCapability);
        Assert.Equal(4, mainVM.DtuGatewayStudioVM.ModbusPortConfigVM.Ports.Count);

        // Badge Text
        Assert.Contains("DTU Gateway", mainVM.DeviceProductBadgeText);
        Assert.Contains("4x RS-485", mainVM.DeviceProductBadgeText);
    }

    [Fact]
    public void SaveAndReloadProject_WithDtuHardware_RestoresFullHardwareCapabilitiesAndSystemTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "DTU_Persist_Test";
            var dtuPreset = wizardVm.Presets.First(p => p.Id == "SYNX_DTU_485");
            wizardVm.SelectStandardPreset(dtuPreset);
            return true;
        };
        mainVM.NewProject();

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_dtu_proj_{Guid.NewGuid():N}.synx");
        try
        {
            // Act 1: Save
            var method = typeof(MainViewModel).GetMethod("SaveProjectInternal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(mainVM, new object[] { tempPath });

            Assert.True(File.Exists(tempPath));

            // Act 2: Create a fresh MainViewModel and load
            var freshVM = new MainViewModel();
            freshVM.LoadProjectFile(tempPath);

            // Assert
            Assert.Equal("DTU_Persist_Test", freshVM.CurrentProjectName);
            Assert.True(freshVM.IsDtuProject);
            Assert.True(freshVM.HasSystemTab);
            Assert.Equal(AppTabIndices.LogicGraph, freshVM.SelectedTabIndex);
            Assert.Same(freshVM.LogicEditorVM, freshVM.CurrentView);
            Assert.Equal(4, freshVM.CurrentProjectMetadata.PhysicalRs485PortCount);
            Assert.NotEqual(0u, freshVM.CurrentProjectMetadata.HardwareCapabilities);
            Assert.True(freshVM.CurrentProjectMetadata.DtuConfig.Enabled);
            Assert.Contains("DTU Gateway", freshVM.DeviceProductBadgeText);
            Assert.Contains("4x RS-485", freshVM.DeviceProductBadgeText);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void MainViewModel_NewProject_WithGatewayPreset_HasWifiMqttRs485_AndSystemTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "Gateway_Station_01";
            var gw = wizardVm.Presets.First(p => p.Id == "SYNX_GW_220");
            wizardVm.SelectStandardPreset(gw);
            return true;
        };

        // Act
        mainVM.NewProject();

        // Assert
        Assert.Equal("Gateway_Station_01", mainVM.CurrentProjectName);
        Assert.True(mainVM.HasSystemTab);
        Assert.Equal(2, mainVM.CurrentProjectMetadata.PhysicalRs485PortCount);
        Assert.Equal(2, mainVM.DtuGatewayStudioVM.ModbusPortConfigVM.Ports.Count);
        Assert.True(mainVM.DtuGatewayStudioVM.HasWifiCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasMqttCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasRs485Capability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasHttpCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasTcpSocketCapability);
    }

    [Fact]
    public void MainViewModel_NewProject_WithDataloggerPreset_HasWifiMqttRs485_AndSystemTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.ShowNewProjectDialogHandler = (wizardVm) =>
        {
            wizardVm.ProjectName = "DL_Station_01";
            var dl = wizardVm.Presets.First(p => p.Id == "SYNX_DL_424");
            wizardVm.SelectStandardPreset(dl);
            return true;
        };

        // Act
        mainVM.NewProject();

        // Assert
        Assert.Equal("DL_Station_01", mainVM.CurrentProjectName);
        Assert.True(mainVM.HasSystemTab);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
        Assert.Equal(2, mainVM.CurrentProjectMetadata.PhysicalRs485PortCount);
        Assert.Equal(2, mainVM.DtuGatewayStudioVM.ModbusPortConfigVM.Ports.Count);
        Assert.True(mainVM.DtuGatewayStudioVM.HasWifiCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasMqttCapability);
        Assert.True(mainVM.DtuGatewayStudioVM.HasRs485Capability);
        Assert.False(mainVM.DtuGatewayStudioVM.HasHttpCapability);
        Assert.False(mainVM.DtuGatewayStudioVM.HasTcpSocketCapability);
    }

    [Fact]
    public async Task NewProjectViewModel_DetectFromConnectedDevice_WhenNoActiveSession_SetsWarning()
    {
        // Arrange
        await AppServices.Instance.SessionManager.CloseCurrentSessionAsync(CancellationToken.None);
        var wizardVm = new NewProjectViewModel();

        // Act
        wizardVm.DetectFromConnectedDevice();

        // Assert
        Assert.False(wizardVm.IsStatusSuccess);
        Assert.False(string.IsNullOrWhiteSpace(wizardVm.StatusMessage));
        Assert.Contains(LocalizationService.Tr("NewProjectNoDeviceConnected"), wizardVm.StatusMessage);
    }

    [Fact]
    public async Task NewProjectViewModel_DetectFromConnectedDevice_WithActiveSession_AutoSelectsPreset()
    {
        // Arrange
        var dtuPreset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "SYNX_DTU_485");
        var testProduct = dtuPreset.BuildProductDefinition();
        var fakeSession = new FakeTestSession(testProduct);
        await AppServices.Instance.SessionManager.SetCurrentSessionAsync(fakeSession);

        try
        {
            var wizardVm = new NewProjectViewModel();
            var initialPreset = wizardVm.Presets.First(p => p.Id == "SYNX_RIO_884");
            wizardVm.SelectStandardPreset(initialPreset);

            // Act
            wizardVm.DetectFromConnectedDevice();

            // Assert
            Assert.True(wizardVm.IsStatusSuccess);
            Assert.Equal("SYNX_DTU_485", wizardVm.SelectedPreset.Id);
            Assert.Contains("SYNX-DTU-485", wizardVm.SelectedPreset.DisplayName);
            Assert.Contains(LocalizationService.Tr("NewProjectDeviceDetectedSuccess"), wizardVm.StatusMessage);
        }
        finally
        {
            await AppServices.Instance.SessionManager.CloseCurrentSessionAsync();
        }
    }

    [Fact]
    public void MainViewModel_PromptStartupNewProject_WhenAccepted_AppliesSelectedPreset()
    {
        // Arrange
        var mainVm = new MainViewModel();
        mainVm.ShowNewProjectDialogHandler = wizardVm =>
        {
            var dtuPreset = wizardVm.Presets.First(p => p.Id == "SYNX_DTU_485");
            wizardVm.SelectStandardPreset(dtuPreset);
            wizardVm.ProjectName = "StartupDtuProject";
            return true;
        };

        // Act
        mainVm.PromptStartupNewProject();

        // Assert
        Assert.NotNull(mainVm.CurrentProjectMetadata);
        Assert.Equal("StartupDtuProject", mainVm.CurrentProjectMetadata.ProjectName);
        Assert.Equal((ushort)SPLC_DeviceClass.DTU, mainVm.CurrentProjectMetadata.DeviceClass);
        Assert.Equal(AppTabIndices.LogicGraph, mainVm.SelectedTabIndex);
        Assert.Same(mainVm.LogicEditorVM, mainVm.CurrentView);
        Assert.True(mainVm.IsDtuProject);
    }

    [Fact]
    public void MainViewModel_PromptStartupNewProject_WhenCancelled_KeepsOriginalProject()
    {
        // Arrange
        var mainVm = new MainViewModel();
        var initialName = mainVm.CurrentProjectMetadata?.ProjectName;
        var initialDeviceClass = mainVm.CurrentProjectMetadata?.DeviceClass;
        mainVm.ShowNewProjectDialogHandler = wizardVm =>
        {
            var dtuPreset = wizardVm.Presets.First(p => p.Id == "SYNX_DTU_485");
            wizardVm.SelectStandardPreset(dtuPreset);
            wizardVm.ProjectName = "StartupDtuProject";
            return false;
        };

        // Act
        mainVm.PromptStartupNewProject();

        // Assert
        Assert.Equal(initialName, mainVm.CurrentProjectMetadata?.ProjectName);
        Assert.Equal(initialDeviceClass, mainVm.CurrentProjectMetadata?.DeviceClass);
    }

    private sealed class FakeTestSession : SimplePLC.Application.Abstractions.IDeviceSession
    {
        public FakeTestSession(ProductDefinition product)
        {
            Product = product;
        }

        public SimplePLC.Application.Models.DeviceEndpoint Endpoint => new SimplePLC.Application.Models.UsbCdcEndpoint("COM99");
        public SimplePLC.Protocol.Dto.DeviceDescriptorDto Descriptor => new()
        {
            DeviceClass = (SimplePLC.Domain.Enums.SPLC_DeviceClass)Product.DeviceClass,
            DeviceVariant = Product.ProductVariant,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            FwVersionMajor = 2,
            FwVersionMinor = 0
        };
        public SimplePLC.Protocol.Dto.DeviceResourceInfoDto ResourceInfo => new()
        {
            DigitalInputCount = (ushort)Product.Resources.DigitalInputs,
            DigitalOutputCount = (ushort)Product.Resources.DigitalOutputs,
            AnalogInputCount = (ushort)Product.Resources.AnalogInputs,
            VirtualFlagCount = (ushort)Product.Resources.VirtualFlags,
            VirtualRegisterCount = (ushort)Product.Resources.VirtualRegisters,
            RetentiveRegisterCount = (ushort)Product.Resources.RetentiveRegisters,
            CounterCount = (ushort)Product.Resources.Counters,
            MaxRules = Product.MaxRules,
            RuntimeTagCount = 128,
            WireProfile = 2
        };
        public ProductDefinition Product { get; }
        public byte SlaveId => 1;
        public SimplePLC.Application.Abstractions.IRuleTableGateway Rules => new SimplePLC.Infrastructure.Gateways.RuleTableGateway(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IRuntimeTagReader RuntimeTags => new SimplePLC.Infrastructure.Devices.RuntimeTagReader(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IDeviceHealthReader Health => new SimplePLC.Infrastructure.Devices.DeviceHealthReader(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.ISystemCommandClient Commands => new SimplePLC.Infrastructure.Devices.SystemCommandClient(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IFunctionBlockGateway FunctionBlocks => new SimplePLC.Infrastructure.Gateways.FunctionBlockGateway(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IDiagnosticGateway Diagnostics => new SimplePLC.Infrastructure.Devices.DiagnosticGateway(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IRtcClockClient RtcClock => new SimplePLC.Infrastructure.Devices.RtcClockClient(AppServices.Instance.ModbusClient);
        public SimplePLC.Application.Abstractions.IWifiClient Wifi => new SimplePLC.Infrastructure.Devices.WifiClient(AppServices.Instance.ModbusClient);
        public bool IsActive => true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await AppServices.Instance.SessionManager.CloseCurrentSessionAsync(CancellationToken.None);
        AppServices.Instance.CurrentProduct = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    }
}
