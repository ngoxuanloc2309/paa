using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

namespace SimplePLC.Application.Logic.Runtime;

/// <summary>
/// Động cơ mô phỏng chu kỳ quét logic PLC (Deterministic Soft-PLC Simulation Engine).
/// Chạy độc lập hoàn toàn trong tầng Application theo Clean Architecture:
/// - Không phụ thuộc UI, ViewModel hay ObservableObject.
/// - Nhận ITagValueStore để đọc/ghi Tag values.
/// - Thực thi trên Pure Domain Rules và CompiledProgram.
/// </summary>
public sealed class RuntimeEngine : IRuntimeEngine
{
    private readonly ITagValueStore _store;
    private readonly Dictionary<int, int> _previous = new();
    private readonly Dictionary<int, long> _dwellStarted = new();
    private readonly Dictionary<int, long> _lastInterval = new();
    private readonly List<RuntimeEvent> _events = new();
    private long _scanCount = 0;

    public long ScanCount => _scanCount;
    public Func<DateTime>? TimeProvider { get; set; }
    private DateTime CurrentTime => TimeProvider?.Invoke() ?? DateTime.Now;

    public RuntimeEngine(ITagValueStore store)
    {
        _store = store;
        foreach (var index in store.AllTagIndices)
            _previous[index] = store.GetValue(index);
    }

    public RuntimeSnapshot Scan(SimplePLC.Application.Logic.Compilation.CompiledProgram program, long tickMs) =>
        Scan(program.Rules, tickMs);

    public RuntimeSnapshot Scan(IReadOnlyList<Rule> rules, long tickMs)
    {
        _scanCount++;
        _events.Clear();
        var pending = new Dictionary<int, long>();
        var evaluations = new List<RuleEvaluationRecord>();
        var writtenTagsInThisScan = new HashSet<int>();

        foreach (var rule in rules.OrderBy(r => r.RuleIndex))
        {
            if (!rule.Enabled)
            {
                evaluations.Add(new RuleEvaluationRecord(
                    rule.RuleIndex,
                    rule.Name,
                    RuleEvaluationStatus.Disabled,
                    "Disabled",
                    null,
                    0,
                    0,
                    null,
                    false,
                    $"[R{rule.RuleIndex + 1}] DISABLED {rule.Name}"));
                continue;
            }

            int trigIndex = rule.Trigger.Tag.TagIndex;
            bool isChained = writtenTagsInThisScan.Contains(trigIndex) ||
                (rule.Guard.HasGuard && rule.Guard.Tag != null && writtenTagsInThisScan.Contains(rule.Guard.Tag.TagIndex));

            var current = Read(trigIndex);
            var previous = _previous.TryGetValue(trigIndex, out var old) ? old : current;

            // 1. Đánh giá Điều kiện Trigger
            bool conditionMet = false;
            string trigDesc;

            switch (rule.Trigger.Type)
            {
                case TriggerKind.Interval:
                    conditionMet = !_lastInterval.TryGetValue(rule.RuleIndex, out var last) || (tickMs - last >= rule.Trigger.ForMs);
                    trigDesc = $"INTERVAL ({rule.Trigger.ForMs}ms)";
                    break;

                case TriggerKind.TimeWindow:
                    var now = CurrentTime;
                    int currentHhmm = now.Hour * 100 + now.Minute;
                    if (rule.Trigger.CompareOp == CompareOperator.Equal)
                    {
                        conditionMet = (currentHhmm == rule.Trigger.ThresholdLo);
                    }
                    else
                    {
                        int start = rule.Trigger.ThresholdLo;
                        int end = rule.Trigger.ThresholdHi;
                        conditionMet = start <= end
                            ? (currentHhmm >= start && currentHhmm <= end)
                            : (currentHhmm >= start || currentHhmm <= end);
                    }
                    trigDesc = $"TIME_WINDOW [{rule.Trigger.ThresholdLo}..{rule.Trigger.ThresholdHi}] (time={currentHhmm:D4})";
                    break;

                case TriggerKind.OnRise:
                    bool riseEdge = previous == 0 && current != 0;
                    bool riseHeld = current != 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.RuleIndex) ? riseHeld : riseEdge;
                    trigDesc = $"{rule.Trigger.Tag.Name} ON_RISE (prev={previous}, curr={current})";
                    break;

                case TriggerKind.OnFall:
                    bool fallEdge = previous != 0 && current == 0;
                    bool fallHeld = current == 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.RuleIndex) ? fallHeld : fallEdge;
                    trigDesc = $"{rule.Trigger.Tag.Name} ON_FALL (prev={previous}, curr={current})";
                    break;

                case TriggerKind.OnChange:
                default:
                    bool compareMet = Compare(rule.Trigger.CompareOp, current, rule.Trigger.ThresholdLo, rule.Trigger.ThresholdHi);
                    if (rule.Trigger.CompareOp != CompareOperator.None)
                    {
                        conditionMet = compareMet;
                        trigDesc = $"{rule.Trigger.Tag.Name} = {current} (Compare {rule.Trigger.CompareOp} {rule.Trigger.ThresholdLo})";
                    }
                    else
                    {
                        conditionMet = previous != current;
                        trigDesc = $"{rule.Trigger.Tag.Name} ON_CHANGE (prev={previous}, curr={current})";
                    }
                    break;
            }

