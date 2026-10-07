using System.IO.Ports;
using SimplePLC.Infrastructure.Abstractions;

namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Hiện thực IUsbCdcTransport bọc System.IO.Ports.SerialPort của .NET 8.
/// Quản lý cổng Virtual COM do STM32 Native USB CDC (TinyUSB CDC) tạo ra.
/// Áp dụng cơ chế bất đồng bộ không chặn (Non-blocking polling with Task.Delay)
/// để triệt tiêu hoàn toàn lỗi treo vô hạn (hang/deadlock) của SerialStream.ReadAsync trên Windows.
/// </summary>
public sealed class UsbCdcTransport : IUsbCdcTransport
{
    private SerialPort? _serialPort;
    private Stream? _stream;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private bool _isDisposed;

    private UsbCdcOptions? _options;

    public bool IsOpen => !_isDisposed && _serialPort != null && _serialPort.IsOpen;

    public async Task OpenAsync(UsbCdcOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                return;
            }

            _options = options;
            var port = new SerialPort(options.PortName)
            {
                // Các giá trị cố định thỏa mãn API SerialPort trên Windows (dummy cho native USB)
                BaudRate = 115200,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = (int)options.ReadTimeout.TotalMilliseconds,
                WriteTimeout = (int)options.WriteTimeout.TotalMilliseconds
            };

            // Mở cổng trên ThreadPool kèm timeout 2s để tránh treo luồng nếu driver COM (như Bluetooth) bị kẹt
            var openTimeout = TimeSpan.FromSeconds(2);
            var openTask = Task.Run(() =>
            {
                port.Open();
                try { port.DtrEnable = true; } catch { }
                try { port.RtsEnable = true; } catch { }
                try { port.DiscardInBuffer(); } catch { }
                try { port.DiscardOutBuffer(); } catch { }
            }, cancellationToken);

            var completed = await Task.WhenAny(openTask, Task.Delay(openTimeout, cancellationToken)).ConfigureAwait(false);
            if (completed != openTask)
            {
                try { port.Dispose(); } catch { }
                throw new TimeoutException($"Opening COM port '{options.PortName}' timed out after {openTimeout.TotalSeconds}s.");
            }

            await openTask.ConfigureAwait(false);

            _serialPort = port;
            _stream = port.BaseStream;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CloseInternal();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("USB CDC transport is not open.");

            byte[] array = data.ToArray();
            var writeTimeout = _options?.WriteTimeout ?? TimeSpan.FromMilliseconds(1000);
            var writeTask = Task.Run(() =>
            {
                _serialPort.Write(array, 0, array.Length);
            }, cancellationToken);

            var completed = await Task.WhenAny(writeTask, Task.Delay(writeTimeout, cancellationToken)).ConfigureAwait(false);
            if (completed != writeTask)
            {
                throw new TimeoutException($"Write timed out after {writeTimeout.TotalMilliseconds}ms.");
            }

            await writeTask.ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        if (buffer.Length == 0)
            return 0;

        var timeout = _options?.ReadTimeout ?? TimeSpan.FromMilliseconds(1000);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var ct = timeoutCts.Token;

        try
        {
            // Polling với Task.Delay (5ms): Khắc phục triệt để lỗi SerialStream.ReadAsync trên Windows
            // khi CancellationToken và ReadTimeout bị driver serial ngó lơ gây treo ứng dụng vĩnh viễn.
            while (!_isDisposed && _serialPort != null && _serialPort.IsOpen)
            {
                ct.ThrowIfCancellationRequested();

                int available = _serialPort.BytesToRead;
                if (available > 0)
                {
                    int toRead = Math.Min(available, buffer.Length);
                    byte[] temp = new byte[toRead];
                    int bytesRead = _serialPort.Read(temp, 0, toRead);
                    temp.AsSpan(0, bytesRead).CopyTo(buffer.Span);
                    return bytesRead;
                }

                await Task.Delay(5, ct).ConfigureAwait(false);
            }

            throw new InvalidOperationException("USB CDC transport is not open.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Read timed out after {timeout.TotalMilliseconds}ms.");
        }
    }

    public void DiscardBuffers()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
            }
            catch
            {
                // Bỏ qua lỗi nếu cổng đang đóng hoặc trạng thái không hợp lệ
            }
        }
    }

    private void EnsureOpen()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(UsbCdcTransport));

        if (_serialPort == null || !_serialPort.IsOpen || _stream == null)
            throw new InvalidOperationException("USB CDC transport is not open.");
    }

    private void CloseInternal()
    {
        if (_serialPort != null)
        {
            try { if (_serialPort.IsOpen) _serialPort.DiscardInBuffer(); } catch { }
            try { if (_serialPort.IsOpen) _serialPort.DiscardOutBuffer(); } catch { }
            try { if (_serialPort.IsOpen) _serialPort.Close(); } catch { }
            try { _serialPort.Dispose(); } catch { }
            _serialPort = null;
            _stream = null;
        }
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
