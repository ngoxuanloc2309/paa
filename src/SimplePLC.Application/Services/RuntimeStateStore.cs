namespace SimplePLC.Application.Services;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Cài đặt thread-safe của IRuntimeStateStore.
/// Quản lý dữ liệu trạng thái bộ nhớ thời gian thực, phục vụ Live Watch Table và Diagnostics.
/// Hỗ trợ TimeProvider của .NET 8 để kiểm thử stale timing hoàn toàn deterministic.
/// </summary>
public sealed class RuntimeStateStore : IRuntimeStateStore
{
    private readonly object _syncLock = new();
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<ushort, RuntimeTagSnapshot> _tags = new();
    private DeviceEndpoint? _endpoint;
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private DeviceHealthInfo? _health;
    private DateTimeOffset _lastSnapshotTime;
    private DateTimeOffset? _lastSuccessfulPollAt;
    private IReadOnlyList<FbTimerRecordDto>? _timers;
    private IReadOnlyList<FbCounterRecordDto>? _counters;

    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromMilliseconds(600); // 3 chu kỳ @ 200ms

    /// <summary>
    /// Khi true, việc đánh giá Stale sẽ tạm dừng (áp dụng khi IDeviceOperationCoordinator đang chạy tác vụ độc quyền).
    /// </summary>
    public bool PollingSuppressed { get; set; }

    public event Action<RuntimeDeviceSnapshot>? SnapshotUpdated;

