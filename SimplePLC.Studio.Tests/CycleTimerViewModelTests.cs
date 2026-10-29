using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class CycleTimerViewModelTests
{
    [Fact]
    public void TimerNodeViewModel_CycleMode_PropertiesAndDurationsWorkCorrectly()
    {
        // Arrange
        var inTag = new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var qTag = new TagModel { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput };

        var vm = new TimerNodeViewModel(
            mode: "CYCLE",
            inTag: inTag,
            qTag: qTag,
            presetMs: 1000,
            onTimeMs: 500,
            offTimeMs: 1500,
            startWithOn: true);

        // Assert basic state
        Assert.True(vm.IsCycleMode);
        Assert.Equal("Cycle Timer", vm.DisplayTitle);
        Assert.True(vm.TimerModeBadgeText == "CYCLE (Repeat Cycle)" || vm.TimerModeBadgeText == "CYCLE (Chu kỳ lặp)");
        Assert.True(vm.ModeGlyph == "🔄" || vm.ModeGlyph == "CYCLE");
        Assert.Equal(500u, vm.OnTimeMs);
        Assert.Equal(1500u, vm.OffTimeMs);
        Assert.True(vm.StartWithOn);
        Assert.Equal(0, vm.StartPhaseIndex);

        // Test changing unit & duration for OnTime: 10 minutes
        vm.OnTimeUnit = "min";
        vm.OnDurationValueText = "10";
        Assert.Equal(600000u, vm.OnTimeMs); // 10 * 60,000

        // Test changing unit & duration for OffTime: 2 hours
        vm.OffTimeUnit = "h";
        vm.OffDurationValueText = "2";
        Assert.Equal(7200000u, vm.OffTimeMs); // 2 * 3,600,000

        // Test changing StartPhaseIndex
        vm.StartPhaseIndex = 1;
        Assert.False(vm.StartWithOn);

        vm.StartPhaseIndex = 0;
        Assert.True(vm.StartWithOn);

        // Verify summary text
        Assert.Contains("CYCLE (10min/2h)", vm.SummaryText);
        Assert.Contains("DO0", vm.SummaryText);
    }

    [Fact]
    public void TimerNodeViewModel_SwitchingMode_UpdatesIsCycleModeDynamically()
    {
        var vm = new TimerNodeViewModel("TON");
        Assert.False(vm.IsCycleMode);
        Assert.Equal("TON Timer", vm.DisplayTitle);

        vm.TimerMode = "CYCLE";
        Assert.True(vm.IsCycleMode);
        Assert.Equal("Cycle Timer", vm.DisplayTitle);

        vm.TimerMode = "TOF";
        Assert.False(vm.IsCycleMode);
        Assert.Equal("TOF Timer", vm.DisplayTitle);
    }
}
