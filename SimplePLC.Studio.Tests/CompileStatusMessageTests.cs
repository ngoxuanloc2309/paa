using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử độc lập kiểm chứng cơ chế định dạng chuỗi trạng thái tổng hợp Compact Token cho toàn bộ phân hệ
/// (Logic Rules, MQTT Services, Modbus Master, Function Blocks) theo chuẩn First Principles & Axiom 2.
/// </summary>
public sealed class CompileStatusMessageTests
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly RuleTableViewModel _ruleTable;

    public CompileStatusMessageTests()
    {
        _tagCatalog = new TagCatalogViewModel();
        _ruleTable = new RuleTableViewModel(_tagCatalog, _ => { });
    }

    [Fact]
    public void CompileStatus_WhenOnlyMqttBlocksPresent_DisplaysMqttReadyToken_AndPopulatesRuleTable()
    {
        // Arrange: Tạo đồ thị chỉ gồm 5 khối MQTT (2 PUB, 3 SUB) không có Action logic
        var vm = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: a => a())
        {
            DebounceDelayMs = 0
        };
        vm.Nodes.Clear();

        var pub1 = new MqttPublishNodeViewModel { Topic = "synaptix/telemetry/1" };
        var pub2 = new MqttPublishNodeViewModel { Topic = "synaptix/telemetry/2" };
        var sub1 = new MqttSubscribeNodeViewModel { Topic = "synaptix/commands/1" };
        var sub2 = new MqttSubscribeNodeViewModel { Topic = "synaptix/commands/2" };
        var sub3 = new MqttSubscribeNodeViewModel { Topic = "synaptix/commands/3" };

        vm.Nodes.Add(pub1);
        vm.Nodes.Add(pub2);
        vm.Nodes.Add(sub1);
        vm.Nodes.Add(sub2);
        vm.Nodes.Add(sub3);

        // Act: Kỹ sư bấm Compile / Save & Compile
        vm.CompileAndSaveRules(isAutoSync: false);

        // Assert 1: Thanh trạng thái KHÔNG được hiển thị "0 valid rules" mà phải hiển thị 5 MQTT ready
        Assert.DoesNotContain("0 valid rules", vm.SaveStatusMessage);
        Assert.True(vm.SaveStatusMessage.Contains("5 MQTT"), $"SaveStatusMessage phải chứa '5 MQTT', thực tế: '{vm.SaveStatusMessage}'");

        // Assert 2: ToolTip và Detail phải cung cấp thông tin chi tiết
        Assert.Contains("5 MQTT", vm.SaveStatusTooltip);
        Assert.Contains("5 MQTT", vm.SaveStatusDetail);
        Assert.True(vm.HasSaveStatusDetail);

        // Assert 3: Toast thông báo lưu phải được kích hoạt thành công
        Assert.True(vm.ShowSaveToast);
        Assert.Contains("5 MQTT", vm.SaveToastMessage);

        // Assert 4: Bảng Rule Table tab MQTT Services phải được cập nhật đủ 5 dịch vụ
        Assert.Equal(5, _ruleTable.MqttServices.Count);
        Assert.Equal(5, _ruleTable.MqttServiceCount);
        Assert.Contains("(5)", _ruleTable.SubTabMqttLabel);
    }

    [Fact]
    public void CompileStatus_WhenOnlyModbusBlocksPresent_DisplaysModbusReadyToken_AndPopulatesRuleTable()
    {
        // Arrange: Tạo đồ thị chỉ gồm 2 khối Modbus Master (1 Read, 1 Write)
        var vm = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: a => a())
        {
            DebounceDelayMs = 0
        };
        vm.Nodes.Clear();

        var mbRead = new ModbusReadNodeViewModel();
        var mbWrite = new ModbusWriteNodeViewModel();

        vm.Nodes.Add(mbRead);
        vm.Nodes.Add(mbWrite);

        // Act: Bấm Save & Compile
        vm.CompileAndSaveRules(isAutoSync: false);

        // Assert:
        Assert.DoesNotContain("0 valid rules", vm.SaveStatusMessage);
        Assert.True(vm.SaveStatusMessage.Contains("2 Modbus"), $"SaveStatusMessage phải chứa '2 Modbus', thực tế: '{vm.SaveStatusMessage}'");
        Assert.True(vm.ShowSaveToast);
        Assert.Contains("2 Modbus", vm.SaveToastMessage);

        // Bảng Rule Table tab Modbus Master có 2 lệnh
        Assert.Equal(2, _ruleTable.ModbusCommands.Count);
        Assert.Equal(2, _ruleTable.ModbusCommandCount);
    }

    [Fact]
    public void CompileStatus_WhenFunctionBlockWithRulePresent_DisplaysRulesAndFbsReady()
    {
        // Arrange: 1 Timer (Input -> Timer -> Action)
        var vm = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: a => a())
        {
            DebounceDelayMs = 0
        };
        vm.Nodes.Clear();

        var diTag = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var doTag = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel(diTag);
        var timer = new TimerNodeViewModel();
        var actNode = new ActionNodeViewModel(doTag);

        vm.Nodes.Add(inNode);
        vm.Nodes.Add(timer);
        vm.Nodes.Add(actNode);

        vm.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], timer.InputConnectors[0]));
        vm.Connections.Add(new ConnectionViewModel(timer.OutputConnectors[0], actNode.InputConnectors[0]));

        // Act: Bấm Save & Compile
        vm.CompileAndSaveRules(isAutoSync: false);

        // Assert:
        Assert.DoesNotContain("0 valid rules", vm.SaveStatusMessage);
        Assert.True(vm.CompileState == CompileState.Valid);
        Assert.True(vm.ShowSaveToast);
        Assert.Single(_ruleTable.FunctionBlocks);
    }

    [Fact]
    public void CompileStatus_WhenMixedRulesAndMqttPresent_DisplaysCompositeToken()
    {
        // Arrange: 1 Logic Rule (DI0 -> DO0) và 2 khối MQTT
        var vm = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: a => a())
        {
            DebounceDelayMs = 0
        };
        vm.Nodes.Clear();

        var diTag = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var doTag = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel(diTag);
        var actNode = new ActionNodeViewModel(doTag);
        vm.Nodes.Add(inNode);
        vm.Nodes.Add(actNode);

        var conn = new ConnectionViewModel(inNode.OutputConnectors[0], actNode.InputConnectors[0]);
        vm.Connections.Add(conn);

        var pub = new MqttPublishNodeViewModel { Topic = "synaptix/telemetry" };
        var sub = new MqttSubscribeNodeViewModel { Topic = "synaptix/commands" };
        vm.Nodes.Add(pub);
        vm.Nodes.Add(sub);

        // Act: Bấm Save & Compile
        vm.CompileAndSaveRules(isAutoSync: false);

        // Assert: Chuỗi tổng hợp phải phản ánh cả 1 rule và 2 MQTT
        Assert.True(vm.SaveStatusMessage.Contains("1") && vm.SaveStatusMessage.Contains("2 MQTT"), 
            $"SaveStatusMessage phải chứa cả 1 rule và 2 MQTT, thực tế: '{vm.SaveStatusMessage}'");

        // Tooltip phân rã chi tiết
        Assert.Contains("1", vm.SaveStatusTooltip);
        Assert.Contains("2 MQTT", vm.SaveStatusTooltip);

        // Bảng Rule Table có cả 1 logic rule và 2 MQTT services
        Assert.Single(_ruleTable.Rules);
        Assert.Equal(2, _ruleTable.MqttServices.Count);
    }

    [Fact]
    public void CompileStatus_WhenMultiSubsystemsPresent_CompactTokenFitsWithinBoundary()
    {
        // Arrange: 1 Rule + 2 MQTT + 1 Modbus
        var vm = new LogicEditorViewModel(_tagCatalog, _ruleTable, uiDispatcher: a => a())
        {
            DebounceDelayMs = 0
        };
        vm.Nodes.Clear();

        var diTag = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var doTag = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel(diTag);
        var actNode = new ActionNodeViewModel(doTag);
        vm.Nodes.Add(inNode);
        vm.Nodes.Add(actNode);
        vm.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], actNode.InputConnectors[0]));

        vm.Nodes.Add(new MqttPublishNodeViewModel { Topic = "p1" });
        vm.Nodes.Add(new MqttSubscribeNodeViewModel { Topic = "s1" });
        vm.Nodes.Add(new ModbusReadNodeViewModel());

        // Act:
        vm.CompileAndSaveRules(isAutoSync: false);

        // Assert: Chuỗi token đa phân hệ gọn gàng
        Assert.True(vm.SaveStatusMessage.Contains("1R") && vm.SaveStatusMessage.Contains("2M") && vm.SaveStatusMessage.Contains("1MB"),
            $"SaveStatusMessage phải chứa '1R, 2M, 1MB ready', thực tế: '{vm.SaveStatusMessage}'");

        // Chiều dài chuỗi ký tự < 35 ký tự để chắc chắn hiển thị hoàn hảo trong khung 200px
        Assert.True(vm.SaveStatusMessage.Length < 35, $"Chuỗi '{vm.SaveStatusMessage}' quá dài ({vm.SaveStatusMessage.Length} ký tự)!");
    }

    [Fact]
    public void RuleTable_WhenTabSwitchedFromCanvas_SubsystemsPopulatedAutomatically()
    {
        // Arrange: Khởi tạo MainViewModel với 2 khối MQTT trên Canvas
        var mainVm = new MainViewModel();
        mainVm.LogicEditorVM.DebounceDelayMs = 0;
        mainVm.LogicEditorVM.Nodes.Clear();

        mainVm.LogicEditorVM.Nodes.Add(new MqttPublishNodeViewModel { Topic = "test/pub" });
        mainVm.LogicEditorVM.Nodes.Add(new MqttSubscribeNodeViewModel { Topic = "test/sub" });

        // Giả lập biên dịch ngầm của Canvas (khi người dùng vẽ)
        mainVm.LogicEditorVM.CompileAndSaveRules(isAutoSync: true);
        Assert.NotNull(mainVm.LogicEditorVM.CurrentProgram);

        // Act: Người dùng click chuyển sang Tab Rule Table (Index = 3)
        mainVm.SelectedTabIndex = AppTabIndices.RuleTable;

        // Assert: RuleTableVM phải tự động làm mới ngay lập tức
        Assert.Equal(2, mainVm.RuleTableVM.MqttServices.Count);
        Assert.Contains("(2)", mainVm.RuleTableVM.SubTabMqttLabel);
    }
}
