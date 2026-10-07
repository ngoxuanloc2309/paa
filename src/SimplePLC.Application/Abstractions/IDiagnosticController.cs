using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Quản lý cấp cao cho phiên Manual Commissioning & Force I/O (Wire Profile V2).
/// Điều phối nhịp tim (Heartbeat), thời gian thuê (Lease), đồng bộ hóa với coordinator và theo dõi các tag đang bị ép.
/// </summary>
public interface IDiagnosticController : IAsyncDisposable, IDisposable
{
    bool IsManualModeActive { get; }
    DiagnosticStatusDto? CurrentStatus { get; }
    ushort LeaseRemainingMs { get; }
    bool IsRetainDirty { get; }
    IReadOnlyDictionary<ushort, int> ForcedTags { get; }

    event Action<DiagnosticStatusDto>? StatusChanged;
    event Action<ushort, int>? TagForced;
    event Action<ushort>? TagReleased;
    event Action? AllTagsReleased;
    event Action<Exception>? ErrorOccurred;

    Task<bool> EnterManualModeAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task<bool> ExitManualModeAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task ForceTagAsync(ushort tagIndex, int rawValue, byte slaveId = 1, CancellationToken cancellationToken = default);
    Task ReleaseTagAsync(ushort tagIndex, byte slaveId = 1, CancellationToken cancellationToken = default);
    Task ReleaseAllAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task CommitRetainAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
    Task DiscardRetainAsync(byte slaveId = 1, CancellationToken cancellationToken = default);
}
