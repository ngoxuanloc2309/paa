using System.IO;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ProjectFileServiceTests
{
    [Fact]
    public void SaveAndLoadProject_Roundtrip_PreservesAllData()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_proj_{Guid.NewGuid():N}.synx");
        try
        {
            var project = new ProjectModel
            {
                Metadata = new ProjectMetadata
                {
                    ProjectName = "Tram_Bom_Tu_Dong",
                    Author = "HoaNV",
                    Description = "Dự án điều khiển trạm bơm tự động 2 bơm luân phiên",
                    TargetDevice = "SynaptiX SYNX-RIO-884",
                    SchemaVersion = 1
                },
                Tags = new List<ProjectTagData>
                {
                    new() { Index = 1, Name = "DI0", Alias = "Báo cạn", Kind = TagKind.DiscreteInput, Channel = 1, Value = 1 },
                    new() { Index = 9, Name = "DO0", Alias = "Bơm 1", Kind = TagKind.DiscreteOutput, Channel = 1, Value = 0 }
                },
                Nodes = new List<ProjectNodeData>
                {
                    new() { Id = "node-inp-1", Type = "Input", LocationX = 100, LocationY = 150, TagName = "DI0" },
                    new() { Id = "node-trg-1", Type = "Trigger", LocationX = 350, LocationY = 150, TriggerType = TriggerType.ON_FALL, ForMs = 5000 },
                    new() { Id = "node-grd-1", Type = "Guard", LocationX = 600, LocationY = 150, CompareOp = CompareOp.GT, ThresholdLo = 80, Negate = true },
                    new() { Id = "node-act-1", Type = "Action", LocationX = 850, LocationY = 150, ActionType = ActionType.SET_TAG, TargetTagName = "DO0", ActionParam = 1 }
                },
                Connections = new List<ProjectConnectionData>
                {
                    new() { SourceNodeId = "node-inp-1", SourceConnectorIndex = 0, SourceConnectorTitle = "Out", TargetNodeId = "node-trg-1", TargetConnectorIndex = 0, TargetConnectorTitle = "In" },
                    new() { SourceNodeId = "node-trg-1", SourceConnectorIndex = 0, SourceConnectorTitle = "Out", TargetNodeId = "node-grd-1", TargetConnectorIndex = 0, TargetConnectorTitle = "Evt" },
                    new() { SourceNodeId = "node-grd-1", SourceConnectorIndex = 0, SourceConnectorTitle = "Out", TargetNodeId = "node-act-1", TargetConnectorIndex = 0, TargetConnectorTitle = "In" }
                }
            };

            // Act: Save
            ProjectFileService.SaveProject(tempFile, project);
            Assert.True(File.Exists(tempFile));

            // Act: Load
            var loaded = ProjectFileService.LoadProject(tempFile);

            // Assert: Metadata
            Assert.NotNull(loaded);
            Assert.Equal("Tram_Bom_Tu_Dong", loaded.Metadata.ProjectName);
            Assert.Equal("HoaNV", loaded.Metadata.Author);
            Assert.Equal("SynaptiX SYNX-RIO-884", loaded.Metadata.TargetDevice);
            Assert.Equal(1, loaded.Metadata.SchemaVersion);

            // Assert: Tags
            Assert.Equal(2, loaded.Tags.Count);
            Assert.Equal("DI0", loaded.Tags[0].Name);
            Assert.Equal("Báo cạn", loaded.Tags[0].Alias);
            Assert.Equal(TagKind.DiscreteInput, loaded.Tags[0].Kind);

            // Assert: Nodes
            Assert.Equal(4, loaded.Nodes.Count);
            var inp = loaded.Nodes[0];
            Assert.Equal("node-inp-1", inp.Id);
            Assert.Equal("Input", inp.Type);
            Assert.Equal(100, inp.LocationX);
            Assert.Equal(150, inp.LocationY);
            Assert.Equal("DI0", inp.TagName);

            var trg = loaded.Nodes[1];
            Assert.Equal(TriggerType.ON_FALL, trg.TriggerType);
            Assert.Equal(5000u, trg.ForMs);

            var grd = loaded.Nodes[2];
            Assert.Equal(CompareOp.GT, grd.CompareOp);
            Assert.Equal(80, grd.ThresholdLo);
            Assert.True(grd.Negate);

            var act = loaded.Nodes[3];
            Assert.Equal(ActionType.SET_TAG, act.ActionType);
            Assert.Equal("DO0", act.TargetTagName);
            Assert.Equal(1, act.ActionParam);

            // Assert: Connections
            Assert.Equal(3, loaded.Connections.Count);
            Assert.Equal("node-inp-1", loaded.Connections[0].SourceNodeId);
            Assert.Equal("node-trg-1", loaded.Connections[0].TargetNodeId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadNonExistentProject_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            ProjectFileService.LoadProject("G:\\non_existent_path_12345.synx");
        });
    }

    [Fact]
    public void RecentProjects_AddAndRetrieve_WorksCorrectly()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"recent_test_{Guid.NewGuid():N}.synx");
        try
        {
            File.WriteAllText(tempFile, "{}");
            ProjectFileService.AddRecentProject(tempFile);

            var list = ProjectFileService.GetRecentProjects();
            Assert.Contains(list, p => string.Equals(p, Path.GetFullPath(tempFile), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadUntitledSynx_DebugInspection()
    {
        string path = @"C:\Users\DELL\Documents\Untitled.synx";
        if (!File.Exists(path)) return;
        var vm = new ViewModels.MainViewModel();
        vm.LoadProjectFile(path);
        Assert.True(vm.RuleTableVM.Rules.Count > 0, $"Rule count: {vm.RuleTableVM.Rules.Count}");
        Assert.True(vm.LogicEditorVM.Nodes.Count > 0, $"Node count: {vm.LogicEditorVM.Nodes.Count}");
    }

    [Theory]
    [InlineData("examples/SPX_Sort_To_Light_Logistics_Center.synx")]
    [InlineData("examples/Modbus_RTU_Master_VFD_And_Sensor_Loop.synx")]
    [InlineData("examples/IIoT_Cold_Chain_Smart_Gateway.synx")]
    public void LoadExampleProjects_LoadsSuccessfullyWithNodesAndMetadata(string relativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", relativePath);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.GetFullPath(relativePath);
        }
        Assert.True(File.Exists(fullPath), $"Example file must exist: {fullPath}");

        var project = ProjectFileService.LoadProject(fullPath);
        Assert.NotNull(project);
        Assert.NotNull(project.Metadata);
        Assert.False(string.IsNullOrWhiteSpace(project.Metadata.ProjectName));
        Assert.NotEmpty(project.Tags);
        Assert.NotEmpty(project.Nodes);
    }
}
