using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class LiveWatchViewModelTests
{
    private static ProductDefinition CreateProduct() => ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void SyncFromSnapshot_InitializesAllTagsAndMatchesCount()
    {
        // Arrange
        var store = new RuntimeStateStore();
        store.InitializeProduct(CreateProduct());

        // Act
        using var vm = new LiveWatchViewModel(store);

        // Assert
        Assert.Equal(124, vm.AllTags.Count);
        Assert.Equal(124, vm.FilteredTags.Count);

        var tag0 = vm.AllTags[0];
        Assert.Equal("DI0", tag0.Name);
        Assert.Equal(TagKind.DiscreteInput, tag0.Kind);
        Assert.Equal(TagQuality.Unknown, tag0.Quality);
        Assert.Equal("OFF [Last]", tag0.FormattedValue);
    }

    [Fact]
    public void TagFormatting_BooleanAndInt32_FormatsAccurately()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        using var vm = new LiveWatchViewModel(store);

        // Act: Update DI0 = 1 (Boolean ON), AI0 = 2048 (Int32)
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 },
            new() { TagIndex = 16, TagName = "AI0", Value = 2048 }
        }, product);

        // Assert
        var di0 = vm.AllTags[0];
        Assert.Equal(1, di0.RawValue);
        Assert.True(di0.IsBoolean);
        Assert.True(di0.IsOn);
        Assert.Equal("ON", di0.FormattedValue);
        Assert.Equal("0x00000001", di0.RawHexText);
        Assert.True(di0.IsModifiedRecently);

        var ai0 = vm.AllTags.First(t => t.Index == 16);
        Assert.Equal(2048, ai0.RawValue);
        Assert.False(ai0.IsBoolean);
        Assert.False(ai0.IsOn);
        Assert.Equal("2,048", ai0.FormattedValue);
        Assert.Equal("0x00000800", ai0.RawHexText);
    }

    [Fact]
    public void FilterByKind_SeparatesTagCategoriesAccurately()
    {
        // Arrange: 8 DI, 8 DO, 4 AI, 32 VFLAG, 32 VREG, 32 VREG_RETAIN, 8 Counter = 124 total
        var store = new RuntimeStateStore();
        store.InitializeProduct(CreateProduct());

        using var vm = new LiveWatchViewModel(store);

        // Act & Assert DI
        vm.SetKindFilter("DI");
        Assert.Equal(8, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.DiscreteInput, t.Kind));

        // Act & Assert DO
        vm.SetKindFilter("DO");
        Assert.Equal(8, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.DiscreteOutput, t.Kind));

        // Act & Assert AI
        vm.SetKindFilter("AI");
        Assert.Equal(4, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.AnalogInput, t.Kind));

        // Act & Assert INTERNAL (VFLAG 32 + VREG 32 + VREG_RETAIN 32 + Counter 8 = 104)
        vm.SetKindFilter("INTERNAL");
        Assert.Equal(104, vm.FilteredTags.Count);

        // Reset to ALL
        vm.SetKindFilter("ALL");
        Assert.Equal(124, vm.FilteredTags.Count);
    }

    [Fact]
    public void FilterBySearchText_FiltersByNameOrAlias()
    {
        var store = new RuntimeStateStore();
        store.InitializeProduct(CreateProduct());

        using var vm = new LiveWatchViewModel(store);

        vm.SearchText = "DI3";
        Assert.Single(vm.FilteredTags);
        Assert.Equal("DI3", vm.FilteredTags[0].Name);

        vm.SearchText = "Analog Input";
        Assert.Equal(4, vm.FilteredTags.Count); // AI0..AI3 descriptions contain "Analog Input"
    }

    [Fact]
    public void DiagnosticsSync_UpdatesHealthProperties()
    {
        var store = new RuntimeStateStore();
        using var vm = new LiveWatchViewModel(store);

        var health = new DeviceHealthInfo
        {
            CpuLoadPercent = 42,
            RamUsagePercent = 55,
            ScanTimeMs = 10,
            MaxScanTimeMs = 12,
            UptimeSeconds = 3661, // 1h 1m 1s
            ResetReason = SPLC_ResetReason.SOFTWARE,
            HealthFlags = SPLC_HealthFlags.NONE
        };

        store.UpdateHealth(health);

        Assert.Equal(42, vm.CpuLoad);
        Assert.Equal(55, vm.RamUsage);
        Assert.Equal(10u, vm.ScanTime);
        Assert.Equal(12u, vm.MaxScanTime);
        string expectedReason = SimplePLC.Studio.Services.LocalizationService.Instance.IsVietnamese ? "Lệnh phần mềm" : "Software Command";
        Assert.Equal(expectedReason, vm.ResetReasonText);
        Assert.Contains("01h 01m 01s", vm.UptimeText);
    }

    [Fact]
    public void ConnectionStatus_DisplaysCorrectSemanticsAndTexts()
    {
        var store = new RuntimeStateStore();
        using var vm = new LiveWatchViewModel(store);
        var loc = SimplePLC.Studio.Services.LocalizationService.Instance;

        string expectedOffline = loc.IsVietnamese ? "Chưa kết nối" : "Offline";
        string expectedOnline = loc.IsVietnamese ? "Đang trực tuyến" : "Online";

        // Default Disconnected
        Assert.Contains(expectedOffline, vm.ConnectionStatusText);
        Assert.Equal(ConnectionStatus.Disconnected, vm.ConnectionStatus);

        // Connected
        store.UpdateConnection(null, ConnectionStatus.Connected);
        Assert.Contains(expectedOnline, vm.ConnectionStatusText);
        Assert.Equal(ConnectionStatus.Connected, vm.ConnectionStatus);

        // Disconnected
        store.UpdateConnection(null, ConnectionStatus.Disconnected);
        Assert.Contains(expectedOffline, vm.ConnectionStatusText);
        Assert.Equal(ConnectionStatus.Disconnected, vm.ConnectionStatus);

        // Test English locale switching
        loc.CurrentLanguage = "en";
        try
        {
            Assert.Contains("Offline", vm.ConnectionStatusText);
            store.UpdateConnection(null, ConnectionStatus.Connected);
            Assert.Contains("Online", vm.ConnectionStatusText);
        }
        finally
        {
            loc.CurrentLanguage = "en";
        }
    }

    [Fact]
    public void WatchTagItemModel_ForceProperties_ShowPrefixFAndState()
    {
        var tag = new SimplePLC.Studio.Models.WatchTagItemModel
        {
            Index = 8,
            Name = "DO0",
            Kind = TagKind.DiscreteOutput,
            DataType = TagDataType.Boolean,
            Quality = TagQuality.Good,
            RawValue = 1
        };

        Assert.True(tag.IsForceable);
        Assert.False(tag.IsForced);
        Assert.DoesNotContain("[F]", tag.FormattedValue);

        // Force to 1
        tag.IsForced = true;
        tag.ForcedValue = 1;
        Assert.Contains("[F]", tag.FormattedValue);
        Assert.Contains("[F]", tag.FormattedDisplayValue);
        Assert.Contains("[F]", tag.BooleanDisplayValue);
    }

    [Fact]
    public async Task LiveWatchViewModel_ForceCommands_UpdatesTagState()
    {
        var store = new RuntimeStateStore();
        store.InitializeProduct(CreateProduct());

        var simulator = new SimplePLC.Infrastructure.Simulator.McuReferenceSimulator(wireProfile: 2);
        var client = new SimplePLC.Infrastructure.Transport.FakeModbusClient(simulator, isConnected: true);
        var gateway = new SimplePLC.Infrastructure.Devices.DiagnosticGateway(client);
        var diagController = new DiagnosticControlService(gateway);

        using var vm = new LiveWatchViewModel(store, diagController);

        // Enter manual mode directly on controller
        await diagController.EnterManualModeAsync();
        Assert.True(vm.IsManualModeActive);

        var do0 = vm.AllTags.First(t => t.Index == 8);
        Assert.False(do0.IsForced);

        // Force DO0 = 1
        await vm.ForceTagBooleanAsync(do0);
        Assert.True(do0.IsForced);
        Assert.Equal(1, do0.RawValue);

        // Release DO0
        await vm.ReleaseTagAsync(do0);
        Assert.False(do0.IsForced);

        // Release All
        await vm.ReleaseAllAsync();
        Assert.False(vm.IsManualModeActive);
    }

    [Fact]
    public void QualityVisualConverters_DistinguishGoodFromStaleAndUnknown()
    {
        var brushConverter = new SimplePLC.Studio.Converters.BooleanQualityToStatusBrushConverter();
        var opacityConverter = new SimplePLC.Studio.Converters.TagQualityToOpacityConverter();

        // 1. Good Quality: ON is active green
        var goodBrush = brushConverter.Convert(new object[] { true, TagQuality.Good }, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(goodBrush);
        Assert.Equal(0x10, goodBrush.Color.R);
        Assert.Equal(0x7C, goodBrush.Color.G);
        Assert.Equal(0x41, goodBrush.Color.B);

        // 2. Unknown Quality (Disconnected / Stale after reset): ON is de-emphasized slate gray (#94A3B8)
        var unknownBrush = brushConverter.Convert(new object[] { true, TagQuality.Unknown }, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(unknownBrush);
        Assert.Equal(0x94, unknownBrush.Color.R);
        Assert.Equal(0xA3, unknownBrush.Color.G);
        Assert.Equal(0xB8, unknownBrush.Color.B);

        // 3. Off Brush is neutral slate (#64748B)
        var offBrush = brushConverter.Convert(new object[] { false, TagQuality.Good }, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(offBrush);
        Assert.Equal(0x64, offBrush.Color.R);
        Assert.Equal(0x74, offBrush.Color.G);
        Assert.Equal(0x8B, offBrush.Color.B);

        // 4. Opacity Converter: Good is 1.0, Unknown/Stale is dimmed (0.65)
        double goodOpacity = (double)opacityConverter.Convert(TagQuality.Good, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture);
        double unknownOpacity = (double)opacityConverter.Convert(TagQuality.Unknown, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture);
        double staleOpacity = (double)opacityConverter.Convert(TagQuality.Stale, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1.0, goodOpacity);
        Assert.Equal(0.65, unknownOpacity);
        Assert.Equal(0.65, staleOpacity);
    }

    [Fact]
    public void WatchTagItemModel_QualityNotGood_AppendsLastSuffix()
    {
        var tag = new SimplePLC.Studio.Models.WatchTagItemModel
        {
            Index = 8,
            Name = "DO0",
            Kind = TagKind.DiscreteOutput,
            DataType = TagDataType.Boolean,
            RawValue = 1,
            Quality = TagQuality.Good
        };

        // When Good: no [Last] suffix
        Assert.Equal("● ON (1)", tag.BooleanDisplayValue);
        Assert.Equal("ON", tag.FormattedValue);
        Assert.Equal("ON", tag.FormattedDisplayValue);

        // When Unknown (after Reset / Disconnect): appends [Last] suffix
        tag.Quality = TagQuality.Unknown;
        Assert.Equal("● ON (1) [Last]", tag.BooleanDisplayValue);
        Assert.Equal("ON [Last]", tag.FormattedValue);
        Assert.Equal("ON [Last]", tag.FormattedDisplayValue);

        // When Stale: also appends [Last] suffix
        tag.Quality = TagQuality.Stale;
        Assert.Equal("● ON (1) [Last]", tag.BooleanDisplayValue);
        Assert.Equal("ON [Last]", tag.FormattedValue);
        Assert.Equal("ON [Last]", tag.FormattedDisplayValue);

        // Check Vietnamese localization
        var loc = SimplePLC.Studio.Services.LocalizationService.Instance;
        loc.CurrentLanguage = "vi";
        try
        {
            tag.NotifyLanguageChanged();
            Assert.Equal("● BẬT (1) [Gần nhất]", tag.BooleanDisplayValue);
            Assert.Equal("ON [Gần nhất]", tag.FormattedValue);
            Assert.Equal("BẬT [Gần nhất]", tag.FormattedDisplayValue);
        }
        finally
        {
            loc.CurrentLanguage = "en";
            tag.NotifyLanguageChanged();
        }
    }
}

