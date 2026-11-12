using System;
using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests verifying Hardware-Aware filtering and safety guards in Blueprints library.
/// </summary>
public sealed class BlueprintHardwareCompatibilityTests
{
    [Fact]
    public void Blueprints_WhenHardwareIs4Di4Do_IncompatibleBlueprintsAreFlaggedAndFiltered()
    {
        // Target: 4DI / 4DO / 0 AI, no special comms
        var resources = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 16,
            Counters: 4);

        var product = DeviceProfileBuilder.Build(
            deviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 3,
            protocolVersion: 2,
            wireProfile: 2,
            maxRules: 50,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE,
            physicalRs485PortCount: 0);

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.SyncWithProductDefinition(product);
        var logicEditor = new LogicEditorViewModel(tagCatalog);
        var vm = new BlueprintsViewModel(logicEditor, () => { });

        // Act
        vm.RefreshHardwareCompatibility(product);

        // Assert: Filter is on by default
        Assert.True(vm.FilterCompatibleOnly);

        // Analog blueprints must be incompatible
        var overheatBp = vm.Blueprints.First(b => b.Id == "overheat");
        Assert.False(overheatBp.IsCompatible);
        Assert.False(string.IsNullOrWhiteSpace(overheatBp.IncompatibleReason));

        // High DI blueprints (e.g. sump_pump needs DI5, safety_door needs DI7) must be incompatible
        var sumpPumpBp = vm.Blueprints.First(b => b.Id == "sump_pump");
        Assert.False(sumpPumpBp.IsCompatible);

        // MQTT blueprints must be incompatible
        var mqttPubBp = vm.Blueprints.First(b => b.Id == "mqtt_pub");
        Assert.False(mqttPubBp.IsCompatible);

        // Modbus blueprints must be incompatible (no physical RS-485 ports)
        var mbReadBp = vm.Blueprints.First(b => b.Id == "mb_read");
        Assert.False(mbReadBp.IsCompatible);

        // Compatible discrete blueprints (e.g. feed needs DI1, DO1; andon needs DI0, DO0)
        var feedBp = vm.Blueprints.First(b => b.Id == "feed");
        Assert.True(feedBp.IsCompatible);

        var andonBp = vm.Blueprints.First(b => b.Id == "andon");
        Assert.True(andonBp.IsCompatible);

