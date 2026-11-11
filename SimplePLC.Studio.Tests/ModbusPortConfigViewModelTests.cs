using System.Collections.Generic;
using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử độc lập cho ViewModel quản lý cấu hình Đa cổng RS-485 Modbus Master (Component 3).
/// </summary>
public sealed class ModbusPortConfigViewModelTests
{
    [Fact]
    public void Constructor_InitializesPortsFromMetadata_RespectingPhysicalLimit()
    {
        // Default metadata (2 ports)
        var metaDefault = new ProjectMetadata();
        var vmDefault = new ModbusPortConfigViewModel(metaDefault);
        Assert.Equal(2, vmDefault.Ports.Count);

        // 4-port hardware (DTU)
        var meta4 = new ProjectMetadata { PhysicalRs485PortCount = 4 };
        var vm = new ModbusPortConfigViewModel(meta4);

        Assert.Equal(4, vm.Ports.Count);
        Assert.NotNull(vm.SelectedPort);
        Assert.Equal((byte)0, vm.SelectedPort.PortIndex);

        // Port 1 default: 9600-8-N-1
        Assert.Equal(9600u, vm.Ports[0].BaudRate);
        Assert.Equal("None", vm.Ports[0].Parity);
        Assert.True(vm.Ports[0].Enabled);

        // Port 2 default: 19200-8-E-1
        Assert.Equal(19200u, vm.Ports[1].BaudRate);
        Assert.Equal("Even", vm.Ports[1].Parity);
        Assert.True(vm.Ports[1].Enabled);
    }

    [Fact]
    public void ApplyCommand_PersistsModificationsToMetadata()
    {
        var meta = new ProjectMetadata { PhysicalRs485PortCount = 4 };
        var vm = new ModbusPortConfigViewModel(meta);

        // Select Port 1 and change Baud to 115200, Parity to Even, StopBits to 2
        vm.SelectedPort = vm.Ports[0];
        vm.SelectedPort.BaudRate = 115200;
        vm.SelectedPort.Parity = "Even";
        vm.SelectedPort.StopBits = 2;
        vm.SelectedPort.ResponseTimeoutMs = 350;
        vm.SelectedPort.RetryCount = 3;
        vm.SelectedPort.PortName = "SENSOR_BUS";

        // Select Port 3 and enable it
        vm.Ports[2].Enabled = true;
        vm.Ports[2].BaudRate = 38400;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(115200u, meta.ModbusPorts[0].BaudRate);
        Assert.Equal("Even", meta.ModbusPorts[0].Parity);
        Assert.Equal((byte)2, meta.ModbusPorts[0].StopBits);
        Assert.Equal((ushort)350, meta.ModbusPorts[0].ResponseTimeoutMs);
        Assert.Equal((ushort)3, meta.ModbusPorts[0].RetryCount);
        Assert.Equal("SENSOR_BUS", meta.ModbusPorts[0].PortName);

        Assert.True(meta.ModbusPorts[2].Enabled);
        Assert.Equal(38400u, meta.ModbusPorts[2].BaudRate);
    }

    [Fact]
    public void ResetDefaultsCommand_RestoresPortDefaults()
    {
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);

        vm.SelectedPort = vm.Ports[0];
        vm.SelectedPort.BaudRate = 115200;
        vm.SelectedPort.Parity = "Odd";

        vm.ResetDefaultsCommand.Execute(null);

