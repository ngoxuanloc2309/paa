namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Các hàm tiện ích hỗ trợ phân tích cổng, đảo ngược toán tử so sánh và gắn nhãn node trong quá trình biên dịch.
/// </summary>
public static class CompilationHelpers
{
    /// <summary>
    /// Đảo ngược toán tử so sánh (ví dụ: &gt; thành &lt;=).
    /// </summary>
    public static CompareOperator InvertCompareOp(CompareOperator op) => op switch
    {
        CompareOperator.GreaterThan => CompareOperator.LessThanOrEqual,
        CompareOperator.LessThan => CompareOperator.GreaterThanOrEqual,
        CompareOperator.GreaterThanOrEqual => CompareOperator.LessThan,
        CompareOperator.LessThanOrEqual => CompareOperator.GreaterThan,
        CompareOperator.Equal => CompareOperator.NotEqual,
        CompareOperator.NotEqual => CompareOperator.Equal,
        _ => CompareOperator.None
    };

    /// <summary>
    /// Kiểm tra ngưỡng giá trị có chứa thành phần thực (Float32) hay không.
    /// </summary>
    public static bool IsFloatThreshold(double lo, double hi, TagDefinition tag) =>
        Math.Abs(lo - Math.Round(lo)) > 1e-6 ||
        Math.Abs(hi - Math.Round(hi)) > 1e-6 ||
        tag.DataType == TagDataType.Float32;

    /// <summary>
    /// Lấy nhãn hiển thị của node hoặc chuỗi mặc định nếu nhãn trống.
    /// </summary>
    public static string GetNodeLabel(LogicNode? node, string fallback = "Node") =>
        !string.IsNullOrWhiteSpace(node?.Label) ? node.Label : fallback;

    /// <summary>
    /// Phân giải chỉ số Tag từ cổng ngõ ra của một Node nguồn (Input, ModbusRead, MqttSubscribe, Scale, Timer, Counter).
    /// </summary>
    public static ushort? ResolveSourcePortTagIndex(LogicNode srcNode, string sourcePort)
    {
        if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
        {
            return srcNode.InputData.TagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.ModbusRead && srcNode.ModbusReadData != null)
        {
            if (string.Equals(sourcePort, "Valid", StringComparison.OrdinalIgnoreCase))
                return srcNode.ModbusReadData.ValidTagIndex;
            if (string.Equals(sourcePort, "Error", StringComparison.OrdinalIgnoreCase))
                return srcNode.ModbusReadData.ErrorTagIndex;
            if (string.Equals(sourcePort, "ErrCode", StringComparison.OrdinalIgnoreCase))
                return srcNode.ModbusReadData.ErrCodeTagIndex;

            var pin = srcNode.ModbusReadData.Pins.FirstOrDefault(p =>
                string.Equals(p.Id, sourcePort, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, sourcePort, StringComparison.OrdinalIgnoreCase));
            return pin?.TargetTagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.MqttSubscribe && srcNode.MqttSubscribeData != null)
        {
            var pin = srcNode.MqttSubscribeData.Pins.FirstOrDefault(p =>
                string.Equals(p.Id, sourcePort, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, sourcePort, StringComparison.OrdinalIgnoreCase));
            return pin?.TargetTagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.Scale && srcNode.ScaleData != null)
        {
            return srcNode.ScaleData.OutTagIndex ?? srcNode.ScaleData.InTagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.Timer && srcNode.TimerData != null)
        {
            return srcNode.TimerData.QTagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.Counter && srcNode.CounterData != null)
        {
            return srcNode.CounterData.QTagIndex;
        }

        return null;
    }
}
