namespace SimplePLC.Application.Logic.Compilation;

public static class StableTopologicalSorter
{
    /// <summary>
    /// Sắp xếp thứ tự thực thi của các Candidate Rules theo thuật toán Topo ổn định (Stable Topological Sort).
    /// Bất biến:
    /// 1. Producer luôn xuất hiện trước Consumer trong mảng kết quả.
    /// 2. Các Rule độc lập được sắp xếp ổn định và tái lặp 100% dựa vào StableOrder và ActionNodeId.
    /// </summary>
    public static IReadOnlyList<RuleAccessInfo> Sort(RuleDependencyGraph graph)
    {
        var rules = graph.Rules;
        int n = rules.Count;
        if (n <= 1)
            return rules;

        var inDegrees = (int[])graph.InDegrees.Clone();
        var available = new List<int>();

        for (int i = 0; i < n; i++)
        {
            if (inDegrees[i] == 0)
            {
                available.Add(i);
            }
        }

        var result = new List<RuleAccessInfo>(n);

        int CompareCandidate(int a, int b)
        {
            int cmp = rules[a].StableOrder.CompareTo(rules[b].StableOrder);
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(rules[a].ActionNodeId, rules[b].ActionNodeId);
        }

        while (available.Count > 0)
        {
            // Chọn ứng viên tối ưu nhất theo (StableOrder ASC, ActionNodeId ASC)
            available.Sort(CompareCandidate);
            int chosen = available[0];
            available.RemoveAt(0);

            result.Add(rules[chosen]);

            foreach (var neighbor in graph.Adjacency[chosen])
            {
                inDegrees[neighbor]--;
                if (inDegrees[neighbor] == 0)
                {
                    available.Add(neighbor);
                }
            }
        }

        return result;
    }
}
