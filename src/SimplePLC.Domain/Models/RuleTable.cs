namespace SimplePLC.Domain.Models;

/// <summary>
/// Quản lý tập hợp danh sách các Rule nghiệp vụ của một dự án SimplePLC.
/// Khống chế dung lượng tối đa 100 quy tắc theo Data Contract V1.
/// </summary>
public sealed class RuleTable
{
    public const int MaxCapacity = 100;

    private readonly List<Rule> _rules = new();

    public IReadOnlyList<Rule> Rules => _rules.AsReadOnly();
    public int Count => _rules.Count;

    public Rule this[int index] => _rules[index];

    public void AddRule(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_rules.Count >= MaxCapacity)
            throw new InvalidOperationException($"Cannot add more rules. Maximum capacity is {MaxCapacity} rules.");

        rule.RuleIndex = _rules.Count;
        _rules.Add(rule);
    }

    public bool RemoveRule(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= _rules.Count)
            return false;

        _rules.RemoveAt(ruleIndex);
        Reindex();
        return true;
    }

    public void Reindex()
    {
        for (int i = 0; i < _rules.Count; i++)
        {
            _rules[i].RuleIndex = i;
        }
    }

    public void Clear()
    {
        _rules.Clear();
    }
}
