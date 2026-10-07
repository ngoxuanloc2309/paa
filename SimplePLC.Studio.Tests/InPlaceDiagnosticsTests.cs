using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class InPlaceDiagnosticsTests
{
    [Fact]
    public void CompileAndSaveRules_MarksHasErrorOnInvalidTimerNode()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);

        // Tạo 1 Timer node không có ngõ vào IN hợp lệ
        var timerNode = new TimerNodeViewModel
        {
            Id = "timer-err-01",
            TimerMode = "TON",
            PresetMs = 5000
        };
        vm.Nodes.Add(timerNode);

        // Act 1: Trong chế độ autoSync (khi vừa thả khối ra canvas), khối cô lập đang soạn thảo (Draft)
        // không bị đánh cờ HasError làm phiền người dùng.
        vm.CompileAndSaveRules(isAutoSync: true);
        Assert.False(timerNode.HasError, "Floating timer node during autoSync should remain neutral draft without HasError");

        // Act 2: Khi người dùng chủ động bấm Compile / Save & Compile (isAutoSync: false),
        // hệ thống sẽ kiểm tra toàn diện và đánh dấu lỗi đầy đủ.
        vm.CompileAndSaveRules(isAutoSync: false);
        Assert.True(timerNode.HasError, "Timer node without valid inputs must have HasError = true on explicit compile");
        Assert.False(string.IsNullOrEmpty(timerNode.ErrorMessage), "ErrorMessage should be populated");
    }

    [Fact]
    public void CompileAndSaveRules_ClearsHasError_WhenNodeBecomesValid()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);

        var inTag = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var outTag = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel { Id = "in-01", Tag = inTag };
        var actNode = new ActionNodeViewModel { Id = "act-01", TargetTag = outTag, ActionType = SimplePLC.Studio.Models.ActionType.SET_TAG, ActionParam = 1 };
        var trigNode = new TriggerNodeViewModel { Id = "trig-01", TriggerType = SimplePLC.Studio.Models.TriggerType.ON_RISE };

        vm.Nodes.Add(inNode);
        vm.Nodes.Add(trigNode);
        vm.Nodes.Add(actNode);

        // Nối dây hoàn chỉnh: in -> trig -> act
        vm.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], trigNode.InputConnectors[0]));
        vm.Connections.Add(new ConnectionViewModel(trigNode.OutputConnectors[0], actNode.InputConnectors[0]));

        // Act
        vm.CompileAndSaveRules(isAutoSync: true);

        // Assert
        Assert.False(inNode.HasError);
        Assert.False(trigNode.HasError);
        Assert.False(actNode.HasError);
    }

    [Fact]
    public void NavigateToDiagnostic_SelectsTargetNodeAndInvokesCenterAction()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);

        var targetNode = new InputNodeViewModel { Id = "target-node-99" };
        vm.Nodes.Add(targetNode);

        GraphNodeViewModel? centeredNode = null;
        vm.RequestCenterOnNode = node => centeredNode = node;

        var diag = new Diagnostic(
            DiagnosticSeverity.Error,
            "Lỗi thử nghiệm",
            NodeId: "target-node-99");

        // Act
        vm.NavigateToDiagnosticCommand.Execute(diag);

        // Assert
        Assert.True(targetNode.IsSelected, "Target node should be selected");
        Assert.Equal(targetNode, vm.SelectedNode);
        Assert.Equal(targetNode, centeredNode);
    }

    [Fact]
    public void CompileAndSaveRules_PartiallyWiredCounter_RemainsNeutralDuringAutoSync_AndFailsOnExplicitCompile()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);

        var di0Tag = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var inNode = new InputNodeViewModel { Id = "in-di0", Tag = di0Tag };
        var counterNode = new CounterNodeViewModel
        {
            Id = "cnt-01",
            CounterMode = "CTD",
            PresetValue = 10
        };

        vm.Nodes.Add(inNode);
        vm.Nodes.Add(counterNode);

        // Nối DI0 -> CD của Counter (chưa nối ngõ ra Q)
        vm.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], counterNode.InputConnectors[0]));

        // Act 1: Trong chế độ autoSync (khi người dùng vừa nối 1 dây ngõ vào, chưa kịp nối ngõ ra)
        // Hệ thống giữ trạng thái trung tính Stale (Cần biên dịch), KHÔNG báo đỏ, KHÔNG đánh lỗi khối
        vm.CompileAndSaveRules(isAutoSync: true);

        Assert.Equal(CompileState.Stale, vm.CompileState);
        Assert.False(counterNode.HasError, "Partially wired counter during editing should NOT show red error");
        Assert.Empty(vm.Diagnostics);

        // Act 2: Khi người dùng bấm Lưu & Biên dịch chủ động (Ctrl+S / isAutoSync = false)
        // Hệ thống sẽ kiểm tra toàn diện, báo lỗi thiếu Q, và TUYỆT ĐỐI không có lỗi kỹ thuật 'Target node not found'
        vm.CompileAndSaveRules(isAutoSync: false);

        Assert.Equal(CompileState.Invalid, vm.CompileState);
        Assert.True(counterNode.HasError, "Partially wired counter must have HasError = true on explicit compile");
        Assert.Contains(vm.Diagnostics, d => d.Message.Contains("ngõ ra Q") || d.Message.Contains("output Q"));
        Assert.DoesNotContain(vm.Diagnostics, d => d.Message.Contains("not found"));
    }
}
