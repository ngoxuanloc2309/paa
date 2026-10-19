using System;
using System.Linq;
using System.Windows;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ModbusBlueprintTests
{
    [Fact]
    public void ApplyBlueprint_MbRead_GeneratesOrthogonalLayout_AndCompilesSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpMbRead = blueprints.Blueprints.First(b => b.Id == "mb_read");
        blueprints.ApplyBlueprint(bpMbRead);

        // 1. Kiểm tra 3 khối (Input DI0 EN, MB_READ, Action DO6 Valid)
        Assert.Equal(3, logicEditor.Nodes.Count);

        var inpEn = logicEditor.Nodes.OfType<InputNodeViewModel>().Single();
        var mbRead = logicEditor.Nodes.OfType<ModbusReadNodeViewModel>().Single();
        var actValid = logicEditor.Nodes.OfType<ActionNodeViewModel>().Single();

        // 2. Kiểm tra tọa độ trực giao không trùng lặp
        Assert.Equal(new Point(80, 100), inpEn.Location);
        Assert.Equal(new Point(340, 100), mbRead.Location);
        Assert.Equal(new Point(680, 100), actValid.Location);

        // 3. Kiểm tra thông số MB_READ
        Assert.Equal(2, mbRead.SlaveId);
        Assert.Equal(ModbusMasterFunctionCode.ReadInputRegisters, mbRead.FunctionCode);
        Assert.Equal((ushort)0, mbRead.StartAddress);
        Assert.Single(mbRead.Pins);
        Assert.Equal("Pressure", mbRead.Pins[0].Label);
        Assert.Equal(0.1f, mbRead.Pins[0].Multiplier);

        // 4. Biên dịch thành công 0 lỗi
        logicEditor.CompileNow();
        Assert.DoesNotContain(logicEditor.Diagnostics, d => d.Severity == Models.DiagnosticSeverity.Error);
    }

    [Fact]
    public void ApplyBlueprint_MbWriteVfd_GeneratesOrthogonalLayout_AndCompilesSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpMbWrite = blueprints.Blueprints.First(b => b.Id == "mb_write_vfd");
        blueprints.ApplyBlueprint(bpMbWrite);

        // 1. Kiểm tra 4 khối (Input DI0, Trigger ON_RISE, MB_WRITE Direct, Action DO0 Done)
        Assert.Equal(4, logicEditor.Nodes.Count);

        var inp = logicEditor.Nodes.OfType<InputNodeViewModel>().Single();
        var trig = logicEditor.Nodes.OfType<TriggerNodeViewModel>().Single();
        var mbWrite = logicEditor.Nodes.OfType<ModbusWriteNodeViewModel>().Single();
        var actDone = logicEditor.Nodes.OfType<ActionNodeViewModel>().Single();

        // 2. Kiểm tra tọa độ không đè dính
        Assert.Equal(new Point(80, 80), inp.Location);
        Assert.Equal(new Point(320, 80), trig.Location);
        Assert.Equal(new Point(560, 80), mbWrite.Location);
        Assert.Equal(new Point(860, 80), actDone.Location);

        // 3. Kiểm tra thông số MB_WRITE Direct
        Assert.Equal(3, mbWrite.SlaveId);
        Assert.Equal(ModbusWriteMode.DirectRegisters, mbWrite.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, mbWrite.FunctionCode);
        Assert.Equal((ushort)40001, mbWrite.StartAddress);
        Assert.Single(mbWrite.Pins);
        Assert.Equal("TargetFreq", mbWrite.Pins[0].Label);

        // 4. Biên dịch thành công 0 lỗi
        logicEditor.CompileNow();
        Assert.DoesNotContain(logicEditor.Diagnostics, d => d.Severity == Models.DiagnosticSeverity.Error);
    }

    [Fact]
    public void ApplyBlueprint_SpxSortToLight_GeneratesOrthogonalLayout_AndCompilesSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpSpx = blueprints.Blueprints.First(b => b.Id == "spx_sort_to_light");
        blueprints.ApplyBlueprint(bpSpx);

        // 1. Kiểm tra 4 khối (MQTT SUB, Input VREG0, DIRECT_WRITE, Action DO4 Còi)
        Assert.Equal(4, logicEditor.Nodes.Count);

        var mqttSub = logicEditor.Nodes.OfType<MqttSubscribeNodeViewModel>().Single();
        var inpBin = logicEditor.Nodes.OfType<InputNodeViewModel>().Single();
        var dirWrite = logicEditor.Nodes.OfType<DirectWriteNodeViewModel>().Single();
        var actChime = logicEditor.Nodes.OfType<ActionNodeViewModel>().Single();

        // 2. Kiểm tra thông số SPX Sort-to-Light DIRECT_WRITE
        Assert.Equal("spx/hub_hn/rack_01/put_command", mqttSub.Topic);
        Assert.Equal(DirectWriteTargetPin.Do3, dirWrite.TargetPin);
        Assert.Equal(DirectWriteMode.AddressableBus, dirWrite.Mode);
        Assert.Equal((ushort)18, dirWrite.ChannelCount);
        Assert.True(dirWrite.IsExclusiveChannel);
        Assert.Equal(0x00FF00u, dirWrite.DefaultColorRgb);

        // 3. Biên dịch thành công 0 lỗi
        logicEditor.CompileNow();
        Assert.DoesNotContain(logicEditor.Diagnostics, d => d.Severity == Models.DiagnosticSeverity.Error);
    }
}
