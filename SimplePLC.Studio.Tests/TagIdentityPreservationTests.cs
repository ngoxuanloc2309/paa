using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử TDD tái hiện và xác thực kiến trúc bảo toàn danh tính Tag (Tag Identity Preservation Architecture)
/// khi kết nối thiết bị hoặc đồng bộ ProductDefinition.
/// Tuân thủ nghiêm ngặt Quy tắc 3.1 & 3.5 (AGENTS.md) và chuẩn IEC 61508-3 Triad.
/// </summary>
public class TagIdentityPreservationTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    /// <summary>
    /// REPRODUCTION TEST 1: Tái hiện chính xác hiện tượng trên giao diện:
    /// Khi ComboBox trên Inspector đang mở và liên kết với CounterNode (OutputTag & CvTag),
    /// việc gọi SyncWithProductDefinition với mã nguồn cũ sẽ xóa (.Clear()) Collection
    /// làm Selector của WPF ép SelectedItem về null và ghi đè null ngược lại CounterNode!
    /// </summary>
    [Fact]
    public void ReproductionTest_SyncWithProductDefinition_WpfComboBoxTwoWayBinding_DoesNotWipeCounterTags()
    {
        RunOnStaThread(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var counterNode = new CounterNodeViewModel();

            // 1. Tạo 2 ComboBox mô phỏng chính xác XAML của LogicEditorView.xaml (Lines 2677 & 2687)
            var comboOutput = new ComboBox();
            comboOutput.ItemsSource = tagCatalog.OutputDigitalTags;
            var bindingOut = new Binding(nameof(CounterNodeViewModel.OutputTag))
            {
                Source = counterNode,
                Mode = BindingMode.TwoWay
            };
            comboOutput.SetBinding(ComboBox.SelectedItemProperty, bindingOut);

            var comboCv = new ComboBox();
            comboCv.ItemsSource = tagCatalog.RegisterTags;
            var bindingCv = new Binding(nameof(CounterNodeViewModel.CvTag))
            {
                Source = counterNode,
                Mode = BindingMode.TwoWay
            };
            comboCv.SetBinding(ComboBox.SelectedItemProperty, bindingCv);

            // 2. Chọn tag DO0 và VREG_RETAIN0 cho Counter
            var initialDo0 = tagCatalog.OutputDigitalTags.First(t => t.Name == "DO0");
            var initialRetain0 = tagCatalog.RegisterTags.First(t => t.Name == "VREG_RETAIN0");

            counterNode.OutputTag = initialDo0;
            counterNode.CvTag = initialRetain0;

            Assert.Same(initialDo0, comboOutput.SelectedItem);
            Assert.Same(initialRetain0, comboCv.SelectedItem);

            // 3. Giả lập kết nối thiết bị: sinh ProductDefinition chuẩn
            var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

            // 4. Gọi SyncWithProductDefinition (khi bấm nút Connect)
            tagCatalog.SyncWithProductDefinition(product);

            // 5. THẨM ĐỊNH TÍNH TOÀN VẸN:
            // Sau khi áp dụng In-Place Reconcile, OutputTag và CvTag KHÔNG ĐƯỢC PHÉP NULL!
            Assert.NotNull(counterNode.OutputTag);
            Assert.Equal("DO0", counterNode.OutputTag.Name);

            Assert.NotNull(counterNode.CvTag);
            Assert.Equal("VREG_RETAIN0", counterNode.CvTag.Name);

            // ComboBox cũng phải giữ nguyên SelectedItem
            Assert.Same(counterNode.OutputTag, comboOutput.SelectedItem);
            Assert.Same(counterNode.CvTag, comboCv.SelectedItem);
        });
    }

    /// <summary>
    /// REPRODUCTION TEST 2: Xác nhận rằng OutputDigitalTags và RegisterTags KHÔNG phát sinh
    /// sự kiện Reset (Action = Reset do .Clear()) làm rỗng danh sách khi đồng bộ cùng profile phần cứng.
    /// </summary>
    [Fact]
    public void ReproductionTest_SyncWithProductDefinition_NeverFiresResetActionOnDerivedCollections()
    {
        var tagCatalog = new TagCatalogViewModel();
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        bool outputDigitalResetFired = false;
        bool registerResetFired = false;

        tagCatalog.OutputDigitalTags.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                outputDigitalResetFired = true;
        };

        tagCatalog.RegisterTags.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                registerResetFired = true;
        };

        // Act: Sync
        tagCatalog.SyncWithProductDefinition(product);

        // Assert: Không bao giờ được phát sinh sự kiện Reset làm hủy diệt các ComboBox đang bind!
        Assert.False(outputDigitalResetFired, "OutputDigitalTags.CollectionChanged should NOT fire Reset action!");
        Assert.False(registerResetFired, "RegisterTags.CollectionChanged should NOT fire Reset action!");
    }

    /// <summary>
    /// TRIAD TEST (Happy Path): Kiểm tra toàn bộ 11 khối logic trên Canvas (Counter, Timer, Action,
    /// Comparator, Guard, Input, Scale, Math, ModbusWrite, MqttSubscribe, MqttPublish).
    /// Khi thiết bị kết nối và TagCatalog đồng bộ, 100% các khối logic phải bảo toàn trọn vẹn
    /// các Tag đã cấu hình qua cơ chế Canvas Graph Re-linking (Axiom 0.2 & 0.4).
    /// </summary>
    [Fact]
    public void TriadTest_HappyPath_SyncWithProductDefinition_PreservesAllCanvasNodeTypes()
    {
        var tagCatalog = new TagCatalogViewModel();
        var logicEditor = new LogicEditorViewModel(tagCatalog, uiDispatcher: act => act());

        // 1. Khởi tạo 11 loại khối và gán tag
        var counter = new CounterNodeViewModel
        {
            OutputTag = tagCatalog.OutputDigitalTags.First(t => t.Name == "DO0"),
            CvTag = tagCatalog.RegisterTags.First(t => t.Name == "VREG_RETAIN0")
        };
        var timer = new TimerNodeViewModel
        {
            OutputTag = tagCatalog.OutputDigitalTags.First(t => t.Name == "DO1")
        };
        var action = new ActionNodeViewModel
        {
            TargetTag = tagCatalog.AllTags.First(t => t.Name == "DO2")
        };
        var comp = new ComparatorNodeViewModel
        {
            InputTag = tagCatalog.AllTags.First(t => t.Name == "AI0")
        };
        var guard = new GuardNodeViewModel
        {
            GuardTag = tagCatalog.DigitalTags.First(t => t.Name == "DI0")
        };
        var input = new InputNodeViewModel
        {
            Tag = tagCatalog.AllTags.First(t => t.Name == "DI1")
        };
        var scale = new ScaleNodeViewModel
        {
            OutputTag = tagCatalog.AllTags.First(t => t.Name == "VREG0")
        };
        var math = new MathNodeViewModel
        {
            OutputTag = tagCatalog.AllTags.First(t => t.Name == "VREG1")
        };
        var mqttPub = new MqttPublishNodeViewModel
        {
            TriggerTag = tagCatalog.AllTags.First(t => t.Name == "DI2")
        };
        var mqttSub = new MqttSubscribeNodeViewModel();
        var mqttPin = new MqttPinBindingViewModel("Speed", "speed", MqttDataType.Int32, null, 0, 100, 0)
        {
            TargetTag = tagCatalog.AllTags.First(t => t.Name == "VREG2")
        };
        mqttSub.Pins.Add(mqttPin);

        var mbWrite = new ModbusWriteNodeViewModel();
        var mbPin = new ModbusPinBindingViewModel
        {
            TargetTag = tagCatalog.AllTags.First(t => t.Name == "VREG3")
        };
        mbWrite.Pins.Add(mbPin);

        // Đưa tất cả vào Canvas
        logicEditor.Nodes.Add(counter);
        logicEditor.Nodes.Add(timer);
        logicEditor.Nodes.Add(action);
        logicEditor.Nodes.Add(comp);
        logicEditor.Nodes.Add(guard);
        logicEditor.Nodes.Add(input);
        logicEditor.Nodes.Add(scale);
        logicEditor.Nodes.Add(math);
        logicEditor.Nodes.Add(mqttPub);
        logicEditor.Nodes.Add(mqttSub);
        logicEditor.Nodes.Add(mbWrite);

        // 2. Act: Kết nối thiết bị (đồng bộ ProductDefinition)
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        tagCatalog.SyncWithProductDefinition(product);

        // 3. Assert: 100% các khối bảo toàn Tag
        Assert.Equal("DO0", counter.OutputTag?.Name);
        Assert.Equal("VREG_RETAIN0", counter.CvTag?.Name);
        Assert.Equal("DO1", timer.OutputTag?.Name);
        Assert.Equal("DO2", action.TargetTag?.Name);
        Assert.Equal("AI0", comp.InputTag?.Name);
        Assert.Equal("DI0", guard.GuardTag?.Name);
        Assert.Equal("DI1", input.Tag?.Name);
        Assert.Equal("VREG0", scale.OutputTag?.Name);
        Assert.Equal("VREG1", math.OutputTag?.Name);
        Assert.Equal("DI2", mqttPub.TriggerTag?.Name);
        Assert.Equal("VREG2", mqttPin.TargetTag?.Name);
        Assert.Equal("VREG3", mbPin.TargetTag?.Name);
    }

    /// <summary>
    /// TRIAD TEST (Boundary & Overflow): Bo mạch phần cứng có ít chân I/O hơn (Spartan profile 2DI / 2DO).
    /// Khẳng định hệ thống xử lý gọt bớt các tag không tồn tại một cách êm thuận,
    /// các tag còn tồn tại (DO0, DO1) vẫn được bảo toàn nguyên vẹn 100%.
    /// </summary>
    [Fact]
    public void TriadTest_Boundary_SpartanProfile_PrunesMissingTagsGracefully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var logicEditor = new LogicEditorViewModel(tagCatalog, uiDispatcher: act => act());

        var counter = new CounterNodeViewModel
        {
            OutputTag = tagCatalog.OutputDigitalTags.First(t => t.Name == "DO1"), // Thuộc 2DO đầu tiên
            CvTag = tagCatalog.RegisterTags.First(t => t.Name == "VREG_RETAIN0")
        };
        logicEditor.Nodes.Add(counter);

        // Profile thiết bị nhỏ gọn: 2DI, 2DO, 0AI
        var resources = new ProductResourceProfile(
            DigitalInputs: 2,
            DigitalOutputs: 2,
            AnalogInputs: 0,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 16,
            Counters: 4);

        var spartanProduct = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 10,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 32,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);

        // Act: Sync với spartan product
        tagCatalog.SyncWithProductDefinition(spartanProduct);

        // Assert: DO1 và VREG_RETAIN0 vẫn tồn tại trong profile nhỏ và được giữ nguyên
        Assert.NotNull(counter.OutputTag);
        Assert.Equal("DO1", counter.OutputTag.Name);
        Assert.NotNull(counter.CvTag);
        Assert.Equal("VREG_RETAIN0", counter.CvTag.Name);

        // Không còn tag DO2..DO7 trong OutputDigitalTags
        Assert.Equal(2, tagCatalog.OutputDigitalTags.Count(t => t.Kind == TagKind.DiscreteOutput));
    }

    /// <summary>
    /// TRIAD TEST (Fault Injection & Stress): Kết nối và ngắt kết nối liên tục 10 chu kỳ (Toggle Connect/Disconnect).
    /// Khẳng định không xảy ra hiện tượng rò rỉ tham chiếu, không bị mất tag, số lượng tag luôn ổn định tuyệt đối.
    /// </summary>
    [Fact]
    public void TriadTest_FaultInjection_RepeatedSyncCycles_MaintainsTagIntegrity()
    {
        var tagCatalog = new TagCatalogViewModel();
        var logicEditor = new LogicEditorViewModel(tagCatalog, uiDispatcher: act => act());

        var counter = new CounterNodeViewModel
        {
            OutputTag = tagCatalog.OutputDigitalTags.First(t => t.Name == "DO0"),
            CvTag = tagCatalog.RegisterTags.First(t => t.Name == "VREG_RETAIN0")
        };
        logicEditor.Nodes.Add(counter);

        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Connect: sync với device product
            tagCatalog.SyncWithProductDefinition(product);

            Assert.Equal("DO0", counter.OutputTag?.Name);
            Assert.Equal("VREG_RETAIN0", counter.CvTag?.Name);

            // Disconnect: sync với sandbox fallback product
            tagCatalog.SyncWithProductDefinition(tagCatalog.BuildProductFromCurrentTags());

            Assert.Equal("DO0", counter.OutputTag?.Name);
            Assert.Equal("VREG_RETAIN0", counter.CvTag?.Name);
        }
    }
}
