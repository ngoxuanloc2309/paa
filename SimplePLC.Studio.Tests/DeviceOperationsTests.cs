using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DeviceOperationsTests
{
    [Fact]
    public void CanDoDeviceAction_IsFalse_WhenDisconnected()
    {
        var mainVM = new MainViewModel();
        Assert.False(mainVM.IsConnected);
        Assert.False(mainVM.CanDoDeviceAction);
        Assert.False(mainVM.UploadFromDeviceCommand.CanExecute(null));
        Assert.False(mainVM.RebootDeviceCommand.CanExecute(null));
        Assert.False(mainVM.FactoryResetDeviceCommand.CanExecute(null));
    }

    [Fact]
    public async Task CanDoDeviceAction_IsTrue_WhenConnected()
    {
        var fakeClient = new FakeModbusClient();
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel();
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        await mainVM.ToggleConnect();

        Assert.True(mainVM.IsConnected);
        Assert.True(mainVM.CanDoDeviceAction);
        Assert.True(mainVM.UploadFromDeviceCommand.CanExecute(null));
        Assert.True(mainVM.RebootDeviceCommand.CanExecute(null));
        Assert.True(mainVM.FactoryResetDeviceCommand.CanExecute(null));

        // Clean up
        await mainVM.ToggleConnect();
    }

    [Fact]
    public async Task UploadFromDevice_WhenConnected_LoadsRulesIntoRuleTable()
    {
        var fakeClient = new FakeModbusClient();
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel();
        mainVM.MessageBoxHandler = (msg, cap, btn, img) => { };
        mainVM.ConfirmBoxHandler = (msg, cap, btn, img) => System.Windows.MessageBoxResult.Yes;
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        await mainVM.ToggleConnect();

        // Load and compile default demo so safety gate passes and virtual MCU has rules
        mainVM.LogicEditorVM.LoadDefaultDemoGraph();
        mainVM.LogicEditorVM.CompileNow();
        Assert.Equal(CompileState.Valid, mainVM.LogicEditorVM.CompileState);

        await mainVM.DeployVM.StartDeployAsync();
        Assert.True(mainVM.DeployVM.IsSuccess);

        int expectedCount = mainVM.RuleTableVM.Rules.Count;
        Assert.True(expectedCount > 0);

        // Clear local rules
        mainVM.RuleTableVM.Rules.Clear();
        Assert.Empty(mainVM.RuleTableVM.Rules);

        // Upload rules from virtual MCU
        await mainVM.UploadFromDeviceAsync();

        // Rules should now be loaded from MCU
        Assert.NotEmpty(mainVM.RuleTableVM.Rules);
        Assert.Equal(expectedCount, mainVM.RuleTableVM.Rules.Count);
        Assert.True(mainVM.RuleTableVM.HasRules);

        // Test Reboot command execution
        await mainVM.RebootDeviceAsync();

        // Test FactoryReset command execution
        await mainVM.FactoryResetDeviceAsync();

        // Clean up
        await mainVM.ToggleConnect();
    }

    [Fact]
    public async Task RuleTableViewModel_UploadFromDevice_TriggersDelegate()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);

        bool delegateCalled = false;
        ruleTableVM.OnUploadFromDeviceRequested = () =>
        {
            delegateCalled = true;
            return Task.CompletedTask;
        };

        await ruleTableVM.UploadFromDeviceAsync();
        Assert.True(delegateCalled);
    }

    [Fact]
    public void DeployViewModel_DeviceInfoBadge_ShowsDisconnected_Initially()
    {
        var deployVM = new DeployViewModel();
        Assert.Contains("USB CDC", deployVM.DeviceInfoBadge);
    }

    [Fact]
    public void LocalizationService_DefaultsToEnglish_And_DeviceTerminology()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = "en";
        // Verify default language is English
        Assert.True(loc.IsEnglish);
        Assert.Equal("en", loc.CurrentLanguage);

        // Verify Device terminology in English
        Assert.Equal("Device", loc["MenuPLC"]);
        Assert.Equal("Upload from Device", loc["RuleBtnUploadFromDevice"]);
        Assert.Equal("Read Configuration from Device (Upload)...", loc["MenuUploadFromDevice"]);
        Assert.Equal("Deploy Configuration to Device...", loc["MenuCommit"]);

        // Verify Vietnamese translation uses Thiết bị
        loc.CurrentLanguage = "vi";
        Assert.Equal("Thiết bị", loc["MenuPLC"]);
        Assert.Equal("Đọc từ thiết bị", loc["RuleBtnUploadFromDevice"]);
        Assert.Equal("Đọc cấu hình từ thiết bị (Upload)...", loc["MenuUploadFromDevice"]);
        Assert.Equal("Nạp cấu hình xuống thiết bị (Deploy)...", loc["MenuCommit"]);

        // Restore English
        loc.CurrentLanguage = "en";
    }

    [Fact]
    public async Task UnexpectedDisconnect_TransitionsImmediatelyToDisconnected_DisablesCommands()
    {
        var fakeClient = new FakeModbusClient();
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel();
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        // Connect first
        await mainVM.ToggleConnect();
        Assert.True(mainVM.IsConnected);
        Assert.Equal(ConnectionLifecycleState.Connected, AppServices.Instance.LifecycleManager.CurrentState);

        // Simulate unexpected disconnect -> under industrial standard (Default), immediately transitions to Disconnected
        await AppServices.Instance.LifecycleManager.NotifyUnexpectedDisconnectAsync(new TimeoutException("Simulated timeout"));

        Assert.False(mainVM.IsConnected);
        Assert.Equal(ConnectionLifecycleState.Disconnected, AppServices.Instance.LifecycleManager.CurrentState);
        Assert.False(mainVM.UploadFromDeviceCommand.CanExecute(null));
    }
}
