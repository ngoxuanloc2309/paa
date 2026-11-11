using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử cổng an toàn nạp (Deploy Safety Gate) và nạp cấu hình thuần (Zero-Rule Config-Only).
/// </summary>
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

    private async Task<(DeployViewModel deployVM, LogicEditorViewModel logicVM, FakeModbusClient client)> CreateFixtureAsync(Func<ProjectMetadata>? getMetadata = null)
    {
        AppServices.Instance.CurrentProduct = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.LoadDefaultDemoGraph();
        logicVM.CompileNow();
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicVM, getMetadata: getMetadata);

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
        Assert.Contains("SynaptiX", deployVM.DeviceInfoBadge);
        Assert.Contains("RS-485/USB", deployVM.DeviceInfoBadge);
        Assert.DoesNotContain("SimplePLC", deployVM.DeviceInfoBadge);
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

    [Fact]
    public async Task DeployToDeviceAsync_WhenDisconnected_DoesNotNavigateToBlueprintsAndStaysOnCurrentTab()
    {
        // Arrange - Ensure client is explicitly disconnected
        AppServices.Instance.SetModbusClient(new FakeModbusClient());
        var mainVm = new MainViewModel();
        mainVm.SelectTab(AppTabIndices.Device);
        Assert.Equal(AppTabIndices.Device, mainVm.SelectedTabIndex);

        // Act - Click Quick Deploy when device is disconnected
        await mainVm.DeployToDeviceAsync();

        // Assert - Does NOT jump to Blueprints (index 4) or Deploy (index 5); stays on Device tab (index 0)
        Assert.Equal(AppTabIndices.Device, mainVm.SelectedTabIndex);
        Assert.Contains("Thiết bị chưa được kết nối", mainVm.LogicEditorVM.SaveStatusMessage);
    }

    [Fact]
    public void Deploy_WhenZeroRules_AndNoModbusChannels_CanDeployIsFalse()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.Rules.Clear();
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();
        logicVM.CompileNow();

        var meta = new ProjectMetadata();
        meta.ModbusPorts.ForEach(p => p.MasterChannels.Clear());

        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicVM, getMetadata: () => meta);

        Assert.False(deployVM.CanDeploy);
        Assert.Contains(LocalizationService.Tr("DeployPreflightNone"), deployVM.PreflightSummary);
    }

    [Fact]
    public async Task Deploy_WhenZeroRules_ButModbusChannelsConfigured_CanDeployIsTrue()
    {
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.Rules.Clear();
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();
        logicVM.CompileNow();

        var meta = new ProjectMetadata();
        meta.ModbusPorts[0].Enabled = true;
        meta.ModbusPorts[0].Role = "Master";
        meta.ModbusPorts[0].MasterChannels.Add(new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = SimplePLC.Protocol.Enums.ModbusDataType.Real,
            TargetTagName = "VFLAG0"
        });

        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicVM, getMetadata: () => meta);

        Assert.True(deployVM.CanDeploy);
        Assert.Contains("0 Rule logic", deployVM.ManifestRuleSummary);
    }

    [Fact]
    public async Task Deploy_WhenZeroRules_WithModbusChannels_DeploysSuccessfully_AndRegistersChannels()
    {
        AppServices.Instance.CurrentProduct = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var meta = new ProjectMetadata();
        meta.ModbusPorts[0].Enabled = true;
        meta.ModbusPorts[0].Role = "Master";
        meta.ModbusPorts[0].MasterChannels.Add(new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = SimplePLC.Protocol.Enums.ModbusDataType.Real,
            TargetTagName = "VFLAG0"
        });

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.Rules.Clear();
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();
        logicVM.CompileNow();

        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM, logicVM, getMetadata: () => meta);
        deployVM.UpdatePreflightSummary();

        Assert.True(deployVM.CanDeploy);

        await deployVM.StartDeployAsync();

        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Contains("1 kênh RS-485 Modbus Master", deployVM.StatusMessage);
    }

    [Fact]
    public async Task DeployToDeviceAsync_WhenOnTab0_WithModbusChannels_DeploysAndStaysOnTab0()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVm = new MainViewModel();
        mainVm.SelectTab(AppTabIndices.Device);
        Assert.Equal(AppTabIndices.Device, mainVm.SelectedTabIndex);

        // Clear rules
        mainVm.LogicEditorVM.Nodes.Clear();
        mainVm.LogicEditorVM.Connections.Clear();
        mainVm.LogicEditorVM.CompileNow();

        // Configure 1 channel on port 0
        mainVm.CurrentProjectMetadata.ModbusPorts[0].Enabled = true;
        mainVm.CurrentProjectMetadata.ModbusPorts[0].Role = "Master";
        mainVm.CurrentProjectMetadata.ModbusPorts[0].MasterChannels.Add(new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = SimplePLC.Protocol.Enums.ModbusDataType.Real,
            TargetTagName = "VFLAG0"
        });

        // Act - Deploy from subtoolbar
        await mainVm.DeployToDeviceAsync();

        // Assert - Stays on Tab 0, deploy was executed without jumping to Tab 4 or 5
        Assert.Equal(AppTabIndices.Device, mainVm.SelectedTabIndex);
        Assert.Contains("Nạp cấu hình thành công", mainVm.LogicEditorVM.SaveStatusMessage);
    }
}
