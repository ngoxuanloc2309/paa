namespace SimplePLC.Domain.Validation;

public sealed record ValidationError(string Code, string Message, int? RuleIndex = null, ushort? TagIndex = null);

/// <summary>
/// Kết quả thẩm định nghiệp vụ của một Rule hoặc toàn bộ RuleTable.
/// </summary>
public sealed class DomainValidationResult
{
    private readonly List<ValidationError> _errors = new();

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors => _errors;

    public void AddError(string code, string message, int? ruleIndex = null, ushort? tagIndex = null)
    {
        _errors.Add(new ValidationError(code, message, ruleIndex, tagIndex));
    }

    public static DomainValidationResult Success() => new();
}
