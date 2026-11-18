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

        if (srcNode.Kind == LogicNodeKind.Trigger && srcNode.TriggerData != null)
        {
            return srcNode.TriggerData.InputTagIndex;
        }

        if (srcNode.Kind == LogicNodeKind.Comparator && srcNode.ComparatorData != null)
        {
            return srcNode.ComparatorData.InTagIndex;
        }

        return null;
    }

    /// <summary>
    /// Phân giải chỉ số Tag từ nguồn tín hiệu ngược dòng theo phương pháp duyệt lặp tuần tự (Iterative Bounded Backtracking).
    /// Tuân thủ IEC 61508-3 SIL2/3 & MISRA-C Rule 17.2: Tuyệt đối không dùng đệ quy, chặn tối đa 16 bước nhảy, triệt tiêu 100% nguy cơ StackOverflow.
    /// </summary>
    /// <param name="graph">Đồ thị logic POCO.</param>
    /// <param name="nodeMap">Bảng tra cứu node theo Id.</param>
    /// <param name="targetNodeId">Node bắt đầu truy vết ngược dòng.</param>
    /// <param name="targetPortName">Tên cổng ngõ vào của target node (ví dụ: "Trig", "EN", "In").</param>
    /// <returns>TagIndex tìm được hoặc null nếu không thể phân giải.</returns>
    public static ushort? ResolveUpstreamTagIndex(
        LogicGraph graph,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        string targetNodeId,
        string? targetPortName = null)
    {
        const int MaxHopCount = 16;
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);

        string currentNodeId = targetNodeId;
        string? currentPortName = targetPortName;
        int hop = 0;

        while (hop++ < MaxHopCount)
        {
            if (!visitedNodes.Add(currentNodeId))
            {
                // Phát hiện chu trình kín (Circular Loop Wire) -> Ngắt ngay lập tức, chống lặp vô tận
                return null;
            }

            var inEdges = graph.GetIncomingEdges(currentNodeId);
            var targetEdge = !string.IsNullOrEmpty(currentPortName)
                ? inEdges.FirstOrDefault(e => string.Equals(e.TargetPort, currentPortName, StringComparison.OrdinalIgnoreCase))
                : inEdges.FirstOrDefault();

            if (targetEdge == null || !nodeMap.TryGetValue(targetEdge.SourceNodeId, out var srcNode))
            {
                return null;
            }

            // 1. Chạm tới Node phát tín hiệu có Tag thực tế -> Trả về kết quả ngay
            switch (srcNode.Kind)
            {
                case LogicNodeKind.Input when srcNode.InputData != null:
                    return srcNode.InputData.TagIndex;

                case LogicNodeKind.Timer when srcNode.TimerData != null:
                    return srcNode.TimerData.QTagIndex;

                case LogicNodeKind.Counter when srcNode.CounterData != null:
                    return srcNode.CounterData.QTagIndex;

                case LogicNodeKind.ModbusRead:
                    return ResolveSourcePortTagIndex(srcNode, targetEdge.SourcePort);

                case LogicNodeKind.MqttSubscribe:
                    return ResolveSourcePortTagIndex(srcNode, targetEdge.SourcePort);

                case LogicNodeKind.Scale when srcNode.ScaleData != null && (srcNode.ScaleData.OutTagIndex.HasValue || srcNode.ScaleData.InTagIndex != 0):
                    return srcNode.ScaleData.OutTagIndex ?? srcNode.ScaleData.InTagIndex;
            }

            // 2. Nếu là Node chuyển tiếp trung gian (Pass-through Adapter) -> Bước tiếp sang node phía trước
            if (srcNode.Kind == LogicNodeKind.Trigger)
            {
                if (srcNode.TriggerData?.InputTagIndex.HasValue == true)
                {
                    return srcNode.TriggerData.InputTagIndex.Value;
                }
                currentNodeId = srcNode.Id;
                currentPortName = "In";
                continue;
            }

            if (srcNode.Kind == LogicNodeKind.Comparator)
            {
                if (srcNode.ComparatorData != null && srcNode.ComparatorData.InTagIndex != 0)
                {
                    return srcNode.ComparatorData.InTagIndex;
                }
                currentNodeId = srcNode.Id;
                currentPortName = "In";
                continue;
            }

            if (srcNode.Kind == LogicNodeKind.Scale)
            {
                currentNodeId = srcNode.Id;
                currentPortName = "IN";
                continue;
            }

            if (srcNode.Kind == LogicNodeKind.Guard)
            {
                if (srcNode.GuardData != null)
                {
                    return srcNode.GuardData.TagIndex;
                }
                currentNodeId = srcNode.Id;
                currentPortName = "In";
                continue;
            }

            break;
        }

        return null;
    }
}
