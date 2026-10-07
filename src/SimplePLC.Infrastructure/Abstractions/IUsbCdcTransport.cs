namespace SimplePLC.Infrastructure.Abstractions;

/// <summary>
/// Trừu tượng hóa tầng truyền dẫn USB CDC (STM32 TinyUSB CDC endpoint).
/// Quản lý vòng đời kết nối và luồng byte stream hai chiều.
/// </summary>
public interface IUsbCdcTransport : IAsyncDisposable
{
    /// <summary>
    /// Cho biết cổng USB CDC hiện đang mở hay không.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Mở cổng USB CDC với các tùy chọn tương ứng.
    /// </summary>
    Task OpenAsync(UsbCdcOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đóng cổng USB CDC an toàn.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi mảng byte xuống thiết bị qua USB CDC.
    /// </summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc dữ liệu từ USB CDC vào bộ đệm bộ nhớ.
    /// Trả về số byte thực tế đã đọc được (có thể từ 1 đến buffer.Length).
    /// </summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa sạch bộ đệm truyền và nhận (Flush/Discard buffers).
    /// </summary>
    void DiscardBuffers();
}
