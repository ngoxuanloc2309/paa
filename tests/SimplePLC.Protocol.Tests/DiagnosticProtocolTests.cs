using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class DiagnosticProtocolTests
{
    [Fact]
    public void DiagnosticConstants_HaveCorrectAddressesAndLengths()
    {
        Assert.Equal(0x0A20, ModbusRegisterMap.DiagBlockBaseAddress);
        Assert.Equal(5, ModbusRegisterMap.DiagBlockLength);

        Assert.Equal(0x0A20, ModbusRegisterMap.DiagCommandAddress);
        Assert.Equal(1, ModbusRegisterMap.DiagCommandLength);

        Assert.Equal(0x0A21, ModbusRegisterMap.DiagStateAddress);
        Assert.Equal(1, ModbusRegisterMap.DiagStateLength);

        Assert.Equal(0x0A22, ModbusRegisterMap.DiagFlagsAddress);
        Assert.Equal(1, ModbusRegisterMap.DiagFlagsLength);

        Assert.Equal(0x0A23, ModbusRegisterMap.DiagLeaseRemainingAddress);
        Assert.Equal(1, ModbusRegisterMap.DiagLeaseRemainingLength);

        Assert.Equal(0x0A24, ModbusRegisterMap.DiagErrorCodeAddress);
        Assert.Equal(1, ModbusRegisterMap.DiagErrorCodeLength);

        Assert.Equal(3000, ModbusRegisterMap.DiagDefaultLeaseMs);
        Assert.Equal(1000, ModbusRegisterMap.DiagHeartbeatIntervalMs);
    }

    [Fact]
    public void DiagnosticStatusDto_CalculatesFlagsAndStatesAccurately()
    {
        var dto = new DiagnosticStatusDto(
            SPLC_DiagState.DIAG_CONTROL,
            SPLC_DiagFlags.RETAIN_DIRTY | SPLC_DiagFlags.LEASE_ACTIVE,
            2500,
            SPLC_DiagErrorCode.NONE);

        Assert.True(dto.IsDiagControl);
        Assert.False(dto.IsEngineRunning);
        Assert.False(dto.IsFault);
        Assert.True(dto.IsRetainDirty);
        Assert.True(dto.IsLeaseActive);
        Assert.Equal(2500, dto.LeaseRemainingMs);
        Assert.Equal(SPLC_DiagErrorCode.NONE, dto.ErrorCode);
    }
}
