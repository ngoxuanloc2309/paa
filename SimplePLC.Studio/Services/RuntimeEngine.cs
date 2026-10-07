using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

public sealed class SimulatedClock : ISimulatedClock
{
    public long NowMs { get; private set; }
    public void Reset() => NowMs = 0;
    public void Advance(long milliseconds) => NowMs += Math.Max(0, milliseconds);
}

public interface ISimulatedClock
{
    long NowMs { get; }
    void Reset();
    void Advance(long milliseconds);
}

public sealed class RuntimeEngine : IRuntimeEngine
{
    private readonly IReadOnlyList<TagModel> _tags;
    private readonly Dictionary<int, int> _previous = new();
    private readonly Dictionary<int, long> _dwellStarted = new();
    private readonly Dictionary<int, long> _lastInterval = new();
    private readonly List<RuntimeEvent> _events = new();
    private long _scanCount = 0;

    public long ScanCount => _scanCount;
    public Func<DateTime>? TimeProvider { get; set; }
    private DateTime CurrentTime => TimeProvider?.Invoke() ?? DateTime.Now;

    public RuntimeEngine(IReadOnlyList<TagModel> tags)
    {
        _tags = tags;
        foreach (var tag in tags)
            _previous[tag.Index] = tag.Value;
    }

    public RuntimeSnapshot Scan(SimplePLC.Application.Logic.Compilation.CompiledProgram program, long tickMs) =>
        Scan(program.Rules, tickMs);

    public RuntimeSnapshot Scan(IReadOnlyList<SimplePLC.Domain.Models.Rule> rules, long tickMs)
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
                case SimplePLC.Domain.Enums.TriggerKind.Interval:
                    conditionMet = !_lastInterval.TryGetValue(rule.RuleIndex, out var last) || (tickMs - last >= rule.Trigger.ForMs);
                    trigDesc = $"INTERVAL ({rule.Trigger.ForMs}ms)";
                    break;

                case SimplePLC.Domain.Enums.TriggerKind.TimeWindow:
                    var now = CurrentTime;
                    int currentHhmm = now.Hour * 100 + now.Minute;
                    if (rule.Trigger.CompareOp == SimplePLC.Domain.Enums.CompareOperator.Equal)
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

