using System;
using System.Linq;
using System.Windows;
using SimplePLC.Application.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class SelectionReentrancyTests
{
    private readonly TagCatalogViewModel _tagCatalog;

    public SelectionReentrancyTests()
    {
        _tagCatalog = new TagCatalogViewModel();
    }

    [Fact]
    public void DeleteSelectedNode_WhenMultipleNodesSelected_DoesNotThrowReentrancyException()
    {
        // Arrange
        var vm = new LogicEditorViewModel(_tagCatalog);
        var tag1 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var tag2 = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inputNode = new InputNodeViewModel(tag1) { Location = new Point(100, 100) };
        var triggerNode = new TriggerNodeViewModel { Location = new Point(300, 100) };
        var actionNode = new ActionNodeViewModel(tag2) { Location = new Point(500, 100) };

        vm.Nodes.Add(inputNode);
        vm.Nodes.Add(triggerNode);
        vm.Nodes.Add(actionNode);

        // Nối dây giữa các node
        vm.CompleteConnection((inputNode.OutputConnectors[0], triggerNode.InputConnectors[0]));
        vm.CompleteConnection((triggerNode.OutputConnectors[0], actionNode.InputConnectors[0]));

        // Chọn tất cả các node
        inputNode.IsSelected = true;
        triggerNode.IsSelected = true;
        actionNode.IsSelected = true;

        Assert.Equal(3, vm.SelectedNodes.Count);

        // Act - Xóa nhiều node cùng lúc (trước đây gây ra InvalidOperationException re-entrancy)
        var exception = Record.Exception(() => vm.DeleteSelectedNode());

        // Assert
        Assert.Null(exception);
        Assert.Empty(vm.Nodes);
        Assert.Empty(vm.SelectedNodes);
        Assert.Empty(vm.Connections);
    }

    [Fact]
    public void AddNode_AndSelectExclusive_DoesNotThrowReentrancyException()
    {
        // Arrange
        var vm = new LogicEditorViewModel(_tagCatalog);
        var tag = _tagCatalog.AllTags.First(t => t.Name == "DI0");

        // Act & Assert: Thêm liên tiếp nhiều node
        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                vm.AddNode("Input", new Point(i * 50, i * 50), tag);
            }
        });

        Assert.Null(exception);
        Assert.Equal(10, vm.Nodes.Count);
        Assert.Single(vm.SelectedNodes);
    }

    [Fact]
    public void ClearCanvas_WhenMultipleNodesAndConnectionsExist_CleansUpSafelyWithoutException()
    {
        // Arrange
        var vm = new LogicEditorViewModel(_tagCatalog);
        var tag1 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var tag2 = _tagCatalog.AllTags.First(t => t.Name == "DO0");

        var n1 = new InputNodeViewModel(tag1);
        var n2 = new ActionNodeViewModel(tag2);

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.CompleteConnection((n1.OutputConnectors[0], n2.InputConnectors[0]));

        n1.IsSelected = true;
        n2.IsSelected = true;

        // Act
        var exception = Record.Exception(() => vm.ClearCanvas());

        // Assert
        Assert.Null(exception);
        Assert.Empty(vm.Nodes);
        Assert.Empty(vm.SelectedNodes);
        Assert.Empty(vm.Connections);
        Assert.Null(vm.SelectedNode);
    }
}
