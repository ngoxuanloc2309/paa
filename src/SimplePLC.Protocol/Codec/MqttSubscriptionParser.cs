namespace SimplePLC.Protocol.Codec;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Kết quả sau khi phân tích cú pháp một gói tin MQTT Subscribe.
/// </summary>
public sealed class MqttParseResult
{
    public bool IsSuccess { get; set; }
    public MqttErrorCode ErrorCode { get; set; } = MqttErrorCode.None;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, double> NumericValues { get; } = new();
    public Dictionary<string, string> StringValues { get; } = new();

    public static MqttParseResult Success(Dictionary<string, double> numericVals, Dictionary<string, string> stringVals)
    {
        var res = new MqttParseResult { IsSuccess = true };
        foreach (var kvp in numericVals) res.NumericValues[kvp.Key] = kvp.Value;
        foreach (var kvp in stringVals) res.StringValues[kvp.Key] = kvp.Value;
        return res;
    }

    public static MqttParseResult Failure(MqttErrorCode code, string message)
    {
        return new MqttParseResult
        {
            IsSuccess = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

/// <summary>
/// Bộ phân tích cú pháp gói tin MQTT Inbound độc lập theo nguyên tắc Atomic All-or-Nothing.
/// Hỗ trợ cả định dạng JSON (kể cả lồng nhau) và Raw chuỗi đơn.
/// </summary>
public static class MqttSubscriptionParser
{
    /// <summary>
    /// Phân tích một payload MQTT theo danh sách cấu hình chân Pins.
    /// Nếu CÓ BẤT KỲ lỗi nào, toàn bộ gói tin bị từ chối (Atomic transaction).
    /// </summary>
    public static MqttParseResult Parse(string? payload, IReadOnlyList<MqttPinBindingDto> pins)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return MqttParseResult.Failure(MqttErrorCode.MissingField, "Payload rỗng hoặc trống.");
        }

        if (pins == null || pins.Count == 0)
        {
            return MqttParseResult.Failure(MqttErrorCode.MissingField, "Chưa cấu hình bất kỳ chân nhận dữ liệu nào.");
        }

        string trimmed = payload.Trim();
        bool isJson = trimmed.StartsWith('{') || trimmed.StartsWith('[');

        if (isJson)
        {
            return ParseJsonPayload(trimmed, pins);
        }
        else
        {
            return ParseRawPayload(trimmed, pins);
        }
    }

    private static MqttParseResult ParseJsonPayload(string json, IReadOnlyList<MqttPinBindingDto> pins)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return MqttParseResult.Failure(MqttErrorCode.JsonSyntaxError, $"Lỗi cú pháp JSON: {ex.Message}");
        }

        using (doc)
        {
            var tempNumeric = new Dictionary<string, double>();
            var tempString = new Dictionary<string, string>();

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return MqttParseResult.Failure(MqttErrorCode.JsonSyntaxError, "Root của payload JSON phải là một đối tượng Object {}.");
            }

            foreach (var pin in pins)
            {
                string path = string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.Label : pin.JsonPath.Trim();
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!TryResolveJsonElement(root, path, out var element))
                {
                    return MqttParseResult.Failure(MqttErrorCode.MissingField, $"Không tìm thấy trường '{path}' trong gói tin JSON.");
                }

                // Ép kiểu dữ liệu an toàn
                if (!TryExtractValue(element, pin.DataType, out double numVal, out string strVal, out string err))
                {
                    return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Trường '{path}': {err}");
                }

