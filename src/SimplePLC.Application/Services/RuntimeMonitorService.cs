namespace SimplePLC.Application.Services;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Models;

public sealed class RuntimeMonitorService : IAsyncDisposable, IDisposable
{
    private IRuntimeTagReader _tagReader;
    private IDeviceHealthReader _healthReader;
    private IFunctionBlockGateway? _fbGateway;
    private readonly IDeviceOperationCoordinator? _coordinator;
    private readonly RuntimeStateStore? _stateStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private readonly object _syncLock = new();
    private bool _isDisposed;

    public event Action<IReadOnlyList<RuntimeTagValue>>? TagsUpdated;
    public event Action<DeviceHealthInfo>? HealthUpdated;
    public event Action<Exception>? PollingError;

    /// <summary>
    /// Callback được kích hoạt khi phát hiện lỗi transport dứt khoát (IOException, cổng bị đóng/rút).
    /// </summary>
    public Func<Exception, Task>? OnTransportLost { get; set; }

    public IRuntimeStateStore? StateStore => _stateStore;

    public bool IsRunning
    {
        get
        {
            lock (_syncLock)
            {
                return _cts is not null && !_cts.IsCancellationRequested && _pollTask is not null && !_pollTask.IsCompleted;
            }
        }
    }

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(200);
    public int HealthCheckDivisor { get; set; } = 5;

    public RuntimeMonitorService(
        IRuntimeTagReader tagReader,
        IDeviceHealthReader healthReader,
        IDeviceOperationCoordinator? coordinator = null,
        RuntimeStateStore? stateStore = null,
        IFunctionBlockGateway? fbGateway = null)
    {
        _tagReader = tagReader ?? throw new ArgumentNullException(nameof(tagReader));
        _healthReader = healthReader ?? throw new ArgumentNullException(nameof(healthReader));
        _coordinator = coordinator;
        _stateStore = stateStore;
        _fbGateway = fbGateway;
    }

    /// <summary>
    /// Gắn một DeviceSession mới vào monitor, đảm bảo dừng triệt để vòng lặp cũ trước khi khởi chạy vòng lặp mới.
    /// </summary>
    public async Task AttachSessionAsync(
        IDeviceSession session,
        ProductDefinition product,
        byte slaveId = 1,
        ushort? tagCount = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(product);

        ushort effectiveTagCount = ResolveEffectiveTagCount(product, tagCount);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);

            _tagReader = session.RuntimeTags;
            _healthReader = session.Health;
            _fbGateway = session.FunctionBlocks;

