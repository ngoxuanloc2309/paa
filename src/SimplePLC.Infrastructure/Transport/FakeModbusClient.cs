using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Simulator;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Adapter kết nối IModbusClient tới McuReferenceSimulator.
/// Cung cấp môi trường kiểm thử ảo hóa hoàn chỉnh với khả năng cấy lỗi và mô phỏng chu kỳ STM32.
/// </summary>
public sealed class FakeModbusClient : IModbusClient
{
    private readonly McuReferenceSimulator _simulator;
    private bool _isConnected;
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;

    public McuReferenceSimulator Simulator => _simulator;

    public int ScanIntervalMs { get; set; } = 20;
    public bool EnableBackgroundScan { get; set; } = true;

    public bool IsConnected
    {
        get => _isConnected && _simulator.IsOnline;
        set => _isConnected = value;
    }

    // Các shortcut cấy lỗi tương thích ngược cho các bộ test hiện có
    public bool SimulateTimeout
    {
        get => _simulator.Control.Faults.TimeoutOnAddress.HasValue;
        set
        {
            if (value) _simulator.Control.Faults.TimeoutOnAddress = 0;
            else _simulator.Control.Faults.TimeoutOnAddress = null;
        }
    }

    public bool SimulateDisconnect
    {
        get => _simulator.Control.Faults.CableDisconnected;
        set => _simulator.Control.Faults.CableDisconnected = value;
    }

    public bool CorruptStagingCrcOnCommit
    {
        get => _simulator.Control.Faults.CorruptCommitCrc;
        set => _simulator.Control.Faults.CorruptCommitCrc = value;
    }

    public int LatencyMs { get; set; }

    public FakeModbusClient(bool isConnected = false)
    {
        _simulator = new McuReferenceSimulator();
        _isConnected = isConnected;
        if (_isConnected && EnableBackgroundScan)
        {
            StartScanLoop();
        }
    }

    public FakeModbusClient(McuReferenceSimulator simulator, bool isConnected = false)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _isConnected = isConnected;
        if (_isConnected && EnableBackgroundScan)
        {
            StartScanLoop();
        }
    }

    private void StartScanLoop()
    {
        if (!EnableBackgroundScan || _scanTask != null) return;
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        int interval = Math.Max(10, ScanIntervalMs);
        _scanTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
            try
            {
                while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    if (_isConnected && _simulator.IsOnline)
                    {
                        _simulator.ExecuteScanPass((uint)interval);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void StopScanLoop()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        _scanTask = null;
    }

    public Task ConnectAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_simulator.Control.Faults.CableDisconnected)
            throw new IOException("Simulated serial connection failure.");

        _isConnected = true;
        if (EnableBackgroundScan)
        {
            StartScanLoop();
        }
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = false;
        StopScanLoop();
        return Task.CompletedTask;
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (LatencyMs > 0)
            await Task.Delay(LatencyMs, cancellationToken);

        return await _simulator.ReadHoldingRegistersAsync(slaveId, startAddress, count, cancellationToken);
    }

    public async Task WriteMultipleRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (LatencyMs > 0)
            await Task.Delay(LatencyMs, cancellationToken);

        await _simulator.WriteMultipleRegistersAsync(slaveId, startAddress, values, cancellationToken);
    }

    public async Task WriteSingleRegisterAsync(
        byte slaveId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (LatencyMs > 0)
            await Task.Delay(LatencyMs, cancellationToken);

        await _simulator.WriteSingleRegisterAsync(slaveId, address, value, cancellationToken);
    }

    private void EnsureConnected()
    {
        if (!_isConnected || !_simulator.IsOnline)
            throw new InvalidOperationException("Modbus client is not connected.");
    }

    public ValueTask DisposeAsync()
    {
        _isConnected = false;
        StopScanLoop();
        return ValueTask.CompletedTask;
    }
}