            // Nếu điều kiện không thỏa mãn -> Reset Dwell ngay lập tức & SKIP
            if (!conditionMet)
            {
                if (_dwellStarted.ContainsKey(rule.RuleIndex))
                    _dwellStarted.Remove(rule.RuleIndex);

                evaluations.Add(new RuleEvaluationRecord(
                    rule.RuleIndex,
                    rule.Name,
                    RuleEvaluationStatus.Skip,
                    trigDesc,
                    null,
                    0,
                    rule.Trigger.ForMs,
                    null,
                    isChained,
                    $"[R{rule.RuleIndex + 1}] SKIP {rule.Name}: {trigDesc} false"));
                continue;
            }

            // 2. Đánh giá Dwell (ForMs)
            long elapsedDwell = 0;
            if (rule.Trigger.ForMs > 0 && rule.Trigger.Type is not (TriggerKind.Interval or TriggerKind.TimeWindow))
            {
                if (!_dwellStarted.TryGetValue(rule.RuleIndex, out var started))
                {
                    _dwellStarted[rule.RuleIndex] = tickMs;
                    started = tickMs;
                }

                elapsedDwell = tickMs - started;
                if (elapsedDwell < rule.Trigger.ForMs)
                {
                    long remaining = rule.Trigger.ForMs - elapsedDwell;
                    pending[rule.RuleIndex] = remaining;
                    evaluations.Add(new RuleEvaluationRecord(
                        rule.RuleIndex,
                        rule.Name,
                        RuleEvaluationStatus.WaitingDwell,
                        trigDesc,
                        null,
                        elapsedDwell,
                        rule.Trigger.ForMs,
                        null,
                        isChained,
                        $"[R{rule.RuleIndex + 1}] WAITING {rule.Name}: {elapsedDwell}/{rule.Trigger.ForMs} ms"));
                    continue;
                }
            }

            // 3. Đánh giá Guard
            if (!GuardIsOpen(rule, out string guardSummary))
            {
                evaluations.Add(new RuleEvaluationRecord(
                    rule.RuleIndex,
                    rule.Name,
                    RuleEvaluationStatus.BlockedByGuard,
                    trigDesc,
                    guardSummary,
                    elapsedDwell,
                    rule.Trigger.ForMs,
                    null,
                    isChained,
                    $"[R{rule.RuleIndex + 1}] BLOCKED {rule.Name}: {guardSummary}"));
                continue;
            }

            // 4. Thỏa mãn toàn bộ -> Thực thi Action (PASS)
            if (rule.Trigger.Type == TriggerKind.Interval)
                _lastInterval[rule.RuleIndex] = tickMs;

            _dwellStarted.Remove(rule.RuleIndex);

            var delta = ExecuteWithDelta(rule, current, tickMs);
            writtenTagsInThisScan.Add(rule.Action.TargetTag.TagIndex);