    public RuntimeStateStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastSnapshotTime = _timeProvider.GetUtcNow();
    }

    public RuntimeDeviceSnapshot CurrentSnapshot
    {
        get
        {
            lock (_syncLock)
            {
                return BuildSnapshotLocked();
            }
        }
    }

    public IReadOnlyList<RuntimeTagSnapshot> GetAllTags()
    {
        lock (_syncLock)
        {
            return _tags.Values.OrderBy(t => t.TagIndex).ToList();
        }
    }

    public RuntimeTagSnapshot? GetTag(ushort tagIndex)
    {
        lock (_syncLock)
        {
            return _tags.TryGetValue(tagIndex, out var tag) ? tag : null;
        }
    }

    public void InitializeProduct(ProductDefinition product)
    {
        ArgumentNullException.ThrowIfNull(product);

        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            _tags.Clear();
            var now = _timeProvider.GetUtcNow();

            foreach (var def in product.Tags)
            {
                var snapshotTag = new RuntimeTagSnapshot(
                    TagIndex: def.TagIndex,
                    Name: def.Name,
                    Alias: def.Description,
                    Kind: def.Kind,
                    DataType: def.DataType,
                    RawValue: 0,
                    Quality: TagQuality.Unknown,
                    LastSuccessfulUpdateAt: now
                );

                _tags[def.TagIndex] = snapshotTag;
            }

            _lastSnapshotTime = now;
            snapshot = BuildSnapshotLocked();
        }

        // BẮT BUỘC: Phát sự kiện ngoài store lock để tránh deadlock khi subscriber gọi lại GetTag()
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void UpdateTags(
        IReadOnlyList<RuntimeTagValue> tagValues,
        ProductDefinition product)
    {
        ArgumentNullException.ThrowIfNull(tagValues);
        ArgumentNullException.ThrowIfNull(product);

        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            var now = _timeProvider.GetUtcNow();
            foreach (var tv in tagValues)
            {
                var def = product.FindTagByIndex(tv.TagIndex);
                string name = def?.Name ?? tv.TagName;
                string? alias = def?.Description;
                TagKind kind = def?.Kind ?? TagKind.None;
                TagDataType dataType = def?.DataType ?? TagDataType.Int32;

                var newSnapshot = new RuntimeTagSnapshot(
                    TagIndex: tv.TagIndex,
                    Name: name,
                    Alias: alias,
                    Kind: kind,
                    DataType: dataType,
                    RawValue: tv.Value,
                    Quality: TagQuality.Good,
                    LastSuccessfulUpdateAt: now
                );

                _tags[tv.TagIndex] = newSnapshot;
            }

            _lastSuccessfulPollAt = now;
            _lastSnapshotTime = now;
            snapshot = BuildSnapshotLocked();
        }

        // Phát sự kiện ngoài store lock
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void UpdateHealth(DeviceHealthInfo health)
    {
        ArgumentNullException.ThrowIfNull(health);

        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            _health = health;
            _lastSnapshotTime = _timeProvider.GetUtcNow();
            snapshot = BuildSnapshotLocked();
        }

        // Phát sự kiện ngoài store lock
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void UpdateConnection(DeviceEndpoint? endpoint, ConnectionStatus status)
    {
        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            _endpoint = endpoint;
            _connectionStatus = status;
            var now = _timeProvider.GetUtcNow();

            if (status != ConnectionStatus.Connected)
            {
                // Khi mất kết nối hoặc ngắt phiên, toàn bộ dữ liệu chuyển sang Unknown
                // nhưng BẢO TOÀN RawValue và LastSuccessfulUpdateAt phục vụ chẩn đoán
                foreach (var kvp in _tags.ToList())
                {
                    if (kvp.Value.Quality != TagQuality.Unknown)
                    {
                        var degraded = kvp.Value with
                        {
                            Quality = TagQuality.Unknown
                        };
                        _tags[kvp.Key] = degraded;
                    }
                }
            }

            _lastSnapshotTime = now;
            snapshot = BuildSnapshotLocked();
        }

        // Phát sự kiện ngoài store lock
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void EvaluateStale(DateTimeOffset? evaluationTime = null)
    {
        // Nếu polling đang bị tạm dừng do tác vụ độc quyền, tuyệt đối KHÔNG chuyển stale
        if (PollingSuppressed)
        {
            return;
        }

        RuntimeDeviceSnapshot? snapshot = null;
        lock (_syncLock)
        {
            var now = evaluationTime ?? _timeProvider.GetUtcNow();
            bool changed = false;

            foreach (var kvp in _tags.ToList())
            {
                if (kvp.Value.Quality == TagQuality.Good && (now - kvp.Value.LastSuccessfulUpdateAt) >= StaleThreshold)
                {
                    // Chuyển sang Stale: Giữ nguyên RawValue và LastSuccessfulUpdateAt
                    var stale = kvp.Value with
                    {
                        Quality = TagQuality.Stale
                    };
                    _tags[kvp.Key] = stale;
                    changed = true;
                }
            }

            if (changed)
            {
                _lastSnapshotTime = now;
                snapshot = BuildSnapshotLocked();
            }
        }

        if (snapshot != null)
        {
            // Phát sự kiện ngoài store lock
            SnapshotUpdated?.Invoke(snapshot);
        }
    }

    public void Reset()
    {
        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            var now = _timeProvider.GetUtcNow();
            foreach (var kvp in _tags.ToList())
            {
                var resetTag = kvp.Value with
                {
                    RawValue = 0,
                    Quality = TagQuality.Unknown,
                    LastSuccessfulUpdateAt = now
                };
                _tags[kvp.Key] = resetTag;
            }

            _health = null;
            _connectionStatus = ConnectionStatus.Disconnected;
            _lastSuccessfulPollAt = null;
            _lastSnapshotTime = now;
            _timers = null;
            _counters = null;
            snapshot = BuildSnapshotLocked();
        }

        // Phát sự kiện ngoài store lock
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void UpdateFunctionBlocks(
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters)
    {
        RuntimeDeviceSnapshot snapshot;
        lock (_syncLock)
        {
            _timers = timers;
            _counters = counters;
            _lastSnapshotTime = _timeProvider.GetUtcNow();
            snapshot = BuildSnapshotLocked();
        }

        SnapshotUpdated?.Invoke(snapshot);
    }

    private RuntimeDeviceSnapshot BuildSnapshotLocked()
    {
        var tagList = _tags.Values.OrderBy(t => t.TagIndex).ToList();
        return new RuntimeDeviceSnapshot(
            Endpoint: _endpoint,
            ConnectionStatus: _connectionStatus,
            Health: _health,
            Tags: tagList,
            Timestamp: _lastSnapshotTime,
            LastSuccessfulPollAt: _lastSuccessfulPollAt,
            Timers: _timers,
            Counters: _counters
        );
    }
}