        Assert.Equal(9600u, vm.SelectedPort.BaudRate);
        Assert.Equal("None", vm.SelectedPort.Parity);
        Assert.Equal((byte)1, vm.SelectedPort.StopBits);
        Assert.Equal((ushort)200, vm.SelectedPort.ResponseTimeoutMs);
    }

    [Fact]
    public void PortItemViewModel_PillAndSummaryReflectState()
    {
        var model = new ModbusPortConfigModel
        {
            PortIndex = 1,
            PortName = "VFD_BUS",
            Enabled = true,
            BaudRate = 19200,
            Parity = "Even",
            StopBits = 1,
            ResponseTimeoutMs = 200,
            RetryCount = 2
        };

        var item = new ModbusPortItemViewModel(model);

        Assert.Equal("ACTIVE", item.StatusPill);
        Assert.Equal("#107C41", item.StatusPillColor);
        Assert.Contains("19200", item.SummaryBadge);
        Assert.Contains("8-E-1", item.SummaryBadge);
        Assert.Contains("Master", item.SummaryBadge);
        Assert.Contains("200ms", item.SummaryBadge);

        item.Enabled = false;
        Assert.Equal("DISABLED", item.StatusPill);
        Assert.Equal("#64748B", item.StatusPillColor);
    }

    [Fact]
    public void DisplayName_AdaptsToActiveLanguage()
    {
        var model = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true
        };
        var vm = new ModbusPortConfigViewModel(new ProjectMetadata { ModbusPorts = new() { model } });

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
        Assert.StartsWith("Port 1", vm.Ports[0].DisplayName);

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.StartsWith("Cổng 1", vm.Ports[0].DisplayName);

        // Reset back to English
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
    }
    [Fact]
    public void ModbusPortConfigDialogView_XAML_InstantiatesWithoutResourceExceptions()
    {
        RunOnSta(() =>
        {
            var meta = new ProjectMetadata();
            var vm = new ModbusPortConfigViewModel(meta);
            var dialog = new SimplePLC.Studio.Views.ModbusPortConfigDialogView(vm);
            Assert.NotNull(dialog);
            Assert.Same(vm, dialog.DataContext);
        });
    }

    private static void RunOnSta(System.Action action)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (System.Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }
    [Fact]
    public void MasterChannels_AddAndRemove_WorksDeterministically()
    {
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);
        var port = vm.SelectedPort!;

        Assert.Empty(port.MasterChannels);
        Assert.False(port.HasMasterChannels);

        // 1. Add first channel
        port.AddMasterChannelCommand.Execute(null);
        Assert.Single(port.MasterChannels);
        Assert.True(port.HasMasterChannels);

        var ch1 = port.MasterChannels[0];
        Assert.Equal((byte)1, ch1.SlaveId);
        Assert.Equal((byte)3, ch1.FunctionCode);
        Assert.True(ch1.IsReadOperation);
        Assert.False(ch1.IsWriteOperation);
        Assert.Equal(SimplePLC.Protocol.Enums.ModbusDataType.Int16, ch1.DataType);
        Assert.Equal(SimplePLC.Protocol.Enums.ModbusEndianness.BigEndian_ABCD, ch1.Endianness);
        Assert.Equal(1.0, ch1.Scale);

        // 2. Add second channel (address should auto-increment)
        ch1.Address = 100;
        port.AddMasterChannelCommand.Execute(null);
        Assert.Equal(2, port.MasterChannels.Count);
        var ch2 = port.MasterChannels[1];
        Assert.Equal(101, ch2.Address);

        // 3. Remove first channel
        port.RemoveMasterChannelCommand.Execute(ch1);
        Assert.Single(port.MasterChannels);
        Assert.Same(ch2, port.MasterChannels[0]);

        // 4. Remove second channel
        port.RemoveMasterChannelCommand.Execute(ch2);
        Assert.Empty(port.MasterChannels);
        Assert.False(port.HasMasterChannels);
    }

    [Fact]
    public void MasterChannels_Persistence_SurvivesApplyAndReload()
    {
        var meta = new ProjectMetadata();
        var vm1 = new ModbusPortConfigViewModel(meta);
        var port1 = vm1.SelectedPort!;

        port1.AddMasterChannelCommand.Execute(null);
        var ch = port1.MasterChannels[0];
        ch.SlaveId = 5;
        ch.FunctionCode = 6; // FC06 Write Single Register
        ch.Address = 40010;
        ch.DataType = SimplePLC.Protocol.Enums.ModbusDataType.Real;
        ch.Endianness = SimplePLC.Protocol.Enums.ModbusEndianness.MidBig_CDAB;
        ch.Scale = 0.1;
        ch.TargetTagName = "VREG2";

        vm1.ApplyCommand.Execute(null);

        // Reload into a fresh ViewModel instance
        var vm2 = new ModbusPortConfigViewModel(meta);
        var port2 = vm2.SelectedPort!;

        Assert.Single(port2.MasterChannels);
        var reloaded = port2.MasterChannels[0];
        Assert.Equal((byte)5, reloaded.SlaveId);
        Assert.Equal((byte)6, reloaded.FunctionCode);
        Assert.False(reloaded.IsReadOperation);
        Assert.True(reloaded.IsWriteOperation);
        Assert.Equal(40010, reloaded.Address);
        Assert.Equal(SimplePLC.Protocol.Enums.ModbusDataType.Real, reloaded.DataType);
        Assert.Equal(SimplePLC.Protocol.Enums.ModbusEndianness.MidBig_CDAB, reloaded.Endianness);
        Assert.Equal(0.1, reloaded.Scale);
        Assert.Equal("VREG2", reloaded.TargetTagName);
    }

    [Fact]
    public void MasterChannels_ScaleNormalization_AutoRevertsOnInvalidInput()
    {
        var ch = new ModbusMasterChannelItemViewModel();
        Assert.Equal(1.0, ch.Scale);
        Assert.Equal("1", ch.ScaleText);

        // Valid float with comma or dot
        ch.ScaleText = "2,5";
        ch.CommitScaleText();
        Assert.Equal(2.5, ch.Scale);
        Assert.Equal("2.5", ch.ScaleText);

        // Invalid text -> Auto-Revert (Axiom 3)
        ch.ScaleText = "abc_invalid";
        ch.CommitScaleText();
        Assert.Equal(2.5, ch.Scale);
        Assert.Equal("2.5", ch.ScaleText);
    }

    [Fact]
    public void TagDisplayName_FollowsTagFirstThenAliasSpecification()
    {
        var userTag = new TagModel
        {
            Index = 0,
            Name = "VREG0",
            Alias = "Nhiệt độ phòng sấy",
            Kind = SimplePLC.Domain.Enums.TagKind.VirtualRegister
        };
        Assert.Equal("[VREG0] — Nhiệt độ phòng sấy", userTag.DisplayName);

        var noneTag = new TagModel
        {
            Index = 65535,
            Name = "NONE",
            Kind = SimplePLC.Domain.Enums.TagKind.None
        };
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
        Assert.Equal("NONE", noneTag.DisplayName);

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.Equal("Không chọn", noneTag.DisplayName);

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
    }
}
