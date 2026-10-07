using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn System Command gửi xuống MCU (0x0A00).
/// </summary>
public sealed class SystemCommandRequestDto
{
    public SPLC_SystemCommand Command { get; set; }

    public SystemCommandRequestDto() { }

    public SystemCommandRequestDto(SPLC_SystemCommand command)
    {
        Command = command;
    }
}

/// <summary>
/// DTO biểu diễn kết quả System Command MCU phản hồi (0x0A01 - 0x0A02).
/// </summary>
public sealed class SystemCommandResultDto
{
    public SPLC_CommandStatus Status { get; set; }
    public SPLC_ErrorCode ErrorCode { get; set; }

    public bool IsSuccess => Status == SPLC_CommandStatus.DONE && ErrorCode == SPLC_ErrorCode.NONE;
}
