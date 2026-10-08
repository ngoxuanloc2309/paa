namespace SimplePLC.Protocol.Models;

/// <summary>
/// Bản đồ chỉ số Tag động (Dynamic Tag Index Map).
/// Tính toán BaseIndex của từng nhóm tag dựa trên số lượng thực tế
/// được khai báo trong DeviceResourceInfo từ MCU.
/// DiBase luôn = 0. Các base còn lại tích lũy lần lượt.
/// </summary>
public readonly struct TagLayoutMap
{
    public ushort DiBase { get; }
    public ushort DiCount { get; }
    public ushort DoBase { get; }
    public ushort DoCount { get; }
    public ushort AiBase { get; }
    public ushort AiCount { get; }
    public ushort VflagBase { get; }
    public ushort VflagCount { get; }
    public ushort VregBase { get; }
    public ushort VregCount { get; }
    public ushort VregRetainBase { get; }
    public ushort VregRetainCount { get; }
    public ushort CounterBase { get; }
    public ushort CounterCount { get; }
    public ushort TotalTags { get; }

    public TagLayoutMap(
        ushort diCount,
        ushort doCount,
        ushort aiCount,
        ushort vflagCount,
        ushort vregCount,
        ushort vregRetainCount,
        ushort counterCount)
    {
        DiCount = diCount;
        DoCount = doCount;
        AiCount = aiCount;
        VflagCount = vflagCount;
        VregCount = vregCount;
        VregRetainCount = vregRetainCount;
        CounterCount = counterCount;

        DiBase = 0;
        DoBase = diCount;
        AiBase = (ushort)(diCount + doCount);
        VflagBase = (ushort)(diCount + doCount + aiCount);
        VregBase = (ushort)(diCount + doCount + aiCount + vflagCount);
        VregRetainBase = (ushort)(diCount + doCount + aiCount + vflagCount + vregCount);
        CounterBase = (ushort)(diCount + doCount + aiCount + vflagCount + vregCount + vregRetainCount);
        TotalTags = (ushort)(diCount + doCount + aiCount + vflagCount + vregCount + vregRetainCount + counterCount);
    }

    /// <summary>
    /// Layout mặc định đầy đủ tài nguyên: 8 DI, 8 DO, 4 AI, 32 VFLAG, 32 VREG, 32 VREG_RETAIN, 8 COUNTER.
    /// Dùng khi chưa kết nối MCU thực (default project state).
    /// </summary>
    public static TagLayoutMap Default { get; } = new(8, 8, 4, 32, 32, 32, 8);
}
