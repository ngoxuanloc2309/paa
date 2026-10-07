using CommunityToolkit.Mvvm.ComponentModel;

namespace SimplePLC.Studio.Models;

public enum TriggerType
{
    ON_CHANGE = 0,
    ON_RISE = 1,
    ON_FALL = 2,
    TIME_POINT = 3,
    TIME_WINDOW = 3,
    INTERVAL = 4
}

public enum CompareOp
{
    NONE = 0,
    EQ = 1,
    NEQ = 2,
    GT = 3,
    LT = 4,
    GTE = 5,
    LTE = 6,
    BETWEEN = 7
}

public enum ActionType
{
    SET_TAG = 0,
    TOGGLE_TAG = 1,
    INC_COUNTER = 2,
    WRITE_REMOTE = 3,
    LOG_EVENT = 4,
    SEND_ALARM = 5,
    ADD_TAG = 6,
    SCALE_TAG = 7
}

public partial class RuleItemModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "R0";

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private TriggerType _triggerType = TriggerType.ON_RISE;

    [ObservableProperty]
    private TagModel? _triggerTag;

    [ObservableProperty]
    private CompareOp _compareOp = CompareOp.NONE;

    [ObservableProperty]
    private int _thresholdLo;

    [ObservableProperty]
    private int _thresholdHi;

    [ObservableProperty]
    private uint _forMs;

    [ObservableProperty]
    private TagModel? _guardTag;

    [ObservableProperty]
    private bool _guardNegated;

    [ObservableProperty]
    private ActionType _actionType = ActionType.SET_TAG;

    [ObservableProperty]
    private TagModel? _actionTag;

    [ObservableProperty]
    private int _actionParam = 1;

    [ObservableProperty]
    private string _narrative = string.Empty;

    [ObservableProperty]
    private string _rawHex = string.Empty;

    [ObservableProperty]
    private string _rawHexBreakdown = string.Empty;

    [ObservableProperty]
    private string? _diagramId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MacroBadgeBackground))]
    [NotifyPropertyChangedFor(nameof(MacroBadgeBorderBrush))]
    [NotifyPropertyChangedFor(nameof(MacroBadgeForeground))]
    private string _macroAttribution = string.Empty;

    public string MacroBadgeBackground => MacroAttribution.StartsWith("CT", StringComparison.OrdinalIgnoreCase) ? "#F0FDFA" : "#EDE9FE";
    public string MacroBadgeBorderBrush => MacroAttribution.StartsWith("CT", StringComparison.OrdinalIgnoreCase) ? "#0F766E" : "#4C1D95";
    public string MacroBadgeForeground => MacroAttribution.StartsWith("CT", StringComparison.OrdinalIgnoreCase) ? "#0F766E" : "#4C1D95";

    /// <summary>
    /// Sơ đồ các node tương ứng trên Logic Graph để có thể nạp lại và chỉnh sửa trực tiếp trên Canvas.
    /// </summary>
    public List<ProjectNodeData>? SourceNodes { get; set; }

    /// <summary>
    /// Các liên kết nối giữa các node của rule này trên Logic Graph.
    /// </summary>
    public List<ProjectConnectionData>? SourceConnections { get; set; }

    public string TriggerSummary
    {
        get
        {
            string tag = TriggerTag?.Name ?? "TAG";
            string type = TriggerType switch
            {
                TriggerType.ON_RISE => "Rise 0→1",
                TriggerType.ON_FALL => "Fall 1→0",
                TriggerType.ON_CHANGE => "Change",
                TriggerType.INTERVAL => $"{ForMs}ms",
                TriggerType.TIME_WINDOW => "Window",
                _ => TriggerType.ToString()
            };
            return $"{tag} ({type})";
        }
    }

    public string GuardSummary
    {
        get
        {
            if (GuardTag == null || GuardTag.Kind == TagKind.None)
                return "—";
            if (CompareOp == CompareOp.NONE)
                return GuardNegated ? $"{GuardTag.Name} == 0" : $"{GuardTag.Name} == 1";

            string op = CompareOp switch
            {
                CompareOp.EQ => "==",
                CompareOp.NEQ => "!=",
                CompareOp.GT => ">",
                CompareOp.LT => "<",
                CompareOp.GTE => ">=",
                CompareOp.LTE => "<=",
                CompareOp.BETWEEN => $"in [{ThresholdLo}..{ThresholdHi}]",
                _ => ""
            };
            return CompareOp == CompareOp.BETWEEN ? $"{GuardTag.Name} {op}" : $"{GuardTag.Name} {op} {ThresholdLo}";
        }
    }

    public string ActionSummary
    {
        get
        {
            string tag = ActionTag?.Name ?? "TAG";
            return ActionType switch
            {
                ActionType.SET_TAG => $"Set {tag}={ActionParam}",
                ActionType.TOGGLE_TAG => $"Toggle {tag}",
                ActionType.INC_COUNTER => $"Inc {tag}",
                ActionType.WRITE_REMOTE => $"Modbus {tag}",
                ActionType.SEND_ALARM => $"Alarm {tag}",
                ActionType.LOG_EVENT => $"Log {tag}",
                ActionType.SCALE_TAG => $"Scale {tag}",
                _ => $"{ActionType} {tag}"
            };
        }
    }

    public bool HasGuard => GuardTag != null && GuardTag.Kind != TagKind.None;

    public string TriggerEventText
    {
        get
        {
            bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;
            string opText = "";
            if (CompareOp != CompareOp.NONE)
            {
                opText = CompareOp switch
                {
                    CompareOp.EQ => $"== {ThresholdLo}",
                    CompareOp.NEQ => $"!= {ThresholdLo}",
                    CompareOp.GT => $"> {ThresholdLo}",
                    CompareOp.LT => $"< {ThresholdLo}",
                    CompareOp.GTE => $">= {ThresholdLo}",
                    CompareOp.LTE => $"<= {ThresholdLo}",
                    CompareOp.BETWEEN => isVi ? $"trong dải [{ThresholdLo}..{ThresholdHi}]" : $"in range [{ThresholdLo}..{ThresholdHi}]",
                    _ => ""
                };
            }

            if (!string.IsNullOrEmpty(opText))
            {
                return opText;
            }

            if (isVi)
            {
                return TriggerType switch
                {
                    TriggerType.ON_RISE => "Sườn lên (0 → 1, BẬT)",
                    TriggerType.ON_FALL => "Sườn xuống (1 → 0, TẮT)",
                    TriggerType.ON_CHANGE => "Đổi trạng thái / giá trị",
                    TriggerType.TIME_WINDOW => ThresholdHi > 0
                        ? $"Trong khung giờ {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2} - {ThresholdHi / 100:D2}:{ThresholdHi % 100:D2}"
                        : $"Đúng thời điểm {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2}",
                    TriggerType.INTERVAL => $"Chu kỳ định kỳ {ForMs} ms",
                    _ => TriggerType.ToString()
                };
            }
            else
            {
                return TriggerType switch
                {
                    TriggerType.ON_RISE => "Rising edge (0 → 1, ON)",
                    TriggerType.ON_FALL => "Falling edge (1 → 0, OFF)",
                    TriggerType.ON_CHANGE => "State / value change",
                    TriggerType.TIME_WINDOW => ThresholdHi > 0
                        ? $"During window {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2} - {ThresholdHi / 100:D2}:{ThresholdHi % 100:D2}"
                        : $"At time {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2}",
                    TriggerType.INTERVAL => $"Interval every {ForMs} ms",
                    _ => TriggerType.ToString()
                };
            }
        }
    }

    public string TriggerDwellText
    {
        get
        {
            if (ForMs == 0 || TriggerType == TriggerType.INTERVAL) return string.Empty;
            bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;
            return isVi ? $"Lọc nhiễu / Giữ ổn định: {ForMs} ms" : $"Debounce / Hold time: {ForMs} ms";
        }
    }

    public string GuardConditionText
    {
        get
        {
            if (!HasGuard || GuardTag == null)
            {
                return SimplePLC.Studio.Services.LocalizationService.Tr("RuleFlowNoGuard");
            }

            bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;
            string tagName = GuardTag.DisplayName;

            return isVi
                ? (GuardNegated ? $"{tagName} == TẮT (0)" : $"{tagName} == BẬT (1)")
                : (GuardNegated ? $"{tagName} == OFF (0)" : $"{tagName} == ON (1)");
        }
    }

    public string ActionDetailText
    {
        get
        {
            bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;
            string targetName = ActionTag?.DisplayName ?? "—";
            if (isVi)
            {
                return ActionType switch
                {
                    ActionType.SET_TAG => $"Đặt giá trị {targetName} = {ActionParam}",
                    ActionType.TOGGLE_TAG => $"Đảo trạng thái {targetName} (BẬT ↔ TẮT)",
                    ActionType.INC_COUNTER => $"Tăng bộ đếm {targetName} thêm {ActionParam}",
                    ActionType.WRITE_REMOTE => $"Ghi truyền thông Modbus {targetName} = {ActionParam}",
                    ActionType.SEND_ALARM => $"Phát tín hiệu cảnh báo đến {targetName} (Mã {ActionParam})",
                    ActionType.LOG_EVENT => $"Ghi nhận sự kiện {targetName} vào nhật ký (Mã {ActionParam})",
                    ActionType.SCALE_TAG => $"Quy đổi tỉ lệ sang {targetName} (Hệ số {ActionParam / 1000.0:0.###})",
                    _ => $"{ActionType} {targetName}"
                };
            }
            else
            {
                return ActionType switch
                {
                    ActionType.SET_TAG => $"Set value {targetName} = {ActionParam}",
                    ActionType.TOGGLE_TAG => $"Toggle state of {targetName} (ON ↔ OFF)",
                    ActionType.INC_COUNTER => $"Increment counter {targetName} by {ActionParam}",
                    ActionType.WRITE_REMOTE => $"Write Modbus {targetName} = {ActionParam}",
                    ActionType.SEND_ALARM => $"Trigger alarm on {targetName} (Code {ActionParam})",
                    ActionType.LOG_EVENT => $"Log event {targetName} into record (Code {ActionParam})",
                    ActionType.SCALE_TAG => $"Scale linear value into {targetName} (Gain {ActionParam / 1000.0:0.###})",
                    _ => $"{ActionType} {targetName}"
                };
            }
        }
    }

    public bool HasDwell => !string.IsNullOrEmpty(TriggerDwellText);

    public string StatusText => Enabled
        ? SimplePLC.Studio.Services.LocalizationService.Tr("RuleStatusActive")
        : SimplePLC.Studio.Services.LocalizationService.Tr("RuleStatusDisabled");

    public IEnumerable<TagModel> RelatedTags
    {
        get
        {
            var list = new List<TagModel>();
            if (TriggerTag != null && TriggerTag.Kind != TagKind.None) list.Add(TriggerTag);
            if (GuardTag != null && GuardTag.Kind != TagKind.None && !list.Contains(GuardTag)) list.Add(GuardTag);
            if (ActionTag != null && ActionTag.Kind != TagKind.None && !list.Contains(ActionTag)) list.Add(ActionTag);
            return list;
        }
    }

    public void UpdateNarrative()
    {
        OnPropertyChanged(nameof(TriggerSummary));
        OnPropertyChanged(nameof(GuardSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(HasGuard));
        OnPropertyChanged(nameof(HasDwell));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TriggerEventText));
        OnPropertyChanged(nameof(TriggerDwellText));
        OnPropertyChanged(nameof(GuardConditionText));
        OnPropertyChanged(nameof(ActionDetailText));
        OnPropertyChanged(nameof(RelatedTags));

        bool isVi = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese;

        string cond = "";
        if (CompareOp != CompareOp.NONE)
        {
            string opStr = CompareOp switch
            {
                CompareOp.EQ => $"== {ThresholdLo}",
                CompareOp.NEQ => $"!= {ThresholdLo}",
                CompareOp.GT => $"> {ThresholdLo}",
                CompareOp.LT => $"< {ThresholdLo}",
                CompareOp.GTE => $">= {ThresholdLo}",
                CompareOp.LTE => $"<= {ThresholdLo}",
                CompareOp.BETWEEN => isVi ? $"trong khoảng [{ThresholdLo}..{ThresholdHi}]" : $"between [{ThresholdLo}..{ThresholdHi}]",
                _ => ""
            };
            cond = $" {opStr}";
        }

        string trig;
        if (isVi)
        {
            trig = TriggerType switch
            {
                TriggerType.ON_RISE => $"Khi {TriggerTag?.DisplayName ?? "???"} chuyển trạng thái BẬT (0 → 1)",
                TriggerType.ON_FALL => $"Khi {TriggerTag?.DisplayName ?? "???"} chuyển trạng thái TẮT (1 → 0)",
                TriggerType.ON_CHANGE => !string.IsNullOrEmpty(cond)
                    ? $"Khi {TriggerTag?.DisplayName ?? "???"}{cond}"
                    : $"Khi {TriggerTag?.DisplayName ?? "???"} đổi giá trị",
                TriggerType.TIME_WINDOW => $"Trong ca {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2} - {ThresholdHi / 100:D2}:{ThresholdHi % 100:D2}",
                TriggerType.INTERVAL => $"Mỗi chu kỳ {ForMs} ms",
                _ => "Khi sự kiện xảy ra"
            };
            if (ForMs > 0 && TriggerType != TriggerType.INTERVAL)
            {
                trig += $", giữ ổn định {ForMs} ms";
            }
        }
        else
        {
            trig = TriggerType switch
            {
                TriggerType.ON_RISE => $"When {TriggerTag?.DisplayName ?? "???"} turns ON (0 → 1)",
                TriggerType.ON_FALL => $"When {TriggerTag?.DisplayName ?? "???"} turns OFF (1 → 0)",
                TriggerType.ON_CHANGE => !string.IsNullOrEmpty(cond)
                    ? $"When {TriggerTag?.DisplayName ?? "???"}{cond}"
                    : $"When {TriggerTag?.DisplayName ?? "???"} value changes",
                TriggerType.TIME_WINDOW => $"During shift {ThresholdLo / 100:D2}:{ThresholdLo % 100:D2} - {ThresholdHi / 100:D2}:{ThresholdHi % 100:D2}",
                TriggerType.INTERVAL => $"Every interval of {ForMs} ms",
                _ => "When event triggers"
            };
            if (ForMs > 0 && TriggerType != TriggerType.INTERVAL)
            {
                trig += $", stable for {ForMs} ms";
            }
        }

        string guard = "";
        if (GuardTag != null && GuardTag.Kind != TagKind.None)
        {
            guard = isVi
                ? (GuardNegated ? $" (NẾU {GuardTag.DisplayName} = TẮT)" : $" (NẾU {GuardTag.DisplayName} = BẬT)")
                : (GuardNegated ? $" (IF {GuardTag.DisplayName} = OFF)" : $" (IF {GuardTag.DisplayName} = ON)");
        }

        string act;
        if (isVi)
        {
            act = ActionType switch
            {
                ActionType.SET_TAG => $"THÌ đặt {ActionTag?.DisplayName ?? "???"} = {ActionParam}",
                ActionType.TOGGLE_TAG => $"THÌ đảo trạng thái {ActionTag?.DisplayName ?? "???"}",
                ActionType.INC_COUNTER => $"THÌ tăng {ActionParam} vào {ActionTag?.DisplayName ?? "???"}",
                ActionType.WRITE_REMOTE => $"THÌ ghi Modbus {ActionTag?.DisplayName ?? "???"} = {ActionParam}",
                ActionType.SEND_ALARM => $"THÌ kích hoạt báo động {ActionTag?.DisplayName ?? "???"}",
                ActionType.LOG_EVENT => $"THÌ ghi log {ActionTag?.DisplayName ?? "???"}",
                ActionType.SCALE_TAG => $"THÌ quy đổi giá trị sang {ActionTag?.DisplayName ?? "???"}",
                _ => $"THÌ thực thi {ActionType}"
            };
        }
        else
        {
            act = ActionType switch
            {
                ActionType.SET_TAG => $"THEN set {ActionTag?.DisplayName ?? "???"} = {ActionParam}",
                ActionType.TOGGLE_TAG => $"THEN toggle {ActionTag?.DisplayName ?? "???"}",
                ActionType.INC_COUNTER => $"THEN increment {ActionTag?.DisplayName ?? "???"} by {ActionParam}",
                ActionType.WRITE_REMOTE => $"THEN write Modbus {ActionTag?.DisplayName ?? "???"} = {ActionParam}",
                ActionType.SEND_ALARM => $"THEN trigger alarm {ActionTag?.DisplayName ?? "???"}",
                ActionType.LOG_EVENT => $"THEN log event {ActionTag?.DisplayName ?? "???"}",
                ActionType.SCALE_TAG => $"THEN scale linear value into {ActionTag?.DisplayName ?? "???"}",
                _ => $"THEN execute {ActionType}"
            };
        }

        Narrative = $"{trig}{guard}, {act}.";

        // Sinh 32-byte MCU binary rule hex theo chuẩn SPLC-APP-MCU-001 Revision 1.7
        byte[] ruleBytes = SimplePLC.Studio.Services.RuleBinaryEncoder.EncodeRuleV17(this);
        RawHex = string.Join(" ", ruleBytes.Select(b => b.ToString("X2")));
        RawHexBreakdown = SimplePLC.Studio.Services.RuleBinaryEncoder.FormatDetailedBreakdown(ruleBytes);
    }

    /// <summary>
    /// Chuyển đổi mô hình UI RuleItemModel sang thực thể nghiệp vụ Domain Rule sạch (R2, R4).
    /// </summary>
    public SimplePLC.Domain.Models.Rule ToDomainRule(SimplePLC.Domain.Models.ProductDefinition product)
    {
        ushort trgIdx = (ushort)(TriggerTag?.Index ?? 0);
        var triggerTagDef = product.FindTagByIndex(trgIdx) ??
                            new SimplePLC.Domain.Models.TagDefinition(trgIdx, TriggerTag?.Name ?? $"TAG_{trgIdx}", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);

        ushort actIdx = (ushort)(ActionTag?.Index ?? 0);
        var actionTagDef = product.FindTagByIndex(actIdx) ??
                           new SimplePLC.Domain.Models.TagDefinition(actIdx, ActionTag?.Name ?? $"TAG_{actIdx}", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);

        var trigger = new SimplePLC.Domain.Models.TriggerModel(triggerTagDef, (SimplePLC.Domain.Enums.TriggerKind)TriggerType)
        {
            CompareOp = (SimplePLC.Domain.Enums.CompareOperator)CompareOp,
            ThresholdLo = ThresholdLo,
            ThresholdHi = ThresholdHi,
            ForMs = ForMs
        };

        var action = new SimplePLC.Domain.Models.ActionModel(actionTagDef, (SimplePLC.Domain.Enums.ActionKind)ActionType, ActionParam);

        SimplePLC.Domain.Models.GuardModel guard = SimplePLC.Domain.Models.GuardModel.Empty;
        if (GuardTag != null && GuardTag.Kind != TagKind.None)
        {
            ushort grdIdx = (ushort)GuardTag.Index;
            var guardTagDef = product.FindTagByIndex(grdIdx) ??
                              new SimplePLC.Domain.Models.TagDefinition(grdIdx, GuardTag.Name, SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
            guard = new SimplePLC.Domain.Models.GuardModel(guardTagDef, GuardNegated);
        }

        return new SimplePLC.Domain.Models.Rule(Index, Id, trigger, action, guard, Enabled);
    }

    /// <summary>
    /// Tái tạo mô hình UI RuleItemModel từ thực thể nghiệp vụ Domain Rule.
    /// </summary>
    public static RuleItemModel FromDomainRule(SimplePLC.Domain.Models.Rule domainRule, IEnumerable<TagModel> availableTags)
    {
        var tagsList = availableTags.ToList();
        string displayId = string.IsNullOrWhiteSpace(domainRule.Name) || domainRule.Name.Contains("Lệnh", StringComparison.OrdinalIgnoreCase) || domainRule.Name.StartsWith("Rule_", StringComparison.OrdinalIgnoreCase)
            ? $"R{domainRule.RuleIndex + 1}"
            : domainRule.Name;

        var item = new RuleItemModel
        {
            Id = displayId,
            Index = domainRule.RuleIndex,
            Enabled = domainRule.Enabled,
            TriggerType = (TriggerType)domainRule.Trigger.Type,
            CompareOp = (CompareOp)domainRule.Trigger.CompareOp,
            ThresholdLo = domainRule.Trigger.ThresholdLo,
            ThresholdHi = domainRule.Trigger.ThresholdHi,
            ForMs = domainRule.Trigger.ForMs,
            TriggerTag = tagsList.FirstOrDefault(t => t.Index == domainRule.Trigger.Tag.TagIndex),
            ActionType = (ActionType)domainRule.Action.Type,
            ActionParam = domainRule.Action.Parameter,
            ActionTag = tagsList.FirstOrDefault(t => t.Index == domainRule.Action.TargetTag.TagIndex)
        };

        if (domainRule.Guard.HasGuard && domainRule.Guard.Tag != null)
        {
            item.GuardTag = tagsList.FirstOrDefault(t => t.Index == domainRule.Guard.Tag.TagIndex);
            item.GuardNegated = domainRule.Guard.Negated;
        }

        item.UpdateNarrative();
        return item;
    }
}
