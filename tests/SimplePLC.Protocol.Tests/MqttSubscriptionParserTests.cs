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

    [Fact]
    public void Parse_ValidJson_FloatPayload_ExtractsDoublePrecisionSuccessfully()
    {
        string json = "{\"temperature\": 25.75, \"pressure\": -12.34}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_temp", Label = "temperature", JsonPath = "temperature", DataType = MqttDataType.Float },
            new() { Id = "pin_press", Label = "pressure", JsonPath = "pressure", DataType = MqttDataType.Float }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(MqttErrorCode.None, result.ErrorCode);
        Assert.Equal(25.75, result.NumericValues["pin_temp"], precision: 2);
        Assert.Equal(-12.34, result.NumericValues["pin_press"], precision: 2);
        Assert.Equal("25.75", result.StringValues["pin_temp"]);
        Assert.Equal("-12.34", result.StringValues["pin_press"]);
    }

    [Fact]
    public void Parse_RawFloatPayload_ExtractsSuccessfully()
    {
        string raw = "75.85";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_flow", Label = "Flow", DataType = MqttDataType.Float, MinValue = 0, MaxValue = 100 }
        };

        var result = MqttSubscriptionParser.Parse(raw, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(75.85, result.NumericValues["pin_flow"], precision: 2);
        Assert.Equal("75.85", result.StringValues["pin_flow"]);
    }

    [Fact]
    public void Parse_NestedJson_DFS_ExtractsAllFieldsSuccessfully()
    {
        // JSON lồng sâu 3 cấp: {"command": {"target": {"grid": 7, "color": 3}}}
        // Pins chỉ cần ghi "grid" và "color" (không cần gõ đường dẫn đầy đủ)
        string json = "{\"command\": {\"target\": {\"grid\": 7, \"color\": 3}}}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.NumericValues["pin_grid"]);
        Assert.Equal(3, result.NumericValues["pin_color"]);
        Assert.Equal("command.target.grid", result.ResolvedPaths["pin_grid"]);
        Assert.Equal("command.target.color", result.ResolvedPaths["pin_color"]);
    }

    [Fact]
    public void Parse_JsonArray_DFS_ExtractsFieldsSuccessfully()
    {
        // JSON mảng: {"items": [{"grid": 9, "color": 4}]}
        string json = "{\"items\": [{\"grid\": 9, \"color\": 4}]}";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(json, pins);

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.NumericValues["pin_grid"]);
        Assert.Equal(4, result.NumericValues["pin_color"]);
        Assert.Equal("items[0].grid", result.ResolvedPaths["pin_grid"]);
        Assert.Equal("items[0].color", result.ResolvedPaths["pin_color"]);
    }

    [Fact]
    public void Parse_SingleQuotedAndUnquotedJson_Lenient_ExtractsSuccessfully()
    {
        // 1. JSON dùng nháy đơn: {'grid': 5, 'color': 2}
        string singleQuoted = "{'grid': 5, 'color': 2}";
        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result1 = MqttSubscriptionParser.Parse(singleQuoted, pins, lenient: true);
        Assert.True(result1.IsSuccess);
        Assert.Equal(5, result1.NumericValues["pin_grid"]);
        Assert.Equal(2, result1.NumericValues["pin_color"]);

        // 2. JSON unquoted keys + trailing comma: {grid: 8, color: 1,}
        string unquoted = "{grid: 8, color: 1,}";
        var result2 = MqttSubscriptionParser.Parse(unquoted, pins, lenient: true);
        Assert.True(result2.IsSuccess);
        Assert.Equal(8, result2.NumericValues["pin_grid"]);
        Assert.Equal(1, result2.NumericValues["pin_color"]);
    }

    [Fact]
    public void Parse_DelimitedKeyValue_ExtractsSuccessfully()
    {
        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        // Dạng 1: grid=11,color=3
        string kv1 = "grid=11,color=3";
        var res1 = MqttSubscriptionParser.Parse(kv1, pins, lenient: true);
        Assert.True(res1.IsSuccess);
        Assert.Equal(11, res1.NumericValues["pin_grid"]);
        Assert.Equal(3, res1.NumericValues["pin_color"]);

        // Dạng 2: grid:15;color:2
        string kv2 = "grid:15;color:2";
        var res2 = MqttSubscriptionParser.Parse(kv2, pins, lenient: true);
        Assert.True(res2.IsSuccess);
        Assert.Equal(15, res2.NumericValues["pin_grid"]);
        Assert.Equal(2, res2.NumericValues["pin_color"]);
    }

    [Fact]
    public void Parse_PositionalCsv_ExtractsSuccessfully()
    {
        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        // Dạng 5,2
        string csv = "5,2";
        var result = MqttSubscriptionParser.Parse(csv, pins, lenient: true);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.NumericValues["pin_grid"]);
        Assert.Equal(2, result.NumericValues["pin_color"]);
    }

    [Fact]
    public void Parse_StringWrappedJson_ExtractsSuccessfully()
    {
        // JSON bị bọc 2 lần nháy do client gửi dạng stringified JSON
        string wrapped = "\"{\\\"grid\\\": 6, \\\"color\\\": 1}\"";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 },
            new() { Id = "pin_color", Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(wrapped, pins);
        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.NumericValues["pin_grid"]);
        Assert.Equal(1, result.NumericValues["pin_color"]);
    }

    [Fact]
    public void Parse_MalformedJson_WithLenientMode_RecoversAndExtractsSuccessfully()
    {
        // Cú pháp JSON lỗi dở dang nhưng vẫn có cặp trường "grid": 5
        string badJson = "{ \"grid\": 5, broken_syntax ";

        var pins = new List<MqttPinBindingDto>
        {
            new() { Id = "pin_grid", Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 }
        };

        var result = MqttSubscriptionParser.Parse(badJson, pins, lenient: true);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.NumericValues["pin_grid"]);
    }
}
