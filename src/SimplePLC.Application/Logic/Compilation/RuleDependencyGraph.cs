namespace SimplePLC.Application.Logic.Compilation;

public sealed class RuleDependencyGraph
{
    public const string ErrCircularDependency = "SPLC-COMP-DEP-001";

    public IReadOnlyList<RuleAccessInfo> Rules { get; }
    public Dictionary<int, HashSet<int>> Adjacency { get; } = new();
    public int[] InDegrees { get; }

    public RuleDependencyGraph(IReadOnlyList<RuleAccessInfo> rules)
    {
        Rules = rules;
        InDegrees = new int[rules.Count];

        for (int i = 0; i < rules.Count; i++)
        {
            Adjacency[i] = new HashSet<int>();
        }

        BuildEdges();
    }

    private void BuildEdges()
    {
        // Xây dựng bảng tra cứu: Tag -> Danh sách Rule index ghi vào Tag đó
        var writersByTag = new Dictionary<ushort, List<int>>();
        for (int i = 0; i < Rules.Count; i++)
        {
            foreach (var writeTag in Rules[i].Writes)
            {
                if (!writersByTag.TryGetValue(writeTag, out var list))
                {
                    list = new List<int>();
                    writersByTag[writeTag] = list;
                }
                list.Add(i);
            }
        }

        // Với mỗi Rule B (consumer), tìm tất cả Rule A (producer) ghi vào Tag mà B đọc
        for (int b = 0; b < Rules.Count; b++)
        {
            var consumer = Rules[b];
            foreach (var readTag in consumer.ProducerDependencyReads)
            {
                if (writersByTag.TryGetValue(readTag, out var producers))
                {
                    foreach (var a in producers)
                    {
                        // Không tạo self-loop (Rule tự đọc/ghi chính nó không tạo edge phụ thuộc bên ngoài)
                        if (a != b)
                        {
                            // Nếu cả producer (a) và consumer (b) cùng thuộc một Macro Instance:
                            // Thứ tự thực thi nội bộ đã được xác định tất định bởi ExpansionIndex (a.ExpansionIndex < b.ExpansionIndex).
                            // Bỏ qua cạnh ngược từ expansion cao về expansion thấp để tránh false cycle trong Controlled Multi-Writer Group.
                            var prodRule = Rules[a];
                            var consRule = Rules[b];
                            if (prodRule.Origin != null && consRule.Origin != null &&
                                prodRule.Origin.MacroInstanceId == consRule.Origin.MacroInstanceId)
                            {
                                if (prodRule.Origin.ExpansionIndex >= consRule.Origin.ExpansionIndex)
                                {
                                    continue;
                                }
                            }

                            if (Adjacency[a].Add(b))
                            {
                                InDegrees[b]++;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Phát hiện chu trình phụ thuộc dữ liệu vòng tròn (Data Dependency Cycle).
    /// </summary>
    public CompileDiagnostic? DetectCycle()
    {
        var tempInDegrees = (int[])InDegrees.Clone();
        var queue = new Queue<int>();

        for (int i = 0; i < Rules.Count; i++)
        {
            if (tempInDegrees[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        int visitedCount = 0;
        var visited = new bool[Rules.Count];

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            visited[u] = true;
            visitedCount++;

            foreach (var v in Adjacency[u])
            {
                tempInDegrees[v]--;
                if (tempInDegrees[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        if (visitedCount < Rules.Count)
        {
            // Các node còn lại không thể giảm bậc vào = các node nằm trong chu trình
            var cycleRuleNodes = new List<string>();
            for (int i = 0; i < Rules.Count; i++)
            {
                if (!visited[i])
                {
                    cycleRuleNodes.Add(Rules[i].ActionNodeId);
                }
            }

            return new CompileDiagnostic(
                ErrCircularDependency,
                DiagnosticSeverity.Error,
                "Phát hiện chu trình phụ thuộc dữ liệu vòng tròn giữa các quy tắc logic (Circular rule dependency).",
                NodeId: cycleRuleNodes.FirstOrDefault(),
                RelatedNodeIds: cycleRuleNodes);
        }

        return null;
    }
}