        // Filtered view should only include compatible blueprints
        var filteredList = vm.FilteredBlueprints.Cast<BlueprintModel>().ToList();
        Assert.DoesNotContain(overheatBp, filteredList);
        Assert.DoesNotContain(sumpPumpBp, filteredList);
        Assert.DoesNotContain(mqttPubBp, filteredList);
        Assert.Contains(feedBp, filteredList);
        Assert.Contains(andonBp, filteredList);
    }

    [Fact]
    public void Blueprints_WhenHardwareIsRemoteIo884_EnablesAnalogAndAll8DiBlueprints()
    {
        // Target: 8DI / 8DO / 4AI, Modbus Master, 2 RS-485 ports
        var resources = new ProductResourceProfile(
            DigitalInputs: 8,
            DigitalOutputs: 8,
            AnalogInputs: 4,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var product = DeviceProfileBuilder.Build(
            deviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 1,
            protocolVersion: 2,
            wireProfile: 2,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: 2);

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.SyncWithProductDefinition(product);
        var logicEditor = new LogicEditorViewModel(tagCatalog);
        var vm = new BlueprintsViewModel(logicEditor, () => { });

        // Act
        vm.RefreshHardwareCompatibility(product);

        // Assert: Analog blueprints are compatible
        var overheatBp = vm.Blueprints.First(b => b.Id == "overheat");
        Assert.True(overheatBp.IsCompatible);

        // 8DI/8DO discrete blueprints are compatible
        var sumpPumpBp = vm.Blueprints.First(b => b.Id == "sump_pump");
        Assert.True(sumpPumpBp.IsCompatible);

        var safetyDoorBp = vm.Blueprints.First(b => b.Id == "safety_door");
        Assert.True(safetyDoorBp.IsCompatible);

        // Modbus master blueprints are compatible
        var mbReadBp = vm.Blueprints.First(b => b.Id == "mb_read");
        Assert.True(mbReadBp.IsCompatible);

        // MQTT still incompatible because this Remote I/O lacks MQTT_PUB
        var mqttPubBp = vm.Blueprints.First(b => b.Id == "mqtt_pub");
        Assert.False(mqttPubBp.IsCompatible);
    }

    [Fact]
    public void Blueprints_WhenFilterCompatibleOnlyToggledOff_ShowsAllBlueprintsWithIncompatibleDisabled()
    {
        var resources = new ProductResourceProfile(4, 4, 0, 16, 16, 16, 4);
        var product = DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO, 3, 2, 2, 50, (ushort)resources.TotalTags, resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC, physicalRs485PortCount: 0);

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.SyncWithProductDefinition(product);
        var logicEditor = new LogicEditorViewModel(tagCatalog);
        var vm = new BlueprintsViewModel(logicEditor, () => { });

        vm.RefreshHardwareCompatibility(product);
        Assert.True(vm.FilterCompatibleOnly);

        // Act: Turn off filter
        vm.FilterCompatibleOnly = false;

        // Assert: All blueprints are visible now
        var allList = vm.FilteredBlueprints.Cast<BlueprintModel>().ToList();
        Assert.Equal(vm.TotalCount, allList.Count);

        // But incompatible ones remain IsCompatible == false
        var overheatBp = allList.First(b => b.Id == "overheat");
        Assert.False(overheatBp.IsCompatible);
    }

    [Fact]
    public void Blueprints_ApplyBlueprint_WhenIncompatible_GuardsAgainstApplication()
    {
        // 4DI/4DO, 0 AI
        var resources = new ProductResourceProfile(4, 4, 0, 16, 16, 16, 4);
        var product = DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO, 3, 2, 2, 50, (ushort)resources.TotalTags, resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC, physicalRs485PortCount: 0);

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.SyncWithProductDefinition(product);
        var logicEditor = new LogicEditorViewModel(tagCatalog);
        var vm = new BlueprintsViewModel(logicEditor, () => { });

        vm.RefreshHardwareCompatibility(product);

        var overheatBp = vm.Blueprints.First(b => b.Id == "overheat");
        Assert.False(overheatBp.IsCompatible);

        // Act: Attempt to apply an incompatible blueprint
        vm.ApplyBlueprint(overheatBp);

        // Assert: Nodes were NOT added, canvas remains empty
        Assert.Empty(logicEditor.Nodes);
    }

    [Fact]
    public void Blueprints_DynamicDeviceFriendlyName_FormatsCorrectlyAndUpdatesCheckboxText()
    {
        var preset884 = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "SYNX_RIO_884");
        var product884 = preset884.BuildProductDefinition();

        var preset440 = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "SYNX_RIO_440");
        var product440 = preset440.BuildProductDefinition();

        // 1. Kiểm tra định dạng tên thân thiện
        string name884 = BlueprintsViewModel.GetFriendlyDeviceName(product884);
        string name440 = BlueprintsViewModel.GetFriendlyDeviceName(product440);

        Assert.Equal("SYNX-RIO-884 (8DI/8DO/4AI)", name884);
        Assert.Equal("SYNX-RIO-440 (4DI/4DO)", name440);

        // 2. Kiểm tra cập nhật nhãn Checkbox động
        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.SyncWithProductDefinition(product884);
        var logicEditor = new LogicEditorViewModel(tagCatalog);
        var vm = new BlueprintsViewModel(logicEditor, () => { });

        vm.RefreshHardwareCompatibility(product884);

        Assert.Contains(name884, vm.FilterCheckboxText);
        Assert.Contains($"{vm.CompatibleCount}/{vm.TotalCount}", vm.FilterCheckboxText);
        Assert.DoesNotContain("Var:", vm.FilterCheckboxText);
        Assert.DoesNotContain("Tags", vm.FilterCheckboxText);
        Assert.False(string.IsNullOrWhiteSpace(vm.FilterCheckboxToolTip));

        // 3. Đổi sang thiết bị 440 -> nhãn checkbox phải cập nhật tức thì
        vm.RefreshHardwareCompatibility(product440);
        Assert.Contains(name440, vm.FilterCheckboxText);
        Assert.DoesNotContain(name884, vm.FilterCheckboxText);
    }
}
