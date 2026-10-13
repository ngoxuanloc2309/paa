using System.Collections.Generic;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class MqttSubscriptionParserTests
{
    [Fact]
    public void Parse_ValidJson_SortToLightPayload_ExtractsAllFieldsSuccessfully()
    {
        // Gói tin của dự án Sort-to-Light: {"grid": 5, "color": 2}
        string json = "{\"grid\": 5, \"color\": 2}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(MqttErrorCode.None, result.ErrorCode);
        Assert.Equal(5, result.NumericValues["pin_grid"]);
        Assert.Equal(2, result.NumericValues["pin_color"]);
    }

    [Fact]
    public void Parse_NestedJsonPath_ExtractsCorrectly()
    {
        string json = "{\"spx\": {\"rack\": {\"target_grid\": 12}}, \"active\": true}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "TargetGrid", JsonPath = "spx.rack.target_grid", DataType = MqttDataType.Int32 },
            new() { Id = "pin_active", Label = "IsActive", JsonPath = "active", DataType = MqttDataType.Bool }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.NumericValues["pin_grid"]);
        Assert.Equal(1.0, result.NumericValues["pin_active"]);
    }

    [Fact]
    public void Parse_AtomicAllOrNothing_OneFieldFails_RejectsEntireMessage()
    {
        // "grid" hợp lệ (5), nhưng "color" sai kiểu không thể parse sang INT ("invalid_color_text")
        string json = "{\"grid\": 5, \"color\": \"invalid_color_text\"}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.False(result.IsSuccess);
        Assert.Equal(MqttErrorCode.TypeMismatch, result.ErrorCode);
        // Không được cập nhật trường nào vào kết quả
        Assert.Empty(result.NumericValues);
    }

    [Fact]
    public void Parse_ValueOutOfRange_RejectsEntireMessage()
    {
        // Kệ chỉ có 18 ô (Min 1, Max 18), gói tin gửi grid = 25
        string json = "{\"grid\": 25, \"color\": 1}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16, MinValue = 1, MaxValue = 18 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.False(result.IsSuccess);
        Assert.Equal(MqttErrorCode.OutOfRange, result.ErrorCode);
        Assert.Empty(result.NumericValues);
    }

    [Fact]
    public void Parse_MissingRequiredField_ReturnsMissingFieldError()
    {
        // Gói tin thiếu trường "color"
        string json = "{\"grid\": 5}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.False(result.IsSuccess);
        Assert.Equal(MqttErrorCode.MissingField, result.ErrorCode);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsJsonSyntaxError()
    {
        string badJson = "{ \"grid\": 5, broken_syntax ";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(badJson, pins);

        Assert.False(result.IsSuccess);
        Assert.Equal(MqttErrorCode.JsonSyntaxError, result.ErrorCode);
    }

    [Fact]
    public void Parse_RawNumberPayload_ExtractsSuccessfully()
    {
        string raw = "42";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_val", Label = "Val", DataType = MqttDataType.Int16, MinValue = 0, MaxValue = 100 }
        };

        var result = MqttSubscriptionParser.Parse(raw, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.NumericValues["pin_val"]);
    }

    [Fact]
    public void Parse_StandardEnvelopePayload_ValuesContainer_ExtractsSuccessfully()
    {
        // Gói tin chuẩn công nghiệp IIoT Envelope: {"device": "SynaptiX_01", "ts": 1728723600000, "values": {"grid": 5, "color": 1}}
        string json = "{\n  \"device\": \"SynaptiX_01\",\n  \"ts\": 1728723600000,\n  \"values\": {\n    \"grid\": 5,\n    \"color\": 1\n  }\n}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.NumericValues["pin_grid"]);
        Assert.Equal(1, result.NumericValues["pin_color"]);
    }
}
