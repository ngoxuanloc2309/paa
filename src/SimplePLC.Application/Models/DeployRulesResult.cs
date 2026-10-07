namespace SimplePLC.Application.Models;

using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Enums;

public sealed record DeployRulesResult
{
    public bool IsSuccess { get; init; }
    public ushort DeployedRuleCount { get; init; }
    public ushort ActiveVersion { get; init; }
    public SPLC_ErrorCode ErrorCode { get; init; } = SPLC_ErrorCode.NONE;
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; init; } = Array.Empty<ValidationError>();

    public static DeployRulesResult Success(ushort ruleCount, ushort activeVersion) =>
        new()
        {
            IsSuccess = true,
            DeployedRuleCount = ruleCount,
            ActiveVersion = activeVersion,
            ErrorCode = SPLC_ErrorCode.NONE
        };

    public static DeployRulesResult DomainValidationError(IEnumerable<ValidationError> errors) =>
        new()
        {
            IsSuccess = false,
            ValidationErrors = errors.ToList(),
            ErrorMessage = "Domain validation failed. Rules were not deployed."
        };

    public static DeployRulesResult HardwareError(SPLC_ErrorCode errorCode, string message, ushort activeVersion = 0) =>
        new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ActiveVersion = activeVersion,
            ErrorMessage = message
        };
}
