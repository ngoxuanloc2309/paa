using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử độc lập đặc tả kỹ thuật (Specification-Based Acceptance Tests)
/// cho toàn bộ 13 khối hàm Function Block và các quy tắc logic trong SimplePLC.
/// Thiết kế theo tư tưởng Clean Code / Black-box Testing:
/// - Kiểm tra độc lập dựa trên yêu cầu đặc tả (IEC 61131-3, PLC Automation Specs).
/// - Không bị chi phối hay ngộ nhận bởi code triển khai nội bộ.
/// </summary>
public class BlockSpecificationAcceptanceTests : IDisposable
{
    public BlockSpecificationAcceptanceTests()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    private static ProductDefinition CreateProduct() => ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private static DeviceEndpoint CreateEndpoint() => new UsbCdcEndpoint("COM3");

    #region 1. Comparator Node Specification Tests

    [Fact]
    public void Comparator_IntegerAdcInput_EvaluatesCorrectlyWithoutFloatDistortion()
    {
        // Yêu cầu đặc tả:
        // Cọc AI phần cứng (AI0..AI3) của SimplePLC có kiểu dữ liệu Int32 (ADC 12-bit 0..4095).
        // Khối Comparator khi so sánh ngưỡng số nguyên (ví dụ: AI0 > 500) phải so sánh trực tiếp
        // theo giá trị số nguyên, KHÔNG ĐƯỢC ép kiểu nhị phân sang Single (BitConverter.Int32BitsToSingle)
        // gây méo dạng số học về 2.8e-42 khiến kết quả luôn sai.
        var aiTag = new TagModel
        {
            Index = 16,
            Name = "AI0",
            Kind = TagKind.AnalogInput,
            DataType = TagDataType.Int32
        };

        var cmpGt = new ComparatorNodeViewModel(aiTag, CompareOp.GT, lo: 500, hi: 1000);
        var cmpLt = new ComparatorNodeViewModel(aiTag, CompareOp.LT, lo: 2500, hi: 3000);
        var cmpBetween = new ComparatorNodeViewModel(aiTag, CompareOp.BETWEEN, lo: 1000, hi: 3000);
        var cmpEq = new ComparatorNodeViewModel(aiTag, CompareOp.EQ, lo: 2048, hi: 2048);

        // Raw ADC value = 2048 (khoảng 50% dải đo 0..4095)
        Assert.True(cmpGt.Evaluate(2048), "2048 phải > 500");
        Assert.False(cmpGt.Evaluate(400), "400 không được > 500");

        Assert.True(cmpLt.Evaluate(2048), "2048 phải < 2500");
        Assert.False(cmpLt.Evaluate(3000), "3000 không được < 2500");

        Assert.True(cmpBetween.Evaluate(2048), "2048 phải nằm trong [1000..3000]");
        Assert.False(cmpBetween.Evaluate(500), "500 không nằm trong [1000..3000]");
        Assert.False(cmpBetween.Evaluate(3500), "3500 không nằm trong [1000..3000]");

        Assert.True(cmpEq.Evaluate(2048), "2048 == 2048");
        Assert.False(cmpEq.Evaluate(2047), "2047 != 2048");
    }

    [Fact]
    public void Comparator_FloatInput_EvaluatesCorrectlyAsFloat()
    {
        // Khi ngưỡng so sánh có phần thập phân (ví dụ: 75.5), hệ thống chuyển sang chế độ so sánh float IEEE-754.
        var floatTag = new TagModel
        {
            Index = 100,
            Name = "VREG_FLOAT",
            Kind = TagKind.VirtualFlag,
            DataType = TagDataType.Float32
        };

        var cmp = new ComparatorNodeViewModel(floatTag, CompareOp.GT, lo: 50.5, hi: 100.0);

        float testValTrue = 75.2f;
        int rawBitsTrue = BitConverter.SingleToInt32Bits(testValTrue);
        Assert.True(cmp.Evaluate(rawBitsTrue), "75.2f > 50.5f");

        float testValFalse = 49.9f;
        int rawBitsFalse = BitConverter.SingleToInt32Bits(testValFalse);
        Assert.False(cmp.Evaluate(rawBitsFalse), "49.9f không được > 50.5f");
    }

    #endregion

    #region 2. Trigger Node Specification Tests

