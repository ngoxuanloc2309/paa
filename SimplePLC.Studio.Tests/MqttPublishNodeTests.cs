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
        Assert.True(node.Icon == "📡" || node.Icon == "MqttPublish");
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
        Assert.True(node.TelemetryStatusPillText == "Sent: 1 msgs" || node.TelemetryStatusPillText == "1 gói");

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
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai(
            hardwareCapabilities: SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.BASE_LOGIC | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.MQTT_PUB | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.ETHERNET);
        var result = compiler.Compile(nodes, connections, catalog, product);

        // Đảm bảo không có lỗi DiagUnsupportedNode
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains(LocalizationService.Tr("DiagUnsupportedNode")) || d.Message.Contains("Unsupported"));
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.MqttTelemetryConfigs);
        Assert.Equal("test/topic", result.Program.MqttTelemetryConfigs[0].Topic);
        Assert.Equal(2000u, result.Program.MqttTelemetryConfigs[0].IntervalMs);
    }

    [Fact]
    public void MqttPublishNode_GeneratePayload_WithFloat_UsesInvariantDotEvenInCommaCulture()
    {
        var prevCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            // Simulate Vietnamese or French culture which uses comma ',' as decimal separator
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("vi-VN");

            var node = new MqttPublishNodeViewModel
            {
                PayloadMode = "Raw"
            };

            string rawPayload = node.GeneratePayload(12.75);
            Assert.Equal("12.75", rawPayload);
            Assert.DoesNotContain(",", rawPayload);

            node.PayloadMode = "Custom";
            node.CustomTemplate = "{\"pressure\":{val}}";
            string customPayload = node.GeneratePayload(34.56);
            Assert.Equal("{\"pressure\":34.56}", customPayload);
            Assert.DoesNotContain(",", customPayload);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prevCulture;
        }
    }

    [Fact]
    public void MqttPublishNode_PublishModeChange_ShowsOrHidesTriggerConnector()
    {
        var node = new MqttPublishNodeViewModel();

        // 1. Default is Event mode: Trig at 0, Val at 1
        Assert.True(node.IsEventMode);
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Val", node.InputConnectors[1].Title);

        // 2. Switch to COV mode: Trig should be hidden
        node.PublishMode = "COV";
        Assert.True(node.IsCovMode);
        Assert.Single(node.InputConnectors);
        Assert.Equal("Val", node.InputConnectors[0].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Trig");

        // 3. Switch to Cyclic/Periodic mode: Trig should remain hidden
        node.PublishMode = "Periodic";
        Assert.True(node.IsPeriodicMode);
        Assert.Single(node.InputConnectors);
        Assert.Equal("Val", node.InputConnectors[0].Title);

        // 4. Switch back to Event mode: Trig restored at index 0
        node.PublishMode = "Event";
        Assert.True(node.IsEventMode);
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Val", node.InputConnectors[1].Title);
    }

    [Fact]
    public void MqttPublishNode_FieldsAdditionAndRemoval_DynamicallyUpdatesValueConnectors()
    {
        var node = new MqttPublishNodeViewModel();
        Assert.Equal("Val", node.InputConnectors[1].Title);

        // Add 1st field "pressure"
        node.AddFieldWithDetails("pressure", null, "bar");
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("pressure", node.InputConnectors[1].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Val");

        // Add 2nd field "temp"
        node.AddFieldWithDetails("temp", null, "°C");
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("pressure", node.InputConnectors[1].Title);
        Assert.Equal("temp", node.InputConnectors[2].Title);

        // Rename 2nd field "temp" -> "temperature"
        node.Fields[1].Key = "temperature";
        Assert.Equal("temperature", node.InputConnectors[2].Title);

        // Remove 2nd field
        node.RemoveField(node.Fields[1]);
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("pressure", node.InputConnectors[1].Title);

        // Remove 1st field (Fields empty): Default Val restored
        node.RemoveField(node.Fields[0]);
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Val", node.InputConnectors[1].Title);
    }

    [Fact]
    public void LogicEditor_ModeSwitch_RemovesConnectionToTrig_WhenTrigIsHidden()
    {
        var catalog = new TagCatalogViewModel();
        var diTag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, Index = 0 };
        catalog.AllTags.Add(diTag);

        var logicEditor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());
        var inpNode = new InputNodeViewModel(diTag);
        var mqttNode = new MqttPublishNodeViewModel { PublishMode = "Event" };

        logicEditor.Nodes.Add(inpNode);
        logicEditor.Nodes.Add(mqttNode);

        // Connect Input -> Trig
        logicEditor.Connect(inpNode.OutputConnectors[0], mqttNode.TriggerConnector);
        Assert.Single(logicEditor.Connections);
        Assert.True(mqttNode.TriggerConnector.IsConnected);

        // Change mode to COV: Trig connector is removed from node, connection should be cleanly removed
        mqttNode.PublishMode = "COV";
        Assert.DoesNotContain(mqttNode.InputConnectors, c => c.Title == "Trig");
        Assert.Empty(logicEditor.Connections);
        Assert.False(inpNode.OutputConnectors[0].IsConnected);
    }

    [Fact]
    public void LogicEditor_ConnectToDynamicFieldConnector_MapsTagCorrectly()
    {
        var catalog = new TagCatalogViewModel();
        var aiTag = new TagModel { Name = "AI0_FLOW", Kind = TagKind.AnalogInput, Index = 1 };
        catalog.AllTags.Add(aiTag);

        var logicEditor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());
        var inpNode = new InputNodeViewModel(aiTag);
        var mqttNode = new MqttPublishNodeViewModel { PublishMode = "Event" };
        mqttNode.AddFieldWithDetails("flow", null, "m3/h");

        logicEditor.Nodes.Add(inpNode);
        logicEditor.Nodes.Add(mqttNode);

        var fieldConn = mqttNode.Fields[0].Connector;
        Assert.NotNull(fieldConn);

        // Connect Input -> flow field connector
        logicEditor.Connect(inpNode.OutputConnectors[0], fieldConn);
        Assert.Single(logicEditor.Connections);
        Assert.Same(aiTag, mqttNode.Fields[0].Tag);
    }

    [Fact]
    public void MqttPublishNode_CustomTemplate_AutoGeneratesMatchingConnectors()
    {
        var node = new MqttPublishNodeViewModel
        {
            PayloadMode = "Custom",
            CustomTemplate = "{\"dev\":\"{device}\",\"ts\":{ts},\"speed\":{speed},\"pressure\":{pressure}}"
        };

        // Event mode default: Trig + speed + pressure
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("speed", node.InputConnectors[1].Title);
        Assert.Equal("pressure", node.InputConnectors[2].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Val");

        // Change template to single {val}
        node.CustomTemplate = "{\"value\":{val}}";
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Val", node.InputConnectors[1].Title);
    }

    [Fact]
    public void MqttPublishNode_CustomTemplate_TokenLimits_CapsAt8Connectors()
    {
        var node = new MqttPublishNodeViewModel
        {
            PayloadMode = "Custom",
            CustomTemplate = "{\"v1\":{v1},\"v2\":{v2},\"v3\":{v3},\"v4\":{v4},\"v5\":{v5},\"v6\":{v6},\"v7\":{v7},\"v8\":{v8},\"v9\":{v9},\"v10\":{v10}}"
        };

        // Max 8 custom value connectors + 1 Trig = 9 connectors
        Assert.Equal(9, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        for (int i = 1; i <= 8; i++)
        {
            Assert.Contains(node.InputConnectors, c => c.Title == $"v{i}");
        }
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "v9");
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "v10");
    }

    [Fact]
    public void LogicEditor_ConnectToCustomConnector_MapsValueInPayload()
    {
        var catalog = new TagCatalogViewModel();
        var tempTag = new TagModel { Name = "AI0_TEMP", Kind = TagKind.AnalogInput, Index = 0, Value = 6500 };
        catalog.AllTags.Add(tempTag);

        var logicEditor = new LogicEditorViewModel(catalog, uiDispatcher: act => act());
        var inpNode = new InputNodeViewModel(tempTag);
        var mqttNode = new MqttPublishNodeViewModel
        {
            PayloadMode = "Custom",
            CustomTemplate = "{\"temperature\":{temp}}"
        };

        logicEditor.Nodes.Add(inpNode);
        logicEditor.Nodes.Add(mqttNode);

        var tempConn = mqttNode.InputConnectors.First(c => c.Title == "temp");
        logicEditor.Connect(inpNode.OutputConnectors[0], tempConn);

        Assert.Single(logicEditor.Connections);
        Assert.Same(tempTag, mqttNode.GetCustomConnectorTag("temp"));

        string payload = mqttNode.GeneratePayload(12.3);
        Assert.Contains("\"temperature\":6500", payload);
    }
}
