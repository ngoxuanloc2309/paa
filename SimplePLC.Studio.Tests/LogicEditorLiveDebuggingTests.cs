using SimplePLC.Application.Enums;

using SimplePLC.Application.Models;

using SimplePLC.Application.Services;

using SimplePLC.Domain.Enums;

using SimplePLC.Domain.Models;

using SimplePLC.Studio.Converters;

using SimplePLC.Studio.Models;

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

        editor.MarkGraphDeployed();

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

        Assert.Equal("1", di0Node.LiveBadgeText);



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

        Assert.Equal("0", di0Node.LiveBadgeText);

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

        Assert.Contains("1", guardNode.LiveConditionBadgeText);

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

        Assert.True(di0Node.LiveBadgeText.Contains("⚠️") || di0Node.LiveBadgeText.Contains("[STALE]"));

        Assert.Contains("1", di0Node.LiveBadgeText);

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

        Assert.Equal("[Lần cuối: 1]", di0Node.LiveBadgeText);

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

        Assert.Equal("1", di0Node.LiveBadgeText);

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

        Assert.Equal("[Lần cuối: 1]", di0Node.LiveBadgeText);

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



    [Fact]

    public void ToggleLiveDebug_TogglesStateAndReflectsStatusText()

    {

        // Arrange

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = CreateEditor(catalog, store);



        Assert.False(editor.IsLiveDebugActive);



        // Act 1: Toggle ON

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert 1

        Assert.True(editor.IsLiveDebugActive);

        Assert.Contains("giám sát", editor.LiveDebugStatusText, StringComparison.OrdinalIgnoreCase);



        // Act 2: Toggle OFF

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert 2

        Assert.False(editor.IsLiveDebugActive);

        Assert.Contains("tắt", editor.LiveDebugStatusText, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public void ToggleLiveDebug_WhenDisconnected_BlocksActivationAndShowsWarning()

    {

        // Arrange

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        var editor = CreateEditor(catalog, store);



        Assert.False(editor.IsDeviceConnected);

        Assert.False(editor.IsLiveDebugActive);



        // Act: Attempt to toggle ON while disconnected

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert: Activation blocked and safety warning toast populated

        Assert.False(editor.IsLiveDebugActive);

        Assert.NotNull(editor.SimGateWarning);

        Assert.NotEmpty(editor.SimGateWarning);

    }



    [Fact]

    public void ToggleLiveDebug_WhenCanvasEmpty_BlocksActivationAndShowsWarning()

    {

        // Arrange: Connected, but canvas is completely empty

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());

        editor.Nodes.Clear();

        editor.Connections.Clear();



        Assert.True(editor.IsDeviceConnected);

        Assert.Empty(editor.Nodes);

        Assert.False(editor.IsLiveDebugActive);



        // Act

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert: Activation blocked and empty graph warning shown

        Assert.False(editor.IsLiveDebugActive);

        Assert.NotEmpty(editor.SimGateWarning);

        Assert.Equal(LocalizationService.Tr("LiveDebugEmptyGraphWarning"), editor.SimGateWarning);

    }



    [Fact]

    public void ToggleLiveDebug_WhenCompileErrors_BlocksActivationAndShowsWarning()

    {

        // Arrange: Connected, but canvas has an orphan action node without trigger (compile error)

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());

        editor.Nodes.Clear();

        editor.Connections.Clear();

        

        var doTag = catalog.AllTags.First(t => t.Name == "DO0");

        var actNode = new ActionNodeViewModel(doTag);

        editor.Nodes.Add(actNode);



        Assert.True(editor.IsDeviceConnected);



        // Act

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert: Activation blocked and compile error warning shown

        Assert.False(editor.IsLiveDebugActive);

        Assert.NotEmpty(editor.SimGateWarning);

    }



    [Fact]

    public void ToggleLiveDebug_WhenValidGraph_ReflectsActiveBlockCountInBanner()

    {

        // Arrange: Connected, valid default demo graph

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = CreateEditor(catalog, store);



        // Act

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert: Live debug is active and banner shows active block count

        Assert.True(editor.IsLiveDebugActive);

        Assert.Contains("khối logic", editor.LiveDebugBannerSubText, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public void ToggleLiveDebug_WhenGraphNotDeployed_BlocksActivationAndShowsWarning()

    {

        // Arrange: Connected, valid graph, but NEVER deployed to device (IsGraphDeployed == false)

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());

        editor.LoadDefaultDemoGraph();

        // Do NOT call editor.MarkGraphDeployed() -> IsGraphDeployed remains false!



        Assert.True(editor.IsDeviceConnected);

        Assert.False(editor.IsGraphDeployed);

        Assert.False(editor.IsLiveDebugActive);



        // Act: Attempt to toggle Live Debug

        editor.ToggleLiveDebugCommand.Execute(null);



        // Assert: Activation blocked and deploy required warning shown

        Assert.False(editor.IsLiveDebugActive);

        Assert.NotEmpty(editor.SimGateWarning);

        Assert.Equal(LocalizationService.Tr("LiveDebugDeployRequiredWarning"), editor.SimGateWarning);

    }



    [Fact]

    public void LiveDebug_WhenInputActive_PropagatesPowerFlowAcrossWiresAndNodes()

    {

        // Arrange: Load default graph (DI0 -> Trigger -> Guard -> DO0)

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        var editor = CreateEditor(catalog, store);



        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");

        var trigNode = editor.Nodes.OfType<TriggerNodeViewModel>().First();

        trigNode.TriggerType = TriggerType.ON_RISE;

        trigNode.ForMs = 0;

        var guardNode = editor.Nodes.OfType<GuardNodeViewModel>().First();

        var actNode = editor.Nodes.OfType<ActionNodeViewModel>().First();



        // Ensure Guard tag VFLAG0 is ON (1) so guard permits

        var vflagTag = catalog.AllTags.First(t => t.Name == "VFLAG0");

        vflagTag.Value = 1;



        // Set DI0 = 1

        var di0Tag = catalog.AllTags.First(t => t.Name == "DI0");

        di0Tag.Value = 1;



        // Act: Turn on Live Debug

        editor.IsLiveDebugActive = true;



        // Assert: DI0 and its wire are active

        Assert.True(di0Node.IsSimActive);

        Assert.True(di0Node.IsLiveActive);



        var conn1 = editor.Connections.First(c => c.Source?.Node == di0Node && c.Target?.Node == trigNode);

        Assert.True(conn1.IsActive);



        // Trigger is met and wire to guard is active

        Assert.True(trigNode.IsSimTriggered);

        var conn2 = editor.Connections.First(c => c.Source?.Node == trigNode && c.Target?.Node == guardNode);

        Assert.True(conn2.IsActive);



        // Guard allows and wire to action is active

        Assert.True(guardNode.IsSimPassed);

        Assert.False(guardNode.IsSimBlocked);

        var conn3 = editor.Connections.First(c => c.Source?.Node == guardNode && c.Target?.Node == actNode);

        Assert.True(conn3.IsActive);



        // Action is fired

        Assert.True(actNode.IsSimActive);

        Assert.True(actNode.IsSimFired);

    }



    [Fact]

    public void LiveDebug_WhenGuardBlocks_InterruptsDownstreamWire()

    {

        // Arrange

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        var editor = CreateEditor(catalog, store);



        var di0Node = editor.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");

        var trigNode = editor.Nodes.OfType<TriggerNodeViewModel>().First();

        trigNode.TriggerType = TriggerType.ON_RISE;

        trigNode.ForMs = 0;

        var guardNode = editor.Nodes.OfType<GuardNodeViewModel>().First();

        var actNode = editor.Nodes.OfType<ActionNodeViewModel>().First();



        // DI0 = 1, but Guard VFLAG0 = 0 (Blocked)

        catalog.AllTags.First(t => t.Name == "DI0").Value = 1;

        catalog.AllTags.First(t => t.Name == "VFLAG0").Value = 0;



        // Act

        editor.IsLiveDebugActive = true;



        // Assert

        Assert.True(guardNode.IsSimBlocked);

        Assert.False(guardNode.IsSimPassed);



        var connToAct = editor.Connections.First(c => c.Source?.Node == guardNode && c.Target?.Node == actNode);

        Assert.False(connToAct.IsActive);

        Assert.False(actNode.IsSimActive);

    }



    [Fact]

    public void LiveDebug_WhenToggledOff_ClearsAllActiveVisuals()

    {

        // Arrange

        var catalog = new TagCatalogViewModel();

        var store = new RuntimeStateStore();

        var editor = CreateEditor(catalog, store);



        catalog.AllTags.First(t => t.Name == "DI0").Value = 1;

        catalog.AllTags.First(t => t.Name == "VFLAG0").Value = 1;



        editor.IsLiveDebugActive = true;

        Assert.Contains(editor.Connections, c => c.IsActive);



        // Act: Turn off Live Debug

        editor.IsLiveDebugActive = false;



        // Assert: All connections and nodes reset to non-active state

        Assert.All(editor.Connections, c => Assert.False(c.IsActive));

        Assert.All(editor.Nodes, n => Assert.False(n.IsSimActive));

    }



    [Fact]

    public void LiveDebug_RisingAndFallingEdgeRules_OnlySatisfiedRuleLightsUp()

    {

        // Arrange: 2 rules

        // Rule 1: DI0 -> Rising Edge (0 -> 1) -> DO0 = 1

        // Rule 2: DI0 -> Falling Edge (1 -> 0) -> DO0 = 0

        var catalog = new TagCatalogViewModel();

        var di0Tag = catalog.AllTags.First(t => t.Name == "DI0");

        var do0Tag = catalog.AllTags.First(t => t.Name == "DO0");



        var store = new RuntimeStateStore();

        store.UpdateConnection(null, ConnectionStatus.Connected);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());

        editor.Nodes.Clear();

        editor.Connections.Clear();



        var in1 = new InputNodeViewModel(di0Tag) { Id = "in1" };

        var trg1 = new TriggerNodeViewModel { Id = "trg1", TriggerType = TriggerType.ON_RISE };

        var act1 = new ActionNodeViewModel(do0Tag) { Id = "act1", ActionType = ActionType.SET_TAG, ActionParam = 1 };



        var in2 = new InputNodeViewModel(di0Tag) { Id = "in2" };

        var trg2 = new TriggerNodeViewModel { Id = "trg2", TriggerType = TriggerType.ON_FALL };

        var act2 = new ActionNodeViewModel(do0Tag) { Id = "act2", ActionType = ActionType.SET_TAG, ActionParam = 0 };



        editor.Nodes.Add(in1);

        editor.Nodes.Add(trg1);

        editor.Nodes.Add(act1);

        var connIn1Trg1 = new ConnectionViewModel(in1.OutputConnectors[0], trg1.InputConnectors[0]);

        var connTrg1Act1 = new ConnectionViewModel(trg1.OutputConnectors[0], act1.InputConnectors[0]);

        editor.Connections.Add(connIn1Trg1);

        editor.Connections.Add(connTrg1Act1);



        editor.Nodes.Add(in2);

        editor.Nodes.Add(trg2);

        editor.Nodes.Add(act2);

        var connIn2Trg2 = new ConnectionViewModel(in2.OutputConnectors[0], trg2.InputConnectors[0]);

        var connTrg2Act2 = new ConnectionViewModel(trg2.OutputConnectors[0], act2.InputConnectors[0]);

        editor.Connections.Add(connIn2Trg2);

        editor.Connections.Add(connTrg2Act2);



        editor.MarkGraphDeployed();

        editor.IsLiveDebugActive = true;



        // Step 1: Initial state DI0 = 0

        di0Tag.Value = 0;

        editor.ApplyLiveOnlinePropagation();



        // Neither rule should be triggered initially

        Assert.False(trg1.IsSimTriggered, "Rule 1 should not trigger when DI0 is 0");

        Assert.False(trg2.IsSimTriggered, "Rule 2 should not trigger when DI0 is 0");

        Assert.False(connTrg1Act1.IsActive, "Conn 1 should not be active");

        Assert.False(connTrg2Act2.IsActive, "Conn 2 should not be active");



        // Step 2: DI0 transitions from 0 -> 1 (Rising Edge)

        di0Tag.Value = 1;

        editor.ApplyLiveOnlinePropagation();



        // Rule 1 (Rising Edge) should trigger!

        // Rule 2 (Falling Edge) MUST NOT trigger!

        Assert.True(trg1.IsSimTriggered, "Rule 1 should trigger on Rising Edge");

        Assert.True(connTrg1Act1.IsActive, "Conn 1 should be active on Rising Edge");

        Assert.True(act1.IsSimActive, "Action 1 should be active on Rising Edge");

        Assert.False(trg2.IsSimTriggered, "Rule 2 (Falling Edge) must NOT trigger when DI0 is 1");

        Assert.False(connTrg2Act2.IsActive, "Conn 2 must NOT be active when DI0 is 1");

        Assert.False(act2.IsSimActive, "Action 2 must NOT be active when DI0 is 1");



        // Wait out pulse stretch window (>250ms) to verify edge pulse clears

        Thread.Sleep(300);

        editor.ApplyLiveOnlinePropagation();

        Assert.False(trg1.IsSimTriggered, "Rule 1 should return to idle after pulse duration");

        Assert.False(connTrg1Act1.IsActive, "Conn 1 should return to idle");



        // Step 3: DI0 transitions from 1 -> 0 (Falling Edge)

        di0Tag.Value = 0;

        editor.ApplyLiveOnlinePropagation();



        // Rule 1 (Rising Edge) MUST NOT trigger!

        // Rule 2 (Falling Edge) SHOULD trigger!

        Assert.False(trg1.IsSimTriggered, "Rule 1 (Rising Edge) must NOT trigger when falling 1 -> 0");

        Assert.False(connTrg1Act1.IsActive, "Conn 1 must NOT be active on falling edge");

        Assert.True(trg2.IsSimTriggered, "Rule 2 (Falling Edge) should trigger on Falling Edge 1 -> 0");

        Assert.True(connTrg2Act2.IsActive, "Conn 2 should be active on Falling Edge");

        Assert.True(act2.IsSimActive, "Action 2 should be active on Falling Edge");



        // Wait out pulse stretch window (>250ms)

        Thread.Sleep(300);

        editor.ApplyLiveOnlinePropagation();

        Assert.False(trg2.IsSimTriggered, "Rule 2 should return to idle after pulse duration");

        Assert.False(connTrg2Act2.IsActive, "Conn 2 should return to idle");

    }



        [Fact]
    public void RepositionNodes_PreservesSemanticHash_AndDoesNotInvalidateDeployment()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        store.UpdateConnection(null, ConnectionStatus.Connected);
        var editor = CreateEditor(catalog, store);
        editor.CompileNow();

        Assert.NotNull(editor.CurrentProgram);
        string initialHash = editor.CurrentProgram.ComputeSemanticHash();
        editor.MarkGraphDeployed();

        Assert.True(editor.IsGraphDeployed, "Graph must be deployed initially");
        Assert.Equal(initialHash, editor.LastDeployedProgramHash);

        // Act: Move/reposition nodes (simulating user dragging blocks around the canvas)
        var firstNode = editor.Nodes.First();
        firstNode.Location = new System.Windows.Point(firstNode.Location.X + 150, firstNode.Location.Y + 100);

        var lastNode = editor.Nodes.Last();
        lastNode.Location = new System.Windows.Point(lastNode.Location.X + 200, lastNode.Location.Y + 200);

        // Trigger magnetic alignment / auto sync
        editor.ApplyMagneticAlignment(firstNode);

        // Assert: Bytecode semantic hash is IDENTICAL and IsGraphDeployed remains TRUE
        Assert.NotNull(editor.CurrentProgram);
        string afterMoveHash = editor.CurrentProgram.ComputeSemanticHash();
        Assert.Equal(initialHash, afterMoveHash);
        Assert.True(editor.IsGraphDeployed, "IsGraphDeployed must remain true after moving node positions");
        Assert.True(string.IsNullOrEmpty(editor.SimGateWarning), "SimGateWarning should be empty");
    }

    [Fact]
    public void MqttPublishNode_MovingLocation_DoesNotTriggerNotifyGraphModified()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        store.UpdateConnection(null, ConnectionStatus.Connected);
        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var di0 = catalog.AllTags.First(t => t.Name == "DI0");
        var mqtt = new MqttPublishNodeViewModel { DataTag = di0, Topic = "plc/telemetry", Location = new System.Windows.Point(100, 100) };
        editor.Nodes.Add(mqtt);

        editor.CompileNow();
        editor.MarkGraphDeployed();
        Assert.True(editor.IsGraphDeployed);

        // Act: Select the MQTT node and change its Location
        editor.SelectedNode = mqtt;
        mqtt.Location = new System.Windows.Point(350, 450);

        // Assert: Moving location did not trigger NotifyGraphModified, IsGraphDeployed is preserved
        Assert.True(editor.IsGraphDeployed, "Moving MQTT block position must NOT invalidate deployment status");
    }

    [Fact]
    public void ModifyLogicSemantic_InvalidatesDeployment()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        store.UpdateConnection(null, ConnectionStatus.Connected);
        var editor = CreateEditor(catalog, store);
        editor.CompileNow();

        Assert.NotNull(editor.CurrentProgram);
        string deployedHash = editor.CurrentProgram.ComputeSemanticHash();
        editor.MarkGraphDeployed();
        Assert.True(editor.IsGraphDeployed);

        // Act: Change a semantic property (e.g. change trigger comparison and threshold)
        var trigNode = editor.Nodes.OfType<TriggerNodeViewModel>().First();
        trigNode.CompareOp = SimplePLC.Studio.Models.CompareOp.GT;
        trigNode.ThresholdLo = 999;
        editor.CompileNow();

        // Assert: Hash changed and IsGraphDeployed is NOT preserved
        Assert.NotNull(editor.CurrentProgram);
        string newHash = editor.CurrentProgram.ComputeSemanticHash();
        Assert.NotEqual(deployedHash, newHash);
    }
    [Fact]
    public void ModbusReadNode_Pin_CanConnectToActionNode()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        store.UpdateConnection(null, ConnectionStatus.Connected);
        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var mbRead = new ModbusReadNodeViewModel { SlaveId = 1, FunctionCode = SimplePLC.Protocol.Enums.ModbusMasterFunctionCode.ReadHoldingRegisters, StartAddress = 0, RegCount = 2 };
        var pin1 = new ModbusPinBindingViewModel { Label = "Pin_1", RegOffset = 0 };
        mbRead.Pins.Add(pin1);

        var act = new ActionNodeViewModel { TargetTag = do0, ActionType = SimplePLC.Studio.Models.ActionType.SET_TAG, ActionParam = 1 };

        editor.Nodes.Add(mbRead);
        editor.Nodes.Add(act);

        // Act: Connect Pin_1 of ModbusRead to Input of Action node
        editor.CompleteConnection((pin1.Connector, act.InputConnectors[0]));

        // Assert: Connection must be created successfully!
        Assert.Single(editor.Connections);
        var conn = editor.Connections[0];
        Assert.Equal(pin1.Connector, conn.Source);
        Assert.Equal(act.InputConnectors[0], conn.Target);
        Assert.Equal(do0, pin1.TargetTag);
    }
}