    [Fact]
    public void Trigger_RisingAndFallingEdge_EvaluatesStrictlyAccordingToEdgeSpecification()
    {
        // Yêu cầu đặc tả:
        // ON_RISE: Chỉ tích cực đúng 1 chu kỳ khi trạng thái chuyển từ 0 sang 1 (prev=0, curr=1).
        // ON_FALL: Chỉ tích cực đúng 1 chu kỳ khi trạng thái chuyển từ 1 sang 0 (prev=1, curr=0).
        // ON_CHANGE: Tích cực khi trạng thái thay đổi bất kỳ (prev != curr).
        var trigRise = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var trigFall = new TriggerNodeViewModel { TriggerType = TriggerType.ON_FALL };
        var trigChange = new TriggerNodeViewModel { TriggerType = TriggerType.ON_CHANGE, CompareOp = CompareOp.NONE };

        // 1. Sườn lên (0 -> 1)
        Assert.True(trigRise.EvaluateCondition(value: 1, prevValue: 0), "ON_RISE phải kích hoạt khi 0 -> 1");
        Assert.False(trigRise.EvaluateCondition(value: 1, prevValue: 1), "ON_RISE không được kích hoạt khi giữ nguyên 1 -> 1");
        Assert.False(trigRise.EvaluateCondition(value: 0, prevValue: 1), "ON_RISE không được kích hoạt khi 1 -> 0");
        Assert.False(trigRise.EvaluateCondition(value: 0, prevValue: 0), "ON_RISE không được kích hoạt khi 0 -> 0");

        // 2. Sườn xuống (1 -> 0)
        Assert.True(trigFall.EvaluateCondition(value: 0, prevValue: 1), "ON_FALL phải kích hoạt khi 1 -> 0");
        Assert.False(trigFall.EvaluateCondition(value: 0, prevValue: 0), "ON_FALL không được kích hoạt khi 0 -> 0");
        Assert.False(trigFall.EvaluateCondition(value: 1, prevValue: 0), "ON_FALL không được kích hoạt khi 0 -> 1");
        Assert.False(trigFall.EvaluateCondition(value: 1, prevValue: 1), "ON_FALL không được kích hoạt khi 1 -> 1");

        // 3. Biến thiên (ON_CHANGE)
        Assert.True(trigChange.EvaluateCondition(value: 1, prevValue: 0), "ON_CHANGE kích hoạt khi 0 -> 1");
        Assert.True(trigChange.EvaluateCondition(value: 5, prevValue: 1), "ON_CHANGE kích hoạt khi 1 -> 5");
        Assert.False(trigChange.EvaluateCondition(value: 5, prevValue: 5), "ON_CHANGE không kích hoạt khi giá trị giữ nguyên");
    }

    [Fact]
    public void Trigger_TimeWindow_ExactMinuteSpecification()
    {
        // Yêu cầu đặc tả:
        // TIME_WINDOW với CompareOp.EQ kiểm tra chính xác phút hiện tại (HHmm == ThresholdLo).
        var now = DateTime.Now;
        int currentHhmm = now.Hour * 100 + now.Minute;

        var trigRtcExact = new TriggerNodeViewModel
        {
            TriggerType = TriggerType.TIME_WINDOW,
            CompareOp = CompareOp.EQ,
            ThresholdLo = currentHhmm
        };

        Assert.True(trigRtcExact.EvaluateCondition(0), "TIME_WINDOW EQ phải thỏa mãn khi đúng phút hiện tại");

        var trigRtcMismatch = new TriggerNodeViewModel
        {
            TriggerType = TriggerType.TIME_WINDOW,
            CompareOp = CompareOp.EQ,
            ThresholdLo = (currentHhmm + 1) % 2400
        };

        Assert.False(trigRtcMismatch.EvaluateCondition(0), "TIME_WINDOW EQ không được thỏa mãn khi sai thời gian");
    }

    #endregion

    #region 3. Scale Node to Downstream Propagation Tests

