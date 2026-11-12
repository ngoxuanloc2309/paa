using SimplePLC.Application.UseCases;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử tích hợp quy trình Deploy của DeployViewModel với MCU Simulator và Transport.
/// </summary>
[Collection("AppServicesDeployTests")]
public class DeployViewModelIntegrationTests : IDisposable
{
    public DeployViewModelIntegrationTests()
    {
        // Tests in this class assert Vietnamese UI strings
        LocalizationService.Instance.CurrentLanguage = "vi";
        AppServices.Instance.CurrentProduct = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
        AppServices.Instance.CurrentProduct = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai();
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

    [Fact]
    public async Task StartDeployAsync_WithMqttTlsRootCa_ProvisionsCertificateToMcu()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();

        var meta = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttBrokerHost = "broker.hivemq.com",
            MqttBrokerPort = 8883,
            MqttTlsEnabled = true,
            MqttTlsCaCertPem = "-----BEGIN CERTIFICATE-----\nMIIB...CA...==\n-----END CERTIFICATE-----\n"
        };

        var deployVM = new DeployViewModel(
            AppServices.Instance.DeployUseCase,
            ruleTableVM,
            getMetadata: () => meta);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.True(deployVM.IsSuccess, $"Deploy failed: {deployVM.StatusMessage}\nLogs:\n{deployVM.ConsoleLog}");
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Contains("[MQTT TLS OK] Root CA committed successfully", deployVM.ConsoleLog);

        ushort activeMask = fakeClient.Simulator.Memory.InternalRead(SimplePLC.Protocol.Constants.ModbusRegisterMap.MqttCertActiveTypeMaskAddress);
        Assert.Equal(0x0001, activeMask & 0x0001);
    }

    [Fact]
    public async Task StartDeployAsync_WithMqttMtls_ProvisionsAllThreeCertificates()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();

        var meta = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttBrokerHost = "broker.hivemq.com",
            MqttBrokerPort = 8883,
            MqttTlsEnabled = true,
            MqttTlsMtlsEnabled = true,
            MqttTlsCaCertPem = "-----BEGIN CERTIFICATE-----\nROOT_CA\n-----END CERTIFICATE-----\n",
            MqttTlsClientCertPem = "-----BEGIN CERTIFICATE-----\nCLIENT_CERT\n-----END CERTIFICATE-----\n",
            MqttTlsClientKeyPem = "-----BEGIN PRIVATE KEY-----\nCLIENT_KEY\n-----END PRIVATE KEY-----\n"
        };

        var deployVM = new DeployViewModel(
            AppServices.Instance.DeployUseCase,
            ruleTableVM,
            getMetadata: () => meta);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.True(deployVM.IsSuccess, $"Deploy failed: {deployVM.StatusMessage}\nLogs:\n{deployVM.ConsoleLog}");
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Contains("[MQTT TLS OK] Root CA committed successfully", deployVM.ConsoleLog);
        Assert.Contains("[MQTT TLS OK] Client Certificate committed successfully", deployVM.ConsoleLog);
        Assert.Contains("[MQTT TLS OK] Client Private Key committed successfully", deployVM.ConsoleLog);

        ushort activeMask = fakeClient.Simulator.Memory.InternalRead(SimplePLC.Protocol.Constants.ModbusRegisterMap.MqttCertActiveTypeMaskAddress);
        Assert.Equal(0x0007, activeMask & 0x0007);
    }

    [Fact]
    public async Task StartDeployAsync_WithMqttWillConfig_ProvisionsLwtToMcu()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();

        var meta = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttBrokerHost = "broker.hivemq.com",
            MqttBrokerPort = 1883,
            MqttClientId = "Station01",
            MqttWillEnabled = true,
            MqttWillTopic = "plant/{device}/status",
            MqttWillPayload = "offline",
            MqttWillQos = 1,
            MqttWillRetain = true
        };

        var deployVM = new DeployViewModel(
            AppServices.Instance.DeployUseCase,
            ruleTableVM,
            getMetadata: () => meta);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.True(deployVM.IsSuccess, $"Deploy failed: {deployVM.StatusMessage}\nLogs:\n{deployVM.ConsoleLog}");
        Assert.Equal(DeployState.Success, deployVM.DeployState);
        Assert.Contains("[MQTT LWT] Writing 32 registers to 0x0E60", deployVM.ConsoleLog);

        // Đọc 32 thanh ghi tại 0x0E60 trên bộ nhớ mô phỏng MCU và giải mã kiểm chứng
        var lwtRegs = fakeClient.Simulator.Memory.ReadRegisters(
            SimplePLC.Protocol.Constants.ModbusRegisterMap.MqttWillConfigBaseAddress,
            SimplePLC.Protocol.Constants.ModbusRegisterMap.MqttWillConfigRegisters);
        var decoded = SimplePLC.Protocol.Codec.MqttWillConfigCodec.Decode(lwtRegs);

        Assert.True(decoded.Enabled);
        Assert.True(decoded.Retain);
        Assert.Equal(1, decoded.Qos);
        Assert.Equal("plant/Station01/status", decoded.Topic);
        Assert.Equal("offline", decoded.Payload);
    }
}
