using System.Diagnostics;

namespace SimplePLC.Infrastructure.Simulator;

public enum SimulatorMode
{
    Deterministic,
    Demo
}

/// <summary>
/// Quản lý thời gian và chu kỳ mô phỏng cho MCU Reference Simulator.
/// Đảm bảo tính tất định 100% trong chế độ kiểm thử tự động (Deterministic)
/// và dao động tự nhiên trong chế độ Demo UI.
/// </summary>
public sealed class SimulatorClock
{
    private readonly Stopwatch _stopwatch = new();
    private long _deterministicTicks = 3600;

    public SimulatorMode Mode { get; set; } = SimulatorMode.Deterministic;

    public SimulatorClock(uint initialTicks = 3600)
    {
        _deterministicTicks = initialTicks;
        _stopwatch.Start();
    }

    /// <summary>
    /// Tăng tick mô phỏng trong chế độ Deterministic.
    /// </summary>
    public void AdvanceTicks(int seconds = 1)
    {
        _deterministicTicks += Math.Max(0, seconds);
    }

    /// <summary>
    /// Đặt lại đồng hồ về 0 (hoặc giá trị chỉ định) khi reboot hoặc power-cycle.
    /// </summary>
    public void Reset(uint initialTicks = 0)
    {
        _deterministicTicks = initialTicks;
        _stopwatch.Restart();
    }

    /// <summary>
    /// Lấy thời gian Uptime (giây).
    /// </summary>
    public uint GetUptimeSeconds()
    {
        if (Mode == SimulatorMode.Deterministic)
        {
            return (uint)_deterministicTicks;
        }

        return (uint)_stopwatch.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// Lấy giá trị CPU load (%) tương ứng với chế độ đang chạy.
    /// </summary>
    public ushort GetCpuLoadPercent()
    {
        if (Mode == SimulatorMode.Deterministic)
        {
            // Dự đoán được: 25% + (tick % 5)% (25..29%)
            return (ushort)(25 + (_deterministicTicks % 5));
        }

        // Demo mode: dao động ngẫu nhiên trong khoảng 18%..26%
        return (ushort)(18 + (Random.Shared.Next(0, 9)));
    }

    /// <summary>
    /// Cho phép cấy giá trị overrun scan time nhân tạo để kiểm thử chẩn đoán.
    /// </summary>
    public uint? OverrunScanTimeMs { get; set; }

    /// <summary>
    /// Lấy giá trị Scan time (ms) tương ứng với chu kỳ Rule Engine danh định 10 ms.
    /// </summary>
    public uint GetScanTimeMs()
    {
        if (OverrunScanTimeMs.HasValue)
        {
            return OverrunScanTimeMs.Value;
        }

        if (Mode == SimulatorMode.Deterministic)
        {
            // Nominal cố định: 10 ms
            return 10;
        }

        // Demo mode: dao động 9, 10, 11 ms
        return (uint)(9 + Random.Shared.Next(0, 3));
    }

    /// <summary>
    /// Lấy giá trị Scan lớn nhất (ms) ghi nhận từ boot.
    /// </summary>
    public uint GetMaxScanTimeMs()
    {
        if (OverrunScanTimeMs.HasValue)
        {
            return Math.Max(10, OverrunScanTimeMs.Value);
        }

        if (Mode == SimulatorMode.Deterministic)
        {
            return 10;
        }

        return 12;
    }

    /// <summary>
    /// Giữ tương thích cho các lệnh gọi cũ nếu cần.
    /// </summary>
    public uint GetScanTimeMicroseconds() => GetScanTimeMs() * 1000;
}