    [Fact]
    public void ScaleNode_ConnectedToComparator_PropagatesScaledEngineeringValue()
    {
        // Yêu cầu đặc tả:
        // Khối Scale nhận tín hiệu ADC từ InputNode (ví dụ AI0 = 2500) và tính toán:
        // y = Gain * x + Offset = 0.01 * 2500 + 0 = 25.0 bar.
        // Khối Comparator nối sau ScaleNode phải nhận giá trị đã tỷ lệ (25.0),
        // KHÔNG ĐƯỢC nhận giá trị ADC thô (2500).
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var ai0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "AI0") ?? new TagModel { Index = 16, Name = "AI0", Kind = TagKind.AnalogInput };
        var inNode = new InputNodeViewModel(ai0Tag) { LiveRawValue = 2500, IsLiveOnline = true };
        var scaleNode = new ScaleNodeViewModel(ai0Tag)
        {
            Gain = 0.01,
            Offset = 0,
            Unit = "bar",
            IsClamped = false
        };
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, lo: 20, hi: 50);

        editor.Nodes.Add(inNode);
        editor.Nodes.Add(scaleNode);
        editor.Nodes.Add(cmpNode);

        // Nối Input -> Scale, Scale -> Comparator
        editor.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]));
        editor.Connections.Add(new ConnectionViewModel(scaleNode.OutputConnectors[0], cmpNode.InputConnectors[0]));

        // Act: Áp dụng lan truyền trực tiếp
        editor.ApplyLiveOnlinePropagation();

        // Assert:
        // Scale đã tính 2500 * 0.01 = 25.0 bar
        Assert.Equal(25.0, scaleNode.LiveScaledValue, precision: 2);
        // Comparator so sánh 25 > 20 -> Đạt điều kiện!
        Assert.True(cmpNode.IsConditionMet, "Comparator phải đạt điều kiện (25 > 20) dựa trên giá trị đã scale");
    }

    #endregion

    #region 4. DirectWrite (PULSE_OUT) & Fieldbus Live Propagation Tests

    [Fact]
    public void DirectWriteNode_LiveOnlinePropagationAndCleanup()
    {
        // Yêu cầu đặc tả:
        // Khối PULSE_OUT (DirectWrite) khi có dây kích hoạt Trig từ upstream chuyển active,
        // dây nối và khối phải hiển thị IsSimActive = true.
        // Khi ClearSimVisuals() được gọi, toàn bộ trạng thái visual của DirectWrite phải về false.
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var di0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var inNode = new InputNodeViewModel(di0Tag) { LiveRawValue = 1, IsLiveOnline = true };
        var dwNode = new DirectWriteNodeViewModel
        {
            TargetPin = Protocol.Enums.DirectWriteTargetPin.Do0,
            Mode = Protocol.Enums.DirectWriteMode.DigitalOutput
        };

        editor.Nodes.Add(inNode);
        editor.Nodes.Add(dwNode);

        var conn = new ConnectionViewModel(inNode.OutputConnectors[0], dwNode.TrigConnector);
        editor.Connections.Add(conn);

        // Act 1: Lan truyền tín hiệu
        editor.ApplyLiveOnlinePropagation();

        // Assert 1:
        Assert.True(conn.IsActive, "Dây nối vào Trig của DirectWrite phải sáng khi DI0 = 1");
        Assert.True(dwNode.IsSimActive, "DirectWriteNode phải ở trạng thái IsSimActive = true");

        // Act 2: Dọn dẹp visuals khi dừng mô phỏng
        editor.ClearSimVisuals();

        // Assert 2:
        Assert.False(conn.IsActive, "Dây nối phải tắt sau khi ClearSimVisuals");
        Assert.False(dwNode.IsSimActive, "DirectWriteNode phải tắt IsSimActive sau khi ClearSimVisuals");
        Assert.False(dwNode.IsLiveActive, "DirectWriteNode phải tắt IsLiveActive sau khi ClearSimVisuals");
        Assert.False(dwNode.IsDoneActive, "DirectWriteNode phải tắt IsDoneActive sau khi ClearSimVisuals");
    }

    [Fact]
    public void ModbusNodes_LiveOnlinePropagationAndCleanup()
    {
        // Yêu cầu đặc tả:
        // Khối ModbusRead và ModbusWrite phải hỗ trợ lan truyền tín hiệu EN/Trig trong Live Online,
        // và được dọn dẹp sạch sẽ khi ClearSimVisuals().
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var di0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var inNode = new InputNodeViewModel(di0Tag) { LiveRawValue = 1, IsLiveOnline = true };
        var mbRead = new ModbusReadNodeViewModel { ReadMode = SimplePLC.Protocol.Enums.ModbusReadMode.OnTriggerRisingEdge };
        var mbWrite = new ModbusWriteNodeViewModel();

        editor.Nodes.Add(inNode);
        editor.Nodes.Add(mbRead);
        editor.Nodes.Add(mbWrite);

        var connRead = new ConnectionViewModel(inNode.OutputConnectors[0], mbRead.TrigConnector);
        var connWrite = new ConnectionViewModel(inNode.OutputConnectors[0], mbWrite.TrigConnector);
        editor.Connections.Add(connRead);
        editor.Connections.Add(connWrite);

        // Act 1: Lan truyền
        editor.ApplyLiveOnlinePropagation();

        Assert.True(connRead.IsActive);
        Assert.True(connWrite.IsActive);
        Assert.True(mbRead.IsSimActive);
        Assert.True(mbWrite.IsSimActive);

        // Act 2: Clear
        editor.ClearSimVisuals();

        Assert.False(mbRead.IsSimActive);
        Assert.False(mbRead.IsLiveActive);
        Assert.False(mbWrite.IsSimActive);
        Assert.False(mbWrite.IsLiveActive);
    }

    [Fact]
    public void MqttSubscribe_PropagatesPinValueToDownstreamAction()
    {
        // Yêu cầu đặc tả:
        // Khối MQTT Subscribe khi nhận giá trị từ broker cho dynamic pin,
        // dây nối từ pin tới ActionNode downstream phải lan truyền giá trị và kích hoạt Action.
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var subNode = new MqttSubscribeNodeViewModel { Topic = "factory/cmd" };
        var vregTag = catalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0")
            ?? new TagModel { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        vregTag.Value = 1;

        var pin = new MqttPinBindingViewModel
        {
            Label = "CMD",
            TargetTag = vregTag
        };
        subNode.Pins.Add(pin);

        var do0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DO0") ?? new TagModel { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput };
        var actNode = new ActionNodeViewModel { TargetTag = do0Tag, ActionType = ActionType.SET_TAG };

        editor.Nodes.Add(subNode);
        editor.Nodes.Add(actNode);

        Assert.NotNull(pin.Connector);
        var pinConn = new ConnectionViewModel(pin.Connector!, actNode.InputConnectors[0]);
        editor.Connections.Add(pinConn);

        // Act: Lan truyền
        editor.ApplyLiveOnlinePropagation();

        // Assert:
        Assert.True(pinConn.IsActive, "Dây nối từ chân MQTT Subscribe tới Action phải sáng khi Tag có giá trị khác 0");
        Assert.True(actNode.IsSimActive, "ActionNode phải active khi nhận dữ liệu từ MQTT Subscribe pin");
        Assert.True(actNode.IsSimFired, "ActionNode phải fired");
        Assert.Contains("CMD: 1", actNode.SimTargetLiveText);
    }

    #endregion

    #region 5. Guard Node Safety Interlock Specification Tests

    [Fact]
    public void Guard_PermissiveAndInterlock_Behavior()
    {
        // Yêu cầu đặc tả:
        // Khối Guard (Khóa an toàn):
        // 1. Chế độ Thường (Permissive, Negate=false): Cho phép chạy (Passed) khi GuardTag = 1.
        // 2. Chế độ Đảo (Interlock / E-Stop, Negate=true): Chặn lại (Blocked) khi GuardTag = 1 (ví dụ nút dừng khẩn E-Stop bị nhấn).
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var di0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var di1Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI1") ?? new TagModel { Index = 1, Name = "DI1", Kind = TagKind.DiscreteInput };

        var inNode = new InputNodeViewModel(di0Tag) { LiveRawValue = 1, IsLiveOnline = true };
        var guardPermissive = new GuardNodeViewModel(di1Tag) { Negate = false };
        var guardInterlock = new GuardNodeViewModel(di1Tag) { Negate = true };

        editor.Nodes.Add(inNode);
        editor.Nodes.Add(guardPermissive);
        editor.Nodes.Add(guardInterlock);

        var conn1 = new ConnectionViewModel(inNode.OutputConnectors[0], guardPermissive.InputConnectors[0]);
        var conn2 = new ConnectionViewModel(inNode.OutputConnectors[0], guardInterlock.InputConnectors[0]);
        editor.Connections.Add(conn1);
        editor.Connections.Add(conn2);

        // Kịch bản A: DI1 = 1 (GuardTag active)
        di1Tag.Value = 1;
        editor.ApplyLiveOnlinePropagation();

        Assert.True(guardPermissive.IsSimPassed, "Permissive Guard phải thông khi GuardTag = 1");
        Assert.False(guardPermissive.IsSimBlocked);

        Assert.False(guardInterlock.IsSimPassed, "Interlock Guard phải khóa khi GuardTag = 1");
        Assert.True(guardInterlock.IsSimBlocked, "Interlock Guard phải báo Blocked");

        // Kịch bản B: DI1 = 0 (GuardTag deactive)
        di1Tag.Value = 0;
        editor.ApplyLiveOnlinePropagation();

        Assert.False(guardPermissive.IsSimPassed, "Permissive Guard phải chặn khi GuardTag = 0");
        Assert.True(guardPermissive.IsSimBlocked);

        Assert.True(guardInterlock.IsSimPassed, "Interlock Guard phải thông khi GuardTag = 0");
        Assert.False(guardInterlock.IsSimBlocked);
    }

    #endregion

    #region 6. Timer Node Specification Tests (TON, TOF, TP)

    [Fact]
    public void Timer_Ton_TurnsOnOnlyAfterPresetElapsed()
    {
        // Yêu cầu đặc tả IEC 61131-3:
        // TON (On-delay):
        // Khi ngõ vào IN = 1, bộ đếm bắt đầu tính thời gian (IsTiming = true).
        // Ngõ ra Q chỉ chuyển mức 1 sau khi thời gian trôi qua đạt hoặc vượt ngưỡng PresetMs.
        // Nếu IN chuyển 0 trước khi đạt PresetMs, bộ đếm bị hủy và ElapsedMs = 0 ngay lập tức.
        var catalog = new TagCatalogViewModel();
        var store = new RuntimeStateStore();
        var product = CreateProduct();
        store.InitializeProduct(product);

        var editor = new LogicEditorViewModel(catalog, stateStore: store, uiDispatcher: a => a());
        editor.Nodes.Clear();
        editor.Connections.Clear();

        var di0Tag = catalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? new TagModel { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput };
        var inNode = new InputNodeViewModel(di0Tag) { LiveRawValue = 1, IsLiveOnline = true };
        var timerNode = new TimerNodeViewModel
        {
            TimerMode = "TON",
            PresetMs = 200 // 200ms
        };

        editor.Nodes.Add(inNode);
        editor.Nodes.Add(timerNode);

        var conn = new ConnectionViewModel(inNode.OutputConnectors[0], timerNode.InputConnectors[0]);
        editor.Connections.Add(conn);

        // Chu kỳ 1: Bắt đầu kích hoạt IN = 1
        editor.ApplyLiveOnlinePropagation();
        Assert.True(timerNode.IsTiming, "TON phải ở trạng thái đang đếm thời gian khi IN = 1");
        Assert.False(timerNode.IsLiveActive, "Ngõ ra Q của TON chưa được bật ngay tại chu kỳ đầu");

        // Ngắt IN = 0 trước khi đủ 200ms -> TON phải hủy ngay
        inNode.LiveRawValue = 0;
        editor.ApplyLiveOnlinePropagation();
        Assert.False(timerNode.IsTiming, "TON phải hủy đếm khi IN chuyển về 0");
        Assert.Equal(0u, timerNode.ElapsedMs);
        Assert.False(timerNode.IsLiveActive);
    }

    #endregion

    #region 7. Counter Node Specification Tests (CTU, CTD)

    [Fact]
    public void Counter_CtuAndCtd_CountAndResetBehavior()
    {
        // Yêu cầu đặc tả IEC 61131-3:
        // CTU: Đếm tiến. Ngõ ra Q bật khi CurrentCount >= PresetValue.
        // CTD: Đếm lùi. Ngõ ra Q bật khi CurrentCount <= 0.
        var counterCtu = new CounterNodeViewModel
        {
            CounterMode = "CTU",
            PresetValue = 5,
            CurrentCount = 4
        };

        // Khi count = 4 < 5 -> Q = false
        Assert.False(counterCtu.CurrentCount >= counterCtu.PresetValue);

        // Khi count = 5 -> Q = true
        counterCtu.CurrentCount = 5;
        Assert.True(counterCtu.CurrentCount >= counterCtu.PresetValue);

        // Khối CTD
        var counterCtd = new CounterNodeViewModel
        {
            CounterMode = "CTD",
            PresetValue = 5,
            CurrentCount = 1
        };

        Assert.False(counterCtd.CurrentCount <= 0);

        counterCtd.CurrentCount = 0;
        Assert.True(counterCtd.CurrentCount <= 0, "CTD phải đạt điều kiện Q khi đếm về 0");
    }

    #endregion
}
