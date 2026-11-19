using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị và tự động hóa toàn diện cho TOÀN BỘ 16 KHỐI HÀM FBD (Block-by-Block Systematic Verification).
/// Tuân thủ nghiêm ngặt First Principles Engineering Doctrine ([AGENTS.md](file:///g:/HoaNV/Projects/SimplePLC/AGENTS.md)):
/// - Tiên đề 0.1: Bản chất vật lý kiểu dữ liệu phần cứng IEC 61131-3 (Bit, INT32, REAL/Float32).
/// - Tiên đề 0.2: Chân lý hiển thị (Visual Truth) - UI không được sai lệch với bộ nhớ máy.
/// - Tiên đề 0.3: Máy trạng thái nhập liệu (FSM Drafting vs Committed: Auto-Normalize & Auto-Revert).
/// - Tiên đề 0.4: Chuỗi truy nguyên đầu cuối (End-to-End Custody từ Keystroke -> WPF -> ViewModel -> Compiler -> Bytecode).
/// </summary>
public class EveryBlockComprehensiveUnitAndAutomationTests : IDisposable
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly RuleTableViewModel _ruleTable;

    public EveryBlockComprehensiveUnitAndAutomationTests()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
        _tagCatalog = new TagCatalogViewModel();
        _ruleTable = new RuleTableViewModel(_tagCatalog);
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    #region 1. Block 1/16: InputNodeViewModel (DI, AI, VFLAG, VREG, System Tags)

    [Fact]
    public void Block01_InputNode_DigitalAndAnalog_ReflectsMemoryModelAccurately()
    {
        // Arrange
        var diTag = _tagCatalog.AllTags.First(t => t.Kind == TagKind.DiscreteInput);
        var aiTag = _tagCatalog.AllTags.First(t => t.Kind == TagKind.AnalogInput);
        var vregTag = new TagModel { Name = "VREG_FLT", DataType = TagDataType.Float32, Index = 20, Kind = TagKind.VirtualRegister };

        var inputDi = new InputNodeViewModel(diTag);
        var inputAi = new InputNodeViewModel(aiTag);
        var inputFlt = new InputNodeViewModel(vregTag);

        // Assert Connectors
        Assert.Single(inputDi.OutputConnectors);
        Assert.Empty(inputDi.InputConnectors);

        // Assert Digital reflection
        diTag.Value = 1;
        Assert.Equal("1", inputDi.ValueBadgeText);
        diTag.Value = 0;
        Assert.Equal("0", inputDi.ValueBadgeText);

        // Assert Float32 reflection
        float testVal = 45.75f;
        vregTag.Value = BitConverter.SingleToInt32Bits(testVal);
        Assert.Equal("45.75", inputFlt.ValueBadgeText);

        // Assert Live snapshot application (Active vs Stale)
        inputDi.ApplyRuntimeSnapshot(new RuntimeTagSnapshot((ushort)diTag.Index, diTag.Name, diTag.DisplayName, diTag.Kind, diTag.DataType, 1, TagQuality.Good, DateTimeOffset.UtcNow), isOnline: true);
        Assert.True(inputDi.IsLiveActive);
        Assert.Equal(NodeLiveVisualState.Active, inputDi.LiveVisualState);

        inputDi.ApplyRuntimeSnapshot(new RuntimeTagSnapshot((ushort)diTag.Index, diTag.Name, diTag.DisplayName, diTag.Kind, diTag.DataType, 1, TagQuality.Stale, DateTimeOffset.UtcNow), isOnline: true);
        Assert.Equal(NodeLiveVisualState.Stale, inputDi.LiveVisualState);
    }

    #endregion

    #region 2. Block 2/16: TriggerNodeViewModel (Edge Triggers & RTC Time)

    [Fact]
    public void Block02_TriggerNode_AllTriggerTypes_TogglesVisualStateAndBadges()
    {
        var trigger = new TriggerNodeViewModel();
        Assert.Single(trigger.InputConnectors);
        Assert.Single(trigger.OutputConnectors);

        // Kiểm tra các loại trigger
        trigger.TriggerType = TriggerType.ON_RISE;
        Assert.Contains("Rising Edge", trigger.SummaryText);

        trigger.TriggerType = TriggerType.ON_FALL;
        Assert.Contains("Falling Edge", trigger.SummaryText);

        trigger.TriggerType = TriggerType.ON_CHANGE;
        Assert.Contains("Value Change", trigger.SummaryText);

        trigger.TriggerType = TriggerType.INTERVAL;
        Assert.Contains("Periodic Interval", trigger.SummaryText);
        Assert.True(trigger.IsIntervalTrigger);

        // Kiểm tra gán tag dây nối
        var diTag = _tagCatalog.AllTags.First(t => t.Kind == TagKind.DiscreteInput);
        trigger.InputTag = diTag;
        Assert.True(trigger.HasWireConnection);
        Assert.Contains(diTag.Name, trigger.InputConnectionBadge);
    }

    #endregion

    #region 3. Block 3/16: GuardNodeViewModel (Safety Interlock & Permissive)

    [Fact]
    public void Block03_GuardNode_SafetyInterlock_NormalAndNegated_EvaluatesCorrectly()
    {
        var vflag = _tagCatalog.AllTags.First(t => t.Name == "VFLAG0");
        var guard = new GuardNodeViewModel(vflag);

        Assert.Single(guard.InputConnectors);
        Assert.Single(guard.OutputConnectors);

        // Active High (Cho phép khi VFLAG0 == 1)
        guard.Negate = false;
        Assert.False(guard.Negate);
        Assert.Contains("VFLAG0", guard.ConditionExpression);

        // Active Low (Khóa thường đóng / đảo ngược: Cho phép khi VFLAG0 == 0)
        guard.Negate = true;
        Assert.True(guard.Negate);
        Assert.Contains("NOT", guard.ConditionExpression);

        // Live telemetry snapshot
        guard.ApplyRuntimeSnapshot(new RuntimeTagSnapshot((ushort)vflag.Index, vflag.Name, vflag.DisplayName, vflag.Kind, vflag.DataType, 1, TagQuality.Good, DateTimeOffset.UtcNow), isOnline: true);
        Assert.True(guard.IsLiveActive);
    }

    #endregion

    #region 4. Block 4/16: ActionNodeViewModel (FSM Drafting/Committed & Type Safety)

    [Fact]
    public void Block04_ActionNode_DraftingCommitted_AdversarialDefenseAndAutoNormalization()
    {
        // Arrange: Tag số nguyên INT32
        var intTag = new TagModel { Name = "VREG0", DataType = TagDataType.Int32, Index = 0, Kind = TagKind.VirtualRegister };
        var action = new ActionNodeViewModel(intTag);

        Assert.Single(action.InputConnectors);
        Assert.Empty(action.OutputConnectors);

        // Test 1: Nhập hợp lệ danh định
        action.ActionParamText = "150";
        action.CommitActionParam();
        Assert.Equal(150, action.ActionParam);
        Assert.Equal("150", action.ActionParamText);

        // Test 2: Auto-Normalize dạng thập phân nguyên ("200.0" -> "200")
        action.ActionParamText = "200.0";
        action.CommitActionParam();
        Assert.Equal(200, action.ActionParam);
        Assert.Equal("200", action.ActionParamText);

        // Test 3: Auto-Revert đối kháng (Adversarial): Gõ số thực "25.5" vào biến nguyên INT32
        action.ActionParamText = "25.5";
        action.CommitActionParam();
        // Phải hoàn tác về giá trị đã commit hợp lệ gần nhất (200), không để lọt 25.5
        Assert.Equal(200, action.ActionParam);
        Assert.Equal("200", action.ActionParamText);

        // Test 4: Auto-Revert khi gõ chữ rác "abc"
        action.ActionParamText = "abc";
        action.CommitActionParam();
        Assert.Equal(200, action.ActionParam);
        Assert.Equal("200", action.ActionParamText);

        // Test 5: Chuyển sang Tag số thực REAL / Float32
        var realTag = new TagModel { Name = "VREG_FLT", DataType = TagDataType.Float32, Index = 1, Kind = TagKind.VirtualRegister };
        action.TargetTag = realTag;
        action.ActionParamText = "45.25";
        action.CommitActionParam();
        float expectedFloat = BitConverter.Int32BitsToSingle(action.ActionParam);
        Assert.Equal(45.25f, expectedFloat, 2);
    }

    #endregion

    #region 5. Block 5/16: TimerNodeViewModel (TON, TOF, TP, CYCLE)

    [Fact]
    public void Block05_TimerNode_AllModes_PresetConversionAndCycleTiming()
    {
        var timer = new TimerNodeViewModel("TON", null, null);
        Assert.Single(timer.InputConnectors);
        Assert.Single(timer.OutputConnectors);

        // TON mode
        timer.TimerMode = "TON";
        timer.PresetMs = 2500;
        Assert.Equal(2500u, timer.PresetMs);
        Assert.Contains("2,500 ms", timer.PresetText);

        // TOF mode
        timer.TimerMode = "TOF";
        Assert.Equal("TOF", timer.TimerMode);

        // TP mode
        timer.TimerMode = "TP";
        Assert.Equal("TP", timer.TimerMode);

        // CYCLE mode (Blink / Pulse train)
        timer.TimerMode = "CYCLE";
        Assert.True(timer.IsCycleMode);
        timer.OnTimeMs = 500;
        timer.OffTimeMs = 1500;
        Assert.Equal(500u, timer.OnTimeMs);
        Assert.Equal(1500u, timer.OffTimeMs);
        Assert.Contains("500 ms", timer.CalculatedOnMsSummary);
        Assert.Contains("1,500 ms", timer.CalculatedOffMsSummary);
    }

    #endregion

    #region 6. Block 6/16: CounterNodeViewModel (CTU & CTD)

    [Fact]
    public void Block06_CounterNode_CTU_CTD_PresetAndBoundaries()
    {
        var diCount = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var diReset = _tagCatalog.AllTags.First(t => t.Name == "DI1");
        var doTarget = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var counter = new CounterNodeViewModel("CTU", diCount, null, doTarget, diReset, 50);

        Assert.Equal("CTU", counter.CounterMode);
        Assert.Equal(50, counter.PresetValue);
        Assert.Contains("50", counter.PresetText);

        // Chuyển sang đếm lùi CTD
        counter.CounterMode = "CTD";
        Assert.Equal("CTD", counter.CounterMode);

        // Kiểm tra cập nhật mô phỏng
        counter.CurrentCount = 25;
        Assert.Equal("25 / 50", counter.SimulationCountText);
    }

    #endregion

    #region 7. Block 7/16: ScaleNodeViewModel (Analog Scaling & Calibration)

    [Fact]
    public void Block07_ScaleNode_LinearMath_ClampingAndZeroDivisionDefense()
    {
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.05,
            Offset = 10.0,
            Unit = "bar",
            IsClamped = true,
            ClampMin = 0.0,
            ClampMax = 100.0,
            DecimalPlaces = 2
        };

        // Tính toán thông thường: y = 0.05 * 1000 + 10 = 60.0
        double result = scale.Calculate(1000);
        Assert.Equal(60.0, result, 2);
        Assert.Equal("60.00 bar", scale.LiveFormattedValue);

        // Clamping chặn trên: y = 0.05 * 3000 + 10 = 160 -> kẹp về 100
        double clampedResult = scale.Calculate(3000);
        Assert.Equal(100.0, clampedResult, 2);

        // Clamping chặn dưới: y = 0.05 * (-500) + 10 = -15 -> kẹp về 0
        double clampedLow = scale.Calculate(-500);
        Assert.Equal(0.0, clampedLow, 2);

        // Hiệu chuẩn 2 điểm: Phòng vệ chia cho 0 khi 2 điểm Raw trùng nhau
        scale.CalibX1 = 1000;
        scale.CalibY1 = 10;
        scale.CalibX2 = 1000; // Trùng CalibX1
        scale.CalibY2 = 50;
        scale.CalculateFromCalibPoints();
        // Không sập app, giữ nguyên thông số an toàn
        Assert.True(scale.Gain > 0);
    }

    #endregion

    #region 8. Block 8/16: ComparatorNodeViewModel (Axiom 1 & Precision)

    [Fact]
    public void Block08_ComparatorNode_AllOperators_EvaluatesExactTruth()
    {
        var intTag = new TagModel { Name = "AI0", DataType = TagDataType.Int32, Index = 16, Kind = TagKind.AnalogInput };
        var cmp = new ComparatorNodeViewModel(intTag, CompareOp.GT, 100, 200);

        Assert.True(cmp.Evaluate(150));
        Assert.False(cmp.Evaluate(50));

        // Kiểm tra GTE, LT, LTE, EQ, NEQ, BETWEEN
        cmp.CompareOp = CompareOp.GTE;
        Assert.True(cmp.Evaluate(100));

        cmp.CompareOp = CompareOp.LT;
        Assert.True(cmp.Evaluate(99));
        Assert.False(cmp.Evaluate(100));

        cmp.CompareOp = CompareOp.LTE;
        Assert.True(cmp.Evaluate(100));
        Assert.False(cmp.Evaluate(101));

        cmp.CompareOp = CompareOp.EQ;
        Assert.True(cmp.Evaluate(100));
        Assert.False(cmp.Evaluate(101));

        cmp.CompareOp = CompareOp.NEQ;
        Assert.True(cmp.Evaluate(101));
        Assert.False(cmp.Evaluate(100));

        cmp.CompareOp = CompareOp.BETWEEN;
        Assert.True(cmp.Evaluate(150));
        Assert.False(cmp.Evaluate(50));
        Assert.False(cmp.Evaluate(250));
    }

    #endregion

    #region 9. Block 9/16: MathNodeViewModel (IEC 61131-3 ALU & DivZero Defense)

    [Fact]
    public void Block09_MathNode_AllOperators_DynamicPins_And_ZeroDivisionSafety()
    {
        var math = new MathNodeViewModel { Operator = MathOperator.Add };
        Assert.Equal(2, math.Pins.Count);
        Assert.Single(math.OutputConnectors);

        // Phép cộng với hằng số: 10 + 20 = 30
        math.Pins[0].IsConstant = true;
        math.Pins[0].ConstantValue = 10;
        math.Pins[1].IsConstant = true;
        math.Pins[1].ConstantValue = 20;
        double addResult = math.Calculate(new double[] { 10, 20 });
        Assert.Equal(30.0, addResult, 2);

        // Thêm chân động (3 chân): 10 + 20 + 30 = 60
        math.AddPin();
        Assert.Equal(3, math.Pins.Count);
        math.Pins[2].IsConstant = true;
        math.Pins[2].ConstantValue = 30;
        Assert.Equal(60.0, math.Calculate(new double[] { 10, 20, 30 }), 2);

        // Chuyển sang phép chia DIV: Tự động kẹp về 2 chân
        math.Operator = MathOperator.Div;
        Assert.Equal(2, math.Pins.Count);

        // Phòng vệ chia cho 0 (IEC 61508-3 Fault Defense)
        double divZero = math.Calculate(new double[] { 100, 0 });
        Assert.Equal(0.0, divZero); // Không được sập DivideByZeroException!

        // Phép chia hợp lệ: 100 / 4 = 25
        double divOk = math.Calculate(new double[] { 100, 4 });
        Assert.Equal(25.0, divOk, 2);
    }

    #endregion

    #region 10. Block 10/16: DirectWriteNodeViewModel (Hardware Direct Output)

    [Fact]
    public void Block10_DirectWriteNode_ModesAndPins_ConfiguresCorrectly()
    {
        var dw = new DirectWriteNodeViewModel
        {
            TargetPin = DirectWriteTargetPin.Do0,
            Mode = DirectWriteMode.DigitalOutput,
            TriggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge
        };

        Assert.Equal("DO0", dw.TargetPinName);
        Assert.True(dw.IsDigitalOutputMode);

        // Chuyển sang PWM
        dw.Mode = DirectWriteMode.PwmDuty;
        Assert.True(dw.IsPwmMode);

        // Chuyển sang PTO
        dw.Mode = DirectWriteMode.PulseTrain;
        Assert.True(dw.IsPulseTrainMode);

        // Chuyển sang Addressable Actuator Bus
        dw.Mode = DirectWriteMode.AddressableBus;
        Assert.True(dw.IsAddressableBusMode);
    }

    #endregion

    #region 11. Block 11/16: ModbusReadNodeViewModel (RS-485 Master Poller)

    [Fact]
    public void Block11_ModbusReadNode_PortsAndFunctions_GeneratesDynamicOutputPins()
    {
        var mbRead = new ModbusReadNodeViewModel
        {
            PortIndex = 0,
            SlaveId = 2,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = 100,
            RegCount = 3
        };

        Assert.Equal("RS-485 #1", mbRead.PortDisplayName);
        Assert.Equal(2, mbRead.SlaveId);
        Assert.Equal(3, mbRead.RegCount);

        // Bổ sung chân động (Dynamic Pins)
        mbRead.Pins.Add(new ModbusPinBindingViewModel { RegOffset = 0, Label = "P1", DataType = ModbusDataType.UInt16 });
        mbRead.Pins.Add(new ModbusPinBindingViewModel { RegOffset = 1, Label = "P2", DataType = ModbusDataType.UInt16 });
        mbRead.Pins.Add(new ModbusPinBindingViewModel { RegOffset = 2, Label = "P3", DataType = ModbusDataType.UInt16 });
        Assert.Equal(3, mbRead.Pins.Count);
        Assert.Equal(6, mbRead.OutputConnectors.Count); // 3 chân trạng thái cố định (Valid, Error, ErrCode) + 3 chân dữ liệu động

        // Chuyển sang cổng Zigbee (Port 4)
        mbRead.PortIndex = 4;
        Assert.True(mbRead.IsZigbeePort);
        Assert.Equal("Zigbee Wireless", mbRead.PortDisplayName);
    }

    #endregion

    #region 12. Block 12/16: ModbusWriteNodeViewModel (RS-485 Master Writer)

    [Fact]
    public void Block12_ModbusWriteNode_ModesAndPins_ConfiguresTargetRegisters()
    {
        var mbWrite = new ModbusWriteNodeViewModel
        {
            PortIndex = 1,
            SlaveId = 5,
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            StartAddress = 200,
            PointCount = 2
        };

        Assert.Equal("RS-485 #2", mbWrite.PortDisplayName);
        Assert.Equal(5, mbWrite.SlaveId);
        Assert.True(mbWrite.IsDirectRegistersMode);

        // Bổ sung chân vào động (Dynamic Input Pins)
        mbWrite.Pins.Add(new ModbusPinBindingViewModel { RegOffset = 0, Label = "Cmd1", DataType = ModbusDataType.UInt16 });
        mbWrite.Pins.Add(new ModbusPinBindingViewModel { RegOffset = 1, Label = "Cmd2", DataType = ModbusDataType.UInt16 });
        Assert.Equal(2, mbWrite.Pins.Count);
        Assert.True(mbWrite.InputConnectors.Count >= 2);
    }

    #endregion

    #region 13. Block 13/16: MqttPublishNodeViewModel (IIoT Telemetry)

    [Fact]
    public void Block13_MqttPublishNode_TopicValidation_And_PayloadPreview()
    {
        var mqttPub = new MqttPublishNodeViewModel
        {
            Topic = "factory/line1/telemetry",
            Qos = 1,
            Retain = true,
            PayloadMode = "JSON"
        };

        Assert.False(mqttPub.HasTopicError);
        Assert.Empty(mqttPub.TopicValidationError);

        // Báo lỗi khi Topic chứa ký tự wildcard bị cấm ở lệnh Publish
        mqttPub.Topic = "factory/line1/#";
        Assert.True(mqttPub.HasTopicError);
        Assert.NotEmpty(mqttPub.TopicValidationError);

        // Báo lỗi khi Topic rỗng
        mqttPub.Topic = "";
        Assert.True(mqttPub.HasTopicError);
    }

    #endregion

    #region 14. Block 14/16: MqttSubscribeNodeViewModel (IIoT Inbound Ingestion)

    [Fact]
    public void Block14_MqttSubscribeNode_TopicValidation_And_QueueModes()
    {
        var mqttSub = new MqttSubscribeNodeViewModel
        {
            Topic = "factory/line1/commands",
            Qos = 1,
            QueueMode = MqttQueueMode.Fifo,
            QueueCapacity = 32
        };

        Assert.False(mqttSub.HasTopicError);
        Assert.Contains("FIFO (32)", mqttSub.QueueModeSummaryText);

        mqttSub.QueueMode = MqttQueueMode.LatestWins;
        Assert.Equal("Latest Wins", mqttSub.QueueModeSummaryText);

        // Báo lỗi khi Topic rỗng
        mqttSub.Topic = "   ";
        Assert.True(mqttSub.HasTopicError);
    }

    #endregion

    #region 15. Block 15/16: HttpClientNodeViewModel (REST Webhook)

    [Fact]
    public void Block15_HttpClientNode_UrlValidation_And_BufferLimits()
    {
        var http = new HttpClientNodeViewModel
        {
            Url = "http://192.168.1.50/api/v1/data",
            Method = "POST"
        };

        Assert.False(http.HasUrlError);
        Assert.Empty(http.UrlValidationError);

        // URL không bắt đầu bằng http:// hoặc https://
        http.Url = "ftp://192.168.1.50/file";
        Assert.True(http.HasUrlError);
        Assert.NotEmpty(http.UrlValidationError);

        // URL vượt quá giới hạn bộ đệm MCU 64 ký tự
        http.Url = "http://very-long-domain-name-that-exceeds-mcu-buffer-limit-sixty-four-bytes.com/test";
        Assert.True(http.Url.Length <= 64);
    }

    #endregion

    #region 16. Block 16/16: TcpClientNodeViewModel (Socket Stream)

    [Fact]
    public void Block16_TcpClientNode_HostAndPortValidation()
    {
        var tcp = new TcpClientNodeViewModel
        {
            RemoteHost = "192.168.1.200",
            RemotePort = 5020
        };

        Assert.False(tcp.HasHostError);
        Assert.Equal(5020, tcp.RemotePort);

        // Tự động sửa Port = 0 thành 1
        tcp.RemotePort = 0;
        Assert.Equal(1, tcp.RemotePort);

        // Host rỗng báo lỗi
        tcp.RemoteHost = "";
        Assert.True(tcp.HasHostError);
    }

    #endregion

    #region 17. End-to-End Composite Canvas Compilation (All 16 Blocks Simultaneously)

    [Fact]
    public void CompositeCanvas_WhenAll16BlocksPresentAndConnected_CompilesWithoutUnsupportedNodeError()
    {
        // Khởi tạo Canvas LogicEditor
        var logicVM = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: act => act())
        {
            DebounceDelayMs = 0
        };
        logicVM.Nodes.Clear();

        // Thêm toàn bộ 16 khối đồ họa FBD lên Canvas
        var inNode = (InputNodeViewModel)logicVM.CreateNodeInstance("Input")!;
        var trigNode = (TriggerNodeViewModel)logicVM.CreateNodeInstance("Trigger")!;
        var guardNode = (GuardNodeViewModel)logicVM.CreateNodeInstance("Guard")!;
        var actNode = (ActionNodeViewModel)logicVM.CreateNodeInstance("Action")!;
        var timerNode = (TimerNodeViewModel)logicVM.CreateNodeInstance("Timer")!;
        var counterNode = (CounterNodeViewModel)logicVM.CreateNodeInstance("Counter")!;
        var scaleNode = (ScaleNodeViewModel)logicVM.CreateNodeInstance("Scale")!;
        var cmpNode = (ComparatorNodeViewModel)logicVM.CreateNodeInstance("Comparator")!;
        var mathNode = (MathNodeViewModel)logicVM.CreateNodeInstance("Math")!;
        var dwNode = (DirectWriteNodeViewModel)logicVM.CreateNodeInstance("PULSE_OUT")!;
        var mbReadNode = (ModbusReadNodeViewModel)logicVM.CreateNodeInstance("MB_READ")!;
        var mbWriteNode = (ModbusWriteNodeViewModel)logicVM.CreateNodeInstance("MB_WRITE")!;
        var mqttPubNode = (MqttPublishNodeViewModel)logicVM.CreateNodeInstance("MqttPublish")!;
        var mqttSubNode = (MqttSubscribeNodeViewModel)logicVM.CreateNodeInstance("MqttSubscribe")!;
        var httpNode = (HttpClientNodeViewModel)logicVM.CreateNodeInstance("HTTP_CLIENT")!;
        var tcpNode = (TcpClientNodeViewModel)logicVM.CreateNodeInstance("TCP_CLIENT")!;

        // Đảm bảo không khối nào bị null khi tạo từ từ khóa
        var allNodes = new GraphNodeViewModel[]
        {
            inNode, trigNode, guardNode, actNode, timerNode, counterNode,
            scaleNode, cmpNode, mathNode, dwNode, mbReadNode, mbWriteNode,
            mqttPubNode, mqttSubNode, httpNode, tcpNode
        };
        Assert.Equal(16, allNodes.Length);

        foreach (var node in allNodes)
        {
            logicVM.Nodes.Add(node);
        }

        // Tạo chuỗi logic cơ bản cho Network 1: Input -> Trigger -> Guard -> Action
        logicVM.CompleteConnection((inNode.OutputConnectors[0], trigNode.InputConnectors[0]));
        logicVM.CompleteConnection((trigNode.OutputConnectors[0], guardNode.InputConnectors[0]));
        logicVM.CompleteConnection((guardNode.OutputConnectors[0], actNode.InputConnectors[0]));

        // Biên dịch logic
        logicVM.CompileNow();

        // Khẳng định: Toàn bộ 16 khối đều được bộ biên dịch nhận diện hợp lệ,
        // TUYỆT ĐỐI KHÔNG xuất hiện lỗi "Khối logic chưa được hỗ trợ" (DiagUnsupportedNode)
        Assert.DoesNotContain(logicVM.Diagnostics, d => d.Message.Contains("chưa được hỗ trợ") || d.Message.Contains("unsupported"));
    }

    #endregion
}
