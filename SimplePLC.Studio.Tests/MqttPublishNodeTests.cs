using System;
using System.Text.Json;
using System.Windows;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class MqttPublishNodeTests
{
    [Fact]
    public void MqttPublishNode_Initialization_HasCorrectConnectorsAndDefaults()
    {
        var node = new MqttPublishNodeViewModel();

        Assert.Equal("MQTT PUB", node.Title);
        Assert.Equal("📡", node.Icon);
        Assert.Equal("synaptix/telemetry/data", node.Topic);
        Assert.Equal("JSON", node.PayloadMode);
        Assert.Equal(0, node.Qos);
        Assert.False(node.Retain);

        // Kiểm tra đúng 2 cổng vào: Trig, Val
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Contains(node.InputConnectors, c => c.Title == "Trig" && c.IsInput);
        Assert.Contains(node.InputConnectors, c => c.Title == "Val" && c.IsInput);

        // Kiểm tra đúng 2 cổng ra: Done, Err
        Assert.Equal(2, node.OutputConnectors.Count);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Done" && !c.IsInput);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Err" && !c.IsInput);
    }

    [Fact]
    public void MqttPublishNode_GeneratePayload_JsonMode_ProducesValidJsonWithAttributes()
    {
        var tag = new TagModel
        {
            Name = "AI0_TEMP",
            Alias = "Nhiệt độ lò sấy",
            Kind = TagKind.AnalogInput,
            Index = 0
        };

        var node = new MqttPublishNodeViewModel(tag, "factory/furnace/temp")
        {
            PayloadMode = "JSON",
            Unit = "°C"
        };

        string jsonStr = node.GeneratePayload(125.5, "SynaptiX_Gateway_01");

        // Assert JSON hợp lệ và chứa đủ các thuộc tính telemetry
        using var doc = JsonDocument.Parse(jsonStr);
        var root = doc.RootElement;

        Assert.Equal("SynaptiX_Gateway_01", root.GetProperty("device").GetString());
        Assert.True(root.GetProperty("ts").GetInt64() > 0);
        var values = root.GetProperty("values");
        Assert.Equal(125.5, values.GetProperty("ai0_temp").GetDouble(), 1);
    }

    [Fact]
    public void MqttPublishNode_GeneratePayload_RawMode_ProducesRawNumericString()
    {
        var node = new MqttPublishNodeViewModel
        {
            PayloadMode = "Raw"
        };

        string payload = node.GeneratePayload(45.28);
        Assert.Equal("45.28", payload);
    }

    [Fact]
    public void MqttPublishNode_GeneratePayload_CustomTemplate_ReplacesPlaceholders()
    {
        var tag = new TagModel
        {
            Name = "FLOW_RATE",
            Kind = TagKind.AnalogInput
        };

        var node = new MqttPublishNodeViewModel(tag)
        {
            PayloadMode = "Custom",
            Unit = "L/min",
            CustomTemplate = "{\"sensor\":\"{tag}\",\"rate\":{val},\"unit\":\"{unit}\",\"gw\":\"{device}\"}"
        };

        string payload = node.GeneratePayload(38.5, "GW_STX");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("FLOW_RATE", root.GetProperty("sensor").GetString());
        Assert.Equal(38.5, root.GetProperty("rate").GetDouble(), 1);
        Assert.Equal("L/min", root.GetProperty("unit").GetString());
        Assert.Equal("GW_STX", root.GetProperty("gw").GetString());
    }

    [Fact]
    public void MqttPublishNode_RecordPublishSuccess_And_RecordPublishError()
    {
        var node = new MqttPublishNodeViewModel();

        Assert.Equal(0, node.PublishCount);
        Assert.False(node.HasPublishError);

        // Success
        node.RecordPublishSuccess(72.4, "NodeTest");
        Assert.Equal(1, node.PublishCount);
        Assert.False(node.HasPublishError);
        Assert.True(node.IsLiveActive);
        Assert.Contains("72.4", node.LastPublishedPayload);
        Assert.Equal("Sent: 1 msgs", node.TelemetryStatusPillText);

        // Error
        node.RecordPublishError("Broker connection lost");
        Assert.True(node.HasPublishError);
        Assert.Equal("Broker connection lost", node.LastErrorMessage);
        Assert.False(node.IsLiveActive);
    }

    [Fact]
    public void MqttPublishNode_ILiveTagBoundNode_ApplySnapshot_And_SetOffline()
    {
        var tag = new TagModel
        {
            Name = "PRESSURE_01",
            Alias = "Cảm biến áp suất",
            Kind = TagKind.AnalogInput,
            Index = 2
        };

        var node = new MqttPublishNodeViewModel(tag);
        Assert.Equal((ushort)2, node.BoundTagIndex);

        // Apply snapshot
        var snapshot = new RuntimeTagSnapshot(
            TagIndex: 2,
            Name: "PRESSURE_01",
            Alias: "Cảm biến áp suất",
            Kind: TagKind.AnalogInput,
            DataType: TagDataType.Int32,
            RawValue: 8500,
            Quality: TagQuality.Good,
            LastSuccessfulUpdateAt: DateTimeOffset.UtcNow);

        node.ApplyRuntimeSnapshot(snapshot, isOnline: true);

        Assert.True(node.IsLiveOnline);
        Assert.True(node.HasReceivedUpdate);
        Assert.Equal(8500, node.LiveRawValue);
        Assert.Equal(TagQuality.Good, node.LiveQuality);
        Assert.Equal(NodeLiveVisualState.Active, node.LiveVisualState);

        // Set offline
        node.SetOffline();
        Assert.False(node.IsLiveOnline);
        Assert.Equal(NodeLiveVisualState.Offline, node.LiveVisualState);
    }

    [Fact]
    public void MqttPublishNode_SerializationAndDeserialization_RoundtripPreservesAllProperties()
    {
        var tag = new TagModel
        {
            Name = "TEMP_FURNACE",
            Kind = TagKind.AnalogInput,
            Index = 1
        };

        var originalNode = new MqttPublishNodeViewModel(tag, "industry/oven/zone1")
        {
            CustomLabel = "Bộ bắn Telemetry Lò 1",
            PayloadMode = "Custom",
            CustomTemplate = "{\"t\":{val},\"u\":\"{unit}\"}",
            Qos = 2,
            Retain = true,
            PublishMode = "COV",
            Deadband = 1.5,
            Unit = "°C",
            Location = new Point(160, 240)
        };

        // Export data
        var projectData = new ProjectNodeData
        {
            Id = originalNode.Id,
            Type = "MqttPublish",
            CustomLabel = originalNode.CustomLabel,
            LocationX = originalNode.Location.X,
            LocationY = originalNode.Location.Y,
            TagName = originalNode.DataTag?.Name ?? string.Empty,
            MqttTopic = originalNode.Topic,
            MqttPayloadMode = originalNode.PayloadMode,
            MqttCustomTemplate = originalNode.CustomTemplate,
            MqttQos = originalNode.Qos,
            MqttRetain = originalNode.Retain,
            MqttPublishMode = originalNode.PublishMode,
            MqttDeadband = originalNode.Deadband,
            MqttUnit = originalNode.Unit
        };

        // Khởi tạo LogicEditorViewModel để test deserialization
        var catalog = new TagCatalogViewModel();
        catalog.AllTags.Add(tag);
        var vm = new LogicEditorViewModel(catalog);

        var restoredNode = vm.CreateNodeFromData(projectData) as MqttPublishNodeViewModel;

        Assert.NotNull(restoredNode);
        Assert.Equal("Bộ bắn Telemetry Lò 1", restoredNode.CustomLabel);
        Assert.Equal("industry/oven/zone1", restoredNode.Topic);
        Assert.Equal("Custom", restoredNode.PayloadMode);
        Assert.Equal("{\"t\":{val},\"u\":\"{unit}\"}", restoredNode.CustomTemplate);
        Assert.Equal(2, restoredNode.Qos);
        Assert.True(restoredNode.Retain);
        Assert.Equal("COV", restoredNode.PublishMode);
        Assert.Equal(1.5, restoredNode.Deadband);
        Assert.Equal("°C", restoredNode.Unit);
        Assert.Equal(160, restoredNode.Location.X);
        Assert.Equal(240, restoredNode.Location.Y);
        Assert.Equal("TEMP_FURNACE", restoredNode.DataTag?.Name);
    }

    [Fact]
    public void MqttPublishNode_HybridModes_ExposesCorrectFlagsAndInterval()
    {
        var node = new MqttPublishNodeViewModel();

        // Default: Event mode
        node.PublishMode = "Event";
        Assert.True(node.IsEventMode);
        Assert.False(node.IsCovMode);
        Assert.False(node.IsPeriodicMode);

        // COV mode
        node.PublishMode = "COV";
        node.Deadband = 2.5;
        Assert.False(node.IsEventMode);
        Assert.True(node.IsCovMode);
        Assert.False(node.IsPeriodicMode);
        Assert.Equal(2.5, node.Deadband);

        // Periodic / Cyclic mode
        node.PublishMode = "Cyclic";
        node.IntervalMs = 5000;
        Assert.False(node.IsEventMode);
        Assert.False(node.IsCovMode);
        Assert.True(node.IsPeriodicMode);
        Assert.Equal(5000u, node.IntervalMs);
    }

    [Fact]
    public void StudioRuleCompiler_WithMqttPublishNode_CompilesSuccessfullyWithoutDiagUnsupportedNode()
    {
        var compiler = new RuleCompiler();
        var nodes = new System.Collections.ObjectModel.ObservableCollection<GraphNodeViewModel>();
        var connections = new System.Collections.ObjectModel.ObservableCollection<ConnectionViewModel>();

        var diTag = new TagModel { Name = "DI0_BTN", Kind = TagKind.DiscreteInput, Index = 0 };
        var inNode = new InputNodeViewModel(diTag);
        var mqttNode = new MqttPublishNodeViewModel(diTag, "test/topic")
        {
            PublishMode = "Periodic",
            IntervalMs = 2000
        };

        nodes.Add(inNode);
        nodes.Add(mqttNode);

        // Connect Input to MQTT Val
        var valConn = new ConnectionViewModel(
            inNode.OutputConnectors.First(),
            mqttNode.InputConnectors.First(c => c.Title == "Val"));
        connections.Add(valConn);

        var catalog = new TagCatalogViewModel();
        catalog.AllTags.Add(diTag);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var result = compiler.Compile(nodes, connections, catalog, product);

        // Đảm bảo không có lỗi DiagUnsupportedNode
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains(LocalizationService.Tr("DiagUnsupportedNode")) || d.Message.Contains("Unsupported"));
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.MqttTelemetryConfigs);
        Assert.Equal("test/topic", result.Program.MqttTelemetryConfigs[0].Topic);
        Assert.Equal(2000u, result.Program.MqttTelemetryConfigs[0].IntervalMs);
    }
}
