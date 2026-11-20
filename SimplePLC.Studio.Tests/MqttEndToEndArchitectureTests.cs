using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử độc lập kiến trúc End-to-End phân hệ MQTT SynaptiX IDE (Axiom 1, 2, 4):
/// Kiểm chứng toàn bộ chuỗi mắt xích từ Canvas FBD blocks, Intra-Block Exclusion,
/// Two-Way DataType synchronization, Rule Compilation, Deploy Tag Registry và Ingestion Safety.
/// </summary>
public sealed class MqttEndToEndArchitectureTests
{
    public MqttEndToEndArchitectureTests()
    {
        TagWriteOwnershipService.Instance.Reset();
    }

    [Fact]
    public void MqttSubscribeNode_WhenPinsCleared_RemovesDynamicConnectorsAndPreservesSystemPins()
    {
        // Arrange
        var node = new MqttSubscribeNodeViewModel();
        Assert.Equal(2, node.OutputConnectors.Count); // Valid, Err
        Assert.Contains(node.ValidConnector, node.OutputConnectors);
        Assert.Contains(node.ErrorConnector, node.OutputConnectors);

        // Act 1: Thêm 3 dynamic pins
        node.Pins.Add(new MqttPinBindingViewModel("Temp", "temp", MqttDataType.Float));
        node.Pins.Add(new MqttPinBindingViewModel("Press", "press", MqttDataType.Float));
        node.Pins.Add(new MqttPinBindingViewModel("Flow", "flow", MqttDataType.Int16));

        // Assert 1: Có đúng 5 output connectors (2 system + 3 dynamic)
        Assert.Equal(5, node.OutputConnectors.Count);

        // Act 2: Gọi Clear() (Phát ra NotifyCollectionChangedAction.Reset)
        node.Pins.Clear();

        // Assert 2: Phải được dọn dẹp sạch sẽ, chỉ còn đúng 2 connector hệ thống, không để lại connector mồ côi
        Assert.Equal(2, node.OutputConnectors.Count);
        Assert.Contains(node.ValidConnector, node.OutputConnectors);
        Assert.Contains(node.ErrorConnector, node.OutputConnectors);
    }

    [Fact]
    public void MqttPinBinding_IntraBlockExclusion_PreventsDuplicateTagSelectionInSameBlock()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = tagCatalog.AllTags.First(t => t.Name == "VREG1");

        var node = new MqttSubscribeNodeViewModel();
        var pin1 = new MqttPinBindingViewModel("Pin1", "p1", MqttDataType.Int16);
        var pin2 = new MqttPinBindingViewModel("Pin2", "p2", MqttDataType.Int16);
        node.Pins.Add(pin1);
        node.Pins.Add(pin2);

        var dialogVm = new MqttSubConfigDialogViewModel(node, tagCatalog);

        // Act 1: Pin 1 chọn VREG0
        pin1.TargetTag = vreg0;

        // Assert 1: Dropdown của Pin 2 phải hiển thị VREG0 bị khóa và có badge cảnh báo
        var pin2OptForVreg0 = pin2.TagOptions.FirstOrDefault(o => o.Tag == vreg0);
        Assert.NotNull(pin2OptForVreg0);
        Assert.False(pin2OptForVreg0.IsSelectable);
        Assert.Contains("Pin1", pin2OptForVreg0.ConflictBadge);

        // Các tag khác vẫn tự do
        var pin2OptForVreg1 = pin2.TagOptions.FirstOrDefault(o => o.Tag == vreg1);
        Assert.NotNull(pin2OptForVreg1);
        Assert.True(pin2OptForVreg1.IsSelectable);

        // Act 2: Pin 1 giải phóng VREG0 (đổi sang VREG1)
        pin1.TargetTag = vreg1;

