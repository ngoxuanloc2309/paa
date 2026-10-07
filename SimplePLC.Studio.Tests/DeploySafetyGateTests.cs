using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

[Collection("AppServicesDeployTests")]
public class DeploySafetyGateTests : IDisposable
{
    public DeploySafetyGateTests()
    {
        // Tests in this class assert Vietnamese UI strings
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    private async Task<(DeployViewModel deployVM, LogicEditorViewModel logicVM, FakeModbusClient client)> CreateFixtureAsync()
    {
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.LoadDefaultDemoGraph();
        logicVM.CompileNow();
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicVM);

        return (deployVM, logicVM, fakeClient);
    }

    [Fact]
    public async Task Deploy_WhenLogicStateIsValid_Succeeds()
    {
        var (deployVM, logicVM, client) = await CreateFixtureAsync();

        Assert.Equal(CompileState.Valid, logicVM.CompileState);
        Assert.NotNull(logicVM.CurrentProgram);
        Assert.True(deployVM.CanDeploy);

        await deployVM.StartDeployAsync();

        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Equal(StepStatus.Success, deployVM.Steps[0].Status);
        Assert.Equal(StepStatus.Success, deployVM.Steps[4].Status);
    }

    [Fact]
    public async Task Deploy_WhenLogicStateIsStale_IsBlockedAtPreflight()
    {
        var (deployVM, logicVM, client) = await CreateFixtureAsync();

        // Alter logic graph so it transitions to Stale
        logicVM.AddNode("Trigger", new System.Windows.Point(500, 500));
        Assert.Equal(CompileState.Stale, logicVM.CompileState);
        Assert.False(deployVM.CanDeploy);

        // Act: attempt deploy
        await deployVM.StartDeployAsync();

        // Assert: Staging buffer must not be touched, DeployState must be Failed
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Failed, deployVM.DeployState);
        Assert.Contains("Stale", deployVM.PreflightSummary);
        Assert.Contains("Dừng trước staging", deployVM.ConsoleLog);
    }

    [Fact]
    public async Task Deploy_WhenLogicStateIsInvalid_IsBlockedAtPreflight()
    {
        var (deployVM, logicVM, client) = await CreateFixtureAsync();

        // Break graph: Action node without incoming edge is an error (Invalid)
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();
        logicVM.Nodes.Add(new ActionNodeViewModel());
        logicVM.CompileNow();

        Assert.Equal(CompileState.Invalid, logicVM.CompileState);
        Assert.False(deployVM.CanDeploy);

        // Act: attempt deploy
        await deployVM.StartDeployAsync();

        // Assert
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Failed, deployVM.DeployState);
        Assert.Contains("Invalid", deployVM.PreflightSummary);
        Assert.Contains("Dừng trước staging", deployVM.ConsoleLog);
    }

    [Fact]
    public async Task Deploy_ConsumesCompiledProgramDirectly_NotMutableRuleTable()
    {
        var (deployVM, logicVM, client) = await CreateFixtureAsync();

        Assert.NotNull(logicVM.CurrentProgram);
        int expectedRuleCount = logicVM.CurrentProgram.RuleCount;

        await deployVM.StartDeployAsync();

        Assert.True(deployVM.IsSuccess);
        Assert.Contains($"Nạp thành công {expectedRuleCount} quy tắc", deployVM.StatusMessage);
    }

    [Fact]
    public void Deploy_DeviceInfoBadge_UsesIndustrialBranding_NoChipName()
    {
        var deployVM = new DeployViewModel();
        Assert.Contains("SimplePLC", deployVM.DeviceInfoBadge);
        Assert.Contains("USB CDC", deployVM.DeviceInfoBadge);
        Assert.DoesNotContain("STM32", deployVM.DeviceInfoBadge);
        Assert.DoesNotContain("ARM", deployVM.DeviceInfoBadge);
    }

    [Fact]
    public async Task Deploy_StateSynchronization_ShowProgressBarFalseInitially()
    {
        var (deployVM, logicVM, client) = await CreateFixtureAsync();
        Assert.False(deployVM.ShowProgressBar);
        Assert.Equal("Sẵn sàng", deployVM.StepperBadgeText);
        Assert.Contains("Sẵn Sàng Nạp", deployVM.StatusTitle);

        await deployVM.StartDeployAsync();
        Assert.True(deployVM.ShowProgressBar);
        Assert.Equal(100, deployVM.ProgressPercent);
        Assert.Equal("Hoàn thành 100%", deployVM.StepperBadgeText);
    }

    [Fact]
    public void Deploy_NavigationCommands_TriggerCorrectTabs()
    {
        int navigatedTab = -1;
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicEditorVM: null, tab => navigatedTab = tab);

        deployVM.NavigateToRulesCommand.Execute(null);
        Assert.Equal(2, navigatedTab);

        deployVM.NavigateToLogicEditorCommand.Execute(null);
        Assert.Equal(0, navigatedTab);

        deployVM.NavigateToLiveWatchCommand.Execute(null);
        Assert.Equal(5, navigatedTab);
    }

    [Fact]
    public void Deploy_ClearLogCommand_ClearsConsole()
    {
        var deployVM = new DeployViewModel();
        Assert.NotEmpty(deployVM.ConsoleLog);

        deployVM.ClearLogCommand.Execute(null);
        Assert.Contains("Đã xóa nhật ký", deployVM.ConsoleLog);
    }
}

