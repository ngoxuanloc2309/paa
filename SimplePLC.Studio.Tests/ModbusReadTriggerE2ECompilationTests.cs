namespace SimplePLC.Studio.Tests;

using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

/// <summary>
/// E2E Integration tests verifying that wiring an MB_READ node output pin directly into a Trigger node
/// compiles successfully without triggering SPLC-COMP-003 ('not connected to an Input node').
/// </summary>
public class ModbusReadTriggerE2ECompilationTests
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly LogicEditorViewModel _vm;
    private readonly RuleCompiler _compiler;

    public ModbusReadTriggerE2ECompilationTests()
    {
        _tagCatalog = new TagCatalogViewModel();
        _vm = new LogicEditorViewModel(_tagCatalog);
        _compiler = new RuleCompiler();
    }

    /// <summary>
    /// [E2E Studio Integration]: MB_READ (Pin Total_kWh -> VFLAG0) kết nối vào Trigger -> Action
    /// phải biên dịch thành công qua Studio RuleCompiler với IsValid == true và không có lỗi SPLC-COMP-003.
    /// </summary>
    [Fact]
    public void Studio_Compile_ModbusReadPin_ConnectedToTrigger_CompilesSuccessfully()
    {
        // Arrange
        var vflag0 = _tagCatalog.AllTags.First(t => t.Name == "VFLAG0");
        var do0 = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var mbNode = new ModbusReadNodeViewModel
        {
            PortIndex = 0,
            SlaveId = 1,
            StartAddress = 0,
            RegCount = 2,
            PollIntervalMs = 1000
        };

        // Add custom pin for Total_kWh mapped to vflag0
        var pin = new ModbusPinBindingViewModel
        {
            Label = "Total_kWh",
            TargetTag = vflag0
        };
        mbNode.Pins.Add(pin);

        var trigNode = new TriggerNodeViewModel();
        var actNode = new ActionNodeViewModel(do0);

        _vm.Nodes.Add(mbNode);
        _vm.Nodes.Add(trigNode);
        _vm.Nodes.Add(actNode);

        // Act: Connect MB_READ (Total_kWh pin) -> Trigger (In)
        var mbPinConnector = mbNode.OutputConnectors.FirstOrDefault(c => c.Title == "Total_kWh");
        Assert.NotNull(mbPinConnector);

        _vm.Connect(mbPinConnector, trigNode.InputConnectors[0]);
        _vm.Connect(trigNode.OutputConnectors[0], actNode.InputConnectors[0]);

        // Assert wire binding propagated TargetTag to Trigger
        Assert.Equal(vflag0, trigNode.InputTag);

        // Compile via Studio RuleCompiler
        var result = _compiler.Compile(_vm.Nodes, _vm.Connections, _tagCatalog);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("not connected to an Input node") || d.Message.Contains("chưa được kết nối với khối Ngõ vào"));
        Assert.Single(result.Rules);
        Assert.Equal(vflag0.Index, result.Rules[0].TriggerTag?.Index);
        Assert.Equal(do0.Index, result.Rules[0].ActionTag?.Index);
    }
}
