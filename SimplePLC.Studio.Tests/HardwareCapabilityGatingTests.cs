using System.Collections.ObjectModel;
using System.Linq;
using SimplePLC.Application.Enums;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class HardwareCapabilityGatingTests
{
    [Fact]
    public void LogicEditor_CapabilityGating_RemoteIoDevice_DisablesMqtt_KeepsModbusMaster()
    {
        var remoteIoPreset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "REMOTE_IO_8DI_8DO_4AI");
        var product = remoteIoPreset.BuildProductDefinition();

        var catalog = new TagCatalogViewModel();
        catalog.SyncWithProductDefinition(product);

        var editor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());

        // Remote I/O không có chip mạng Ethernet/Wi-Fi -> khóa MQTT
        Assert.False(editor.IsMqttPublishSupported);
        Assert.False(editor.IsMqttSubscribeSupported);
        Assert.True(editor.MqttPublishDisabledTooltip.Contains("Yêu cầu") || editor.MqttPublishDisabledTooltip.Contains("Requires"));

        // Remote I/O có các cổng RS-485 khác -> hỗ trợ Modbus Master
        Assert.True(editor.IsModbusMasterSupported);
    }

    [Fact]
    public void LogicEditor_CapabilityGating_LegacyV1Device_DisablesModbusMaster()
    {
        var catalog = new TagCatalogViewModel();
        var resources = ProductResourceProfile.DefaultRemoteIo;
        var pureLogicProduct = DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE);

        catalog.SyncWithProductDefinition(pureLogicProduct);
        var editor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());

        Assert.False(editor.IsModbusMasterSupported);
        Assert.True(editor.ModbusReadDisabledTooltip.Contains("Yêu cầu") || editor.ModbusReadDisabledTooltip.Contains("Requires"));
    }

    [Fact]
    public void LogicEditor_CapabilityGating_GatewayDevice_EnablesMqttAndModbus()
    {
        var gatewayPreset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "GATEWAY_RS485_ETH");
        var product = gatewayPreset.BuildProductDefinition();

        var catalog = new TagCatalogViewModel();
        catalog.SyncWithProductDefinition(product);

        var editor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());

        Assert.True(editor.IsMqttPublishSupported);
        Assert.True(editor.IsMqttSubscribeSupported);
        Assert.True(editor.IsModbusMasterSupported);
    }

    [Fact]
    public void LogicEditor_CapabilityGating_DynamicSwitchPreset_UpdatesPropertiesLive()
    {
        var catalog = new TagCatalogViewModel();
        var editor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());

        // Bắt đầu với Gateway (có MQTT & Modbus)
        var gatewayPreset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "GATEWAY_RS485_ETH");
        catalog.SyncWithProductDefinition(gatewayPreset.BuildProductDefinition());

        Assert.True(editor.IsMqttPublishSupported);
        Assert.True(editor.IsMqttSubscribeSupported);
        Assert.True(editor.IsModbusMasterSupported);

        // Chuyển sang Remote IO (không có mạng, nhưng có RS-485)
        var remoteIoPreset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "REMOTE_IO_8DI_8DO_4AI");
        catalog.SyncWithProductDefinition(remoteIoPreset.BuildProductDefinition());

        Assert.False(editor.IsMqttPublishSupported);
        Assert.False(editor.IsMqttSubscribeSupported);
        Assert.True(editor.IsModbusMasterSupported);
    }

    [Fact]
    public void RuleCompiler_TargetDeviceLacksMqtt_EmitsDiagnosticWarning()
    {
        var compiler = new RuleCompiler();
        var nodes = new ObservableCollection<GraphNodeViewModel>();
        var connections = new ObservableCollection<ConnectionViewModel>();

        var diTag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, Index = 0 };
        var inNode = new InputNodeViewModel(diTag);
        var mqttNode = new MqttPublishNodeViewModel(diTag, "factory/telemetry");

        nodes.Add(inNode);
        nodes.Add(mqttNode);

        var catalog = new TagCatalogViewModel();
        catalog.AllTags.Add(diTag);

        var resources = ProductResourceProfile.DefaultRemoteIo;
        var productWithoutMqtt = DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 1,
            protocolVersion: 2,
            wireProfile: 2,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER);

        var result = compiler.Compile(nodes, connections, catalog, productWithoutMqtt);

        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning &&
                                                (d.Message.Contains("MQTT Publish") || d.Message.Contains("MQTT")));
    }

    [Fact]
    public void RuleCompiler_TargetDeviceLacksModbusMaster_EmitsDiagnosticWarning()
    {
        var compiler = new RuleCompiler();
        var nodes = new ObservableCollection<GraphNodeViewModel>();
        var connections = new ObservableCollection<ConnectionViewModel>();

        var diTag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, Index = 0 };
        var inNode = new InputNodeViewModel(diTag);
        var mbNode = new ModbusReadNodeViewModel();

        nodes.Add(inNode);
        nodes.Add(mbNode);

        var catalog = new TagCatalogViewModel();
        catalog.AllTags.Add(diTag);

        var resources = ProductResourceProfile.DefaultRemoteIo;
        var productWithoutModbus = DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE);

        var result = compiler.Compile(nodes, connections, catalog, productWithoutModbus);

        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning &&
                                                (d.Message.Contains("Modbus Master") || d.Message.Contains("Modbus")));
    }
}
