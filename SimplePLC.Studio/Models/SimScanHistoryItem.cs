namespace SimplePLC.Studio.Models;

public sealed class SimScanHistoryItem
{
    public long ScanNumber { get; set; }
    public long TimestampMs { get; set; }
    public string TimeDisplay => $"{TimestampMs / 1000.0:F2}s";
    
    public string InputChange { get; set; } = string.Empty;
    public string TriggerResult { get; set; } = string.Empty;
    public string TriggerReason { get; set; } = string.Empty;
    public bool TriggerPassed { get; set; }
    
    public string GuardStatus { get; set; } = string.Empty;
    public string GuardReason { get; set; } = string.Empty;
    public bool GuardPassed { get; set; }
    
    public string ActionStatus { get; set; } = string.Empty;
    public string OutputDelta { get; set; } = string.Empty;
    public bool ActionFired { get; set; }
    
    public string DwellStatus { get; set; } = string.Empty;
    public string SummaryLine { get; set; } = string.Empty;

    public int RuleIndex { get; set; }
    public string ScanDisplay => RuleIndex > 0 ? $"#{ScanNumber} (R{RuleIndex})" : $"#{ScanNumber}";

    public bool HasInputChange { get; set; }

    public bool HasActivity =>
        HasInputChange ||
        TriggerPassed ||
        ActionFired ||
        string.Equals(GuardStatus, "BLOCKED", System.StringComparison.OrdinalIgnoreCase);

    public string ActivityBadgeText
    {
        get
        {
            bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;
            if (ActionFired) return isVi ? "LỆNH XUẤT" : "ACTION";
            if (string.Equals(GuardStatus, "BLOCKED", System.StringComparison.OrdinalIgnoreCase)) return isVi ? "BỊ KHÓA" : "BLOCKED";
            if (TriggerPassed) return isVi ? "KÍCH HOẠT" : "TRIGGERED";
            if (HasInputChange) return isVi ? "I/O ĐỔI" : "I/O CHANGED";
            return isVi ? "ỔN ĐỊNH" : "STEADY";
        }
    }

    public string ActivityBadgeColor =>
        ActionFired ? "#006487" :
        string.Equals(GuardStatus, "BLOCKED", System.StringComparison.OrdinalIgnoreCase) ? "#B91C1C" :
        TriggerPassed ? "#006487" :
        HasInputChange ? "#107C41" : "#64748B";
}

public sealed class SimSpeedOption
{
    public int IntervalMs { get; }
    public string DisplayText { get; }

    public SimSpeedOption(int intervalMs, string displayText)
    {
        IntervalMs = intervalMs;
        DisplayText = displayText;
    }

    public override string ToString() => DisplayText;
}
