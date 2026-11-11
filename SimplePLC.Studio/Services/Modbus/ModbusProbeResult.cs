using System;

namespace SimplePLC.Studio.Services.Modbus;

/// <summary>
/// Phân loại lỗi khi đọc thử Modbus qua cổng COM phần cứng.
/// </summary>
public enum ModbusProbeFaultKind
{
    None = 0,
    PortNotOpen = 1,
    Timeout = 2,
    CrcError = 3,
    ModbusException = 4,
    GeneralFault = 5
}

/// <summary>
/// Kết quả đọc thử một kênh Modbus Master từ cảm biến ngoài thực tế.
/// </summary>
public sealed record ModbusProbeResult
{
    public bool IsSuccess { get; init; }
    public double? ScaledValue { get; init; }
    public string RawValueDisplay { get; init; } = string.Empty;
    public long LatencyMs { get; init; }
    public ModbusProbeFaultKind FaultKind { get; init; } = ModbusProbeFaultKind.None;
    public string DiagnosticMessage { get; init; } = string.Empty;
    public string TroubleshootingHint { get; init; } = string.Empty;

    public static ModbusProbeResult Success(double scaled, string rawDisplay, long latencyMs, string statusMsg = "Phản hồi tốt (OK)") => new()
    {
        IsSuccess = true,
        ScaledValue = scaled,
        RawValueDisplay = rawDisplay,
        LatencyMs = latencyMs,
        FaultKind = ModbusProbeFaultKind.None,
        DiagnosticMessage = statusMsg
    };

    public static ModbusProbeResult Failure(ModbusProbeFaultKind faultKind, string message, string hint, long latencyMs = 0) => new()
    {
        IsSuccess = false,
        LatencyMs = latencyMs,
        FaultKind = faultKind,
        DiagnosticMessage = message,
        TroubleshootingHint = hint
    };
}
