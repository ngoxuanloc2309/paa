using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử tự động hóa toàn diện quy trình đầu-cuối Studio UI ↔ MCU:
/// 1. Kiểm thử 10 Blueprints công nghiệp: Nạp Blueprint -> Biên dịch AST -> Deploy xuống MCU -> Upload lại lên Studio.
/// 2. Kiểm thử bảo toàn Flash Retain cho Blueprint Đếm Sản Lượng qua chu kỳ khởi động lại.
/// 3. Kiểm thử Pre-flight Safety Gate bảo vệ MCU khỏi các trạng thái logic lỗi / sơ đồ rỗng.
/// 4. Kiểm thử các lệnh điều khiển thiết bị (Upload, Soft Reboot, Factory Reset).
/// </summary>
[Collection("AppServicesDeployTests")]
public class McuStudioRoundtripAutomationTests
{
    private async Task<(MainViewModel MainVM, FakeModbusClient Client)> CreateConnectedStudioFixtureAsync()
    {
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM_STUDIO_VIRTUAL", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel(act => act());
        mainVM.LogicEditorVM.DebounceDelayMs = 0;
        mainVM.MessageBoxHandler = (msg, cap, btn, img) => { };
        mainVM.ConfirmBoxHandler = (msg, cap, btn, img) =>
        {
            if (btn == System.Windows.MessageBoxButton.YesNoCancel)
                return System.Windows.MessageBoxResult.No;
            return System.Windows.MessageBoxResult.Yes;
        };
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        // Đảm bảo bắt đầu với dự án sạch 0 rules
        mainVM.NewProject();

        await mainVM.ToggleConnect();
        Assert.True(mainVM.IsConnected, "MainViewModel must be connected to virtual simulator.");

        return (mainVM, fakeClient);
    }

    [Fact]
    public async Task Test_01_AllFactoryBlueprints_Compile_Deploy_UploadToStudio_Roundtrip()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            var blueprints = mainVM.BlueprintsVM.Blueprints;
            Assert.Equal(23, blueprints.Count);


