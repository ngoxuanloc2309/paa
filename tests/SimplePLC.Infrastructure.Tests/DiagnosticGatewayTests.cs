using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class DiagnosticGatewayTests
{
    [Fact]
    public async Task DiagnosticGateway_EnterHeartbeatExitFlow_WorksExpectedly()
    {
        // Arrange
        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var gateway = new DiagnosticGateway(client);

        // 1. Initial State: ENGINE_RUNNING
        var initial = await gateway.ReadDiagnosticStatusAsync();
        Assert.Equal(SPLC_DiagState.ENGINE_RUNNING, initial.State);
        Assert.False(initial.IsDiagControl);

        // 2. Send ENTER_DIAG
        var entered = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.ENTER_DIAG);
        Assert.Equal(SPLC_DiagState.DIAG_CONTROL, entered.State);
        Assert.True(entered.IsDiagControl);
        Assert.True(entered.IsLeaseActive);
        Assert.Equal(3000, entered.LeaseRemainingMs);

        // 3. Send HEARTBEAT
        var heartbeat = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.HEARTBEAT);
        Assert.Equal(SPLC_DiagState.DIAG_CONTROL, heartbeat.State);
        Assert.Equal(3000, heartbeat.LeaseRemainingMs);

        // 4. Send EXIT_DIAG
        var exited = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.EXIT_DIAG);
        Assert.Equal(SPLC_DiagState.ENGINE_RUNNING, exited.State);
        Assert.False(exited.IsDiagControl);
        Assert.False(exited.IsLeaseActive);
    }

    [Fact]
    public async Task DiagnosticGateway_WriteTagValue_UpdatesRawRegistersAndRetainDirty()
    {
        // Arrange
        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var gateway = new DiagnosticGateway(client);

        await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.ENTER_DIAG);

        // Act: Write DO0 (TagIndex 8) = 1
        await gateway.WriteTagValueAsync(1, 8, 1);
        var do0Regs = await client.ReadHoldingRegistersAsync(1, (ushort)(0x0900 + 8 * 2), 2);
        int do0Val = (do0Regs[0] << 16) | do0Regs[1];
        Assert.Equal(1, do0Val);

        // Write VREG_RETAIN0 (TagIndex 84, address 0x09A8) = 12345
        await gateway.WriteTagValueAsync(1, 84, 12345);
        var status = await gateway.ReadDiagnosticStatusAsync();
        Assert.True(status.IsRetainDirty);

        // Attempt EXIT_DIAG while dirty -> Rejected with ERR_RETAIN_DIRTY
        var rejectExit = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.EXIT_DIAG);
        Assert.Equal(SPLC_DiagState.DIAG_CONTROL, rejectExit.State);
        Assert.Equal(SPLC_DiagErrorCode.RETAIN_DIRTY, rejectExit.ErrorCode);

        // Commit Retain -> Clears Dirty flag
        var committed = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.COMMIT_RETAIN);
        Assert.False(committed.IsRetainDirty);

        // Now EXIT_DIAG succeeds
        var finalExit = await gateway.SendDiagnosticCommandAsync(1, SPLC_DiagCommand.EXIT_DIAG);
        Assert.Equal(SPLC_DiagState.ENGINE_RUNNING, finalExit.State);
    }
}