            lock (_syncLock)
            {
                _stateStore?.InitializeProduct(product);
                _stateStore?.UpdateConnection(session.Endpoint, ConnectionStatus.Connected);

                _cts = new CancellationTokenSource();
                _pollTask = RunPollingLoopAsync(product, slaveId, effectiveTagCount, _cts.Token);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Ngắt session khỏi monitor, dừng triệt để vòng lặp polling và chuyển state store về Disconnected.
    /// </summary>
    public async Task DetachSessionAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
            _stateStore?.UpdateConnection(null, ConnectionStatus.Disconnected);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartAsync(
        ProductDefinition product,
        byte slaveId = 1,
        ushort? tagCount = null)
    {
        ArgumentNullException.ThrowIfNull(product);

        ushort effectiveTagCount = ResolveEffectiveTagCount(product, tagCount);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Bất biến: Dừng và đợi vòng lặp cũ kết thúc hoàn toàn trước khi bắt đầu vòng lặp mới
            await StopInternalAsync().ConfigureAwait(false);

            lock (_syncLock)
            {
                _stateStore?.InitializeProduct(product);
                _stateStore?.UpdateConnection(null, ConnectionStatus.Connected);

                _cts = new CancellationTokenSource();
                _pollTask = RunPollingLoopAsync(product, slaveId, effectiveTagCount, _cts.Token);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Start(
        ProductDefinition product,
        byte slaveId = 1,
        ushort? tagCount = null)
    {
        StartAsync(product, slaveId, tagCount).GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopInternalAsync()
    {
        CancellationTokenSource? cts;
        Task? pollTask;

        lock (_syncLock)
        {
            cts = _cts;
            pollTask = _pollTask;
            _cts = null;
            _pollTask = null;
        }

        if (cts is not null)
        {
            cts.Cancel();
            if (pollTask is not null)
            {
                try
                {
                    await pollTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
                catch
                {
                    // Bỏ qua lỗi polling khi đang stop
                }
            }
            cts.Dispose();
            _stateStore?.UpdateConnection(null, ConnectionStatus.Disconnected);
        }
    }

    private async Task RunPollingLoopAsync(
        ProductDefinition product,
        byte slaveId,
        ushort tagCount,
        CancellationToken ct)
    {
        int tickCounter = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                IAsyncDisposable? lease = null;
                if (_coordinator != null)
                {
                    lease = await _coordinator.TryAcquirePollingLeaseAsync(ct).ConfigureAwait(false);
                    if (lease == null)
                    {
                        // Tác vụ độc quyền đang chạy -> Tạm dừng tính Stale để không làm nhấp nháy UI
                        if (_stateStore != null)
                        {
                            _stateStore.PollingSuppressed = true;
                        }
                        goto DelayNext;
                    }
                }

                if (_stateStore != null)
                {
                    _stateStore.PollingSuppressed = false;
                }

                await using (lease)
                {
                    // 1. Đọc Runtime Tags (R6)
                    var rawValues = await _tagReader.ReadRuntimeTagValuesAsync(slaveId, tagCount, ct).ConfigureAwait(false);
                    var timestamp = DateTime.UtcNow;

                    List<RuntimeTagValue> tagValues;
                    if (product.Tags.Count > 0)
                    {
                        tagValues = new List<RuntimeTagValue>(product.Tags.Count);
                        foreach (var def in product.Tags)
                        {
                            int val = (def.TagIndex < rawValues.Length) ? rawValues[def.TagIndex] : 0;
                            tagValues.Add(new RuntimeTagValue
                            {
                                TagIndex = def.TagIndex,
                                TagName = def.Name,
                                Value = val,
                                Quality = 0,
                                Timestamp = timestamp
                            });
                        }
                    }
                    else
                    {
                        tagValues = new List<RuntimeTagValue>(rawValues.Length);
                        for (ushort i = 0; i < rawValues.Length; i++)
                        {
                            ushort tagIdx = i;
                            tagValues.Add(new RuntimeTagValue
                            {
                                TagIndex = tagIdx,
                                TagName = $"TAG_{tagIdx}",
                                Value = rawValues[i],
                                Quality = 0,
                                Timestamp = timestamp
                            });
                        }
                    }

                    TagsUpdated?.Invoke(tagValues);
                    _stateStore?.UpdateTags(tagValues, product);

                    // 1b. Đọc Function Block subsystem nếu thiết bị hỗ trợ Wire Profile V2
                    if (product.SupportsDedicatedFunctionBlocks && _fbGateway != null)
                    {
                        try
                        {
                            var (fbTimers, fbCounters) = await _fbGateway.ReadFunctionBlocksAsync(slaveId, ct).ConfigureAwait(false);
                            _stateStore?.UpdateFunctionBlocks(fbTimers, fbCounters);
                        }
                        catch
                        {
                            // Bỏ qua lỗi đọc FB tạm thời nếu thiết bị đang bận hoặc scan boundary
                        }
                    }

                    // 2. Định kỳ đọc Device Health
                    tickCounter++;
                    if (tickCounter >= HealthCheckDivisor)
                    {
                        tickCounter = 0;
                        var healthDto = await _healthReader.ReadHealthAsync(slaveId, ct).ConfigureAwait(false);
                        var healthInfo = DeviceHealthMapper.ToInfo(healthDto);
                        HealthUpdated?.Invoke(healthInfo);
                        _stateStore?.UpdateHealth(healthInfo);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PollingError?.Invoke(ex);

                // Phân loại lỗi: Chỉ lỗi transport dứt khoát mới kích hoạt recovery
                if (IsDefinitiveTransportError(ex))
                {
                    _stateStore?.UpdateConnection(null, ConnectionStatus.Disconnected);

                    if (OnTransportLost != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await OnTransportLost(ex).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Bỏ qua lỗi trong callback recovery
                            }
                        });
                    }

                    // Dừng vòng lặp polling của session đã mất kết nối vật lý này
                    break;
                }
                // Nếu là lỗi tạm thời (timeout đơn lẻ, CRC frame sai): Không trigger reconnect,
                // để cơ chế Stale decay (Phase B) đánh giá.
            }

            DelayNext:
            _stateStore?.EvaluateStale();

            try
            {
                await Task.Delay(PollingInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static ushort ResolveEffectiveTagCount(ProductDefinition product, ushort? tagCount)
    {
        if (tagCount.HasValue && tagCount.Value > 0)
        {
            return tagCount.Value;
        }

        if (product.Tags.Count == 0)
        {
            return 0;
        }

        // Với fixed allocation map: TagIndex có thể rời rạc (sparse), ví dụ VFLAG7 có index = 27.
        // Cần đọc dải slot từ 0 đến max(TagIndex), do đó số lượng slot cần đọc là max(TagIndex) + 1.
        int maxIndex = product.Tags.Max(t => t.TagIndex);
        return (ushort)Math.Max(maxIndex + 1, product.Tags.Count);
    }

    /// <summary>
    /// Nhận diện lỗi transport dứt khoát (rút cáp, cổng COM đóng, stream hỏng) phân biệt với timeout Modbus tạm thời.
    /// </summary>
    public static bool IsDefinitiveTransportError(Exception ex)
    {
        return ex is System.IO.IOException
            || ex is UnauthorizedAccessException
            || ex is ObjectDisposedException
            || (ex is InvalidOperationException ioe && ioe.Message.Contains("not open", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        lock (_syncLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
