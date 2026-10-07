using SimplePLC.Infrastructure.Abstractions;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Bộ giả lập IUsbCdcTransport chạy in-memory cho kiểm thử đơn vị và môi trường giả lập (SIMULATOR).
/// Cho phép lập trình sẵn chuỗi phản hồi (pre-canned responses),
/// mô phỏng việc xé gói 64 bytes của TinyUSB CDC, và mô phỏng lỗi Timeout / ngắt kết nối.
/// </summary>
public sealed class FakeUsbCdcTransport : IUsbCdcTransport
{
    private readonly Queue<byte> _incomingBytes = new();
    private readonly List<byte[]> _sentRequests = new();

    public bool IsOpen { get; set; }
    public UsbCdcOptions? LastOpenedOptions { get; private set; }
    public IReadOnlyList<byte[]> SentRequests => _sentRequests;

    public bool SimulateTimeout { get; set; }
    public bool SimulateDisconnectOnWrite { get; set; }
    public bool SimulateDisconnectOnRead { get; set; }
    public int? SimulateChunkSize { get; set; } // Ví dụ: 64 để mô phỏng TinyUSB CDC packet size
    public int ReadDelayMs { get; set; }
    public Func<ReadOnlyMemory<byte>, Task>? OnWriteAsync { get; set; }

    public Task OpenAsync(UsbCdcOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastOpenedOptions = options ?? throw new ArgumentNullException(nameof(options));
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsOpen = false;
        return Task.CompletedTask;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen)
            throw new InvalidOperationException("Transport is not open.");

        if (SimulateDisconnectOnWrite)
        {
            IsOpen = false;
            throw new IOException("Simulated USB CDC cable disconnect during write.");
        }

        _sentRequests.Add(data.ToArray());

        if (OnWriteAsync != null)
        {
            await OnWriteAsync(data).ConfigureAwait(false);
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen)
            throw new InvalidOperationException("Transport is not open.");

        if (SimulateDisconnectOnRead)
        {
            IsOpen = false;
            throw new IOException("Simulated USB CDC cable disconnect during read.");
        }

        if (SimulateTimeout)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            return 0; // Gây ra timeout
        }

        if (ReadDelayMs > 0)
        {
            await Task.Delay(ReadDelayMs, cancellationToken).ConfigureAwait(false);
        }

        if (_incomingBytes.Count == 0)
        {
            return 0;
        }

        int maxToRead = buffer.Length;
        if (SimulateChunkSize.HasValue && SimulateChunkSize.Value > 0)
        {
            maxToRead = Math.Min(maxToRead, SimulateChunkSize.Value);
        }

        int toCopy = Math.Min(maxToRead, _incomingBytes.Count);
        for (int i = 0; i < toCopy; i++)
        {
            buffer.Span[i] = _incomingBytes.Dequeue();
        }

        return toCopy;
    }

    public bool ClearOnDiscard { get; set; } = false;

    public void DiscardBuffers()
    {
        if (ClearOnDiscard)
        {
            _incomingBytes.Clear();
        }
    }

    public void EnqueueResponse(ReadOnlySpan<byte> response)
    {
        foreach (byte b in response)
        {
            _incomingBytes.Enqueue(b);
        }
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        _incomingBytes.Clear();
        return ValueTask.CompletedTask;
    }
}
