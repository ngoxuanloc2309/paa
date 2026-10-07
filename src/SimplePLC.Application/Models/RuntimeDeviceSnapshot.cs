using SimplePLC.Protocol.Dto;

namespace SimplePLC.Application.Models;

/// <summary>
/// Ảnh chụp tổng thể trạng thái thiết bị thời gian thực:
/// Tách biệt rõ ràng phần cứng chẩn đoán (Health), trạng thái kết nối (Connection),
/// toàn bộ bảng Tag thời gian thực, và phân vùng Function Block (Timer/Counter).
/// </summary>
public sealed record RuntimeDeviceSnapshot(
    DeviceEndpoint? Endpoint,
    ConnectionStatus ConnectionStatus,
    DeviceHealthInfo? Health,
    IReadOnlyList<RuntimeTagSnapshot> Tags,
    DateTimeOffset Timestamp,
    DateTimeOffset? LastSuccessfulPollAt = null,
    IReadOnlyList<FbTimerRecordDto>? Timers = null,
    IReadOnlyList<FbCounterRecordDto>? Counters = null
);
