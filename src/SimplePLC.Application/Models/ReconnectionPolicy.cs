namespace SimplePLC.Application.Models;

/// <summary>
/// Chính sách cấu hình quy trình kết nối lại tự động 2 tầng (Fast Recovery + Passive Wait).
/// </summary>
public sealed record ReconnectionPolicy
{
    /// <summary>
    /// Danh sách khoảng thời gian chờ giữa các lần thử nhanh trong tầng 1 (Fast Recovery).
    /// Mặc định: 1s -> 2s -> 3s -> 5s -> 5s (5 lần thử).
    /// </summary>
    public IReadOnlyList<TimeSpan> FastRetryDelays { get; init; } = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Chu kỳ thăm dò trong tầng 2 (Passive Wait) sau khi đã hết lượt thử nhanh.
    /// Mặc định: 5 giây một lần.
    /// </summary>
    public TimeSpan PassiveWaitInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Số lần lỗi truyền thông transport liên tiếp tối đa trước khi coi là mất kết nối hoàn toàn.
    /// Mặc định: 5 lần.
    /// </summary>
    public int MaxConsecutiveTransportFailures { get; init; } = 5;

    /// <summary>
    /// Thời gian chờ tĩnh sau khi gửi lệnh REBOOT để MCU kịp reset và ngắt USB trước khi bắt đầu quét lại.
    /// Mặc định: 2 giây.
    /// </summary>
    public TimeSpan ExpectedRestartDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// <summary>
    /// Cho phép tiếp tục thăm dò thụ động (Passive Wait) sau khi hết lượt thử nhanh hay chuyển hẳn về Disconnected.
    /// Mặc định: true.
    /// </summary>
    public bool EnablePassiveWait { get; init; } = true;

    /// <summary>
    /// Cho phép tự động kích hoạt vòng lặp kết nối lại khi mất kết nối bất ngờ.
    /// Mặc định trong chuẩn công nghiệp (Siemens TIA Portal / Rockwell Studio 5000): false (chuyển thẳng về Disconnected, chờ người dùng kết nối lại).
    /// </summary>
    public bool AutoReconnectOnDisconnect { get; init; } = false;

    /// <summary>
    /// Thời gian chờ tối đa cho mỗi lượt thăm dò descriptor (probe handshake) của một candidate endpoint.
    /// Mặc định: 1000ms.
    /// </summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Cấu hình mặc định dành cho môi trường sản xuất thực tế.
    /// </summary>
    public static ReconnectionPolicy Default => new();

    /// <summary>
    /// Cấu hình nhanh dành cho Unit Tests (tránh chờ lâu khi không dùng FakeTimeProvider).
    /// </summary>
    public static ReconnectionPolicy TestingFast => new()
    {
        FastRetryDelays = new[]
        {
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(50)
        },
        PassiveWaitInterval = TimeSpan.FromMilliseconds(50),
        ExpectedRestartDelay = TimeSpan.FromMilliseconds(30),
        ProbeTimeout = TimeSpan.FromMilliseconds(50),
        MaxConsecutiveTransportFailures = 3,
        AutoReconnectOnDisconnect = true
    };

    /// <summary>
    /// Cấu hình cực nhanh dành cho Unit Tests chạy tức thì (1ms).
    /// </summary>
    public static ReconnectionPolicy TestingImmediate => new()
    {
        FastRetryDelays = new[]
        {
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(1)
        },
        PassiveWaitInterval = TimeSpan.FromMilliseconds(5),
        ExpectedRestartDelay = TimeSpan.FromMilliseconds(1),
        ProbeTimeout = TimeSpan.FromMilliseconds(20),
        MaxConsecutiveTransportFailures = 2,
        AutoReconnectOnDisconnect = true
    };
}