                case SimplePLC.Domain.Enums.TriggerKind.OnRise:
                    bool riseEdge = previous == 0 && current != 0;
                    bool riseHeld = current != 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.RuleIndex) ? riseHeld : riseEdge;
                    trigDesc = $"{rule.Trigger.Tag.Name} ON_RISE (prev={previous}, curr={current})";
                    break;

                case SimplePLC.Domain.Enums.TriggerKind.OnFall:
                    bool fallEdge = previous != 0 && current == 0;
                    bool fallHeld = current == 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.RuleIndex) ? fallHeld : fallEdge;
                    trigDesc = $"{rule.Trigger.Tag.Name} ON_FALL (prev={previous}, curr={current})";
                    break;

                case SimplePLC.Domain.Enums.TriggerKind.OnChange:
                default:
                    bool compareMet = Compare((CompareOp)rule.Trigger.CompareOp, current, rule.Trigger.ThresholdLo, rule.Trigger.ThresholdHi);
                    if (rule.Trigger.CompareOp != SimplePLC.Domain.Enums.CompareOperator.None)
                    {
                        conditionMet = compareMet;
                        trigDesc = $"{rule.Trigger.Tag.Name} = {current} (Compare {(CompareOp)rule.Trigger.CompareOp} {rule.Trigger.ThresholdLo})";
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
            if (rule.Trigger.ForMs > 0 && rule.Trigger.Type is not (SimplePLC.Domain.Enums.TriggerKind.Interval or SimplePLC.Domain.Enums.TriggerKind.TimeWindow))
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
            if (rule.Trigger.Type == SimplePLC.Domain.Enums.TriggerKind.Interval)
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
        foreach (var tag in _tags)
            _previous[tag.Index] = tag.Value;

        // Sinh danh sách Trace định dạng
        var trace = BuildTrace(_scanCount, tickMs, evaluations);

        return new RuntimeSnapshot(
            tickMs,
            _tags.ToDictionary(t => t.Index, t => t.Value),
            _events.ToArray(),
            pending,
            _scanCount,
            evaluations,
            trace);
    }

    public RuntimeSnapshot Scan(IReadOnlyList<RuleItemModel> rules, long tickMs)
    {
        _scanCount++;
        _events.Clear();
        var pending = new Dictionary<int, long>();
        var evaluations = new List<RuleEvaluationRecord>();
        var writtenTagsInThisScan = new HashSet<int>();

        foreach (var rule in rules.OrderBy(r => r.Index))
        {
            if (!rule.Enabled)
            {
                evaluations.Add(new RuleEvaluationRecord(
                    rule.Index,
                    rule.Id,
                    RuleEvaluationStatus.Disabled,
                    "Disabled",
                    null,
                    0,
                    0,
                    null,
                    false,
                    $"[R{rule.Index + 1}] DISABLED {rule.Id}"));
                continue;
            }

            int trigIndex = rule.TriggerTag?.Index ?? 0;
            bool isChained = writtenTagsInThisScan.Contains(trigIndex) ||
                (rule.GuardTag != null && writtenTagsInThisScan.Contains(rule.GuardTag.Index));

            var current = Read(trigIndex);
            var previous = _previous.TryGetValue(trigIndex, out var old) ? old : current;

            bool conditionMet = false;
            string trigDesc;

            switch (rule.TriggerType)
            {
                case TriggerType.INTERVAL:
                    conditionMet = !_lastInterval.TryGetValue(rule.Index, out var last) || (tickMs - last >= rule.ForMs);
                    trigDesc = $"INTERVAL ({rule.ForMs}ms)";
                    break;

                case TriggerType.TIME_WINDOW:
                    var now2 = CurrentTime;
                    int currentHhmm2 = now2.Hour * 100 + now2.Minute;
                    if (rule.CompareOp == CompareOp.EQ)
                    {
                        conditionMet = (currentHhmm2 == rule.ThresholdLo);
                    }
                    else
                    {
                        int start = rule.ThresholdLo;
                        int end = rule.ThresholdHi;
                        conditionMet = start <= end
                            ? (currentHhmm2 >= start && currentHhmm2 <= end)
                            : (currentHhmm2 >= start || currentHhmm2 <= end);
                    }
                    trigDesc = $"TIME_WINDOW [{rule.ThresholdLo}..{rule.ThresholdHi}] (time={currentHhmm2:D4})";
                    break;

                case TriggerType.ON_RISE:
                    bool riseEdge = previous == 0 && current != 0;
                    bool riseHeld = current != 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.Index) ? riseHeld : riseEdge;
                    trigDesc = $"{rule.TriggerTag?.Name} ON_RISE (prev={previous}, curr={current})";
                    break;

                case TriggerType.ON_FALL:
                    bool fallEdge = previous != 0 && current == 0;
                    bool fallHeld = current == 0;
                    conditionMet = _dwellStarted.ContainsKey(rule.Index) ? fallHeld : fallEdge;
                    trigDesc = $"{rule.TriggerTag?.Name} ON_FALL (prev={previous}, curr={current})";
                    break;

                case TriggerType.ON_CHANGE:
                default:
                    bool compareMet = Compare(rule.CompareOp, current, rule.ThresholdLo, rule.ThresholdHi);
                    if (rule.CompareOp != CompareOp.NONE)
                    {
                        conditionMet = compareMet;
                        trigDesc = $"{rule.TriggerTag?.Name} = {current} (Compare {rule.CompareOp} {rule.ThresholdLo})";
                    }
                    else
                    {
                        conditionMet = previous != current;
                        trigDesc = $"{rule.TriggerTag?.Name} ON_CHANGE (prev={previous}, curr={current})";
                    }
                    break;
            }

            if (!conditionMet)
            {
                if (_dwellStarted.ContainsKey(rule.Index))
                    _dwellStarted.Remove(rule.Index);

                evaluations.Add(new RuleEvaluationRecord(
                    rule.Index,
                    rule.Id,
                    RuleEvaluationStatus.Skip,
                    trigDesc,
                    null,
                    0,
                    rule.ForMs,
                    null,
                    isChained,
                    $"[R{rule.Index + 1}] SKIP {rule.Id}: {trigDesc} false"));
                continue;
            }

            long elapsedDwell = 0;
            if (rule.ForMs > 0 && rule.TriggerType is not (TriggerType.INTERVAL or TriggerType.TIME_POINT or TriggerType.TIME_WINDOW))
            {
                if (!_dwellStarted.TryGetValue(rule.Index, out var started))
                {
                    _dwellStarted[rule.Index] = tickMs;
                    started = tickMs;
                }

                elapsedDwell = tickMs - started;
                if (elapsedDwell < rule.ForMs)
                {
                    long remaining = rule.ForMs - elapsedDwell;
                    pending[rule.Index] = remaining;
                    evaluations.Add(new RuleEvaluationRecord(
                        rule.Index,
                        rule.Id,
                        RuleEvaluationStatus.WaitingDwell,
                        trigDesc,
                        null,
                        elapsedDwell,
                        rule.ForMs,
                        null,
                        isChained,
                        $"[R{rule.Index + 1}] WAITING {rule.Id}: {elapsedDwell}/{rule.ForMs} ms"));
                    continue;
                }
            }

            if (!GuardIsOpen(rule, out string guardSummary))
            {
                evaluations.Add(new RuleEvaluationRecord(
                    rule.Index,
                    rule.Id,
                    RuleEvaluationStatus.BlockedByGuard,
                    trigDesc,
                    guardSummary,
                    elapsedDwell,
                    rule.ForMs,
                    null,
                    isChained,
                    $"[R{rule.Index + 1}] BLOCKED {rule.Id}: {guardSummary}"));
                continue;
            }

            if (rule.TriggerType == TriggerType.INTERVAL)
                _lastInterval[rule.Index] = tickMs;

            _dwellStarted.Remove(rule.Index);

            var delta = ExecuteWithDelta(rule, current, tickMs);
            if (rule.ActionTag != null)
                writtenTagsInThisScan.Add(rule.ActionTag.Index);

            string chainTag = isChained ? " [CHAINED]" : "";
            evaluations.Add(new RuleEvaluationRecord(
                rule.Index,
                rule.Id,
                RuleEvaluationStatus.Pass,
                trigDesc,
                string.IsNullOrEmpty(guardSummary) ? null : guardSummary,
                rule.ForMs,
                rule.ForMs,
                delta,
                isChained,
                $"[R{rule.Index + 1}] PASS {rule.Id}{chainTag} → {delta?.FormattedDelta}"));
        }

        foreach (var rule in rules)
            if (_dwellStarted.TryGetValue(rule.Index, out var started))
                pending.TryAdd(rule.Index, Math.Max(0, (long)rule.ForMs - (tickMs - started)));

        foreach (var tag in _tags)
            _previous[tag.Index] = tag.Value;

        var trace = BuildTrace(_scanCount, tickMs, evaluations);

        return new RuntimeSnapshot(
            tickMs,
            _tags.ToDictionary(t => t.Index, t => t.Value),
            _events.ToArray(),
            pending,
            _scanCount,
            evaluations,
            trace);
    }

    private static IReadOnlyList<string> BuildTrace(long scanNumber, long tickMs, List<RuleEvaluationRecord> evaluations)
    {
        var list = new List<string>();
        var activeEvals = evaluations.Where(e => e.Status != RuleEvaluationStatus.Skip && e.Status != RuleEvaluationStatus.Disabled).ToList();

        list.Add($"--- Scan #{scanNumber} ({tickMs} ms) ---");
        if (activeEvals.Count == 0)
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

    private bool GuardIsOpen(SimplePLC.Domain.Models.Rule rule, out string summary)
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

    private bool GuardIsOpen(RuleItemModel rule, out string summary)
    {
        if (rule.GuardTag == null || rule.GuardTag.Kind == TagKind.None)
        {
            summary = string.Empty;
            return true;
        }
        var current = Read(rule.GuardTag.Index);
        var open = current != 0;
        var passed = rule.GuardNegated ? !open : open;
        string expected = rule.GuardNegated ? "0" : "1";
        summary = passed
            ? $"Guard {rule.GuardTag.Name} == {current} (Passed)"
            : $"Guard {rule.GuardTag.Name} == {current} (Blocked, cần {expected})";
        return passed;
    }

    private ActionExecutionDelta? ExecuteWithDelta(SimplePLC.Domain.Models.Rule rule, int sourceValue, long tickMs)
    {
        var targetIndex = rule.Action.TargetTag.TagIndex;
        var target = _tags.FirstOrDefault(t => t.Index == targetIndex);
        if (target == null && rule.Action.Type is not (SimplePLC.Domain.Enums.ActionKind.LogEvent or SimplePLC.Domain.Enums.ActionKind.SendAlarm))
            return null;

        int beforeVal = target?.Value ?? 0;
        int afterVal = beforeVal;
        string formattedDelta;

        switch (rule.Action.Type)
        {
            case SimplePLC.Domain.Enums.ActionKind.SetTag:
                afterVal = rule.Action.Parameter;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (SET)";
                break;
            case SimplePLC.Domain.Enums.ActionKind.ToggleTag:
                afterVal = beforeVal == 0 ? 1 : 0;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (TOGGLE)";
                break;
            case SimplePLC.Domain.Enums.ActionKind.IncrementCounter:
                afterVal = beforeVal + rule.Action.Parameter;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (+{rule.Action.Parameter})";
                break;
            case SimplePLC.Domain.Enums.ActionKind.AddTag:
                afterVal = beforeVal + sourceValue;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (+{sourceValue})";
                break;
            case SimplePLC.Domain.Enums.ActionKind.ScaleTag:
                afterVal = (int)((long)sourceValue * rule.Action.Parameter / 1000 + rule.Trigger.ThresholdHi);
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (SCALE)";
                break;
            case SimplePLC.Domain.Enums.ActionKind.SendAlarm:
                _events.Add(new(tickMs, rule.RuleIndex, $"Alarm {rule.Action.Parameter}"));
                formattedDelta = $"ALARM #{rule.Action.Parameter}";
                break;
            case SimplePLC.Domain.Enums.ActionKind.LogEvent:
                _events.Add(new(tickMs, rule.RuleIndex, $"Log tag {target?.Name ?? "source"} = {sourceValue}"));
                formattedDelta = $"LOG: {target?.Name ?? "source"} = {sourceValue}";
                break;
            case SimplePLC.Domain.Enums.ActionKind.WriteRemote:
                _events.Add(new(tickMs, rule.RuleIndex, $"Remote write {target?.Name} = {rule.Action.Parameter}"));
                formattedDelta = $"REMOTE: {target?.Name} = {rule.Action.Parameter}";
                break;
            default:
                formattedDelta = $"{rule.Action.Type}";
                break;
        }

        _events.Add(new(tickMs, rule.RuleIndex, $"Rule {rule.Name} fired: {rule.Action.Type}"));
        return new ActionExecutionDelta(
            targetIndex,
            target?.Name ?? "EVENT",
            (ActionType)rule.Action.Type,
            beforeVal,
            afterVal,
            formattedDelta);
    }

    private ActionExecutionDelta? ExecuteWithDelta(RuleItemModel rule, int sourceValue, long tickMs)
    {
        var target = rule.ActionTag;
        if (target == null && rule.ActionType is not (ActionType.LOG_EVENT or ActionType.SEND_ALARM))
            return null;

        int beforeVal = target?.Value ?? 0;
        int afterVal = beforeVal;
        string formattedDelta;

        switch (rule.ActionType)
        {
            case ActionType.SET_TAG:
                afterVal = rule.ActionParam;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (SET)";
                break;
            case ActionType.TOGGLE_TAG:
                afterVal = beforeVal == 0 ? 1 : 0;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (TOGGLE)";
                break;
            case ActionType.INC_COUNTER:
                afterVal = beforeVal + rule.ActionParam;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (+{rule.ActionParam})";
                break;
            case ActionType.ADD_TAG:
                afterVal = beforeVal + sourceValue;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (+{sourceValue})";
                break;
            case ActionType.SCALE_TAG:
                afterVal = sourceValue * rule.ActionParam / 1000 + rule.ThresholdHi;
                Write(target!, afterVal);
                formattedDelta = $"{target!.Name}: {beforeVal} → {afterVal} (SCALE)";
                break;
            case ActionType.SEND_ALARM:
                _events.Add(new(tickMs, rule.Index, $"Alarm {rule.ActionParam}"));
                formattedDelta = $"ALARM #{rule.ActionParam}";
                break;
            case ActionType.LOG_EVENT:
                _events.Add(new(tickMs, rule.Index, $"Log tag {target?.Name ?? "source"} = {sourceValue}"));
                formattedDelta = $"LOG: {target?.Name ?? "source"} = {sourceValue}";
                break;
            case ActionType.WRITE_REMOTE:
                _events.Add(new(tickMs, rule.Index, $"Remote write {target?.Name} = {rule.ActionParam}"));
                formattedDelta = $"REMOTE: {target?.Name} = {rule.ActionParam}";
                break;
            default:
                formattedDelta = $"{rule.ActionType}";
                break;
        }

        _events.Add(new(tickMs, rule.Index, $"Rule {rule.Id} fired: {rule.ActionType}"));
        return new ActionExecutionDelta(
            target?.Index ?? 0,
            target?.Name ?? "EVENT",
            rule.ActionType,
            beforeVal,
            afterVal,
            formattedDelta);
    }

    public void Reset()
    {
        _scanCount = 0;
        _previous.Clear();
        foreach (var tag in _tags)
            _previous[tag.Index] = tag.Value;
        _dwellStarted.Clear();
        _lastInterval.Clear();
        _events.Clear();
    }

    private int Read(int? index) => index.HasValue ? _tags.FirstOrDefault(t => t.Index == index.Value)?.Value ?? 0 : 0;
    private static void Write(TagModel tag, int value) => tag.Value = value;

    private static bool Compare(CompareOp op, int value, int lo, int hi) => op switch
    {
        CompareOp.NONE => true,
        CompareOp.EQ => value == lo,
        CompareOp.NEQ => value != lo,
        CompareOp.GT => value > lo,
        CompareOp.LT => value < lo,
        CompareOp.GTE => value >= lo,
        CompareOp.LTE => value <= lo,
        CompareOp.BETWEEN => value >= lo && value <= hi,
        _ => false
    };
}

public interface IRuntimeEngine
{
    Func<DateTime>? TimeProvider { get; set; }
    RuntimeSnapshot Scan(IReadOnlyList<RuleItemModel> rules, long tickMs);
    RuntimeSnapshot Scan(SimplePLC.Application.Logic.Compilation.CompiledProgram program, long tickMs);
    RuntimeSnapshot Scan(IReadOnlyList<SimplePLC.Domain.Models.Rule> rules, long tickMs);
    void Reset();
    long ScanCount { get; }
}