            string chainTag = isChained ? " [CHAINED]" : "";
            evaluations.Add(new RuleEvaluationRecord(
                rule.RuleIndex,
                rule.Name,
                RuleEvaluationStatus.Pass,
                trigDesc,
                string.IsNullOrEmpty(guardSummary) ? null : guardSummary,
                rule.Trigger.ForMs,
                rule.Trigger.ForMs,
                delta,
                isChained,
                $"[R{rule.RuleIndex + 1}] PASS {rule.Name}{chainTag} → {delta?.FormattedDelta}"));
        }

        // Cập nhật pending cho các rule đang dwell
        foreach (var rule in rules)
            if (_dwellStarted.TryGetValue(rule.RuleIndex, out var started))
                pending.TryAdd(rule.RuleIndex, Math.Max(0, (long)rule.Trigger.ForMs - (tickMs - started)));

        // Cập nhật previous tag values ở cuối chu kỳ scan
        foreach (var index in _store.AllTagIndices)
            _previous[index] = _store.GetValue(index);

        // Sinh danh sách Trace định dạng
        var trace = BuildTrace(_scanCount, tickMs, evaluations);

        var valuesSnapshot = _store.AllTagIndices.ToDictionary(idx => idx, idx => _store.GetValue(idx));

        return new RuntimeSnapshot(
            tickMs,
            valuesSnapshot,
            _events.ToArray(),
            pending,
            _scanCount,
            evaluations,
            trace);
    }

    public void Reset()
    {
        _scanCount = 0;
        _previous.Clear();
        foreach (var index in _store.AllTagIndices)
            _previous[index] = _store.GetValue(index);
        _dwellStarted.Clear();
        _lastInterval.Clear();
        _events.Clear();
    }

    private int Read(int? index) => index.HasValue ? _store.GetValue(index.Value) : 0;
    private void Write(int tagIndex, int value) => _store.SetValue(tagIndex, value);

    private bool GuardIsOpen(Rule rule, out string summary)
    {
        if (!rule.Guard.HasGuard || rule.Guard.Tag == null)
        {
            summary = string.Empty;
            return true;
        }
        var current = Read(rule.Guard.Tag.TagIndex);
        var open = current != 0;
        var passed = rule.Guard.Negated ? !open : open;
        string expected = rule.Guard.Negated ? "0" : "1";
        summary = passed
            ? $"Guard {rule.Guard.Tag.Name} == {current} (Passed)"
            : $"Guard {rule.Guard.Tag.Name} == {current} (Blocked, cần {expected})";
        return passed;
    }

    private ActionExecutionDelta? ExecuteWithDelta(Rule rule, int sourceValue, long tickMs)
    {
        var targetIndex = rule.Action.TargetTag.TagIndex;
        if (!_store.ContainsTag(targetIndex) && rule.Action.Type is not (ActionKind.LogEvent or ActionKind.SendAlarm))
            return null;

        int beforeVal = _store.GetValue(targetIndex);
        int afterVal = beforeVal;
        string targetName = rule.Action.TargetTag.Name;
        string formattedDelta;

        switch (rule.Action.Type)
        {
            case ActionKind.SetTag:
                afterVal = rule.Action.Parameter;
                Write(targetIndex, afterVal);
                formattedDelta = $"{targetName}: {beforeVal} → {afterVal} (SET)";
                break;
            case ActionKind.ToggleTag:
                afterVal = beforeVal == 0 ? 1 : 0;
                Write(targetIndex, afterVal);
                formattedDelta = $"{targetName}: {beforeVal} → {afterVal} (TOGGLE)";
                break;
            case ActionKind.IncrementCounter:
                afterVal = beforeVal + rule.Action.Parameter;
                Write(targetIndex, afterVal);
                formattedDelta = $"{targetName}: {beforeVal} → {afterVal} (+{rule.Action.Parameter})";
                break;
            case ActionKind.AddTag:
                afterVal = beforeVal + sourceValue;
                Write(targetIndex, afterVal);
                formattedDelta = $"{targetName}: {beforeVal} → {afterVal} (+{sourceValue})";
                break;
            case ActionKind.ScaleTag:
                afterVal = (int)((long)sourceValue * rule.Action.Parameter / 1000 + rule.Trigger.ThresholdHi);
                Write(targetIndex, afterVal);
                formattedDelta = $"{targetName}: {beforeVal} → {afterVal} (SCALE)";
                break;
            case ActionKind.SendAlarm:
                _events.Add(new(tickMs, rule.RuleIndex, $"Alarm {rule.Action.Parameter}"));
                formattedDelta = $"ALARM #{rule.Action.Parameter}";
                break;
            case ActionKind.LogEvent:
                _events.Add(new(tickMs, rule.RuleIndex, $"Log tag {targetName} = {sourceValue}"));
                formattedDelta = $"LOG: {targetName} = {sourceValue}";
                break;
            case ActionKind.WriteRemote:
                _events.Add(new(tickMs, rule.RuleIndex, $"Remote write {targetName} = {rule.Action.Parameter}"));
                formattedDelta = $"REMOTE: {targetName} = {rule.Action.Parameter}";
                break;
            default:
                formattedDelta = $"{rule.Action.Type}";
                break;
        }

        _events.Add(new(tickMs, rule.RuleIndex, $"Rule {rule.Name} fired: {rule.Action.Type}"));
        return new ActionExecutionDelta(
            targetIndex,
            targetName,
            rule.Action.Type,
            beforeVal,
            afterVal,
            formattedDelta);
    }

    private static List<string> BuildTrace(long scanCount, long tickMs, List<RuleEvaluationRecord> evals)
    {
        var list = new List<string>
        {
            $"--- CHU KỲ QUÉT #{scanCount} (t = {tickMs} ms) ---"
        };

        var activeEvals = evals.Where(e => e.Status != RuleEvaluationStatus.Disabled).ToList();
        if (activeEvals.Count == 0)
        {
            list.Add("  (Không có rules nào được kích hoạt)");
            return list;
        }

        if (activeEvals.All(e => e.Status == RuleEvaluationStatus.Skip))
        {
            list.Add("  (Tất cả rules đều ở trạng thái SKIP hoặc giữ nguyên)");
            return list;
        }

        foreach (var eval in activeEvals)
        {
            string prefix = eval.Status switch
            {
                RuleEvaluationStatus.Pass => "✔ PASS   ",
                RuleEvaluationStatus.WaitingDwell => "⧖ WAITING",
                RuleEvaluationStatus.BlockedByGuard => "⛔ BLOCKED",
                _ => "• SKIP   "
            };

            string chainNote = eval.IsChainedInSameScan ? " [SAME-SCAN CHAINED]" : "";

            if (eval.Status == RuleEvaluationStatus.Pass)
            {
                list.Add($"  {prefix} [R{eval.RuleIndex + 1}] {eval.RuleName}: {eval.TriggerSummary}{chainNote} → {eval.ActionDelta?.FormattedDelta}");
            }
            else if (eval.Status == RuleEvaluationStatus.BlockedByGuard)
            {
                list.Add($"  {prefix} [R{eval.RuleIndex + 1}] {eval.RuleName}: {eval.GuardSummary}");
            }
            else if (eval.Status == RuleEvaluationStatus.WaitingDwell)
            {
                list.Add($"  {prefix} [R{eval.RuleIndex + 1}] {eval.RuleName}: {eval.TriggerSummary} · Dwell: {eval.DwellElapsedMs}/{eval.DwellRequiredMs} ms");
            }
        }

        return list;
    }

    private static bool Compare(CompareOperator op, int value, int lo, int hi) => op switch
    {
        CompareOperator.None => true,
        CompareOperator.Equal => value == lo,
        CompareOperator.NotEqual => value != lo,
        CompareOperator.GreaterThan => value > lo,
        CompareOperator.LessThan => value < lo,
        CompareOperator.GreaterThanOrEqual => value >= lo,
        CompareOperator.LessThanOrEqual => value <= lo,
        CompareOperator.Between => value >= lo && value <= hi,
        _ => false
    };
}
