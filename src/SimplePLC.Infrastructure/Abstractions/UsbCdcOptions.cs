namespace SimplePLC.Infrastructure.Abstractions;

/// <summary>
/// Cấu hình kết nối cho thiết bị STM32 Native USB CDC (Virtual COM Port).
/// Không chứa các thông số UART/RS485 truyền thống (BaudRate, Parity, StopBits)
/// vì giao tiếp vật lý diễn ra trực tiếp qua USB Full-Speed bus (12 Mbps).
/// </summary>
public sealed record UsbCdcOptions
{
    public string PortName { get; init; }
    public TimeSpan ReadTimeout { get; init; }
    public TimeSpan WriteTimeout { get; init; }

    public UsbCdcOptions(
        string portName,
        TimeSpan? readTimeout = null,
        TimeSpan? writeTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or whitespace.", nameof(portName));

        PortName = portName;
        ReadTimeout = readTimeout ?? TimeSpan.FromMilliseconds(1000);
        WriteTimeout = writeTimeout ?? TimeSpan.FromMilliseconds(1000);
    }
}
