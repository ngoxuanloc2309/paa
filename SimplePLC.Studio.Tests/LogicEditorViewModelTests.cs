using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Tests;

public class LogicEditorViewModelTests : IDisposable
{
    public LogicEditorViewModelTests()
    {
        // Tests in this class assert Vietnamese UI strings
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }


    private (LogicEditorViewModel vm, TriggerNodeViewModel trig, ActionNodeViewModel act) CreateTestFixture()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var trig = new TriggerNodeViewModel { Location = new Point(100, 100) };
        var act = new ActionNodeViewModel { Location = new Point(300, 100) };

        vm.Nodes.Add(trig);
        vm.Nodes.Add(act);

        return (vm, trig, act);
    }



    [Fact]
    public void StepConnection_ZeroOffset_GeneratesDirectStraightLineWhenAligned_AndOrthogonalWhenShifted()
    {
        string? straightGeom = null;
        string? steppedGeom = null;
        var thread = new Thread(() =>
        {
            // Case 1: Same Y -> perfectly straight line without any vertical kink or deviation
            var conn1 = new Nodify.StepConnection
            {
                Source = new Point(100, 100),
                Target = new Point(300, 100),
                SourceOffset = new Size(0, 0),
                TargetOffset = new Size(0, 0),
            };
            var prop = typeof(Nodify.BaseConnection).GetProperty("DefiningGeometry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            straightGeom = ((System.Windows.Media.Geometry)prop.GetValue(conn1)!).ToString();

            // Case 2: Different Y -> 90-degree orthogonal right angles (midX at 200, vertical drop from 100 to 200)
            var conn2 = new Nodify.StepConnection
            {
                Source = new Point(100, 100),
                Target = new Point(300, 200),
                SourceOffset = new Size(0, 0),
                TargetOffset = new Size(0, 0),
            };
            steppedGeom = ((System.Windows.Media.Geometry)prop.GetValue(conn2)!).ToString();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.NotNull(straightGeom);
        Assert.NotNull(steppedGeom);
        // Case 1: Same Y -> perfectly straight line (only Y=100)
        Assert.Contains("100,100", straightGeom);
        Assert.Contains("300,100", straightGeom);
        Assert.DoesNotContain("200", straightGeom.Replace("200,100", ""));

        // Case 2: Different Y -> 90-degree orthogonal right angles
        Assert.Contains("200,100", steppedGeom); // Horizontal segment to midpoint
        Assert.Contains("200,200", steppedGeom); // Vertical drop to target Y
        Assert.Contains("300,200", steppedGeom); // Horizontal segment into target
    }

    [Fact]
    public void AllBlueprints_HorizontalAndBranchedNodes_AreMathematicallyAligned()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: action => action());
        var blueprintsVM = new BlueprintsViewModel(logicVM, () => {});

        foreach (var bp in blueprintsVM.Blueprints)
        {
            blueprintsVM.ApplyBlueprintCommand.Execute(bp);

            // Verify horizontal main line: All single-chain nodes are placed at Y = 120
            foreach (var node in logicVM.Nodes)
            {
                // Nodes on the main trunk line (not branched)
                if (node.Location.Y != 50 && node.Location.Y != 190)
                {
                    Assert.Equal(120, node.Location.Y);
                }
            }

            // Verify symmetrical branching: top branch at Y=50 (-70px), bottom branch at Y=190 (+70px)
            var topBranches = logicVM.Nodes.Where(n => Math.Abs(n.Location.Y - 50) < 0.001).ToList();
            var bottomBranches = logicVM.Nodes.Where(n => Math.Abs(n.Location.Y - 190) < 0.001).ToList();
            Assert.Equal(topBranches.Count, bottomBranches.Count);

            // Verify connections between horizontal nodes have identical node Y
            foreach (var conn in logicVM.Connections)
            {
                if (Math.Abs(conn.Source.Node.Location.Y - 120) < 0.001 && Math.Abs(conn.Target.Node.Location.Y - 120) < 0.001)
                {
                    Assert.Equal(conn.Source.Node.Location.Y, conn.Target.Node.Location.Y);
                }
            }
        }
    }


    [Fact]
    public void AddNode_DeselectsPreviouslySelectedNodes_AndSelectsNewNode()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var node1 = new TriggerNodeViewModel { IsSelected = true };
        vm.Nodes.Add(node1);
        vm.SelectedNode = node1;

        Assert.True(node1.IsSelected);
        Assert.Same(node1, vm.SelectedNode);

        // Act: Drag & drop new node into canvas
        vm.AddNode("Guard", new Point(250, 150));

        // Assert: Previously selected node must be deselected, and only the new node is selected
        Assert.False(node1.IsSelected);
        Assert.Equal(2, vm.Nodes.Count);
        var newNode = vm.Nodes[1];
        Assert.True(newNode.IsSelected);
        Assert.Same(newNode, vm.SelectedNode);
        Assert.Single(vm.SelectedNodes);
        Assert.Contains(newNode, vm.SelectedNodes);
    }

    [Fact]
    public void SelectExclusive_DeselectsAllOtherNodes_AndExclusivelySelectsTarget()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { IsSelected = true };
        var n2 = new GuardNodeViewModel { IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { IsSelected = false };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        // Act: User selects n3 exclusively
        vm.SelectExclusive(n3);

        // Assert
        Assert.False(n1.IsSelected);
        Assert.False(n2.IsSelected);
        Assert.True(n3.IsSelected);
        Assert.Same(n3, vm.SelectedNode);
        Assert.Single(vm.SelectedNodes);
        Assert.Contains(n3, vm.SelectedNodes);
    }

    [Fact]
    public void AlignSelectedNodesHorizontally_MultipleSelectedNodes_AlignsToSameY()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 73.4), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(350, 126.8), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(600, 88.2), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        // Act: Click align horizontally
        vm.AlignSelectedNodesHorizontally();

        // Assert: All 3 nodes must share the exact same Y coordinate (aligned horizontally to a round 10px multiple)
        Assert.Equal(70, n1.Location.Y);
        Assert.Equal(70, n2.Location.Y);
        Assert.Equal(70, n3.Location.Y);
        Assert.Equal(100, n1.Location.X);
        Assert.Equal(350, n2.Location.X);
        Assert.Equal(600, n3.Location.X);
    }

    [Fact]
    public void AlignSelectedNodesVertically_MultipleSelectedNodes_AlignsToSameX()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(143.4, 100), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(226.8, 250), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(88.2, 400), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        // Act
        vm.AlignSelectedNodesVertically();

        // Assert: All 3 nodes must share the exact same X coordinate (snapped to 140px)
        Assert.Equal(140, n1.Location.X);
        Assert.Equal(140, n2.Location.X);
        Assert.Equal(140, n3.Location.X);
        Assert.Equal(100, n1.Location.Y);
        Assert.Equal(250, n2.Location.Y);
        Assert.Equal(400, n3.Location.Y);
    }

    [Fact]
    public void AlignTop_MultipleSelectedNodes_AlignsToMinY()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 150), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(300, 80), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(500, 220), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignTop();

        Assert.Equal(80, n1.Location.Y);
        Assert.Equal(80, n2.Location.Y);
        Assert.Equal(80, n3.Location.Y);
        Assert.Equal(100, n1.Location.X);
        Assert.Equal(300, n2.Location.X);
        Assert.Equal(500, n3.Location.X);
    }

    [Fact]
    public void AlignMiddle_MultipleSelectedNodes_AlignsToAverageY()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 60), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(300, 100), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(500, 140), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignMiddle();

        Assert.Equal(100, n1.Location.Y);
        Assert.Equal(100, n2.Location.Y);
        Assert.Equal(100, n3.Location.Y);
    }

    [Fact]
    public void AlignBottom_MultipleSelectedNodes_AlignsToMaxY()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 50), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(300, 120), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(500, 250), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignBottom();

        Assert.Equal(250, n1.Location.Y);
        Assert.Equal(250, n2.Location.Y);
        Assert.Equal(250, n3.Location.Y);
    }

    [Fact]
    public void AlignLeft_MultipleSelectedNodes_AlignsToMinX()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(120, 50), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(70, 150), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(250, 250), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignLeft();

        Assert.Equal(70, n1.Location.X);
        Assert.Equal(70, n2.Location.X);
        Assert.Equal(70, n3.Location.X);
        Assert.Equal(50, n1.Location.Y);
        Assert.Equal(150, n2.Location.Y);
        Assert.Equal(250, n3.Location.Y);
    }

    [Fact]
    public void AlignCenter_MultipleSelectedNodes_AlignsToAverageX()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 50), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(200, 150), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(300, 250), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignCenter();

        Assert.Equal(200, n1.Location.X);
        Assert.Equal(200, n2.Location.X);
        Assert.Equal(200, n3.Location.X);
    }

    [Fact]
    public void AlignRight_MultipleSelectedNodes_AlignsToMaxX()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 50), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(200, 150), IsSelected = true };
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(350, 250), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        vm.AlignRight();

        Assert.Equal(350, n1.Location.X);
        Assert.Equal(350, n2.Location.X);
        Assert.Equal(350, n3.Location.X);
    }

    [Fact]
    public void AlignTop_SupportsUndo()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 150), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(300, 80), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);

        vm.AlignTop();
        Assert.Equal(80, n1.Location.Y);
        Assert.Equal(80, n2.Location.Y);

        vm.Undo();
        Assert.Equal(150, vm.Nodes[0].Location.Y);
        Assert.Equal(80, vm.Nodes[1].Location.Y);
    }

    [Fact]
    public void DistributeNodesHorizontally_ThreeNodes_DistributesEvenly()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 100), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(150, 100), IsSelected = true }; // intermediate, not centered
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(500, 100), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        // Act
        vm.DistributeNodesHorizontally();

        // Assert: First is at 100, Last is at 500, middle node is evenly distributed at 300
        Assert.Equal(100, n1.Location.X);
        Assert.Equal(300, n2.Location.X);
        Assert.Equal(500, n3.Location.X);
    }

    [Fact]
    public void DistributeNodesVertically_ThreeNodes_DistributesEvenly()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(100, 100), IsSelected = true };
        var n2 = new GuardNodeViewModel { Location = new Point(100, 150), IsSelected = true }; // intermediate
        var n3 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(100, 700), IsSelected = true };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);
        vm.Nodes.Add(n3);

        // Act
        vm.DistributeNodesVertically();

        // Assert: First is at 100, Last is at 700, middle node is evenly distributed at 400
        Assert.Equal(100, n1.Location.Y);
        Assert.Equal(400, n2.Location.Y);
        Assert.Equal(700, n3.Location.Y);
    }

    [Fact]
    public void SnapAllNodesToGrid_SnapsFractionalCoordinatesToMultiplesOfTen()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();

        var n1 = new TriggerNodeViewModel { Location = new Point(63.4, 87.6) };
        var n2 = new ActionNodeViewModel(tagCatalog.AllTags[0]) { Location = new Point(308.2, 142.1) };

        vm.Nodes.Add(n1);
        vm.Nodes.Add(n2);

        // Act
        vm.SnapAllNodesToGrid();

        // Assert: Coordinates snapped to nearest 10px multiples
        Assert.Equal(60, n1.Location.X);
        Assert.Equal(90, n1.Location.Y);
        Assert.Equal(310, n2.Location.X);
        Assert.Equal(140, n2.Location.Y);
    }

    [Fact]
    public void ApplyMagneticAlignment_ConnectedNeighborNearby_SnapsToExactSameY()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        trig.Location = new Point(100, 80);
        act.Location = new Point(350, 87.5); // dragged slightly off within 20px

        vm.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);

        // Act: User releases drag on act
        vm.ApplyMagneticAlignment(act);

        // Assert: act magnetically snaps to the exact Y of trig (80)
        Assert.Equal(80, act.Location.Y);
    }

    [Fact]
    public void CompleteConnection_NodesNearby_AutoAlignsTargetToSourceY()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        trig.Location = new Point(100, 80);
        act.Location = new Point(350, 95); // within 35px threshold

        // Act: Wire is drawn between them
        vm.CompleteConnection((trig.OutputConnectors[0], act.InputConnectors[0]));

        // Assert: Target node automatically aligns horizontally to source node's Y
        Assert.Equal(80, act.Location.Y);
        Assert.Single(vm.Connections);
    }

    [Fact]
    public void AlignSelectedNodesHorizontally_SingleSelectedNode_AlignsWithConnectedNeighbor()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        trig.Location = new Point(100, 80);
        act.Location = new Point(350, 110);
        vm.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);

        // Only act is selected
        vm.SelectExclusive(act);

        // Act
        vm.AlignSelectedNodesHorizontally();

        // Assert: act aligns to trig's Y
        Assert.Equal(80, act.Location.Y);
    }

    [Fact]
    public void DeleteSelectedNode_MultipleSelectedNodes_DeletesAllSelectedNodesAndAllConnections()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var inp = new InputNodeViewModel(tagCatalog.AllTags[0]);
        var trig = new TriggerNodeViewModel();
        var grd = new GuardNodeViewModel();
        var act = new ActionNodeViewModel(tagCatalog.AllTags[1]);

        vm.Nodes.Add(inp);
        vm.Nodes.Add(trig);
        vm.Nodes.Add(grd);
        vm.Nodes.Add(act);

        vm.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
        vm.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
        vm.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);

        Assert.Equal(4, vm.Nodes.Count);
        Assert.Equal(3, vm.Connections.Count);

        // Act: Simulating user sweeping/box-selecting ALL blocks on canvas
        foreach (var node in vm.Nodes)
        {
            node.IsSelected = true;
        }

        vm.DeleteSelectedNode();

        // Assert: Everything swept is deleted in one single action!
        Assert.Empty(vm.Nodes);
        Assert.Empty(vm.Connections);
        Assert.Null(vm.SelectedNode);
    }

    [Fact]
    public void DeleteSelectedNode_SubsetSelected_DeletesOnlySelectedAndCleansConnectedPins()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var inp = new InputNodeViewModel(tagCatalog.AllTags[0]);
        var trig = new TriggerNodeViewModel();
        var grd = new GuardNodeViewModel();
        var act = new ActionNodeViewModel(tagCatalog.AllTags[1]);

        vm.Nodes.Add(inp);
        vm.Nodes.Add(trig);
        vm.Nodes.Add(grd);
        vm.Nodes.Add(act);

        vm.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
        vm.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
        vm.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);

        // Act: Select only Trigger and Guard
        trig.IsSelected = true;
        grd.IsSelected = true;
        inp.IsSelected = false;
        act.IsSelected = false;

        vm.DeleteSelectedNode();

        // Assert: Trigger and Guard deleted; Input and Action survive
        Assert.Equal(2, vm.Nodes.Count);
        Assert.Contains(inp, vm.Nodes);
        Assert.Contains(act, vm.Nodes);
        Assert.DoesNotContain(trig, vm.Nodes);
        Assert.DoesNotContain(grd, vm.Nodes);

        // Connections attached to deleted nodes must be cleaned up
        Assert.Empty(vm.Connections);
        Assert.False(inp.OutputConnectors[0].IsConnected);
        Assert.False(act.InputConnectors[0].IsConnected);
    }

    [Fact]
    public void CompleteConnection_OutputToInput_CreatesConnection()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        var source = trig.OutputConnectors[0];
        var target = act.InputConnectors[0];

        // Act - simulating Nodify passing ValueTuple<object, object>
        vm.CompleteConnectionCommand.Execute((source, target));

        // Assert
        Assert.Single(vm.Connections);
        var conn = vm.Connections[0];
        Assert.Same(source, conn.Source);
        Assert.Same(target, conn.Target);
        Assert.True(source.IsConnected);
        Assert.True(target.IsConnected);
    }

    [Fact]
    public void CompleteConnection_InputToOutput_ReversesAndCreatesConnection()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        var source = act.InputConnectors[0]; // Dragged from Input
        var target = trig.OutputConnectors[0]; // Dropped on Output

        // Act
        vm.CompleteConnectionCommand.Execute((source, target));

        // Assert
        Assert.Single(vm.Connections);
        var conn = vm.Connections[0];
        Assert.Same(trig.OutputConnectors[0], conn.Source);
        Assert.Same(act.InputConnectors[0], conn.Target);
    }

    [Fact]
    public void CompleteConnection_SameNode_DoesNothing()
    {
        // Arrange
        var (vm, _, _) = CreateTestFixture();
        // Guard node has both Input and Output
        var guard = new GuardNodeViewModel { Location = new Point(200, 100) };
        vm.Nodes.Add(guard);

        // Act - connecting guard's output to guard's input
        vm.CompleteConnectionCommand.Execute((guard.OutputConnectors[0], guard.InputConnectors[0]));

        // Assert
        Assert.Empty(vm.Connections);
    }

    [Fact]
    public void CompleteConnection_SamePortType_DoesNothing()
    {
        // Arrange
        var (vm, trig, _) = CreateTestFixture();
        var trig2 = new TriggerNodeViewModel { Location = new Point(100, 200) };
        vm.Nodes.Add(trig2);

        // Act - connecting Output to Output
        vm.CompleteConnectionCommand.Execute((trig.OutputConnectors[0], trig2.OutputConnectors[0]));

        // Assert
        Assert.Empty(vm.Connections);
    }

    [Fact]
    public void CompleteConnection_ReplacesExistingTargetConnection()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        var trig2 = new TriggerNodeViewModel { Location = new Point(100, 200) };
        vm.Nodes.Add(trig2);

        // Connect trig -> act
        vm.CompleteConnectionCommand.Execute((trig.OutputConnectors[0], act.InputConnectors[0]));
        Assert.Single(vm.Connections);
        Assert.Same(trig.OutputConnectors[0], vm.Connections[0].Source);

        // Act - connect trig2 -> act (overwriting act's single input wire)
        vm.CompleteConnectionCommand.Execute((trig2.OutputConnectors[0], act.InputConnectors[0]));

        // Assert
        Assert.Single(vm.Connections);
        Assert.Same(trig2.OutputConnectors[0], vm.Connections[0].Source);
        Assert.Same(act.InputConnectors[0], vm.Connections[0].Target);
    }

    [Fact]
    public void RemoveConnection_RemovesConnectionSuccessfully()
    {
        // Arrange
        var (vm, trig, act) = CreateTestFixture();
        vm.CompleteConnectionCommand.Execute((trig.OutputConnectors[0], act.InputConnectors[0]));
        Assert.Single(vm.Connections);
        var conn = vm.Connections[0];

        // Act
        vm.RemoveConnectionCommand.Execute(conn);

        // Assert
        Assert.Empty(vm.Connections);
        Assert.False(trig.OutputConnectors[0].IsConnected);
        Assert.False(act.InputConnectors[0].IsConnected);
    }

    [Fact]
    public void EditingCustomLabel_UpdatesDisplayTitle()
    {
        // Arrange
        var trig = new TriggerNodeViewModel();
        Assert.Equal("Kích hoạt", trig.DisplayTitle);

        // Act
        trig.CustomLabel = "Kích hoạt dừng ép";

        // Assert
        Assert.Equal("Kích hoạt dừng ép", trig.DisplayTitle);
    }

    [Fact]
    public void EditingTagAlias_UpdatesTagShortTextAndNarrative()
    {
        // Arrange
        var tag = new TagModel { Name = "DI0", Alias = "Cũ" };
        var inputNode = new InputNodeViewModel(tag);
        Assert.Equal("Cũ", inputNode.TagShortText);

        var vm = new LogicEditorViewModel(new TagCatalogViewModel());
        vm.SelectedNode = inputNode;

        // Act
        tag.Alias = "Cảm biến cửa mở";

        // Assert
        Assert.Equal("Cảm biến cửa mở", inputNode.TagShortText);
        Assert.Contains("Cảm biến cửa mở", vm.NarrativePreview);
    }

    [Fact]
    public void CompactBadgeText_ReflectsPropertyUpdates()
    {
        // Arrange
        var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, ForMs = 0 };
        Assert.Equal("Sườn lên", trig.CompactBadgeText);

        // Act & Assert
        trig.ForMs = 15000;
        Assert.Equal("Sườn lên (15000ms)", trig.CompactBadgeText);

        trig.CompareOp = CompareOp.GT;
        trig.ThresholdLo = 50;
        Assert.Equal("> 50 (15000ms)", trig.CompactBadgeText);

        var guard = new GuardNodeViewModel(new TagModel { Name = "VFLAG0" }) { Negate = false };
        Assert.Equal("VFLAG0", guard.CompactBadgeText);

        guard.Negate = true;
        Assert.Equal("! VFLAG0", guard.CompactBadgeText);

        var tag = new TagModel { Name = "DO0" };
        var act = new ActionNodeViewModel(tag) { ActionType = ActionType.SET_TAG, ActionParam = 1 };
        Assert.Equal("DO0 = 1", act.CompactBadgeText);
    }

    [Fact]
    public void HighContrastExpressions_RenderProperly()
    {
        // Trigger expressions
        var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_FALL, ForMs = 15000, CompareOp = CompareOp.EQ, ThresholdLo = 1 };
        Assert.Equal("Sườn xuống (1 → 0)", trig.TriggerActionText);
        Assert.Equal("15000ms", trig.TriggerParamText);
        Assert.Equal("== 1", trig.ConditionExpression);

        // Guard expressions
        var guard = new GuardNodeViewModel(new TagModel { Name = "VFLAG0" }) { Negate = false };
        Assert.Equal("VFLAG0", guard.ConditionExpression);
        Assert.Equal(SimplePLC.Studio.Services.LocalizationService.Tr("BadgeGuardPass"), guard.ConditionBadgeText);

        guard.Negate = true;
        Assert.Equal("NOT VFLAG0", guard.ConditionExpression);
        Assert.Equal(SimplePLC.Studio.Services.LocalizationService.Tr("BadgeGuardInvert"), guard.ConditionBadgeText);
    }

    [Fact]
    public void InputNode_ZeroRedundancy_DisplayProperties()
    {
        var tag = new TagModel { Name = "DI0", Alias = "Máy đang chạy", Kind = TagKind.DiscreteInput, Value = 0 };
        var inputNode = new InputNodeViewModel(tag);

        // Header displays Tag Name and Tag Kind (no duplicate "Input Tag")
        Assert.Equal("DI0", inputNode.DisplayTitle);
        Assert.Equal("DI", inputNode.TagKindText);

        // Body displays Alias and Value Status (no duplicate "DI0")
        Assert.Equal("Máy đang chạy", inputNode.TagDescriptionText);
        Assert.Equal("○ TẮT", inputNode.ValueBadgeText);

        // When Value transitions to 1
        tag.Value = 1;
        Assert.Equal("● BẬT", inputNode.ValueBadgeText);

        // Action node displays target tag alias without duplicating target tag code
        var tagDO0 = new TagModel { Name = "DO0", Alias = "Đèn còi đỏ", Kind = TagKind.DiscreteOutput };
        var actionNode = new ActionNodeViewModel(tagDO0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };
        Assert.Equal("DO0 = 1", actionNode.ActionExpression);
        Assert.Equal("Đèn còi đỏ", actionNode.TargetTagShortText);
    }

    [Fact]
    public void GuardNode_FourBlockChain_EndToEnd_RuleProducedWithGuardTag()
    {
        // Setup
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var vm = new LogicEditorViewModel(tagCatalog, ruleTable);
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var vflag0 = tagCatalog.AllTags.First(t => t.Name == "VFLAG0");
        vflag0.Alias = "Trong ca";
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inp = new InputNodeViewModel(di0) { Location = new Point(60, 80) };
        var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_FALL, CompareOp = CompareOp.EQ, ThresholdLo = 1, Location = new Point(310, 80) };
        var grd = new GuardNodeViewModel(vflag0) { Location = new Point(570, 80) };
        var act = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(830, 80) };

        // Assert pin counts: Guard must have exactly 1 input ("In") and 1 output ("Out")
        Assert.Single(grd.InputConnectors);
        Assert.Equal("In", grd.InputConnectors[0].Title);
        Assert.Single(grd.OutputConnectors);
        Assert.Equal("Out", grd.OutputConnectors[0].Title);

        // Assert expression includes actual configured GuardTag (using friendly Alias)
        Assert.Equal("Trong ca", grd.ConditionExpression);

        vm.Nodes.Add(inp);
        vm.Nodes.Add(trig);
        vm.Nodes.Add(grd);
        vm.Nodes.Add(act);

        // Connect 4 blocks in linear chain
        vm.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
        vm.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
        vm.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);

        // Act
        vm.CompileNow();

        // Assert: 1 valid rule produced with correct GuardTag
        Assert.Equal(1, vm.ValidRuleCount);
        var rule = ruleTable.Rules.First();
        Assert.Equal("DI0", rule.TriggerTag?.Name);
        Assert.Equal("VFLAG0", rule.GuardTag?.Name);
        Assert.Equal("DO0", rule.ActionTag?.Name);
        Assert.Equal(CompareOp.EQ, rule.CompareOp);
        Assert.Equal(1, rule.ThresholdLo);
    }

    [Fact]
    public void TriggerNode_ComparisonMode_TogglesRangeAndPromptsProperly()
    {
        var trig = new TriggerNodeViewModel();

        // Default: NONE
        Assert.Equal(CompareOp.NONE, trig.CompareOp);
        Assert.False(trig.IsRangeComparison);
        Assert.False(trig.HasComparison);

        // Switch to EQ
        trig.CompareOp = CompareOp.EQ;
        Assert.Equal(CompareOp.EQ, trig.CompareOp);
        Assert.False(trig.IsRangeComparison);
        Assert.True(trig.HasComparison);
        Assert.Contains("so sánh", trig.ThresholdPromptText);

        // Switch to BETWEEN range
        trig.CompareOp = CompareOp.BETWEEN;
        Assert.True(trig.IsRangeComparison);
        Assert.True(trig.HasComparison);
        Assert.Contains("Ngưỡng dưới", trig.ThresholdPromptText);

        // Switch to NONE
        trig.CompareOp = CompareOp.NONE;
        Assert.False(trig.IsRangeComparison);
        Assert.False(trig.HasComparison);
    }
}
