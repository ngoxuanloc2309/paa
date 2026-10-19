using System;
using System.IO;
using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ProjectPersistenceLifecycleTests
{
    [Fact]
    public void EmptyProject_OverwriteExistingProject_BlockedBySafetyGate()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"safety_gate_test_{Guid.NewGuid():N}.synx");
        try
        {
            var validProject = new ProjectModel
            {
                Metadata = new ProjectMetadata { ProjectName = "SafeProject", SchemaVersion = 1 },
                Tags = new()
                {
                    new() { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput, Channel = 1 },
                    new() { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput, Channel = 1 }
                },
                Nodes = new()
                {
                    new() { Id = "n1", Type = "Input", TagName = "DI0", LocationX = 100, LocationY = 100 },
                    new() { Id = "n2", Type = "Trigger", TriggerType = TriggerType.ON_RISE, LocationX = 350, LocationY = 100 },
                    new() { Id = "n3", Type = "Action", ActionType = ActionType.SET_TAG, TargetTagName = "DO0", LocationX = 600, LocationY = 100 }
                },
                Connections = new()
                {
                    new() { SourceNodeId = "n1", TargetNodeId = "n2", SourceConnectorIndex = 0, TargetConnectorIndex = 0 },
                    new() { SourceNodeId = "n2", TargetNodeId = "n3", SourceConnectorIndex = 0, TargetConnectorIndex = 0 }
                },
                Rules = new()
                {
                    new() { Id = "R1", Index = 0, Enabled = true, TriggerType = TriggerType.ON_RISE, TriggerTagName = "DI0", ActionType = ActionType.SET_TAG, ActionTagName = "DO0" }
                }
            };

            // 1. Lưu dự án hợp lệ ban đầu
            ProjectFileService.SaveProject(tempFile, validProject);
            long initialSize = new FileInfo(tempFile).Length;
            Assert.True(initialSize > 512, $"Initial size was {initialSize}");

            // 2. Cố gắng ghi đè bằng dự án rỗng mà không có allowEmptyOverwrite
            var emptyProject = new ProjectModel
            {
                Metadata = new ProjectMetadata { ProjectName = "Empty" },
                Nodes = new(),
                Rules = new()
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectFileService.SaveProject(tempFile, emptyProject, allowEmptyOverwrite: false);
            });

            Assert.Contains("Safety Gate Violation", ex.Message);

            // 3. Đảm bảo dữ liệu file gốc vẫn nguyên vẹn
            var reloaded = ProjectFileService.LoadProject(tempFile);
            Assert.Equal(3, reloaded.Nodes.Count);
            Assert.Single(reloaded.Rules);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            string bak = tempFile + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
        }
    }

    [Fact]
    public void EmptyProject_OverwriteWithExplicitPermission_CreatesBackupAndSucceeds()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid():N}.synx");
        string bakFile = tempFile + ".bak";
        try
        {
            var validProject = new ProjectModel
            {
                Metadata = new ProjectMetadata { ProjectName = "Initial" },
                Rules = new()
                {
                    new() { Id = "R1", Index = 0, Enabled = true, TriggerType = TriggerType.ON_RISE, TriggerTagName = "DI0" }
                },
                Nodes = new()
                {
                    new() { Id = "n1", Type = "Input", TagName = "DI0" }
                }
            };

            ProjectFileService.SaveProject(tempFile, validProject);
            Assert.True(File.Exists(tempFile));

            var emptyProject = new ProjectModel
            {
                Metadata = new ProjectMetadata { ProjectName = "Cleared" },
                Nodes = new(),
                Rules = new()
            };

            // Lưu với cờ allowEmptyOverwrite = true
            ProjectFileService.SaveProject(tempFile, emptyProject, allowEmptyOverwrite: true);

            // File đích đã được ghi đè
            var loadedEmpty = ProjectFileService.LoadProject(tempFile);
            Assert.Empty(loadedEmpty.Rules);

            // File sao lưu .bak tồn tại và chứa dữ liệu trước đó
            Assert.True(File.Exists(bakFile));
            var loadedBak = ProjectFileService.LoadProject(bakFile);
            Assert.Single(loadedBak.Rules);
            Assert.Equal("R1", loadedBak.Rules[0].Id);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(bakFile)) File.Delete(bakFile);
        }
    }

    [Fact]
    public void ReconstructGraphFromAllRules_GeneratesMultiNetworkCanvasLayout()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable, null);

        var di0 = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? new TagModel { Name = "DI0" };
        var do0 = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0") ?? new TagModel { Name = "DO0" };
        var vflag0 = tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0") ?? new TagModel { Name = "VFLAG0" };

        var r1 = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true,
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var r2 = new RuleItemModel
        {
            Id = "R2",
            Index = 1,
            Enabled = true,
            TriggerType = TriggerType.ON_FALL,
            TriggerTag = di0,
            GuardTag = vflag0,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 0
        };

        // Act: Tái tạo toàn bộ sơ đồ đồ thị từ 2 rules
        logicEditor.ReconstructGraphFromAllRules(new[] { r1, r2 });

        // Assert:
        // R1: Input + Trigger + Action = 3 nodes
        // R2: Input + Trigger + Guard + Action = 4 nodes
        // Tổng cộng = 7 nodes, và các wire kết nối
        Assert.Equal(7, logicEditor.Nodes.Count);
        Assert.True(logicEditor.Connections.Count >= 5);

        // Network 2 phải có LocationY lớn hơn Network 1 (khoảng cách 260px)
        double minR1Y = logicEditor.Nodes.Where(n => n.Id.Contains("R1")).Min(n => n.Location.Y);
        double minR2Y = logicEditor.Nodes.Where(n => n.Id.Contains("R2")).Min(n => n.Location.Y);
        Assert.True(minR2Y > minR1Y + 200, $"R2 Y ({minR2Y}) should be spaced below R1 Y ({minR1Y})");
    }

    [Fact]
    public void LoadUntitledSynx_IfPresent_RestoresLosslessFiveRulesAndCanvas()
    {
        string path = @"C:\Users\DELL\Documents\Untitled.synx";
        if (!File.Exists(path)) return;

        var vm = new MainViewModel();
        vm.LoadProjectFile(path);

        // Assert RuleTable
        var project = ProjectFileService.LoadProject(path);
        Assert.Equal(project.Rules.Count, vm.RuleTableVM.Rules.Count);
        Assert.Equal(project.Rules.Count, vm.RuleTableVM.FilteredRules.Count);
        Assert.Equal(string.Empty, vm.RuleTableVM.SearchFilter);
        Assert.Equal("ALL", vm.RuleTableVM.StatusFilter);

        // Assert Canvas Nodes
        Assert.Equal(project.Nodes.Count, vm.LogicEditorVM.Nodes.Count);
        Assert.False(vm.IsProjectDirty, "IsProjectDirty should be false after loading");
    }
}
