using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class MqttSubscribeNodeViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultConnectors()
    {
        var node = new MqttSubscribeNodeViewModel("spx/rack_01/cmd");

        Assert.Equal("MQTT SUB", node.Title);
        Assert.Equal("spx/rack_01/cmd", node.Topic);
        Assert.Empty(node.InputConnectors);

        Assert.Equal(2, node.OutputConnectors.Count);
        Assert.Equal("Valid", node.OutputConnectors[0].Title);
        Assert.Equal("Err", node.OutputConnectors[1].Title);
    }

    [Fact]
    public void AddPin_SynchronizesDynamicOutputConnector()
    {
        var node = new MqttSubscribeNodeViewModel();

        node.AddPin(new MqttPinBindingViewModel
        {
            Label = "grid",
            JsonPath = "grid",
            DataType = MqttDataType.Int16
        });

        Assert.Single(node.Pins);
        // 2 chân cố định (Valid, Err) + 1 chân động (grid) = 3 chân
        Assert.Equal(3, node.OutputConnectors.Count);
        Assert.Equal("grid", node.OutputConnectors[2].Title);
    }

    [Fact]
    public void RenamePin_UpdatesConnectorTitle()
    {
        var node = new MqttSubscribeNodeViewModel();
        var pin = new MqttPinBindingViewModel
        {
            Label = "old_name",
            JsonPath = "data"
        };
        node.AddPin(pin);

        Assert.Equal("old_name", node.OutputConnectors[2].Title);

        // Đổi tên nhãn của pin
        pin.Label = "target_grid";

        Assert.Equal("target_grid", node.OutputConnectors[2].Title);
    }

    [Fact]
    public void RemovePin_RemovesConnectorFromCanvas()
    {
        var node = new MqttSubscribeNodeViewModel();
        var pin = new MqttPinBindingViewModel { Label = "temp_pin" };
        node.AddPin(pin);

        Assert.Equal(3, node.OutputConnectors.Count);

        node.RemovePin(pin);

        Assert.Empty(node.Pins);
        Assert.Equal(2, node.OutputConnectors.Count);
    }

    [Fact]
    public void ProcessInboundPayload_ValidSortToLightMessage_UpdatesPinsAndCounters()
    {
        var node = new MqttSubscribeNodeViewModel("spx/sort");
        var pinGrid = new MqttPinBindingViewModel { Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16 };
        var pinColor = new MqttPinBindingViewModel { Label = "color", JsonPath = "color", DataType = MqttDataType.Int16 };
        node.AddPin(pinGrid);
        node.AddPin(pinColor);

        var result = node.ProcessInboundPayload("{\"grid\": 7, \"color\": 3}");

        Assert.True(result.IsSuccess);
        Assert.Equal(1u, node.MsgCount);
        Assert.Equal(0u, node.DroppedCount);
        Assert.True(node.IsValidActive);
        Assert.False(node.IsErrorActive);

        Assert.Equal(7, pinGrid.CurrentValue);
        Assert.Equal(3, pinColor.CurrentValue);
    }

    [Fact]
    public void ProcessInboundPayload_InvalidMessage_IncrementsDroppedCountAndSetsErrorCode()
    {
        var node = new MqttSubscribeNodeViewModel("spx/sort");
        var pinGrid = new MqttPinBindingViewModel { Label = "grid", JsonPath = "grid", DataType = MqttDataType.Int16, MinValue = 1, MaxValue = 18 };
        node.AddPin(pinGrid);

        // Ô 99 vượt quá MaxValue 18
        var result = node.ProcessInboundPayload("{\"grid\": 99}");

        Assert.False(result.IsSuccess);
        Assert.Equal(MqttErrorCode.OutOfRange, node.LastErrorCode);
        Assert.Equal(0u, node.MsgCount);
        Assert.Equal(1u, node.DroppedCount);
        Assert.False(node.IsValidActive);
        Assert.True(node.IsErrorActive);
    }

    [Fact]
    public void LogicEditor_CreateNodeInstance_MqttSubscribe_ReturnsValidNode()
    {
        var editor = new LogicEditorViewModel(new TagCatalogViewModel());
        var node = editor.CreateNodeInstance("MqttSubscribe");

        Assert.NotNull(node);
        Assert.IsType<MqttSubscribeNodeViewModel>(node);
        Assert.Equal("MQTT SUB", node.Title);
    }

    [Fact]
    public void LogicEditor_RoundtripPersistence_MqttSubscribe_PreservesAllConfigAndPins()
    {
        var catalog = new TagCatalogViewModel();
        var editor = new LogicEditorViewModel(catalog);

        var original = new MqttSubscribeNodeViewModel("factory/sort/line_1")
        {
            Qos = 2,
            QueueMode = MqttQueueMode.LatestWins,
            IgnoreRetained = false,
            FailSafeAction = MqttFailSafeAction.ResetToDefault,
            FailSafeTimeoutMs = 5000
        };

        original.Pins.Add(new MqttPinBindingViewModel("box_id", "data.box_id", MqttDataType.Int32, null, 1, 999999, 0));
        original.Pins.Add(new MqttPinBindingViewModel("color", "data.color", MqttDataType.Int16, null, 1, 8, 1));

        // Export/Save to ProjectNodeData
        var pData = LogicEditorViewModel.ExportSingleNodeData(original);

        Assert.Equal("MqttSubscribe", pData.Type);
        Assert.Equal("factory/sort/line_1", pData.MqttTopic);
        Assert.Equal(2, pData.MqttQos);
        Assert.Equal("LatestWins", pData.MqttSubQueueMode);
        Assert.False(pData.MqttSubIgnoreRetained);
        Assert.Equal(5000u, pData.MqttSubTimeoutMs);
        Assert.Equal("ResetToDefault", pData.MqttSubFailSafeAction);
        Assert.Equal(2, pData.MqttSubPins.Count);

        // Import/Load back
        var restored = editor.CreateNodeFromData(pData) as MqttSubscribeNodeViewModel;

        Assert.NotNull(restored);
        Assert.Equal("factory/sort/line_1", restored.Topic);
        Assert.Equal(2, restored.Qos);
        Assert.Equal(MqttQueueMode.LatestWins, restored.QueueMode);
        Assert.False(restored.IgnoreRetained);
        Assert.Equal(5000u, restored.FailSafeTimeoutMs);
        Assert.Equal(MqttFailSafeAction.ResetToDefault, restored.FailSafeAction);
        Assert.Equal(2, restored.Pins.Count);

        Assert.Equal("box_id", restored.Pins[0].Label);
        Assert.Equal("data.box_id", restored.Pins[0].JsonPath);
        Assert.Equal(MqttDataType.Int32, restored.Pins[0].DataType);
        Assert.Equal(1.0, restored.Pins[0].MinValue);
        Assert.Equal(999999.0, restored.Pins[0].MaxValue);

        Assert.Equal("color", restored.Pins[1].Label);
        Assert.Equal("data.color", restored.Pins[1].JsonPath);
        Assert.Equal(MqttDataType.Int16, restored.Pins[1].DataType);

        // Connectors on Canvas must match: 2 fixed + 2 dynamic = 4
        Assert.Equal(4, restored.OutputConnectors.Count);
        Assert.Equal("Valid", restored.OutputConnectors[0].Title);
        Assert.Equal("Err", restored.OutputConnectors[1].Title);
        Assert.Equal("box_id", restored.OutputConnectors[2].Title);
        Assert.Equal("color", restored.OutputConnectors[3].Title);
    }

    [Fact]
    public void Dialog_RunSimulation_UpdatesCurrentStringValue()
    {
        var node = new MqttSubscribeNodeViewModel();
        var pinGrid = new MqttPinBindingViewModel("grid", "grid", MqttDataType.Int16);
        var pinColor = new MqttPinBindingViewModel("color", "color", MqttDataType.Int16);
        node.Pins.Add(pinGrid);
        node.Pins.Add(pinColor);

        var dialogVm = new MqttSubConfigDialogViewModel(node, new TagCatalogViewModel());
        dialogVm.RunSimulation();

        Assert.Equal("5", pinGrid.CurrentStringValue);
        Assert.Equal("1", pinColor.CurrentStringValue);
    }

    [Fact]
    public void Dialog_AddPinAndRemovePin_PreservesUserCustomPayload()
    {
        var node = new MqttSubscribeNodeViewModel();
        var pinGrid = new MqttPinBindingViewModel("grid", "grid", MqttDataType.Int16);
        node.Pins.Add(pinGrid);

        var dialogVm = new MqttSubConfigDialogViewModel(node, new TagCatalogViewModel());

        // Người dùng dán chuỗi payload custom của họ
        string customUserPayload = "{ 'custom_command': { 'grid': 12, 'color': 3 } }";
        dialogVm.SimulationInput = customUserPayload;
        dialogVm.RunSimulation();

        // Kiểm tra đã bóc tách được trường đầu tiên
        Assert.Equal("12", pinGrid.CurrentStringValue);

        // Khi người dùng bấm thêm chân "color", chuỗi payload custom KHÔNG được phép bị ghi đè!
        dialogVm.AddPin();
        Assert.Equal(customUserPayload, dialogVm.SimulationInput);

        // Sau khi đổi tên chân mới thành "color", chạy lại bóc tách phải ra giá trị 3
        var addedPin = node.Pins[^1];
        addedPin.FieldName = "color";
        dialogVm.RunSimulation();

        Assert.Equal(customUserPayload, dialogVm.SimulationInput);
        Assert.Equal("3", addedPin.CurrentStringValue);

        // Khi xóa bớt 1 chân, chuỗi custom payload vẫn phải được bảo toàn nguyên vẹn
        dialogVm.RemovePin(addedPin);
        Assert.Equal(customUserPayload, dialogVm.SimulationInput);

        // Chỉ khi người dùng chủ động bấm "ResetSamplePayload" thì chuỗi mẫu mặc định mới được tạo lại
        dialogVm.ResetSamplePayload();
        Assert.NotEqual(customUserPayload, dialogVm.SimulationInput);
        Assert.Contains("SynaptiX_Controller", dialogVm.SimulationInput);
    }

    [Fact]
    public void QueueModeSummaryText_ReflectsModeAndCapacity()
    {
        var node = new MqttSubscribeNodeViewModel();
        Assert.Equal(MqttQueueMode.Fifo, node.QueueMode);
        Assert.Equal(16, node.QueueCapacity);
        Assert.Equal("FIFO (16)", node.QueueModeSummaryText);

        node.QueueCapacity = 8;
        Assert.Equal("FIFO (8)", node.QueueModeSummaryText);

        node.QueueMode = MqttQueueMode.LatestWins;
        Assert.Equal("Latest Wins", node.QueueModeSummaryText);

        node.QueueMode = MqttQueueMode.Fifo;
        Assert.Equal("FIFO (8)", node.QueueModeSummaryText);
    }
}


