using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// Đại diện cho ảnh chụp thanh ghi Diagnostic Block (0x0A20..0x0A24) từ MCU.
/// </summary>
public sealed record DiagnosticStatusDto(
    SPLC_DiagState State,
    SPLC_DiagFlags Flags,
    ushort LeaseRemainingMs,
    SPLC_DiagErrorCode ErrorCode)
{
    public bool IsEngineRunning => State == SPLC_DiagState.ENGINE_RUNNING;
    public bool IsDiagControl => State == SPLC_DiagState.DIAG_CONTROL;
    public bool IsTransitioning => State == SPLC_DiagState.TRANSITIONING;
    public bool IsFault => State == SPLC_DiagState.FAULT;

    public bool IsRetainDirty => Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY);
    public bool IsLeaseActive => Flags.HasFlag(SPLC_DiagFlags.LEASE_ACTIVE);
}