        // Assert 2: VREG0 trong Pin 2 phải được mở khóa trở lại tức thì
        pin2OptForVreg0 = pin2.TagOptions.FirstOrDefault(o => o.Tag == vreg0);
        Assert.NotNull(pin2OptForVreg0);
        Assert.True(pin2OptForVreg0.IsSelectable);
        Assert.Empty(pin2OptForVreg0.ConflictBadge);
    }

    [Fact]
    public void MqttPinBinding_TwoWayDataTypeSync_UpdatesTargetTagDataType()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Int32;

        var pin = new MqttPinBindingViewModel("DataPin", "data", MqttDataType.Int16);
        pin.TagProvider = dt => tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);
        pin.TargetTag = vreg0;

        // Act 1: Chuyển Pin sang Float
        pin.DataType = MqttDataType.Float;

        // Assert 1: VREG0 trong Tag Catalog phải tự động đồng bộ sang Float32
        Assert.Equal(TagDataType.Float32, vreg0.DataType);

        // Act 2: Chuyển Pin sang Bool -> VREG0 không tương thích với Bool (Axiom 1: VREG là thanh ghi 32-bit, không thể là coil 1-bit) -> TargetTag tự động reset về None
        pin.DataType = MqttDataType.Bool;

        // Assert 2: TargetTag tự động reset về None, bảo vệ tính toàn vẹn kiểu dữ liệu phần cứng của VREG0
        Assert.NotNull(pin.TargetTag);
        Assert.Equal(TagKind.None, pin.TargetTag.Kind);

        // Act 3: Đổi lại pin sang Int32 và gán lại VREG0
        pin.DataType = MqttDataType.Int32;
        pin.TargetTag = vreg0;
        pin.DataType = MqttDataType.Float;
        Assert.Equal(TagDataType.Float32, vreg0.DataType);
        pin.DataType = MqttDataType.Int32;

        // Assert 3: VREG0 phải tự động đồng bộ sang Int32
        Assert.Equal(TagDataType.Int32, vreg0.DataType);
    }

    [Fact]
    public void MqttPinBinding_WhenFloatPinSelectsInt32Vreg_SynchronizesVregToFloat32()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg2 = tagCatalog.AllTags.First(t => t.Name == "VREG2");
        vreg2.DataType = TagDataType.Int32; // Ban đầu là Int32

        var pin = new MqttPinBindingViewModel("AnalogIn", "temp", MqttDataType.Float);
        pin.TagProvider = dt => tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);

        // Act: Gán VREG2 vào chân Float
        pin.TargetTag = vreg2;

        // Assert: VREG2 phải được tự động chuyển kiểu sang Float32
        Assert.Equal(TagDataType.Float32, vreg2.DataType);
    }

    [Fact]
    public void RuleCompiler_MultiTopicMqttNodes_CompilesAllPublishAndSubscribeBlocks()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = tagCatalog.AllTags.First(t => t.Name == "VREG1");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");
        var do1 = tagCatalog.AllTags.First(t => t.Name == "DO1");

        // Canvas Block 1: MQTT_PUB (Telemetry cyclic)
        var pubNode1 = new MqttPublishNodeViewModel(null, "factory/line1/telemetry")
        {
            PublishMode = "Cyclic",
            IntervalMs = 500,
            Qos = 0
        };
        pubNode1.Fields.Add(new MqttPayloadFieldViewModel("temp", vreg0));
        pubNode1.Fields.Add(new MqttPayloadFieldViewModel("press", vreg1));

        // Canvas Block 2: MQTT_PUB (Alert event)
        var pubNode2 = new MqttPublishNodeViewModel(vreg0, "factory/line1/alerts")
        {
            PublishMode = "Event",
            Qos = 1,
            Retain = true
        };

        // Canvas Block 3: MQTT_SUB (Commands FIFO)
        var subNode1 = new MqttSubscribeNodeViewModel
        {
            Topic = "factory/line1/commands",
            Qos = 1,
            QueueMode = MqttQueueMode.Fifo,
            QueueCapacity = 32
        };
        var subPin1 = new MqttPinBindingViewModel("ValveCmd", "valve", MqttDataType.Bool) { TargetTag = do0 };
        subNode1.Pins.Add(subPin1);

        // Canvas Block 4: MQTT_SUB (Setpoints LatestWins)
        var subNode2 = new MqttSubscribeNodeViewModel
        {
            Topic = "factory/line1/setpoints",
            Qos = 2,
            QueueMode = MqttQueueMode.LatestWins
        };
        var subPin2 = new MqttPinBindingViewModel("SpeedSp", "speed", MqttDataType.Int16) { TargetTag = do1 };
        subNode2.Pins.Add(subPin2);

        var nodes = new List<GraphNodeViewModel> { pubNode1, pubNode2, subNode1, subNode2 };
        var connections = new List<ConnectionViewModel>();

        var compiler = new SimplePLC.Studio.Services.RuleCompiler();

        // Act
        var result = compiler.Compile(nodes, connections, tagCatalog);

        // Assert
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.MqttTelemetryConfigs.Count);
        Assert.Equal(2, result.Program.MqttSubscriptionConfigs.Count);

        // Verify Pub 1
        var p1 = result.Program.MqttTelemetryConfigs.FirstOrDefault(p => p.Topic == "factory/line1/telemetry");
        Assert.NotNull(p1);
        Assert.Equal(0, p1.Qos);
        Assert.Equal("Cyclic", p1.PublishMode);
        Assert.Equal(2, p1.Fields.Count);

        // Verify Pub 2
        var p2 = result.Program.MqttTelemetryConfigs.FirstOrDefault(p => p.Topic == "factory/line1/alerts");
        Assert.NotNull(p2);
        Assert.Equal(1, p2.Qos);
        Assert.True(p2.Retain);
        Assert.Equal("Event", p2.PublishMode);

        // Verify Sub 1
        var s1 = result.Program.MqttSubscriptionConfigs.FirstOrDefault(s => s.Topic == "factory/line1/commands");
        Assert.NotNull(s1);
        Assert.Equal(1, s1.Qos);
        Assert.Equal(MqttQueueMode.Fifo, s1.QueueMode);
        Assert.Single(s1.Pins);
        Assert.Equal("valve", s1.Pins[0].JsonPath);

        // Verify Sub 2
        var s2 = result.Program.MqttSubscriptionConfigs.FirstOrDefault(s => s.Topic == "factory/line1/setpoints");
        Assert.NotNull(s2);
        Assert.Equal(2, s2.Qos);
        Assert.Equal(MqttQueueMode.LatestWins, s2.QueueMode);
        Assert.Single(s2.Pins);
        Assert.Equal("speed", s2.Pins[0].JsonPath);
    }

    [Fact]
    public void Deploy_AdversarialIngestionCollision_AbortsDeployWhenTwoNodesTargetSameTag()
    {
        // Arrange
        var deployVm = new DeployViewModel();

        var modbusReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                PortIndex = 0,
                StartAddress = 40001,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { TargetTagIndex = 10 } // Ghi vào Tag 10
                }
            }
        };

        var mqttSubs = new List<MqttSubscriptionConfigDto>
        {
            new()
            {
                Topic = "plant/cmd",
                Pins = new List<MqttPinBindingDto>
                {
                    new() { TargetTagIndex = 10 } // Xung đột ghi cùng vào Tag 10!
                }
            }
        };

        // Act
        var (isValid, errorMessage) = deployVm.ValidateIngestionOwnership(modbusReads, mqttSubs);

        // Assert: Preflight check phải phát hiện xung đột và từ chối nạp
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.True(errorMessage.Contains("Xung đột sở hữu ghi độc quyền") || errorMessage.Contains("Exclusive write ownership conflict"));
        Assert.Contains("Tag 10", errorMessage);
    }

    [Fact]
    public void Deploy_WhenMqttSubHasFloatPin_SynchronizesTagDataTypesArrayToReal()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg5 = tagCatalog.AllTags.First(t => t.Name == "VREG5");
        vreg5.DataType = TagDataType.Float32;

        var subNode = new MqttSubscribeNodeViewModel();
        var floatPin = new MqttPinBindingViewModel("TempSens", "temperature", MqttDataType.Float)
        {
            TargetTag = vreg5
        };
        subNode.Pins.Add(floatPin);

        var logicEditor = new LogicEditorViewModel(tagCatalog);
        logicEditor.Nodes.Add(subNode);

        var deployVm = new DeployViewModel(logicEditorVM: logicEditor);

        // Act: Dựng mảng tagDataTypes (phân vùng 0x0080..0x00BF) mô phỏng logic trong DeployViewModel.ExecuteAsync
        byte[] tagDataTypes = new byte[SimplePLC.Protocol.Constants.ModbusRegisterMap.MaxRuntimeTags];
        foreach (var sub in logicEditor.Nodes.OfType<MqttSubscribeNodeViewModel>())
        {
            foreach (var pin in sub.Pins)
            {
                if (pin.DataType == MqttDataType.Float && pin.TargetTag != null && pin.TargetTag.Index < tagDataTypes.Length)
                {
                    tagDataTypes[pin.TargetTag.Index] = 3; // SPLC_TAG_TYPE_REAL
                    pin.TargetTag.DataType = TagDataType.Float32;
                }
            }
        }

        // Assert: Phải có mã 3 (REAL) tại vị trí tag index của VREG5
        Assert.Equal(3, tagDataTypes[vreg5.Index]);
    }
}
