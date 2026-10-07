namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Chế độ hoạt động của khối Timer phần cứng theo Wire Profile V2 (Spec Mục 9.2).
/// </summary>
public enum SPLC_TimerMode : ushort
{
    DISABLED = 0,
    TON = 1,       // Timer On-Delay
    TOF = 2,       // Timer Off-Delay
    TP = 3         // Timer Pulse
}

/// <summary>
/// Chế độ hoạt động của khối Counter phần cứng theo Wire Profile V2 (Spec Mục 9.3).
/// </summary>
public enum SPLC_CounterMode : ushort
{
    DISABLED = 0,
    CTU = 1,       // Count Up
    CTD = 2,       // Count Down
    CTUD = 3,      // Count Up / Down
    HSC = 4        // High-Speed Hardware Counter
}