                // Kiểm tra ngưỡng Min / Max
                if (pin.DataType != MqttDataType.String)
                {
                    if (pin.MinValue.HasValue && numVal < pin.MinValue.Value)
                    {
                        return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                            $"Trường '{path}' có giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({pin.MinValue.Value}).");
                    }
                    if (pin.MaxValue.HasValue && numVal > pin.MaxValue.Value)
                    {
                        return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                            $"Trường '{path}' có giá trị {numVal} lớn hơn ngưỡng tối đa ({pin.MaxValue.Value}).");
                    }

                    tempNumeric[pin.Id] = numVal;
                }

                tempString[pin.Id] = strVal;
            }

            return MqttParseResult.Success(tempNumeric, tempString);
        }
    }

    private static MqttParseResult ParseRawPayload(string raw, IReadOnlyList<MqttPinBindingDto> pins)
    {
        // Chế độ Raw: gán chuỗi thô này cho pin đầu tiên (hoặc pin không có JsonPath)
        var tempNumeric = new Dictionary<string, double>();
        var tempString = new Dictionary<string, string>();

        var targetPin = pins[0];
        if (targetPin.DataType == MqttDataType.String)
        {
            tempString[targetPin.Id] = raw;
            return MqttParseResult.Success(tempNumeric, tempString);
        }

        if (targetPin.DataType == MqttDataType.Bool)
        {
            if (bool.TryParse(raw, out bool bVal) || raw == "1" || string.Equals(raw, "ON", StringComparison.OrdinalIgnoreCase))
            {
                double d = (bVal || raw == "1" || string.Equals(raw, "ON", StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;
                tempNumeric[targetPin.Id] = d;
                tempString[targetPin.Id] = d > 0 ? "TRUE" : "FALSE";
                return MqttParseResult.Success(tempNumeric, tempString);
            }
            if (raw == "0" || string.Equals(raw, "OFF", StringComparison.OrdinalIgnoreCase))
            {
                tempNumeric[targetPin.Id] = 0.0;
                tempString[targetPin.Id] = "FALSE";
                return MqttParseResult.Success(tempNumeric, tempString);
            }

            return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Không thể chuyển đổi chuỗi '{raw}' sang kiểu BOOL.");
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedNum))
        {
            if (targetPin.MinValue.HasValue && parsedNum < targetPin.MinValue.Value)
            {
                return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                    $"Giá trị {parsedNum} nhỏ hơn ngưỡng tối thiểu ({targetPin.MinValue.Value}).");
            }
            if (targetPin.MaxValue.HasValue && parsedNum > targetPin.MaxValue.Value)
            {
                return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                    $"Giá trị {parsedNum} lớn hơn ngưỡng tối đa ({targetPin.MaxValue.Value}).");
            }

            tempNumeric[targetPin.Id] = parsedNum;
            tempString[targetPin.Id] = parsedNum.ToString(CultureInfo.InvariantCulture);
            return MqttParseResult.Success(tempNumeric, tempString);
        }

        return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Không thể chuyển đổi chuỗi '{raw}' sang số ({targetPin.DataType}).");
    }

    /// <summary>
    /// Tìm kiếm trường JSON theo đường dẫn (hỗ trợ dấu chấm: "data.speed", đồng thời hỗ trợ wrapper chuẩn như "values", "data", "payload", "params").
    /// </summary>
    private static bool TryResolveJsonElement(JsonElement root, string path, out JsonElement result)
    {
        result = root;
        string[] tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        // 1. Thử tìm kiếm trực tiếp từ root (hỗ trợ flat JSON hoặc explicit path)
        if (TryResolveTokens(root, tokens, out result))
        {
            return true;
        }

        // 2. Thử tìm kiếm bên trong các wrapper envelope chuẩn công nghiệp nếu chưa tìm thấy ở root
        string[] standardWrappers = { "values", "data", "payload", "params" };
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var wrapper in standardWrappers)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, wrapper, StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (TryResolveTokens(prop.Value, tokens, out result))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool TryResolveTokens(JsonElement root, string[] tokens, out JsonElement result)
    {
        result = root;
        JsonElement current = root;
        foreach (string token in tokens)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!current.TryGetProperty(token, out JsonElement next))
            {
                // Thử tìm không phân biệt chữ hoa chữ thường
                bool found = false;
                foreach (var prop in current.EnumerateObject())
                {
                    if (string.Equals(prop.Name, token, StringComparison.OrdinalIgnoreCase))
                    {
                        next = prop.Value;
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            current = next;
        }

        result = current;
        return true;
    }

    /// <summary>
    /// Trích xuất và ép kiểu giá trị từ JsonElement sang kiểu dữ liệu mong muốn.
    /// </summary>
    private static bool TryExtractValue(JsonElement element, MqttDataType dataType, out double numVal, out string strVal, out string error)
    {
        numVal = 0.0;
        strVal = string.Empty;
        error = string.Empty;

        switch (dataType)
        {
            case MqttDataType.Bool:
                if (element.ValueKind == JsonValueKind.True) { numVal = 1.0; strVal = "TRUE"; return true; }
                if (element.ValueKind == JsonValueKind.False) { numVal = 0.0; strVal = "FALSE"; return true; }
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int n))
                {
                    numVal = n != 0 ? 1.0 : 0.0;
                    strVal = numVal > 0 ? "TRUE" : "FALSE";
                    return true;
                }
                if (element.ValueKind == JsonValueKind.String)
                {
                    string s = element.GetString()?.Trim() ?? string.Empty;
                    if (bool.TryParse(s, out bool b)) { numVal = b ? 1.0 : 0.0; strVal = numVal > 0 ? "TRUE" : "FALSE"; return true; }
                    if (s == "1" || string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase)) { numVal = 1.0; strVal = "TRUE"; return true; }
                    if (s == "0" || string.Equals(s, "OFF", StringComparison.OrdinalIgnoreCase)) { numVal = 0.0; strVal = "FALSE"; return true; }
                }
                error = $"Không thể ép kiểu '{element}' sang BOOL.";
                return false;

            case MqttDataType.Int16:
            case MqttDataType.Int32:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double dVal))
                {
                    numVal = Math.Round(dVal);
                    strVal = ((long)numVal).ToString();
                    return true;
                }
                if (element.ValueKind == JsonValueKind.String &&
                    double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedD))
                {
                    numVal = Math.Round(parsedD);
                    strVal = ((long)numVal).ToString();
                    return true;
                }
                error = $"Không thể ép kiểu '{element}' sang Số nguyên.";
                return false;

            case MqttDataType.Float:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double fVal))
                {
                    numVal = fVal;
                    strVal = numVal.ToString("G", CultureInfo.InvariantCulture);
                    return true;
                }
                if (element.ValueKind == JsonValueKind.String &&
                    double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedF))
                {
                    numVal = parsedF;
                    strVal = numVal.ToString("G", CultureInfo.InvariantCulture);
                    return true;
                }
                error = $"Không thể ép kiểu '{element}' sang Số thực FLOAT.";
                return false;

            case MqttDataType.String:
                strVal = element.ValueKind == JsonValueKind.String ? (element.GetString() ?? string.Empty) : element.ToString();
                numVal = 0.0;
                return true;

            default:
                error = $"Kiểu dữ liệu không được hỗ trợ: {dataType}";
                return false;
        }
    }
}
