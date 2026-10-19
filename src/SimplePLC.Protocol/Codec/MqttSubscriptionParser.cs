namespace SimplePLC.Protocol.Codec;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public string? FaultyField { get; set; }
    public Dictionary<string, double> NumericValues { get; } = new();
    public Dictionary<string, string> StringValues { get; } = new();
    public Dictionary<string, string> ResolvedPaths { get; } = new();

    public static MqttParseResult Success(
        Dictionary<string, double> numericVals, 
        Dictionary<string, string> stringVals,
        Dictionary<string, string>? resolvedPaths = null)
    {
        var res = new MqttParseResult { IsSuccess = true };
        foreach (var kvp in numericVals) res.NumericValues[kvp.Key] = kvp.Value;
        foreach (var kvp in stringVals) res.StringValues[kvp.Key] = kvp.Value;
        if (resolvedPaths != null)
        {
            foreach (var kvp in resolvedPaths) res.ResolvedPaths[kvp.Key] = kvp.Value;
        }
        return res;
    }

    public static MqttParseResult Failure(MqttErrorCode code, string message, string? faultyField = null)
    {
        return new MqttParseResult
        {
            IsSuccess = false,
            ErrorCode = code,
            ErrorMessage = message,
            FaultyField = faultyField
        };
    }
}

/// <summary>
/// Bộ phân tích cú pháp gói tin MQTT Inbound đa tầng (Universal Multi-Tier Parser) theo nguyên tắc Atomic All-or-Nothing:
/// - Tier 1: Standard JSON + Bounded DFS Deep Search (Object & Array up to depth 10, case/punctuation-insensitive).
/// - Tier 2: Lenient Regex Fallback (xử lý JSON phi chuẩn: nháy đơn, unquoted keys, string-wrapped JSON).
/// - Tier 3: Delimited Key-Value Parser (plain text: grid=5,color=1 hoặc grid:5;color:1).
/// - Tier 4: Positional CSV Fallback (danh sách giá trị tuần tự: 5,1).
/// </summary>
public static class MqttSubscriptionParser
{
    private static readonly string[] StandardWrappers = { "values", "data", "payload", "params", "command", "result", "state" };

    /// <summary>
    /// Phân tích một payload MQTT theo danh sách cấu hình chân Pins.
    /// Nếu CÓ BẤT KỲ lỗi nào, toàn bộ gói tin bị từ chối (Atomic transaction).
    /// </summary>
    public static MqttParseResult Parse(string? payload, IReadOnlyList<MqttPinBindingDto> pins, bool lenient = false)
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

