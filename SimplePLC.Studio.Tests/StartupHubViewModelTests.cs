using System.IO;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị cho màn hình khởi động tiêu chuẩn quốc tế (Startup Hub / Welcome Window)
/// theo học thuyết TDD & IEC 61508-3.
/// </summary>
public class StartupHubViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _fakeProject1;
    private readonly string _fakeProject2;
    private readonly string _uniqueAlpha;
    private readonly string _uniqueBeta;

    public StartupHubViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SynaptixStartupHubTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        ProjectFileService.CustomRecentProjectsFilePath = Path.Combine(_tempDir, "recent_projects.json");

        _uniqueAlpha = "Alpha_" + Guid.NewGuid().ToString("N");
        _uniqueBeta = "Beta_" + Guid.NewGuid().ToString("N");

        _fakeProject1 = Path.Combine(_tempDir, $"{_uniqueAlpha}.synx");
        _fakeProject2 = Path.Combine(_tempDir, $"{_uniqueBeta}.synx");

        File.WriteAllText(_fakeProject1, "{}");
        File.WriteAllText(_fakeProject2, "{}");

        // Seed recent projects
        ProjectFileService.AddRecentProject(_fakeProject1, _uniqueAlpha, "SynaptiX SYNX-RIO-884", "[SYNX-RIO-884]");
        ProjectFileService.AddRecentProject(_fakeProject2, _uniqueBeta, "SynaptiX SYNX-DTU-485", "[SYNX-DTU-485]");
    }

    public void Dispose()
    {
        ProjectFileService.CustomRecentProjectsFilePath = null;
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void StartupHubViewModel_InitialState_LoadsRecentProjectsAndPorts()
    {
        var vm = new StartupHubViewModel();

        Assert.NotEmpty(vm.RecentProjects);
        Assert.NotEmpty(vm.FilteredRecentProjects);
        Assert.True(vm.HasRecentProjects);
        Assert.True(vm.HasFilteredResults);
        Assert.Contains(vm.RecentProjects, p => p.ProjectName.Contains(_uniqueAlpha));
    }

    [Fact]
    public void StartupHubViewModel_SearchFilter_FiltersByProjectNameOrHardwareBadge()
    {
        var vm = new StartupHubViewModel();

        // Filter by unique project name
        vm.SearchText = _uniqueAlpha;
        Assert.Single(vm.FilteredRecentProjects);
        Assert.Equal(_uniqueAlpha, vm.FilteredRecentProjects[0].ProjectName);

        // Filter by unique project name for Beta
        vm.SearchText = _uniqueBeta;
        Assert.Single(vm.FilteredRecentProjects);
        Assert.Equal(_uniqueBeta, vm.FilteredRecentProjects[0].ProjectName);

        // Clear filter
        vm.SearchText = string.Empty;
        Assert.True(vm.FilteredRecentProjects.Count >= 2);

        // Filter non-existing
        vm.SearchText = "NonExistingXYZ_9999999";
        Assert.Empty(vm.FilteredRecentProjects);
        Assert.False(vm.HasFilteredResults);
    }

    [Fact]
    public void StartupHubViewModel_LimitsRecentProjectsDisplay_ToMaximumFive()
    {
        var vm = new StartupHubViewModel();
        vm.RecentProjects.Clear();

        // Thêm 10 dự án giả lập vào danh sách gốc
        for (int i = 1; i <= 10; i++)
        {
            var entry = new RecentProjectEntry
            {
                ProjectName = $"Project_{i:D2}",
                FilePath = $"C:\\Projects\\Project_{i:D2}.synx",
                HardwareTarget = "SYNX-RIO-884",
                HardwareBadge = "[PLC]"
            };
            vm.RecentProjects.Add(new RecentProjectItemViewModel(entry));
        }

        vm.ApplyFilter();

        // Đảm bảo chỉ hiển thị tối đa 5 dự án gần đây nhất để giao diện luôn tinh giản, không rối mắt
        Assert.Equal(StartupHubViewModel.MaxRecentProjectsDisplay, vm.FilteredRecentProjects.Count);
        Assert.Equal(5, vm.FilteredRecentProjects.Count);
    }

    [Fact]
    public void StartupHubViewModel_Commands_TriggerExpectedActionsAndClose()
    {
        var vm = new StartupHubViewModel();
        bool closed = false;
        vm.RequestClose += () => closed = true;

        // 1. Create New Project
        closed = false;
        vm.CreateNewProjectCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(StartupHubAction.NewProject, vm.SelectedAction);

        // 2. Open Local Project
        closed = false;
        vm.OpenLocalProjectCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(StartupHubAction.OpenLocalProject, vm.SelectedAction);

        // 3. Open Recent Project
        closed = false;
        var firstItem = vm.RecentProjects[0];
        vm.OpenRecentProjectCommand.Execute(firstItem);
        Assert.True(closed);
        Assert.Equal(StartupHubAction.OpenRecentProject, vm.SelectedAction);
        Assert.Equal(firstItem.FilePath, vm.SelectedRecentProjectPath);

        // 4. Upload From Device
        closed = false;
        vm.UploadFromDeviceCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(StartupHubAction.UploadFromDevice, vm.SelectedAction);

        // 5. Continue Blank
        closed = false;
        vm.ContinueBlankCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(StartupHubAction.ContinueBlank, vm.SelectedAction);
    }

    [Fact]
    public void StartupHubViewModel_PinAndRemove_PersistsChanges()
    {
        var vm = new StartupHubViewModel();
        var item = vm.RecentProjects.FirstOrDefault(p => p.FilePath == _fakeProject1);
        Assert.NotNull(item);

        // Toggle Pin
        bool initialPin = item.IsPinned;
        item.TogglePinCommand.Execute(null);

        // Re-read from service
        var entries = ProjectFileService.GetRecentProjectEntries();
        var updatedEntry = entries.FirstOrDefault(e => e.FilePath == _fakeProject1);
        Assert.NotNull(updatedEntry);
        Assert.Equal(!initialPin, updatedEntry.IsPinned);

        // Remove
        item.RemoveCommand.Execute(null);
        entries = ProjectFileService.GetRecentProjectEntries();
        Assert.DoesNotContain(entries, e => e.FilePath == _fakeProject1);
    }

    [Fact]
    public void RecentProjectItemViewModel_FormatRelativeTime_ReturnsCorrectString()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
        string justNow = RecentProjectItemViewModel.FormatRelativeTime(DateTime.UtcNow);
        Assert.Equal("Vừa xong", justNow);

        string minutesAgo = RecentProjectItemViewModel.FormatRelativeTime(DateTime.UtcNow.AddMinutes(-5));
        Assert.Contains("phút trước", minutesAgo);

        LocalizationService.Instance.CurrentLanguage = "en";
        string justNowEn = RecentProjectItemViewModel.FormatRelativeTime(DateTime.UtcNow);
        Assert.Equal("Just now", justNowEn);

        string minutesAgoEn = RecentProjectItemViewModel.FormatRelativeTime(DateTime.UtcNow.AddMinutes(-5));
        Assert.Contains("5m ago", minutesAgoEn);
    }

    [Fact]
    public void MainViewModel_PromptStartupHub_IntegratesWithHandler()
    {
        var mainVM = new MainViewModel(act => act());
        bool startupHubInvoked = false;

        mainVM.ShowStartupHubDialogHandler = (hubVm) =>
        {
            startupHubInvoked = true;
            hubVm.CreateNewProject();
            return true;
        };

        // When dialog closes with NewProject, it triggers NewProject dialog handler
        bool newProjectInvoked = false;
        mainVM.ShowNewProjectDialogHandler = (newProjVm) =>
        {
            newProjectInvoked = true;
            return false; // Cancel out
        };

        mainVM.PromptStartupHub();

        Assert.True(startupHubInvoked, "ShowStartupHubDialogHandler must be called by PromptStartupHub()");
        Assert.True(newProjectInvoked, "NewProject() must be triggered when SelectedAction is NewProject");
    }

    [Fact]
    public void MainViewModel_InitialState_DefaultsToStartupHubView()
    {
        var mainVM = new MainViewModel(act => act());

        Assert.NotNull(mainVM.CurrentView);
        Assert.IsType<StartupHubViewModel>(mainVM.CurrentView);
        Assert.True(mainVM.IsStartPageActive);

        // When ContinueBlank is triggered, transitions to LogicGraph workspace view
        mainVM.StartupHubVM.ContinueBlankCommand.Execute(null);
        Assert.False(mainVM.IsStartPageActive);
        Assert.IsType<LogicEditorViewModel>(mainVM.CurrentView);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);

        // When CloseProject is triggered, transitions back to StartupHubView
        mainVM.CloseProjectCommand.Execute(null);
        Assert.True(mainVM.IsStartPageActive);
        Assert.IsType<StartupHubViewModel>(mainVM.CurrentView);
    }
}
