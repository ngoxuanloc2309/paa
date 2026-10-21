using System;
using System.Linq;
using System.Text.Json;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Validation;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class MqttVisualFieldBuilderTests
{
    [Fact]
    public void MqttTopicValidator_Validate_CorrectlyIdentifiesValidAndInvalidTopics()
    {
        // Valid topics
        Assert.True(MqttTopicValidator.Validate("factory/line1/temp", out string err1));
        Assert.Empty(err1);

        Assert.True(MqttTopicValidator.Validate("synaptix/{device}/telemetry", out string err2));
        Assert.Empty(err2);

        // Invalid: Empty or whitespace
        Assert.False(MqttTopicValidator.Validate("", out string err3));
        Assert.NotEmpty(err3);

        Assert.False(MqttTopicValidator.Validate("   ", out string err4));
        Assert.NotEmpty(err4);

        // Invalid: Wildcards + and #
        Assert.False(MqttTopicValidator.Validate("factory/+/temp", out string err5));
        Assert.Contains("+", err5);

        Assert.False(MqttTopicValidator.Validate("factory/line1/#", out string err6));
        Assert.Contains("#", err6);

        // Invalid: Starts with $
        Assert.False(MqttTopicValidator.Validate("$SYS/broker/status", out string err7));
        Assert.Contains("$", err7);

        // Invalid: Leading or trailing slash
        Assert.False(MqttTopicValidator.Validate("/factory/temp", out string err8));
        Assert.NotEmpty(err8);

        Assert.False(MqttTopicValidator.Validate("factory/temp/", out string err9));
        Assert.NotEmpty(err9);

        // Invalid: Double slash
        Assert.False(MqttTopicValidator.Validate("factory//temp", out string err10));
        Assert.NotEmpty(err10);
    }

    [Fact]
    public void MqttTopicValidator_Sanitize_CleansVietnameseAndInvalidCharacters()
    {
        // Vietnamese accents and spaces
        string sanitized = MqttTopicValidator.Sanitize("  Hệ thống / Lò sấy #01 / Nhiệt độ  ");
        Assert.Equal("he_thong/lo_say_01/nhiet_do", sanitized);

        // Strips wildcards and leading/trailing slashes
        string sanitizedWildcards = MqttTopicValidator.Sanitize("/sensor/+/value/#/");
        Assert.Equal("sensor/value", sanitizedWildcards);

        // Sanitizing an already clean topic returns it clean
        string clean = MqttTopicValidator.Sanitize("synaptix/line1/speed");
        Assert.Equal("synaptix/line1/speed", clean);
    }

    [Fact]
    public void MqttPublishNode_VisualFieldBuilder_GeneratesMultiFieldJson()
    {
        var tag1 = new TagModel { Name = "AI0_TEMP", Value = 75, Kind = TagKind.AnalogInput, Index = 0 };
        var tag2 = new TagModel { Name = "AI1_PRESS", Value = 120, Kind = TagKind.AnalogInput, Index = 1 };
        var tag3 = new TagModel { Name = "DI0_RUN", Value = 1, Kind = TagKind.DiscreteInput, Index = 2 };

        var node = new MqttPublishNodeViewModel();
        node.AddFieldWithDetails("temperature", tag1, "°C");
        node.AddFieldWithDetails("pressure", tag2, "bar");
        node.AddFieldWithDetails("is_running", tag3, "");

        Assert.Equal(3, node.Fields.Count);
        Assert.Equal("3 Tags", node.DataTagBadgeText);

        string payload = node.GeneratePayload(0, "Station_A");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("Station_A", root.GetProperty("device").GetString());
        Assert.True(root.GetProperty("ts").GetInt64() > 0);
        var values = root.GetProperty("values");
        Assert.Equal(75, values.GetProperty("temperature").GetDouble());
        Assert.Equal(120, values.GetProperty("pressure").GetDouble());
        Assert.Equal(1, values.GetProperty("is_running").GetDouble());
    }

    [Fact]
    public void MqttPublishNode_VisualFieldBuilder_TogglesDeviceAndTimestamp()
    {
        var tag = new TagModel { Name = "AI0_TEMP", Value = 42, Kind = TagKind.AnalogInput, Index = 0 };
        var node = new MqttPublishNodeViewModel();
        node.AddFieldWithDetails("temp", tag, "°C");

        node.IncludeDevice = false;
        node.IncludeTimestamp = false;

        string payload = node.GeneratePayload(0, "Station_A");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("device", out _));
        Assert.False(root.TryGetProperty("ts", out _));
        var values = root.GetProperty("values");
        Assert.Equal(42, values.GetProperty("temp").GetDouble());
    }

    [Fact]
    public void MqttPublishNode_VisualFieldBuilder_RemoveField_UpdatesFieldsAndPreview()
    {
        var node = new MqttPublishNodeViewModel();
        node.AddFieldWithDetails("f1", null, "");
        node.AddFieldWithDetails("f2", null, "");

        Assert.Equal(2, node.Fields.Count);

        var first = node.Fields[0];
        node.RemoveFieldCommand.Execute(first);

        Assert.Single(node.Fields);
        Assert.Equal("f2", node.Fields[0].Key);
    }

    [Fact]
    public void MqttPublishNode_QosSegmentedCommands_WorkCorrectly()
    {
        var node = new MqttPublishNodeViewModel();

        // Default is QoS 0
        Assert.Equal(0, node.Qos);
        Assert.True(node.IsQos0);
        Assert.False(node.IsQos1);
        Assert.False(node.IsQos2);

        // Switch to QoS 1
        node.SetQos1Command.Execute(null);
        Assert.Equal(1, node.Qos);
        Assert.False(node.IsQos0);
        Assert.True(node.IsQos1);
        Assert.False(node.IsQos2);

        // Switch to QoS 2
        node.SetQos2Command.Execute(null);
        Assert.Equal(2, node.Qos);
        Assert.False(node.IsQos0);
        Assert.False(node.IsQos1);
        Assert.True(node.IsQos2);

        // Switch back to QoS 0
        node.SetQos0Command.Execute(null);
        Assert.Equal(0, node.Qos);
        Assert.True(node.IsQos0);
    }

    [Fact]
    public void MqttPublishNode_Presets_ApplyExpectedConfigurations()
    {
        var node = new MqttPublishNodeViewModel();

        // Preset Telemetry
        node.ApplyTelemetryPresetCommand.Execute(null);
        Assert.Equal("synaptix/{device}/telemetry", node.Topic);

        // Preset Alarms (QoS 1)
        node.ApplyAlarmPresetCommand.Execute(null);
        Assert.Equal("synaptix/{device}/alarms", node.Topic);
        Assert.Equal(1, node.Qos);
        Assert.True(node.IsQos1);

        // Preset Status (Retain = true)
        node.ApplyStatusPresetCommand.Execute(null);
        Assert.Equal("synaptix/{device}/status", node.Topic);
        Assert.True(node.Retain);
    }

    [Fact]
    public void MqttPublishNode_SanitizeTopicCommand_CleansInPlace()
    {
        var node = new MqttPublishNodeViewModel
        {
            Topic = "  Bơm nước / Trạm 1 / Áp suất  "
        };

        Assert.True(node.HasTopicError);

        node.SanitizeTopicCommand.Execute(null);

        Assert.Equal("bom_nuoc/tram_1/ap_suat", node.Topic);
        Assert.False(node.HasTopicError);
    }

    [Fact]
    public void LogicEditorViewModel_MqttSerialization_RoundtripPreservesFields()
    {
        var tagCatalog = new TagCatalogViewModel();
        var tag1 = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput) ?? new TagModel { Name = "AI0" };
        var tag2 = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput) ?? new TagModel { Name = "DI0" };

        var vm = new LogicEditorViewModel(tagCatalog);

        var mqtt = new MqttPublishNodeViewModel
        {
            Topic = "plant/furnace/telemetry",
            PayloadMode = "JSON",
            Qos = 1,
            Retain = true,
            IncludeDevice = true,
            IncludeTimestamp = false
        };
        mqtt.AddFieldWithDetails("furnace_temp", tag1, "degC");
        mqtt.AddFieldWithDetails("heater_active", tag2, "bool");

        // Serialize
        var serializedData = LogicEditorViewModel.ExportSingleNodeData(mqtt);

        Assert.Equal("MqttPublish", serializedData.Type);
        Assert.Equal("plant/furnace/telemetry", serializedData.MqttTopic);
        Assert.Equal(1, serializedData.MqttQos);
        Assert.True(serializedData.MqttRetain);
        Assert.True(serializedData.MqttIncludeDevice);
        Assert.False(serializedData.MqttIncludeTimestamp);
        Assert.Equal(2, serializedData.MqttFields.Count);
        Assert.Equal("furnace_temp", serializedData.MqttFields[0].Key);
        Assert.Equal(tag1.Name, serializedData.MqttFields[0].TagName);
        Assert.Equal("degC", serializedData.MqttFields[0].Unit);

        // Deserialize
        var restoredNode = vm.CreateNodeFromData(serializedData) as MqttPublishNodeViewModel;

        Assert.NotNull(restoredNode);
        Assert.Equal("plant/furnace/telemetry", restoredNode.Topic);
        Assert.Equal(1, restoredNode.Qos);
        Assert.True(restoredNode.Retain);
        Assert.True(restoredNode.IncludeDevice);
        Assert.False(restoredNode.IncludeTimestamp);
        Assert.Equal(2, restoredNode.Fields.Count);
        Assert.Equal("furnace_temp", restoredNode.Fields[0].Key);
        Assert.Equal("degC", restoredNode.Fields[0].Unit);
        Assert.Equal("heater_active", restoredNode.Fields[1].Key);
        Assert.Equal("bool", restoredNode.Fields[1].Unit);
    }

    [Fact]
    public void MqttPublishNode_AutoFormatsPayloadAccordingToTagDataType_IEC61131()
    {
        // Tag 1: REAL (Float32) with value 24.5f
        float floatVal = 24.5f;
        var realTag = new TagModel
        {
            Name = "AI0",
            Kind = TagKind.AnalogInput,
            DataType = TagDataType.Float32,
            Value = BitConverter.SingleToInt32Bits(floatVal)
        };

        // Tag 2: DINT (Int32) with value 1500
        var dintTag = new TagModel
        {
            Name = "VREG0",
            Kind = TagKind.VirtualRegister,
            DataType = TagDataType.Int32,
            Value = 1500
        };

        // Tag 3: BOOL (Boolean) with value 1 (True)
        var boolTag = new TagModel
        {
            Name = "DO0",
            Kind = TagKind.DiscreteOutput,
            DataType = TagDataType.Boolean,
            Value = 1
        };

        var mqtt = new MqttPublishNodeViewModel
        {
            Topic = "sensor/telemetry",
            PayloadMode = "JSON",
            IncludeDevice = false,
            IncludeTimestamp = false,
            WrapValues = false
        };
        mqtt.AddFieldWithDetails("temperature", realTag, "C");
        mqtt.AddFieldWithDetails("rpm", dintTag, "rpm");
        mqtt.AddFieldWithDetails("running", boolTag, "");

        string payloadJson = mqtt.GeneratePayload(0, "dev");

        // Parse JSON to verify exact types
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        Assert.Equal(24.5, root.GetProperty("temperature").GetDouble(), 1);
        Assert.Equal(1500, root.GetProperty("rpm").GetInt32());
        Assert.Equal(1, root.GetProperty("running").GetInt32());
    }

    [Fact]
    public void TagCatalog_AllowsConfiguringRealDataType_AndPersistsToProject()
    {
        var catalog = new TagCatalogViewModel();
        var ai0 = catalog.AllTags.First(t => t.Kind == TagKind.AnalogInput);
        Assert.True(ai0.IsDataTypeEditable);

        // Change AI0 to REAL
        ai0.DataType = TagDataType.Float32;
        Assert.Equal("REAL", ai0.DataTypeText);

        // Verify BuildProduct reflects Float32
        var product = catalog.BuildProductFromCurrentTags();
        var ai0Def = product.Tags.First(t => t.TagIndex == ai0.Index);
        Assert.Equal(TagDataType.Float32, ai0Def.DataType);

        // Export to project and restore
        var exported = catalog.ExportToProjectData();
        var ai0Exported = exported.First(t => t.Index == ai0.Index);
        Assert.Equal(TagDataType.Float32, ai0Exported.DataType);

        var newCatalog = new TagCatalogViewModel();
        newCatalog.LoadFromProjectData(exported);
        var ai0Restored = newCatalog.AllTags.First(t => t.Index == ai0.Index);
        Assert.Equal(TagDataType.Float32, ai0Restored.DataType);
        Assert.Equal("REAL", ai0Restored.DataTypeText);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Mqtt_BilingualTooltips_AreFullyLocalized(string culture)
    {
        var originalLang = Services.LocalizationService.Instance.CurrentLanguage;
        try
        {
            Services.LocalizationService.Instance.CurrentLanguage = culture;

            var keys = new[]
            {
                "MqttHeaderTooltip",
                "MqttPayloadModeTooltip",
                "InspectorMqttColKeyTooltip",
                "InspectorMqttColTagTooltip",
                "InspectorMqttColDataTypeTooltip",
                "MqttFieldKeyTooltip",
                "MqttFieldTagTooltip",
                "MqttFieldDataTypeTooltip",
                "MqttFieldDeleteTooltip",
                "MqttAddFieldTooltip",
                "MqttCustomTemplateTooltip",
                "MqttCustomTemplateCounterTooltip",
                "MqttCustomTemplateHintTooltip",
                "MqttPreviewPayloadTooltip",
                "MqttReadyStatusTooltip",
                "MqttBtnApplyTooltip",
                "MqttFooterTopicTooltip",
                "MqttSubHeaderTooltip",
                "MqttSubColFieldNameTooltip",
                "MqttSubColTypeTooltip",
                "MqttSubFieldNameTooltip",
                "MqttSubFieldTypeTooltip",
                "MqttSubFieldDeleteTooltip",
                "MqttSubBtnAddFieldTooltip",
                "MqttSubBtnResetSampleTooltip",
                "MqttSubTestInputTooltip",
                "MqttSubBtnRunTestTooltip",
                "MqttSubResultsPreviewTooltip",
                "MqttSubFooterTopicTooltip",
                "MqttSubBtnApplyTooltip",
                "InspectorNodeLabelTooltip",
                "InspectorMqttTopicTooltip",
                "InspectorMqttPublishModeTooltip",
                "InspectorMqttIntervalTooltip",
                "InspectorMqttDeadbandTooltip",
                "InspectorMqttQosTooltip",
                "InspectorMqttQos0Tooltip",
                "InspectorMqttQos1Tooltip",
                "InspectorMqttQos2Tooltip",
                "InspectorMqttRetainTooltip",
                "InspectorMqttConfigureTooltip",
                "InspectorMqttSubTopicTooltip",
                "InspectorMqttSubConfigureTooltip"
            };

            foreach (var key in keys)
            {
                var text = Services.LocalizationService.Tr(key);
                Assert.False(string.IsNullOrWhiteSpace(text), $"Key '{key}' must have non-empty translation for culture '{culture}'");
                Assert.False(text.StartsWith("!"), $"Key '{key}' must not be missing in culture '{culture}'");
            }
        }
        finally
        {
            Services.LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }
}
