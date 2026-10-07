using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

[Collection("AppServicesDeployTests")]
public class DeployViewModelIntegrationTests : IDisposable
{
    public DeployViewModelIntegrationTests()
    {
        // Tests in this class assert Vietnamese UI strings
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }


    [Fact]
    public async Task StartDeployAsync_WhenDeviceConnectedAndRulesValid_CompletesAll5StepsSuccessfully()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Equal(100, deployVM.ProgressPercent);
        Assert.All(deployVM.Steps, step => Assert.Equal(StepStatus.Success, step.Status));
        Assert.Contains(LocalizationService.Tr("DeployLogStagingOk"), deployVM.ConsoleLog);
    }

    [Fact]
    public async Task StartDeployAsync_WhenDeviceNotConnected_FailsAtStep1()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        // Do not connect!
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Failed, deployVM.DeployState);
        Assert.Equal(StepStatus.Failed, deployVM.Steps[0].Status);
        Assert.Contains("Chưa kết nối thiết bị", deployVM.ConsoleLog);
    }

    [Fact]
    public async Task StartDeployAsync_WhenCrcCorrupted_FailsAtCommit()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        fakeClient.CorruptStagingCrcOnCommit = true;
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Failed, deployVM.DeployState);
        Assert.Equal(StepStatus.Failed, deployVM.Steps[1].Status);
        Assert.Contains("CRC_MISMATCH", deployVM.ConsoleLog);
    }

    [Fact]
    public async Task StartDeployAsync_CanDeployConsecutivelyWithoutBlock()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM);

        // Act 1: Deploy first time
        await deployVM.StartDeployAsync();

        // Assert 1
        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Contains("Bắt Đầu Nạp", deployVM.ActionButtonText);
        Assert.True(deployVM.CanDeploy);

        // Act 2: Deploy second time immediately without modifying rules
        deployVM.PrimaryActionCommand.Execute(null);
        await Task.Delay(500); // Allow async execution

        // Assert 2
        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Equal(100, deployVM.ProgressPercent);
        Assert.All(deployVM.Steps, step => Assert.Equal(StepStatus.Success, step.Status));
    }

    [Fact]
    public async Task DeployViewModel_WhenRulesAddedOrModified_AutoResetsSuccessStateAndUpdatesManifest()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();
        int initialCount = ruleTableVM.Rules.Count;
        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTableVM);

        // Deploy initial rules
        await deployVM.StartDeployAsync();
        Assert.True(deployVM.IsSuccess);

        // Act: Add a new rule
        var newRule = new RuleItemModel
        {
            Id = $"R{initialCount + 1}",
            Index = initialCount,
            Enabled = true,
            TriggerTag = tagCatalog.AllTags.FirstOrDefault(t => t.Index == 0),
            ActionTag = tagCatalog.AllTags.FirstOrDefault(t => t.Index == 8)
        };
        ruleTableVM.Rules.Add(newRule);

        // Assert: Auto-reset occurs
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Idle, deployVM.DeployState);
        Assert.All(deployVM.Steps, step => Assert.Equal(StepStatus.Pending, step.Status));
        Assert.Equal(initialCount + 1, deployVM.ManifestRuleCount);
        Assert.Contains("Bắt Đầu Nạp", deployVM.ActionButtonText);

        // Can deploy new rules immediately
        await deployVM.StartDeployAsync();
        Assert.True(deployVM.IsSuccess);
        Assert.Equal(DeployState.Success, deployVM.DeployState);
    }
}
