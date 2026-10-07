using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Converters;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class LogicEditorLiveDebuggingTests : IDisposable
{
    public LogicEditorLiveDebuggingTests()
    {
        // Tests in this class assert Vietnamese UI strings (e.g. "BẬT", "Lần cuối")
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    private static ProductDefinition CreateProduct() => ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private static DeviceEndpoint CreateEndpoint() => new UsbCdcEndpoint("COM3");

    private static LogicEditorViewModel CreateEditor(TagCatalogViewModel catalog, RuntimeStateStore store, Action<Action>? uiDispatcher = null)
    {
        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: uiDispatcher ?? (a => a()));
        editor.LoadDefaultDemoGraph();
        return editor;
    }

    [Fact]
    public void InputNode_WhenDigitalTagUpdates_UpdatesLiveValueAndActiveState()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        // Act 1: Update DI0 = 1 (Active)
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);

        // Assert 1
        Assert.True(di0Node.IsLiveOnline);
        Assert.Equal(1, di0Node.LiveRawValue);
        Assert.True(di0Node.IsLiveActive);
        Assert.Equal(TagQuality.Good, di0Node.LiveQuality);
        Assert.Equal(NodeLiveVisualState.Active, di0Node.LiveVisualState);
        Assert.Equal("● BẬT", di0Node.LiveBadgeText);

        // Act 2: Update DI0 = 0 (Inactive)
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 0 }
        }, product);

        // Assert 2
        Assert.True(di0Node.IsLiveOnline);
        Assert.Equal(0, di0Node.LiveRawValue);
        Assert.False(di0Node.IsLiveActive);
        Assert.Equal(TagQuality.Good, di0Node.LiveQuality);
        Assert.Equal(NodeLiveVisualState.Inactive, di0Node.LiveVisualState);
        Assert.Equal("○ TẮT", di0Node.LiveBadgeText);
    }

    [Fact]
    public void InputNode_WhenAnalogTagUpdates_DisplaysFormattedEngineeringValue()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var ai0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "AI0");
        Assert.NotNull(ai0Node);

        // Act
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 16, TagName = "AI0", Value = 2048 }
        }, product);

        // Assert
        Assert.True(ai0Node.IsLiveOnline);
        Assert.Equal(2048, ai0Node.LiveRawValue);
        Assert.False(ai0Node.IsLiveActive); // Analog is not boolean active
        Assert.Equal(TagQuality.Good, ai0Node.LiveQuality);
        Assert.Equal("2,048", ai0Node.LiveBadgeText);
    }

    [Fact]
    public void ActionNode_WhenTargetTagUpdates_ReflectsRuntimeValueInBadge()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var do0Node = editor.Nodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.TargetTag?.Name == "DO0");
        Assert.NotNull(do0Node);

        // Act 1: Turn DO0 = 1 (ON)
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 8, TagName = "DO0", Value = 1 }
        }, product);

        // Assert 1
        Assert.True(do0Node.IsLiveOnline);
        Assert.Equal(1, do0Node.LiveRawValue);
        Assert.True(do0Node.IsLiveActive);
        Assert.Equal(NodeLiveVisualState.Active, do0Node.LiveVisualState);
        Assert.Contains("[Current: ON]", do0Node.LiveTargetBadgeText);

        // Act 2: Turn DO0 = 0 (OFF)
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 8, TagName = "DO0", Value = 0 }
        }, product);

        // Assert 2
        Assert.Equal(0, do0Node.LiveRawValue);
        Assert.False(do0Node.IsLiveActive);
        Assert.Equal(NodeLiveVisualState.Inactive, do0Node.LiveVisualState);
        Assert.Contains("[Current: OFF]", do0Node.LiveTargetBadgeText);
    }

    [Fact]
    public void GuardNode_WhenGuardTagUpdates_ReflectsConditionTagState()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var guardNode = editor.Nodes.OfType<GuardNodeViewModel>().FirstOrDefault(n => n.GuardTag?.Name == "VFLAG0");
        Assert.NotNull(guardNode);

        // Act
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 20, TagName = "VFLAG0", Value = 1 }
        }, product);

        // Assert
        Assert.True(guardNode.IsLiveOnline);
        Assert.Equal(1, guardNode.LiveRawValue);
        Assert.True(guardNode.IsLiveActive);
        Assert.Equal(NodeLiveVisualState.Active, guardNode.LiveVisualState);
        Assert.True(guardNode.LiveConditionBadgeText.Contains("ON") || guardNode.LiveConditionBadgeText.Contains("BẬT"));
        Assert.Contains(guardNode.GuardTagShortText, guardNode.LiveConditionBadgeText);
    }

    [Fact]
    public void QualityStale_ReflectsStaleStateAndAppendsWarningIcon()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        // Simulate a tag snapshot directly with Stale quality
        var staleSnapshot = new RuntimeTagSnapshot(
            TagIndex: 0,
            Name: "DI0",
            Alias: null,
            Kind: TagKind.DiscreteInput,
            DataType: TagDataType.Boolean,
            RawValue: 1,
            Quality: TagQuality.Stale,
            LastSuccessfulUpdateAt: DateTimeOffset.UtcNow.AddSeconds(-5));

        // Act
        di0Node.ApplyRuntimeSnapshot(staleSnapshot, isOnline: true);

        // Assert
        Assert.Equal(TagQuality.Stale, di0Node.LiveQuality);
        Assert.Equal(NodeLiveVisualState.Stale, di0Node.LiveVisualState);
        Assert.Contains("⚠️", di0Node.LiveBadgeText);
        Assert.Contains("BẬT", di0Node.LiveBadgeText);
    }

    [Fact]
    public void Disconnect_PreservesLastKnownValueWithOfflineVisualState()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        // Act 1: Connect and update DI0 = 1
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);
        Assert.Equal(1, di0Node.LiveRawValue);

        // Act 2: Disconnect
        store.UpdateConnection(null, ConnectionStatus.Disconnected);

        // Assert 2
        Assert.False(di0Node.IsLiveOnline);
        Assert.Equal(NodeLiveVisualState.Offline, di0Node.LiveVisualState);
        Assert.Equal(1, di0Node.LiveRawValue); // Preserved!
        Assert.Equal("[Lần cuối: BẬT]", di0Node.LiveBadgeText);
    }

    [Fact]
    public void NodeLiveStateConverters_ReturnsExpectedVisualBrushes()
    {
        // Arrange
        var bgConverter = new NodeLiveBadgeBackgroundConverter();
        var borderConverter = new NodeLiveBadgeBorderConverter();
        var fgConverter = new NodeLiveBadgeForegroundConverter();

        // Act & Assert Active
        var activeBg = bgConverter.Convert(NodeLiveVisualState.Active, typeof(object), null!, null!);
        var activeBorder = borderConverter.Convert(NodeLiveVisualState.Active, typeof(object), null!, null!);
        var activeFg = fgConverter.Convert(NodeLiveVisualState.Active, typeof(object), null!, null!);
        Assert.NotNull(activeBg);
        Assert.NotNull(activeBorder);
        Assert.NotNull(activeFg);

        // Act & Assert Stale
        var staleBg = bgConverter.Convert(NodeLiveVisualState.Stale, typeof(object), null!, null!);
        var staleBorder = borderConverter.Convert(NodeLiveVisualState.Stale, typeof(object), null!, null!);
        var staleFg = fgConverter.Convert(NodeLiveVisualState.Stale, typeof(object), null!, null!);
        Assert.NotNull(staleBg);
        Assert.NotNull(staleBorder);
        Assert.NotNull(staleFg);

        // Act & Assert Offline
        var offlineBg = bgConverter.Convert(NodeLiveVisualState.Offline, typeof(object), null!, null!);
        var offlineBorder = borderConverter.Convert(NodeLiveVisualState.Offline, typeof(object), null!, null!);
        var offlineFg = fgConverter.Convert(NodeLiveVisualState.Offline, typeof(object), null!, null!);
        Assert.NotNull(offlineBg);
        Assert.NotNull(offlineBorder);
        Assert.NotNull(offlineFg);

        // Act & Assert Inactive
        var inactiveBg = bgConverter.Convert(NodeLiveVisualState.Inactive, typeof(object), null!, null!);
        var inactiveBorder = borderConverter.Convert(NodeLiveVisualState.Inactive, typeof(object), null!, null!);
        var inactiveFg = fgConverter.Convert(NodeLiveVisualState.Inactive, typeof(object), null!, null!);
        Assert.NotNull(inactiveBg);
        Assert.NotNull(inactiveBorder);
        Assert.NotNull(inactiveFg);
    }

    [Fact]
    public void ThrottledUpdates_RapidSequentialUpdates_AppliesFinalSnapshotAccurately()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);

        // Act: Fire rapid succession of updates
        for (int i = 0; i <= 42; i++)
        {
            store.UpdateTags(new List<RuntimeTagValue>
            {
                new() { TagIndex = 0, TagName = "DI0", Value = i }
            }, product);
        }

        // Assert: The final snapshot value (42) is accurately reflected
        Assert.Equal(42, di0Node.LiveRawValue);
        Assert.True(di0Node.IsLiveActive);
        Assert.Equal("● BẬT", di0Node.LiveBadgeText);
    }

    [Fact]
    public void ChangingNodeTag_RebuildsRuntimeBindingIndex()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);
        Assert.True(editor.TagToNodesIndex.ContainsKey(0));
        Assert.Contains(di0Node, editor.TagToNodesIndex[0]);

        var di1Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI1");
        Assert.NotNull(di1Tag);

        // Act: Change tag from DI0 (index 0) to DI1 (index 1)
        di0Node.Tag = di1Tag;

        // Assert: Index rebuilt, di0Node moved from index 0 to index 1
        Assert.DoesNotContain(di0Node, editor.TagToNodesIndex.GetValueOrDefault((ushort)0) ?? Enumerable.Empty<ILiveTagBoundNode>());
        Assert.Contains(di0Node, editor.TagToNodesIndex[1]);

        // Verify updates on DI1 propagate to di0Node
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 1, TagName = "DI1", Value = 1 }
        }, product);

        Assert.Equal(1, di0Node.LiveRawValue);
        Assert.True(di0Node.IsLiveActive);
    }

    [Fact]
    public void UnchangedSnapshot_DoesNotRaiseNodePropertyChanges()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);

        // Attach PropertyChanged listener and reset counter
        int propChangeCount = 0;
        di0Node.PropertyChanged += (s, e) => propChangeCount++;

        // Act: Send identical snapshot (same value 1, same Good quality)
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);

        // Assert: Diff engine skips updating unchanged tag, so 0 PropertyChanged events raised
        Assert.Equal(0, propChangeCount);
    }

    [Fact]
    public void Unknown_DoesNotClearLastKnownRawValue()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);
        Assert.Equal(1, di0Node.LiveRawValue);

        // Act: Disconnect / SetOffline
        di0Node.SetOffline();

        // Assert: Last known raw value preserved, state is Offline
        Assert.False(di0Node.IsLiveOnline);
        Assert.Equal(NodeLiveVisualState.Offline, di0Node.LiveVisualState);
        Assert.Equal(1, di0Node.LiveRawValue);
        Assert.True(di0Node.HasReceivedUpdate);
        Assert.Equal("[Lần cuối: BẬT]", di0Node.LiveBadgeText);
    }

    [Fact]
    public void NumericTag_DoesNotSetIsLiveActive()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var ai0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "AI0");
        Assert.NotNull(ai0Node);

        // Act: Update analog tag AI0 = 2048
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 16, TagName = "AI0", Value = 2048 }
        }, product);

        // Assert: Numeric/analog tags must never set IsLiveActive to true
        Assert.Equal(2048, ai0Node.LiveRawValue);
        Assert.False(ai0Node.IsLiveActive);
        Assert.Equal(NodeLiveVisualState.Inactive, ai0Node.LiveVisualState);
    }

    [Fact]
    public void MultipleSnapshotsWithinThrottleWindow_AppliesLatestOnly()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();

        // Mock UI dispatcher that holds callbacks without running them immediately
        var pendingActions = new List<Action>();
        var editor = CreateEditor(catalog, store, action => pendingActions.Add(action));

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);

        // Act: Fire 3 snapshots while dispatcher callback is pending
        store.UpdateTags(new List<RuntimeTagValue> { new() { TagIndex = 0, TagName = "DI0", Value = 10 } }, product);
        store.UpdateTags(new List<RuntimeTagValue> { new() { TagIndex = 0, TagName = "DI0", Value = 20 } }, product);
        store.UpdateTags(new List<RuntimeTagValue> { new() { TagIndex = 0, TagName = "DI0", Value = 30 } }, product);

        // Only one dispatch should be scheduled
        Assert.Single(pendingActions);

        // Node still has not applied values yet
        Assert.Equal(0, di0Node.LiveRawValue);

        // Drain the scheduled action
        pendingActions[0]();

        // Assert: Latest snapshot (Value = 30) was applied, intermediate ones coalesced
        Assert.Equal(30, di0Node.LiveRawValue);
    }

    [Fact]
    public void NodeRemoved_NoLongerReceivesRuntimeUpdates()
    {
        // Arrange
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var catalog = new TagCatalogViewModel();
        var editor = CreateEditor(catalog, store);

        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "DI0");
        Assert.NotNull(di0Node);

        // Act: Remove node from editor canvas
        editor.Nodes.Remove(di0Node);

        // Verify index no longer tracks this node
        Assert.DoesNotContain(di0Node, editor.TagToNodesIndex.GetValueOrDefault((ushort)0) ?? Enumerable.Empty<ILiveTagBoundNode>());

        // Update tag in store
        store.UpdateConnection(CreateEndpoint(), ConnectionStatus.Connected);
        store.UpdateTags(new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 }
        }, product);

        // Assert: Removed node did not receive runtime update
        Assert.Equal(0, di0Node.LiveRawValue);
        Assert.False(di0Node.IsLiveOnline);
    }
}
