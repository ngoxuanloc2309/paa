using System.Linq;
using System.Windows;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class MqttBlueprintTests
{
    [Fact]
    public void ApplyBlueprint_MqttPub_GeneratesOrthogonalNonOverlappingLayout_AndCompilesSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpMqttPub = blueprints.Blueprints.First(b => b.Id == "mqtt_pub");
        blueprints.ApplyBlueprint(bpMqttPub);

        // 1. Kiểm tra đủ 5 khối
        Assert.Equal(5, logicEditor.Nodes.Count);

        var inpTrig = logicEditor.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");
        var inpVal = logicEditor.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        var trig = logicEditor.Nodes.OfType<TriggerNodeViewModel>().Single();
        var actDone = logicEditor.Nodes.OfType<ActionNodeViewModel>().Single();
        var mqttPub = logicEditor.Nodes.OfType<MqttPublishNodeViewModel>().Single();

        // 2. Kiểm tra tọa độ không đè dính nhau
        Assert.Equal(new Point(80, 60), inpTrig.Location);
        Assert.Equal(new Point(340, 60), trig.Location);
        Assert.Equal(new Point(660, 60), actDone.Location);
        Assert.Equal(new Point(340, 220), inpVal.Location);
        Assert.Equal(new Point(660, 200), mqttPub.Location);

        // Đảm bảo khoảng cách giữa các khối tối thiểu 100px
        Assert.True(Math.Abs(mqttPub.Location.X - trig.Location.X) >= 200);
        Assert.True(Math.Abs(inpVal.Location.Y - trig.Location.Y) >= 150);

        // 3. Kiểm tra cấu hình MQTT Pub
        Assert.Equal("synaptix/telemetry/pressure", mqttPub.Topic);
        Assert.Equal("Event", mqttPub.PublishMode);
        Assert.Equal(0.5, mqttPub.Deadband);
        Assert.Equal("bar", mqttPub.Unit);
        Assert.Equal(1, mqttPub.Qos);

        // 4. Biên dịch thành công 0 lỗi và sinh 1 rule PLC
        logicEditor.CompileNow();
        Assert.DoesNotContain(logicEditor.Diagnostics, d => d.Severity == Models.DiagnosticSeverity.Error);
        Assert.Single(ruleTable.Rules);
    }

    [Fact]
    public void ApplyBlueprint_MqttSub_GeneratesOrthogonalNonOverlappingLayout_AndCompilesSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpMqttSub = blueprints.Blueprints.First(b => b.Id == "mqtt_sub");
        blueprints.ApplyBlueprint(bpMqttSub);

        // 1. Kiểm tra Cách 1: Tối giản gồm 3 khối (MQTT Sub, DO7 Báo gói tin, DO1 Bơm cấp liệu)
        Assert.Equal(3, logicEditor.Nodes.Count);

        var actValid = logicEditor.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO7");
        var actStart = logicEditor.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO1");
        var mqttSub = logicEditor.Nodes.OfType<MqttSubscribeNodeViewModel>().Single();

        // 2. Kiểm tra tọa độ không đè dính nhau (Cột 1: X=200, Cột 2: X=600)
        Assert.Equal(new Point(200, 100), mqttSub.Location);
        Assert.Equal(new Point(600, 80), actValid.Location);
        Assert.Equal(new Point(600, 200), actStart.Location);

        // Đảm bảo khoảng cách giữa các khối rõ ràng
        Assert.True(Math.Abs(actValid.Location.X - mqttSub.Location.X) >= 300);
        Assert.True(Math.Abs(actStart.Location.Y - actValid.Location.Y) >= 100);

        // 3. Kiểm tra cấu hình MQTT Sub
        Assert.Equal("synaptix/commands/inbound", mqttSub.Topic);
        Assert.Equal(1, mqttSub.Qos);
        Assert.Single(mqttSub.Pins);
        Assert.Equal("start", mqttSub.Pins[0].JsonPath);

        // 4. Biên dịch thành công 0 lỗi, pure IIoT dispatcher không sinh rule đồng bộ 10ms
        logicEditor.CompileNow();
        Assert.DoesNotContain(logicEditor.Diagnostics, d => d.Severity == Models.DiagnosticSeverity.Error);
        Assert.Empty(ruleTable.Rules);
    }

    [Fact]
    public void ConnectorViewModel_Tooltips_AreDescriptiveAndLocalized()
    {
        var loc = SimplePLC.Studio.Services.LocalizationService.Instance;
        var originalLang = loc.CurrentLanguage;
        try
        {
            loc.CurrentLanguage = "vi";

            var mqttPub = new MqttPublishNodeViewModel();
            var mqttSub = new MqttSubscribeNodeViewModel();
            var pin = new MqttPinBindingViewModel("TempPin", "temp", MqttDataType.Float);
            mqttSub.AddPin(pin);

            // 1. MQTT Pub Pins (Chế độ Tiếng Việt: Hiển thị tiếng Việt hoàn chỉnh)
            Assert.Contains("Trig: Xung kích hoạt phát bản tin", mqttPub.InputConnectors[0].TooltipText);
            Assert.False(string.IsNullOrWhiteSpace(mqttPub.InputConnectors[0].Description));
            Assert.Contains("Val: Tín hiệu dữ liệu", mqttPub.InputConnectors[1].TooltipText);
            Assert.Contains("Done: Xung báo hoàn tất", mqttPub.OutputConnectors[0].TooltipText);
            Assert.Contains("Err: Báo lỗi kết nối", mqttPub.OutputConnectors[1].TooltipText);

            // 2. MQTT Sub Pins (Chế độ Tiếng Việt: Hiển thị tiếng Việt hoàn chỉnh)
            Assert.Contains("EN: Khóa cho phép khối hoạt động", mqttSub.EnableConnector.TooltipText);
            Assert.False(string.IsNullOrWhiteSpace(mqttSub.EnableConnector.Description));
            Assert.Contains("Valid: Xung báo nhận được gói tin", mqttSub.ValidConnector.TooltipText);
            Assert.Contains("Err: Báo lỗi kết nối", mqttSub.ErrorConnector.TooltipText);
            Assert.NotNull(pin.Connector);
            Assert.Contains("TempPin: Trích xuất trường JSON 'temp' (Float)", pin.Connector.TooltipText);

            // 3. Kiểm tra chuyển đổi ngôn ngữ động sang Tiếng Anh khi chọn English
            loc.CurrentLanguage = "en";
            Assert.Contains("Trig: Pulse to publish MQTT message", mqttPub.InputConnectors[0].TooltipText);
            Assert.Contains("EN: Block operation enable", mqttSub.EnableConnector.TooltipText);
            Assert.Contains("Valid: Pulse asserting a valid MQTT message", mqttSub.ValidConnector.TooltipText);
            Assert.NotNull(pin.Connector);
            Assert.Contains("TempPin: Extract JSON field 'temp' (Float)", pin.Connector.TooltipText);
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }
}