        // Xử lý chuỗi JSON bị bọc bởi string quote (ví dụ: "\"{ \\\"grid\\\": 5 }\"")
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            try
            {
                using var stringDoc = JsonDocument.Parse(trimmed);
                if (stringDoc.RootElement.ValueKind == JsonValueKind.String)
                {
                    string unescaped = stringDoc.RootElement.GetString()?.Trim() ?? string.Empty;
                    if (unescaped.StartsWith('{') || unescaped.StartsWith('['))
                    {
                        trimmed = unescaped;
                    }
                }
            }
            catch
            {
                // Giữ nguyên trimmed nếu không unescape được
            }
        }

        bool looksLikeJson = trimmed.StartsWith('{') || trimmed.StartsWith('[');

        if (looksLikeJson)
        {
            var jsonRes = ParseJsonPayload(trimmed, pins, allowDfs: true);
            if (jsonRes.IsSuccess)
            {
                return jsonRes;
            }

            // Nếu JSON parse thất bại và bật chế độ lenient, thử qua Regex fallback
            if (lenient)
            {
                var lenientRes = ParseLenientRegexPayload(trimmed, pins);
                if (lenientRes.IsSuccess)
                {
                    return lenientRes;
                }
            }

            return jsonRes;
        }
        else
        {
            // Plain text: Thử Key-Value hoặc Positional CSV
            return ParseRawPayload(trimmed, pins, lenient);
        }
    }

    private static MqttParseResult ParseJsonPayload(string json, IReadOnlyList<MqttPinBindingDto> pins, bool allowDfs)
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
            var resolvedPaths = new Dictionary<string, string>();

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
            {
                return MqttParseResult.Failure(MqttErrorCode.JsonSyntaxError, "Root của payload JSON phải là một đối tượng Object {} hoặc mảng Array [].");
            }

            foreach (var pin in pins)
            {
                string targetKey = string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.Label : pin.JsonPath.Trim();
                if (string.IsNullOrEmpty(targetKey))
                {
                    continue;
                }

                if (!TryResolveJsonElement(root, targetKey, allowDfs, out var element, out string resolvedPath))
                {
                    return MqttParseResult.Failure(MqttErrorCode.MissingField, $"Không tìm thấy trường '{targetKey}' trong gói tin JSON.", targetKey);
                }

                // Ép kiểu dữ liệu an toàn
                if (!TryExtractValue(element, pin.DataType, out double numVal, out string strVal, out string err))
                {
                    return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Trường '{targetKey}': {err}", targetKey);
                }

                // Kiểm tra ngưỡng Min / Max
                if (pin.DataType != MqttDataType.String)
                {
                    if (pin.MinValue.HasValue && numVal < pin.MinValue.Value)
                    {
                        return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                            $"Trường '{targetKey}' có giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({pin.MinValue.Value}).", targetKey);
                    }
                    if (pin.MaxValue.HasValue && numVal > pin.MaxValue.Value)
                    {
                        return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                            $"Trường '{targetKey}' có giá trị {numVal} lớn hơn ngưỡng tối đa ({pin.MaxValue.Value}).", targetKey);
                    }

                    tempNumeric[pin.Id] = numVal;
                }

                tempString[pin.Id] = strVal;
                resolvedPaths[pin.Id] = resolvedPath;
            }

            return MqttParseResult.Success(tempNumeric, tempString, resolvedPaths);
        }
    }

    /// <summary>
    /// Tier 2: Lenient Fallback Parser sử dụng Regular Expression chịu lỗi cho JSON dị biệt (nháy đơn, unquoted keys, v.v.).
    /// </summary>
    private static MqttParseResult ParseLenientRegexPayload(string raw, IReadOnlyList<MqttPinBindingDto> pins)
    {
        var tempNumeric = new Dictionary<string, double>();
        var tempString = new Dictionary<string, string>();
        var resolvedPaths = new Dictionary<string, string>();

        foreach (var pin in pins)
        {
            string targetKey = string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.Label : pin.JsonPath.Trim();
            if (string.IsNullOrEmpty(targetKey)) continue;

            // Lấy token cuối cùng nếu path có dấu chấm (vd: values.grid -> grid)
            string lastToken = targetKey.Contains('.') ? targetKey.Split('.')[^1].Trim() : targetKey;

            // Pattern: ['"]?targetKey['"]?\s*[:=]\s*(?:['"](?<val>[^'"]*)['"]|(?<val>[^,;\s\r\n}\]]+))
            string escapedKey = Regex.Escape(lastToken);
            string pattern = $@"(?:['""]?{escapedKey}['""]?\s*[:=]\s*(?:['""](?<val>[^'""]*)['""]|(?<val>[^,;\s\r\n}}\]]+)))";

            var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                // Thử khớp không dấu gạch dưới / gạch ngang
                string normKey = NormalizeKey(lastToken);
                string flexPattern = $@"(?:['""]?{Regex.Escape(normKey)}['""]?\s*[:=]\s*(?:['""](?<val>[^'""]*)['""]|(?<val>[^,;\s\r\n}}\]]+)))";
                match = Regex.Match(raw, flexPattern, RegexOptions.IgnoreCase);
            }

            if (!match.Success)
            {
                return MqttParseResult.Failure(MqttErrorCode.MissingField, $"Không tìm thấy trường '{targetKey}' bằng bộ lọc dung thứ.", targetKey);
            }

            string extractedVal = match.Groups["val"].Value.Trim();

            if (!TryExtractValueFromString(extractedVal, pin.DataType, out double numVal, out string strVal, out string err))
            {
                return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Trường '{targetKey}': {err}", targetKey);
            }

            if (pin.DataType != MqttDataType.String)
            {
                if (pin.MinValue.HasValue && numVal < pin.MinValue.Value)
                {
                    return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                        $"Trường '{targetKey}' có giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({pin.MinValue.Value}).", targetKey);
                }
                if (pin.MaxValue.HasValue && numVal > pin.MaxValue.Value)
                {
                    return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                        $"Trường '{targetKey}' có giá trị {numVal} lớn hơn ngưỡng tối đa ({pin.MaxValue.Value}).", targetKey);
                }

                tempNumeric[pin.Id] = numVal;
            }

            tempString[pin.Id] = strVal;
            resolvedPaths[pin.Id] = lastToken;
        }

        return MqttParseResult.Success(tempNumeric, tempString, resolvedPaths);
    }

    /// <summary>
    /// Tier 3 & Tier 4: Plain Text Payload (Key-Value lexer hoặc Positional CSV).
    /// </summary>
    private static MqttParseResult ParseRawPayload(string raw, IReadOnlyList<MqttPinBindingDto> pins, bool lenient)
    {
        var tempNumeric = new Dictionary<string, double>();
        var tempString = new Dictionary<string, string>();
        var resolvedPaths = new Dictionary<string, string>();

        // Trường hợp 1: Chỉ có duy nhất 1 chân cấu hình -> gán toàn bộ chuỗi thô
        if (pins.Count == 1)
        {
            var targetPin = pins[0];
            if (!TryExtractValueFromString(raw, targetPin.DataType, out double numVal, out string strVal, out string err))
            {
                return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, err);
            }

            if (targetPin.DataType != MqttDataType.String)
            {
                if (targetPin.MinValue.HasValue && numVal < targetPin.MinValue.Value)
                {
                    return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                        $"Giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({targetPin.MinValue.Value}).");
                }
                if (targetPin.MaxValue.HasValue && numVal > targetPin.MaxValue.Value)
                {
                    return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                        $"Giá trị {numVal} lớn hơn ngưỡng tối đa ({targetPin.MaxValue.Value}).");
                }

                tempNumeric[targetPin.Id] = numVal;
            }

            tempString[targetPin.Id] = strVal;
            resolvedPaths[targetPin.Id] = "raw";
            return MqttParseResult.Success(tempNumeric, tempString, resolvedPaths);
        }

        // Trường hợp 2: Có nhiều chân cấu hình và bật lenient
        if (lenient)
        {
            // 2A. Thử Delimited Key-Value (ví dụ: grid=5,color=1 hoặc grid:5;color:1 hoặc grid:5\ncolor:1)
            var kvMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pairs = raw.Split(new[] { ',', ';', '&', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pair in pairs)
            {
                int sepIdx = pair.IndexOfAny(new[] { '=', ':' });
                if (sepIdx > 0 && sepIdx < pair.Length - 1)
                {
                    string k = pair[..sepIdx].Trim().Trim('\'', '"');
                    string v = pair[(sepIdx + 1)..].Trim().Trim('\'', '"');
                    kvMap[k] = v;
                    kvMap[NormalizeKey(k)] = v;
                }
            }

            if (kvMap.Count > 0)
            {
                bool allFound = true;
                foreach (var pin in pins)
                {
                    string targetKey = string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.Label : pin.JsonPath.Trim();
                    string lastToken = targetKey.Contains('.') ? targetKey.Split('.')[^1].Trim() : targetKey;

                    if (!kvMap.TryGetValue(lastToken, out string? val) && !kvMap.TryGetValue(NormalizeKey(lastToken), out val))
                    {
                        allFound = false;
                        break;
                    }

                    if (!TryExtractValueFromString(val, pin.DataType, out double numVal, out string strVal, out string err))
                    {
                        return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Trường '{targetKey}': {err}");
                    }

                    if (pin.DataType != MqttDataType.String)
                    {
                        if (pin.MinValue.HasValue && numVal < pin.MinValue.Value)
                        {
                            return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                                $"Trường '{targetKey}' có giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({pin.MinValue.Value}).");
                        }
                        if (pin.MaxValue.HasValue && numVal > pin.MaxValue.Value)
                        {
                            return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                                $"Trường '{targetKey}' có giá trị {numVal} lớn hơn ngưỡng tối đa ({pin.MaxValue.Value}).");
                        }

                        tempNumeric[pin.Id] = numVal;
                    }

                    tempString[pin.Id] = strVal;
                    resolvedPaths[pin.Id] = lastToken;
                }

                if (allFound)
                {
                    return MqttParseResult.Success(tempNumeric, tempString, resolvedPaths);
                }
            }

            // 2B. Thử Positional CSV (ví dụ: 5,1 hoặc 5;1)
            var tokens = raw.Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length >= pins.Count)
            {
                tempNumeric.Clear();
                tempString.Clear();
                resolvedPaths.Clear();

                for (int i = 0; i < pins.Count; i++)
                {
                    var pin = pins[i];
                    string token = tokens[i].Trim('\'', '"');

                    if (!TryExtractValueFromString(token, pin.DataType, out double numVal, out string strVal, out string err))
                    {
                        return MqttParseResult.Failure(MqttErrorCode.TypeMismatch, $"Vị trí #{i + 1} ({pin.Label}): {err}");
                    }

                    if (pin.DataType != MqttDataType.String)
                    {
                        if (pin.MinValue.HasValue && numVal < pin.MinValue.Value)
                        {
                            return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                                $"Vị trí #{i + 1} ({pin.Label}) có giá trị {numVal} nhỏ hơn ngưỡng tối thiểu ({pin.MinValue.Value}).");
                        }
                        if (pin.MaxValue.HasValue && numVal > pin.MaxValue.Value)
                        {
                            return MqttParseResult.Failure(MqttErrorCode.OutOfRange,
                                $"Vị trí #{i + 1} ({pin.Label}) có giá trị {numVal} lớn hơn ngưỡng tối đa ({pin.MaxValue.Value}).");
                        }

                        tempNumeric[pin.Id] = numVal;
                    }

                    tempString[pin.Id] = strVal;
                    resolvedPaths[pin.Id] = $"pos[{i}]";
                }

                return MqttParseResult.Success(tempNumeric, tempString, resolvedPaths);
            }
        }

        return MqttParseResult.Failure(MqttErrorCode.TypeMismatch,
            $"Không thể phân tích payload thô '{raw}' cho {pins.Count} chân ngõ ra.");
    }

    /// <summary>
    /// Tìm kiếm trường JSON theo đường dẫn (hỗ trợ dấu chấm: "data.speed", wrappers envelope, và DFS deep search).
    /// </summary>
    private static bool TryResolveJsonElement(JsonElement root, string path, bool allowDfs, out JsonElement result, out string resolvedPath)
    {
        result = root;
        resolvedPath = path;
        string[] tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        // 1. Thử tìm kiếm trực tiếp từ root (hỗ trợ flat JSON hoặc explicit dotted path)
        if (TryResolveTokens(root, tokens, out result, out string exactResolved))
        {
            resolvedPath = exactResolved;
            return true;
        }

        // 2. Thử tìm kiếm bên trong các wrapper envelope chuẩn công nghiệp
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var wrapper in StandardWrappers)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, wrapper, StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (TryResolveTokens(prop.Value, tokens, out result, out string subResolved))
                        {
                            resolvedPath = $"{prop.Name}.{subResolved}";
                            return true;
                        }
                    }
                }
            }
        }

        // 3. Nếu chưa tìm thấy và bật allowDfs, kích hoạt Bounded DFS Deep Search
        if (allowDfs)
        {
            string targetLeaf = tokens[^1];
            if (TryDeepSearchJsonElement(root, targetLeaf, string.Empty, 0, 10, out result, out resolvedPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveTokens(JsonElement root, string[] tokens, out JsonElement result, out string resolvedPath)
    {
        result = root;
        resolvedPath = string.Empty;
        JsonElement current = root;
        var pathSegments = new List<string>();

        foreach (string token in tokens)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            bool found = false;
            JsonElement next = default;
            string actualName = token;

            foreach (var prop in current.EnumerateObject())
            {
                if (IsKeyMatch(prop.Name, token))
                {
                    next = prop.Value;
                    actualName = prop.Name;
                    found = true;
                    break;
                }
            }

            if (!found) return false;

            pathSegments.Add(actualName);
            current = next;
        }

        result = current;
        resolvedPath = string.Join('.', pathSegments);
        return true;
    }

    /// <summary>
    /// Thuật toán tìm kiếm theo chiều sâu có giới hạn (Bounded DFS, max depth = 10) trên cấu trúc JSON.
    /// Quét qua cả đối tượng Object và phần tử mảng Array.
    /// </summary>
    private static bool TryDeepSearchJsonElement(
        JsonElement current,
        string targetKey,
        string currentPath,
        int currentDepth,
        int maxDepth,
        out JsonElement foundElement,
        out string resolvedPath)
    {
        foundElement = default;
        resolvedPath = string.Empty;

        if (currentDepth > maxDepth)
        {
            return false;
        }

        if (current.ValueKind == JsonValueKind.Object)
        {
            // 1. Kiểm tra toàn bộ thuộc tính ở cấp hiện tại trước
            foreach (var prop in current.EnumerateObject())
            {
                if (IsKeyMatch(prop.Name, targetKey))
                {
                    foundElement = prop.Value;
                    resolvedPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    return true;
                }
            }

            // 2. Duyệt đệ quy vào các Object và Array con
            foreach (var prop in current.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                {
                    string nextPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    if (TryDeepSearchJsonElement(prop.Value, targetKey, nextPath, currentDepth + 1, maxDepth, out foundElement, out resolvedPath))
                    {
                        return true;
                    }
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var item in current.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                {
                    string nextPath = $"{currentPath}[{idx}]";
                    if (TryDeepSearchJsonElement(item, targetKey, nextPath, currentDepth + 1, maxDepth, out foundElement, out resolvedPath))
                    {
                        return true;
                    }
                }
                idx++;
            }
        }

        return false;
    }

    private static bool IsKeyMatch(string propName, string targetKey)
    {
        if (string.Equals(propName, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normProp = NormalizeKey(propName);
        string normTarget = NormalizeKey(targetKey);
        return string.Equals(normProp, normTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string s)
    {
        Span<char> buffer = stackalloc char[s.Length];
        int idx = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '_' && c != '-' && c != ' ')
            {
                buffer[idx++] = c;
            }
        }
        return new string(buffer[..idx]);
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
                    return TryExtractValueFromString(element.GetString() ?? string.Empty, dataType, out numVal, out strVal, out error);
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
                if (element.ValueKind == JsonValueKind.String)
                {
                    return TryExtractValueFromString(element.GetString() ?? string.Empty, dataType, out numVal, out strVal, out error);
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
                if (element.ValueKind == JsonValueKind.String)
                {
                    return TryExtractValueFromString(element.GetString() ?? string.Empty, dataType, out numVal, out strVal, out error);
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

    /// <summary>
    /// Trích xuất và ép kiểu giá trị từ chuỗi thuần sang kiểu dữ liệu đích.
    /// </summary>
    private static bool TryExtractValueFromString(string rawStr, MqttDataType dataType, out double numVal, out string strVal, out string error)
    {
        numVal = 0.0;
        strVal = string.Empty;
        error = string.Empty;
        string s = rawStr.Trim();

        switch (dataType)
        {
            case MqttDataType.Bool:
                if (bool.TryParse(s, out bool b))
                {
                    numVal = b ? 1.0 : 0.0;
                    strVal = numVal > 0 ? "TRUE" : "FALSE";
                    return true;
                }
                if (s == "1" || string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase))
                {
                    numVal = 1.0;
                    strVal = "TRUE";
                    return true;
                }
                if (s == "0" || string.Equals(s, "OFF", StringComparison.OrdinalIgnoreCase))
                {
                    numVal = 0.0;
                    strVal = "FALSE";
                    return true;
                }
                error = $"Không thể chuyển đổi chuỗi '{rawStr}' sang kiểu BOOL.";
                return false;

            case MqttDataType.Int16:
            case MqttDataType.Int32:
                if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedInt))
                {
                    numVal = Math.Round(parsedInt);
                    strVal = ((long)numVal).ToString();
                    return true;
                }
                error = $"Không thể chuyển đổi chuỗi '{rawStr}' sang số nguyên.";
                return false;

            case MqttDataType.Float:
                if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedFloat))
                {
                    numVal = parsedFloat;
                    strVal = numVal.ToString("G", CultureInfo.InvariantCulture);
                    return true;
                }
                error = $"Không thể chuyển đổi chuỗi '{rawStr}' sang số thực FLOAT.";
                return false;

            case MqttDataType.String:
                strVal = rawStr;
                numVal = 0.0;
                return true;

            default:
                error = $"Kiểu dữ liệu không được hỗ trợ: {dataType}";
                return false;
        }
    }
}
