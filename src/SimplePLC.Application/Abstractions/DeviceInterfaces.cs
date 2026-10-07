using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Kết quả triển khai nạp bảng quy tắc (Deploy Rules) theo quy chuẩn R10.
/// </summary>
public sealed record RuleDeployResult(
    bool IsSuccess,
    ushort ConfigStatus,
    SPLC_ErrorCode ErrorCode,
    ushort ActiveVersion = 0,
    string ErrorMessage = "");

/// <summary>
/// Đọc nhận dạng thiết bị (Device Descriptor) tại địa chỉ 0x0000 và thông tin tài nguyên (Device Resource Info) tại 0x0020.
/// </summary>
public interface IDeviceDescriptorReader
{
    Task<DeviceDescriptorDto> ReadDescriptorAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task<DeviceResourceInfoDto> ReadResourceInfoAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đọc bảng Active Rule Table từ thiết bị tại địa chỉ 0x0100.
/// </summary>
public interface IRuleTableReader
{
    Task<IReadOnlyList<RuleRecordDto>> ReadActiveRulesAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
}

/// <summary>
/// Nạp bảng quy tắc xuống thiết bị tuân thủ quy trình 4 bước qua Staging (R10).
/// </summary>
public interface IRuleTableWriter
{
    Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        CancellationToken cancellationToken = default);

    Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, cancellationToken);
}

/// <summary>
/// Cổng giao tiếp toàn diện cho bảng quy tắc (đọc active table và nạp staging).
/// </summary>
public interface IRuleTableGateway : IRuleTableReader, IRuleTableWriter
{
}

/// <summary>
/// Giao tiếp với phân vùng Function Block chuyên dụng (0x0B00..0x0B7F).
/// </summary>
public interface IFunctionBlockGateway
{
    Task WriteFunctionBlocksAsync(
        byte slaveId,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<FbTimerRecordDto> Timers, IReadOnlyList<FbCounterRecordDto> Counters)> ReadFunctionBlocksAsync(
        byte slaveId = 1,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Đọc tình trạng sức khỏe thiết bị (Device Health) tại địa chỉ 0x0800.
/// </summary>
public interface IDeviceHealthReader
{
    Task<DeviceHealthDto> ReadHealthAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đọc và đồng bộ thời gian thực (RTC Clock) với MCU tại địa chỉ 0x0810 (FC03/FC16).
/// </summary>
public interface IRtcClockClient
{
    Task<RtcClockDto> ReadRtcClockAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task WriteRtcClockAsync(RtcClockDto rtc, byte slaveId = 1, CancellationToken cancellationToken = default);
    Task<RtcClockDto> SyncToNowAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task<RtcClockDto> SyncSmartAsync(byte slaveId = 1, uint maxDriftSeconds = 2, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đọc mảng giá trị Tag runtime sống tại địa chỉ 0x0900.
/// </summary>
public interface IRuntimeTagReader
{
    Task<int[]> ReadRuntimeTagValuesAsync(
        byte slaveId = 1,
        ushort count = 124,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gửi lệnh bảo trì hệ thống (System Commands) xuống thiết bị tại địa chỉ 0x0A00.
/// </summary>
public interface ISystemCommandClient
{
    Task<SystemCommandResultDto> ExecuteCommandAsync(
        byte slaveId,
        SPLC_SystemCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Giao tiếp với phân vùng Diagnostic Control & Manual Commissioning (Wire Profile V2, 0x0A20..0x0A24 & 0x0900).
/// </summary>
public interface IDiagnosticGateway
{
    Task<DiagnosticStatusDto> ReadDiagnosticStatusAsync(
        byte slaveId = 1,
        CancellationToken cancellationToken = default);

    Task<DiagnosticStatusDto> SendDiagnosticCommandAsync(
        byte slaveId,
        SPLC_DiagCommand command,
        CancellationToken cancellationToken = default);

    Task WriteTagValueAsync(
        byte slaveId,
        ushort tagIndex,
        int rawValue,
        CancellationToken cancellationToken = default);

    Task WriteTagsBatchAsync(
        byte slaveId,
        ushort startTagIndex,
        IReadOnlyList<int> rawValues,
        CancellationToken cancellationToken = default);
}

