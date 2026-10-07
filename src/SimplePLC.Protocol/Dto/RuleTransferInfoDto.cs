namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn thông tin transfer Rule Table xuống MCU (tương ứng RULE_COUNT_STAGED 0x9002 và EXPECTED_CRC16 0x9003).
/// </summary>
public sealed class RuleTransferInfoDto
{
    public ushort RuleCount { get; set; }
    public ushort ExpectedCrc16 { get; set; }

    public RuleTransferInfoDto() { }

    public RuleTransferInfoDto(ushort ruleCount, ushort expectedCrc16)
    {
        RuleCount = ruleCount;
        ExpectedCrc16 = expectedCrc16;
    }
}
