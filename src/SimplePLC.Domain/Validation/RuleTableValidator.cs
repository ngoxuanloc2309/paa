using SimplePLC.Domain.Models;

namespace SimplePLC.Domain.Validation;

/// <summary>
/// Thẩm định toàn bộ bảng RuleTable.
/// </summary>
public static class RuleTableValidator
{
    public const string ErrorTableCapacityExceeded = "ERR_TABLE_CAPACITY_EXCEEDED";

    public static DomainValidationResult Validate(RuleTable table)
    {
        var result = new DomainValidationResult();

        if (table == null)
        {
            result.AddError("ERR_TABLE_NULL", "Rule table cannot be null.");
            return result;
        }

        if (table.Count > RuleTable.MaxCapacity)
        {
            result.AddError(ErrorTableCapacityExceeded, $"Rule count ({table.Count}) exceeds maximum capacity ({RuleTable.MaxCapacity}).");
        }

        foreach (var rule in table.Rules)
        {
            var ruleResult = RuleValidator.Validate(rule);
            foreach (var err in ruleResult.Errors)
            {
                result.AddError(err.Code, err.Message, err.RuleIndex, err.TagIndex);
            }
        }

        return result;
    }
}
