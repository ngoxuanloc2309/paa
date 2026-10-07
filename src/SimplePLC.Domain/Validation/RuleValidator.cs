using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

namespace SimplePLC.Domain.Validation;

/// <summary>
/// Bộ thẩm định các bất biến nghiệp vụ của một Rule theo chuẩn SimplePLC Domain.
/// </summary>
public static class RuleValidator
{
    public const string ErrorTriggerTagMissing = "ERR_TRIGGER_TAG_MISSING";
    public const string ErrorActionTagMissing = "ERR_ACTION_TAG_MISSING";
    public const string ErrorActionTagReadOnly = "ERR_ACTION_TAG_READONLY";
    public const string ErrorBetweenThresholdsInvalid = "ERR_BETWEEN_THRESHOLDS_INVALID";
    public const string ErrorIntervalForMsZero = "ERR_INTERVAL_FOR_MS_ZERO";
    public const string ErrorGuardTagNotBoolean = "ERR_GUARD_TAG_NOT_BOOLEAN";
    public const string ErrorToggleTargetNotBoolean = "ERR_TOGGLE_TARGET_NOT_BOOLEAN";
    public const string ErrorIncrementTargetNotInteger = "ERR_INCREMENT_TARGET_NOT_INTEGER";

    /// <summary>
    /// Thẩm định một Rule nghiệp vụ.
    /// </summary>
    public static DomainValidationResult Validate(Rule rule)
    {
        var result = new DomainValidationResult();

        if (rule == null)
        {
            result.AddError("ERR_RULE_NULL", "Rule cannot be null.");
            return result;
        }

        int ruleIdx = rule.RuleIndex;

        // 1. Kiểm tra Trigger Tag
        if (rule.Trigger?.Tag == null || rule.Trigger.Tag.Kind == TagKind.None)
        {
            result.AddError(ErrorTriggerTagMissing, $"Rule #{ruleIdx + 1} ('{rule.Name}'): Trigger tag is missing or invalid.", ruleIdx);
        }

        // 2. Kiểm tra Action Target Tag
        if (rule.Action?.TargetTag == null || rule.Action.TargetTag.Kind == TagKind.None)
        {
            result.AddError(ErrorActionTagMissing, $"Rule #{ruleIdx + 1} ('{rule.Name}'): Action target tag is missing or invalid.", ruleIdx);
        }
        else
        {
            // Bất biến: Không được ghi vào Tag Read-Only (DI, AI)
            if (rule.Action.TargetTag.IsReadOnly)
            {
                result.AddError(
                    ErrorActionTagReadOnly,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): Cannot write to read-only tag '{rule.Action.TargetTag.Name}' ({rule.Action.TargetTag.Kind}).",
                    ruleIdx,
                    rule.Action.TargetTag.TagIndex
                );
            }

            // Bất biến: ToggleTag chỉ áp dụng cho Boolean
            if (rule.Action.Type == ActionKind.ToggleTag && rule.Action.TargetTag.DataType != TagDataType.Boolean)
            {
                result.AddError(
                    ErrorToggleTargetNotBoolean,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): Action 'ToggleTag' can only target Boolean tags, but '{rule.Action.TargetTag.Name}' is {rule.Action.TargetTag.DataType}.",
                    ruleIdx,
                    rule.Action.TargetTag.TagIndex
                );
            }

            // Bất biến: IncrementCounter chỉ áp dụng cho Integer
            if (rule.Action.Type == ActionKind.IncrementCounter && rule.Action.TargetTag.DataType != TagDataType.Int32)
            {
                result.AddError(
                    ErrorIncrementTargetNotInteger,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): Action 'IncrementCounter' can only target Integer tags, but '{rule.Action.TargetTag.Name}' is {rule.Action.TargetTag.DataType}.",
                    ruleIdx,
                    rule.Action.TargetTag.TagIndex
                );
            }
        }

        // 3. Kiểm tra phép so sánh Between (ngoại trừ TimeWindow vì hỗ trợ ca đêm xuyên đêm cross-midnight ThresholdLo > ThresholdHi)
        if (rule.Trigger != null && rule.Trigger.CompareOp == CompareOperator.Between && rule.Trigger.Type != TriggerKind.TimeWindow)
        {
            if (rule.Trigger.ThresholdLo > rule.Trigger.ThresholdHi)
            {
                result.AddError(
                    ErrorBetweenThresholdsInvalid,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): ThresholdLo ({rule.Trigger.ThresholdLo}) cannot be greater than ThresholdHi ({rule.Trigger.ThresholdHi}) for Between operator.",
                    ruleIdx
                );
            }
        }

        // 4. Kiểm tra Trigger Interval
        if (rule.Trigger != null && rule.Trigger.Type == TriggerKind.Interval)
        {
            if (rule.Trigger.ForMs == 0)
            {
                result.AddError(
                    ErrorIntervalForMsZero,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): Interval trigger requires ForMs > 0.",
                    ruleIdx
                );
            }
        }

        // 5. Kiểm tra Guard Tag (nếu có)
        if (rule.Guard != null && rule.Guard.HasGuard)
        {
            if (rule.Guard.Tag!.DataType != TagDataType.Boolean)
            {
                result.AddError(
                    ErrorGuardTagNotBoolean,
                    $"Rule #{ruleIdx + 1} ('{rule.Name}'): Guard tag '{rule.Guard.Tag.Name}' must be a Boolean tag, but is {rule.Guard.Tag.DataType}.",
                    ruleIdx,
                    rule.Guard.Tag.TagIndex
                );
            }
        }

        return result;
    }
}