            foreach (var bp in blueprints)
            {
                // Bắt đầu sạch cho từng Blueprint
                mainVM.NewProject();

                // Bước 1: Áp dụng Blueprint vào LogicEditor
                mainVM.BlueprintsVM.ApplyBlueprint(bp);

                // Bước 2: Biên dịch sơ đồ thành Domain Rules
                mainVM.LogicEditorVM.CompileNow();
                var diagMsg = string.Join("; ", mainVM.LogicEditorVM.Diagnostics.Select(d => $"{d.Severity}: {d.Message}"));
                Assert.True(mainVM.LogicEditorVM.CompileState == CompileState.Valid, $"Blueprint '{bp.Id}' failed compile: {diagMsg}");
                Assert.NotNull(mainVM.LogicEditorVM.CurrentProgram);

                int expectedRuleCount = bp.RuleCount;
                Assert.Equal(expectedRuleCount, mainVM.RuleTableVM.Rules.Count);

                // Lưu bản sao thông tin rule trước khi nạp để đối soát
                var compiledSnapshot = mainVM.RuleTableVM.Rules
                    .Select(r => new
                    {
                        TriggerTagName = r.TriggerTag?.Name,
                        r.TriggerType,
                        ActionTagName = r.ActionTag?.Name,
                        r.ActionType,
                        r.ActionParam,
                        r.HasGuard,
                        GuardTagName = r.GuardTag?.Name,
                        r.GuardNegated
                    })
                    .ToList();

                // Bước 3: Deploy xuống MCU qua Staging & Atomic Commit
                await mainVM.DeployVM.StartDeployAsync();
                Assert.True(mainVM.DeployVM.IsSuccess, $"Blueprint '{bp.Id}' failed to deploy: {mainVM.DeployVM.StatusMessage}");
                Assert.Equal(DeployState.Success, mainVM.DeployVM.DeployState);
                Assert.All(mainVM.DeployVM.Steps, s => Assert.Equal(StepStatus.Success, s.Status));

                // Kiểm tra trực tiếp trên MCU Simulator: Flash đã commit đúng số lượng rule
                Assert.True(client.Simulator.Control.Flash.HasCommittedData);
                Assert.Equal(expectedRuleCount, client.Simulator.Control.Flash.StoredRuleCount);

                // Bước 4: Xóa sạch danh sách rule trên UI Studio
                mainVM.RuleTableVM.Rules.Clear();
                Assert.Empty(mainVM.RuleTableVM.Rules);

                // Bước 5: Đọc ngược toàn bộ từ MCU lên Studio qua UploadFromDevice
                await mainVM.UploadFromDeviceAsync();

                // Bước 6: Thẩm định đối soát (Roundtrip Fidelity)
                Assert.Equal(expectedRuleCount, mainVM.RuleTableVM.Rules.Count);
                for (int i = 0; i < expectedRuleCount; i++)
                {
                    var original = compiledSnapshot[i];
                    var uploaded = mainVM.RuleTableVM.Rules[i];

                    Assert.Equal(original.TriggerTagName, uploaded.TriggerTag?.Name);
                    Assert.Equal(original.TriggerType, uploaded.TriggerType);
                    Assert.Equal(original.ActionTagName, uploaded.ActionTag?.Name);
                    Assert.Equal(original.ActionType, uploaded.ActionType);
                    Assert.Equal(original.ActionParam, uploaded.ActionParam);
                    Assert.Equal(original.HasGuard, uploaded.HasGuard);
                    if (original.HasGuard)
                    {
                        Assert.Equal(original.GuardTagName, uploaded.GuardTag?.Name);
                        Assert.Equal(original.GuardNegated, uploaded.GuardNegated);
                    }
                }
            }
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }

    [Fact]
    public async Task Test_02_RetainCounterBlueprint_PreservesDataAcrossReboot()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            mainVM.NewProject();

            // 1. Áp dụng Blueprint counter (Sản lượng)
            var counterBp = mainVM.BlueprintsVM.Blueprints.First(b => b.Id == "counter");
            mainVM.BlueprintsVM.ApplyBlueprint(counterBp);
            mainVM.LogicEditorVM.CompileNow();

            // 2. Nạp xuống MCU
            await mainVM.DeployVM.StartDeployAsync();
            Assert.True(mainVM.DeployVM.IsSuccess);
            Assert.Equal(2, client.Simulator.Control.Flash.StoredRuleCount);

            // 3. Giả lập tích lũy bộ đếm Retain trên MCU (VREG_RETAIN0 = 150 sản phẩm)
            var r0Tag = mainVM.RuleTableVM.Rules[0].ActionTag!;
            client.Simulator.Control.SetTagValue((ushort)r0Tag.Index, 150);
            Assert.Equal(150, client.Simulator.Control.GetTagValue((ushort)r0Tag.Index));

            // 4. Giả lập khởi động lại phần mềm MCU (Software Reboot)
            client.Simulator.Control.SoftwareReboot();

            // 5. Thẩm định sau reboot:
            // MCU phải bảo toàn toàn vẹn Active Rules từ Flash không bay hơi (non-volatile)
            Assert.Equal(2, client.Simulator.Control.Flash.StoredRuleCount);
            Assert.True(client.Simulator.Control.Flash.HasCommittedData);

            // Upload lại từ MCU lên Studio: Rules tải lên vẫn đầy đủ 2 rules ban đầu
            mainVM.RuleTableVM.Rules.Clear();
            await mainVM.UploadFromDeviceAsync();
            Assert.Equal(2, mainVM.RuleTableVM.Rules.Count);
            Assert.Equal("VREG_RETAIN0", mainVM.RuleTableVM.Rules[0].ActionTag?.Name);
            Assert.Equal("VREG_RETAIN2", mainVM.RuleTableVM.Rules[1].ActionTag?.Name);
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }

    [Fact]
    public async Task Test_03_Studio_DeploySafetyGate_BlocksEmptyOrStaleRules()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            // Case 1: Bảng rule rỗng
            mainVM.RuleTableVM.Rules.Clear();
            mainVM.LogicEditorVM.NewRuleCanvas();
            Assert.False(mainVM.DeployVM.CanDeploy);

            await mainVM.DeployVM.StartDeployAsync();
            Assert.False(mainVM.DeployVM.IsSuccess);
            Assert.Equal(DeployState.Failed, mainVM.DeployVM.DeployState);

            // Case 2: Sơ đồ bị Stale (chưa đồng bộ)
            mainVM.LogicEditorVM.CompileState = CompileState.Stale;
            Assert.False(mainVM.DeployVM.CanDeploy);

            await mainVM.DeployVM.StartDeployAsync();
            Assert.False(mainVM.DeployVM.IsSuccess);
            Assert.Equal(DeployState.Failed, mainVM.DeployVM.DeployState);

            // Case 3: Sơ đồ bị Invalid (lỗi cú pháp)
            mainVM.LogicEditorVM.CompileState = CompileState.Invalid;
            Assert.False(mainVM.DeployVM.CanDeploy);

            await mainVM.DeployVM.StartDeployAsync();
            Assert.False(mainVM.DeployVM.IsSuccess);
            Assert.Equal(DeployState.Failed, mainVM.DeployVM.DeployState);
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }

    [Fact]
    public async Task Test_04_Studio_DeviceActionGuard_Upload_Reboot_FactoryReset()
    {
        var fakeClient = new FakeModbusClient();
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel(act => act());
        mainVM.LogicEditorVM.DebounceDelayMs = 0;
        mainVM.MessageBoxHandler = (msg, cap, btn, img) => { };
        mainVM.ConfirmBoxHandler = (msg, cap, btn, img) => System.Windows.MessageBoxResult.Yes;

        // Khi chưa kết nối: Toàn bộ lệnh thiết bị phải bị vô hiệu hóa (Disabled)
        Assert.False(mainVM.IsConnected);
        Assert.False(mainVM.CanDoDeviceAction);
        Assert.False(mainVM.UploadFromDeviceCommand.CanExecute(null));
        Assert.False(mainVM.RebootDeviceCommand.CanExecute(null));
        Assert.False(mainVM.FactoryResetDeviceCommand.CanExecute(null));

        // Kết nối vào Simulator
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";
        mainVM.NewProject();
        await mainVM.ToggleConnect();
        Assert.True(mainVM.IsConnected);
        Assert.True(mainVM.CanDoDeviceAction);

        // Nạp thử 1 rule để MCU có dữ liệu
        mainVM.LogicEditorVM.LoadDefaultDemoGraph();
        mainVM.LogicEditorVM.CompileNow();
        await mainVM.DeployVM.StartDeployAsync();
        Assert.True(mainVM.DeployVM.IsSuccess);
        Assert.True(fakeClient.Simulator.Control.Flash.HasCommittedData);

        // Test lệnh Factory Reset từ Studio
        await mainVM.FactoryResetDeviceAsync();

        // Sau Factory Reset: MCU Flash đã bị xóa sạch
        Assert.False(fakeClient.Simulator.Control.Flash.HasCommittedData);

        // Upload lại từ MCU: Số lượng rule phải bằng 0
        mainVM.RuleTableVM.Rules.Clear();
        await mainVM.UploadFromDeviceAsync();
        Assert.Empty(mainVM.RuleTableVM.Rules);

        await mainVM.ToggleConnect();
    }
}
