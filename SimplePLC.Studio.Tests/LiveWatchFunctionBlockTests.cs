using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class LiveWatchFunctionBlockTests
{
    [Fact]
    public void ApplySnapshotDiff_WithTimerTelemetry_UpdatesTimerNodeProgressAndState()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var editor = new LogicEditorViewModel(catalog);
        var timerNode = new TimerNodeViewModel("TON", presetMs: 5000);
        editor.Nodes.Add(timerNode);

        var endpoint = new UsbCdcEndpoint("COM5");

        // Act 1: Timer đang chạy 50%
        var timerDtoInProgress = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 5000,
            ElapsedMs = 2500,
            StatusBits = (ushort)(ModbusRegisterMap.FbTimerStatusBitIn | ModbusRegisterMap.FbTimerStatusBitRunning)
        };

        var snapshotInProgress = new RuntimeDeviceSnapshot(
            Endpoint: endpoint,
            ConnectionStatus: ConnectionStatus.Connected,
            Health: null,
            Tags: Array.Empty<RuntimeTagSnapshot>(),
            Timestamp: DateTimeOffset.UtcNow,
            Timers: new[] { timerDtoInProgress },
            Counters: Array.Empty<FbCounterRecordDto>()
        );

        editor.ApplySnapshotDiff(snapshotInProgress);

        // Assert 1
        Assert.Equal(2500u, timerNode.ElapsedMs);
        Assert.Equal(50.0, timerNode.ProgressPercent);
        Assert.True(timerNode.IsTiming);
        Assert.False(timerNode.IsLiveActive);
        Assert.Equal("2500 / 5000 ms", timerNode.SimulationProgressText);
        Assert.Equal("50% (5s)", timerNode.TimerStatusPillText);

        // Act 2: Timer hoàn tất (Q = 1)
        var timerDtoDone = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 5000,
            ElapsedMs = 5000,
            StatusBits = (ushort)(ModbusRegisterMap.FbTimerStatusBitIn | ModbusRegisterMap.FbTimerStatusBitQ)
        };

        var snapshotDone = new RuntimeDeviceSnapshot(
            Endpoint: endpoint,
            ConnectionStatus: ConnectionStatus.Connected,
            Health: null,
            Tags: Array.Empty<RuntimeTagSnapshot>(),
            Timestamp: DateTimeOffset.UtcNow,
            Timers: new[] { timerDtoDone },
            Counters: Array.Empty<FbCounterRecordDto>()
        );

        editor.ApplySnapshotDiff(snapshotDone);

        // Assert 2
        Assert.Equal(5000u, timerNode.ElapsedMs);
        Assert.Equal(100.0, timerNode.ProgressPercent);
        Assert.False(timerNode.IsTiming);
        Assert.True(timerNode.IsLiveActive);
        Assert.Equal("DONE · 5000 ms", timerNode.SimulationProgressText);
        Assert.Equal("Q = ON", timerNode.TimerStatusPillText);
    }

    [Fact]
    public void ApplySnapshotDiff_WithCounterTelemetry_UpdatesCounterNodeCurrentCountAndState()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var editor = new LogicEditorViewModel(catalog);
        var counterNode = new CounterNodeViewModel { CounterMode = "CTU", PresetValue = 50 };
        editor.Nodes.Add(counterNode);

        var endpoint = new UsbCdcEndpoint("COM5");

        // Act 1: Counter đang đếm 18 / 50
        var counterDtoInProgress = new FbCounterRecordDto
        {
            Mode = SPLC_CounterMode.CTU,
            PresetValue = 50,
            CurrentValue = 18,
            StatusBits = ModbusRegisterMap.FbCounterStatusBitCu
        };

        var snapshotInProgress = new RuntimeDeviceSnapshot(
            Endpoint: endpoint,
            ConnectionStatus: ConnectionStatus.Connected,
            Health: null,
            Tags: Array.Empty<RuntimeTagSnapshot>(),
            Timestamp: DateTimeOffset.UtcNow,
            Timers: Array.Empty<FbTimerRecordDto>(),
            Counters: new[] { counterDtoInProgress }
        );

        editor.ApplySnapshotDiff(snapshotInProgress);

        // Assert 1
        Assert.Equal(18, counterNode.CurrentCount);
        Assert.False(counterNode.IsLiveActive);
        Assert.Equal("18 / 50", counterNode.SimulationCountText);
        Assert.Equal("PV: 50", counterNode.CounterStatusPillText);

        // Act 2: Counter đạt ngưỡng (Q = 1)
        var counterDtoTripped = new FbCounterRecordDto
        {
            Mode = SPLC_CounterMode.CTU,
            PresetValue = 50,
            CurrentValue = 50,
            StatusBits = (ushort)(ModbusRegisterMap.FbCounterStatusBitCu | ModbusRegisterMap.FbCounterStatusBitQ)
        };

        var snapshotTripped = new RuntimeDeviceSnapshot(
            Endpoint: endpoint,
            ConnectionStatus: ConnectionStatus.Connected,
            Health: null,
            Tags: Array.Empty<RuntimeTagSnapshot>(),
            Timestamp: DateTimeOffset.UtcNow,
            Timers: Array.Empty<FbTimerRecordDto>(),
            Counters: new[] { counterDtoTripped }
        );

        editor.ApplySnapshotDiff(snapshotTripped);

        // Assert 2
        Assert.Equal(50, counterNode.CurrentCount);
        Assert.True(counterNode.IsLiveActive);
        Assert.Equal("50 / 50", counterNode.SimulationCountText);
        Assert.Equal("Q = ON", counterNode.CounterStatusPillText);
    }
}
