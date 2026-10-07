using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Simulator;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Tầng truyền dẫn USB CDC ảo hóa trực tiếp tới McuModbusRtuServer.
/// Mô phỏng đầy đủ hành vi luồng byte nối tiếp (Virtual COM),
/// cơ chế xé gói 64 bytes của USB FS (TinyUSB CDC endpoint),
/// độ trễ baud rate thực tế, và ngắt cáp/timeout.
/// </summary>
public sealed class VirtualComMcuTransport : IUsbCdcTransport
{
    private readonly McuModbusRtuServer _server;
    private readonly Queue<byte> _rxQueue = new();
    private readonly object _rxLock = new();
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private bool _isOpen;
    private bool _isDisposed;

    public bool IsOpen => _isOpen && !_isDisposed;
    public McuModbusRtuServer Server => _server;
    public McuReferenceSimulator Simulator => _server.Simulator;

    /// <summary>
    /// Kích thước gói tối đa khi mô phỏng USB CDC FS (mặc định 64 bytes).
    /// Nếu null: không chia gói.
    /// </summary>
    public int? MaxPacketSize { get; set; } = 64;

    /// <summary>
    /// Độ trễ mô phỏng (ms) khi đọc phản hồi từ MCU.
    /// </summary>
    public int ResponseDelayMs { get; set; } = 0;

    /// <summary>
    /// Cờ mô phỏng đứt cáp truyền thông.
    /// </summary>
    public bool SimulateCableDisconnect { get; set; } = false;

    public int TotalBytesSent { get; private set; }
    public int TotalBytesReceived { get; private set; }

    public VirtualComMcuTransport(McuModbusRtuServer? server = null)
    {
        _server = server ?? new McuModbusRtuServer();
    }

    public Task OpenAsync(UsbCdcOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(VirtualComMcuTransport));

        if (SimulateCableDisconnect)
            throw new IOException($"Cannot open COM port {options.PortName}: Device disconnected or offline.");

        _isOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _isOpen = false;
        lock (_rxLock)
        {
            _rxQueue.Clear();
        }
        return Task.CompletedTask;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        if (SimulateCableDisconnect)
        {
            _isOpen = false;
            throw new IOException("Simulated cable disconnected during Write.");
        }

        TotalBytesSent += data.Length;

        // Chuyển mảng byte request xuống McuModbusRtuServer để xử lý
        byte[] responseBytes = await _server.ProcessFrameAsync(data, cancellationToken).ConfigureAwait(false);

        if (responseBytes.Length > 0)
        {
            TotalBytesReceived += responseBytes.Length;
            lock (_rxLock)
            {
                foreach (byte b in responseBytes)
                {
                    _rxQueue.Enqueue(b);
                }
            }
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        if (SimulateCableDisconnect)
        {
            _isOpen = false;
            throw new IOException("Simulated cable disconnected during Read.");
        }

        if (ResponseDelayMs > 0)
        {
            await Task.Delay(ResponseDelayMs, cancellationToken).ConfigureAwait(false);
        }

        lock (_rxLock)
        {
            if (_rxQueue.Count == 0)
            {
                return 0;
            }

            int countToRead = buffer.Length;
            if (MaxPacketSize.HasValue && MaxPacketSize.Value > 0)
            {
                countToRead = Math.Min(countToRead, MaxPacketSize.Value);
            }

            int actualRead = Math.Min(countToRead, _rxQueue.Count);
            for (int i = 0; i < actualRead; i++)
            {
                buffer.Span[i] = _rxQueue.Dequeue();
            }

            return actualRead;
        }
    }

    public void DiscardBuffers()
    {
        lock (_rxLock)
        {
            _rxQueue.Clear();
        }
    }

    private void EnsureOpen()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(VirtualComMcuTransport));
        if (!_isOpen)
            throw new InvalidOperationException("Virtual COM transport is not open.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        await CloseAsync().ConfigureAwait(false);
        _ioLock.Dispose();
    }
}
