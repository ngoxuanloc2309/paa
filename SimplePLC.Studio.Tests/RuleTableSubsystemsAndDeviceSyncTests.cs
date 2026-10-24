using System.Collections.Generic;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử tính năng đồng bộ phân hệ Thiết bị (Device Layer / ProjectMetadata)
/// và hiển thị đa phân hệ trên Bảng Rule (Multi-Subsystem Sub-Tabs).
/// </summary>
public class RuleTableSubsystemsAndDeviceSyncTests
{
    [Fact]
    public void Test_RuleTableViewModel_UpdateSubsystemsFromDevice_PopulatesAllTabsCorrectly()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);

        var fullConfig = new FullDeviceConfigurationDto
        {
            ModbusMasterReads = new List<ModbusMasterReadConfigDto>
            {
                new()
                {
                    BlockIndex = 0,
                    PortIndex = 0,
                    SlaveId = 1,
                    FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                    StartAddress = 0x0000,
                    RegCount = 6,
                    ReadMode = ModbusReadMode.Cyclic,
                    PollIntervalMs = 1000,
                    Enabled = true,
                    Pins = new List<ModbusMasterPinBindingDto>
                    {
                        new() { Label = "V1" },
                        new() { Label = "V2" },
                        new() { Label = "V3" }
                    }
                }
            },
            ModbusMasterWrites = new List<ModbusMasterWriteConfigDto>
            {
                new()
                {
                    BlockIndex = 0,
                    PortIndex = 0,
                    SlaveId = 2,
                    FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
                    StartAddress = 0x0100,
                    PointCount = 2,
                    TriggerMode = ModbusWriteTriggerMode.Cyclic,
                    CyclicIntervalMs = 500,
                    WriteMode = ModbusWriteMode.ChannelIndexed,
                    Enabled = true
                }
            },
            DirectWrites = new List<DirectWriteConfigDto>
            {
                new()
                {
                    Id = "DW1",
                    TargetPin = DirectWriteTargetPin.Do0,
                    Mode = DirectWriteMode.DigitalOutput,
                    ChannelCount = 18
                }
            },
            MqttTelemetries = new List<MqttTelemetryConfigDto>
            {
                new()
                {
                    Topic = "factory/telemetry/data",
                    Qos = 1,
                    PublishMode = "Periodic",
                    IntervalMs = 1000,
                    Fields = new List<MqttPayloadFieldDto>
                    {
                        new() { Key = "temp", TagIndex = 0 },
                        new() { Key = "pressure", TagIndex = 1 }
                    }
                }
            },
            MqttSubscriptions = new List<MqttSubscriptionConfigDto>
            {
                new()
                {
                    Topic = "factory/commands/inbound",
                    Qos = 1,
                    Pins = new List<MqttPinBindingDto>
                    {
                        new() { Label = "cmd_start" }
                    }
                }
            },
            Timers = new List<FbTimerRecordDto>
            {
                new()
                {
                    Mode = SPLC_TimerMode.TON,
                    PresetMs = 5000,
                    RuleRef = 1,
                    Running = true
                }
            },
            Counters = new List<FbCounterRecordDto>
            {
                new()
                {
                    Mode = SPLC_CounterMode.CTU,
                    PresetValue = 10,
                    CurrentValue = 4,
                    Q = false
                }
            }
        };

        ruleTableVM.UpdateSubsystemsFromDevice(fullConfig);

        // Xác minh Modbus Master (1 Read + 1 Write + 1 DirectWrite = 3 commands)
        Assert.Equal(3, ruleTableVM.ModbusCommands.Count);
        Assert.Equal(3, ruleTableVM.ModbusCommandCount);
        Assert.Equal("MB1", ruleTableVM.ModbusCommands[0].Id);
        Assert.Equal("MW1", ruleTableVM.ModbusCommands[1].Id);
        Assert.Equal("DW1", ruleTableVM.ModbusCommands[2].Id);

        // Xác minh MQTT Services (1 Pub + 1 Sub = 2 services)
        Assert.Equal(2, ruleTableVM.MqttServices.Count);
        Assert.Equal(2, ruleTableVM.MqttServiceCount);
        Assert.Equal("PUB1", ruleTableVM.MqttServices[0].Id);
        Assert.Equal("SUB1", ruleTableVM.MqttServices[1].Id);

        // Xác minh Function Blocks (1 Timer + 1 Counter = 2 FBs)
        Assert.Equal(2, ruleTableVM.FunctionBlocks.Count);
        Assert.Equal(2, ruleTableVM.FunctionBlockCount);
        Assert.Equal("T1", ruleTableVM.FunctionBlocks[0].Id);
        Assert.Equal("C1", ruleTableVM.FunctionBlocks[1].Id);

        // Xác minh chuyển đổi Sub-Tab
        ruleTableVM.SelectSubTab(1);
        Assert.Equal(1, ruleTableVM.SelectedSubTabIndex);
        ruleTableVM.SelectSubTab(2);
        Assert.Equal(2, ruleTableVM.SelectedSubTabIndex);
        ruleTableVM.SelectSubTab(3);
        Assert.Equal(3, ruleTableVM.SelectedSubTabIndex);
        ruleTableVM.SelectSubTab(0);
        Assert.Equal(0, ruleTableVM.SelectedSubTabIndex);
    }

    [Fact]
    public void Test_NodeDialogs_OpenDeviceSettingsCommand_InvokesCallbacksSuccessfully()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", BaudRate = 115200 }
        };

        // 1. ModbusReadConfigDialogViewModel
        var readNode = new ModbusReadNodeViewModel();
        var readVm = new ModbusReadConfigDialogViewModel(readNode, tagCatalog, ports);
        bool portSettingsOpened = false;
        readVm.OnOpenDevicePortSettings = () => portSettingsOpened = true;
        readVm.OpenDevicePortSettings();
        Assert.True(portSettingsOpened, "ModbusRead dialog must trigger port settings callback.");

        // Kiểm tra làm mới danh sách cổng
        var newPorts = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port A", BaudRate = 9600 },
            new() { PortIndex = 1, PortName = "Port B", BaudRate = 115200 }
        };
        readVm.RefreshAvailablePorts(newPorts);
        Assert.Equal(2, readVm.AvailablePorts.Count);

        // 2. ModbusWriteConfigDialogViewModel
        var writeNode = new ModbusWriteNodeViewModel();
        var writeVm = new ModbusWriteConfigDialogViewModel(writeNode, tagCatalog, ports);
        bool writePortOpened = false;
        writeVm.OnOpenDevicePortSettings = () => writePortOpened = true;
        writeVm.OpenDevicePortSettings();
        Assert.True(writePortOpened, "ModbusWrite dialog must trigger port settings callback.");

        // 3. MqttConfigDialogViewModel
        var pubNode = new MqttPublishNodeViewModel();
        var pubVm = new MqttConfigDialogViewModel(pubNode, tagCatalog);
        bool brokerOpenedFromPub = false;
        pubVm.OnOpenDeviceBrokerSettings = () => brokerOpenedFromPub = true;
        pubVm.OpenDeviceBrokerSettings();
        Assert.True(brokerOpenedFromPub, "MqttPub dialog must trigger broker settings callback.");

        // 4. MqttSubConfigDialogViewModel
        var subNode = new MqttSubscribeNodeViewModel();
        var subVm = new MqttSubConfigDialogViewModel(subNode, tagCatalog);
        bool brokerOpenedFromSub = false;
        subVm.OnOpenDeviceBrokerSettings = () => brokerOpenedFromSub = true;
        subVm.OpenDeviceBrokerSettings();
        Assert.True(brokerOpenedFromSub, "MqttSub dialog must trigger broker settings callback.");
    }

    [Fact]
    public async System.Threading.Tasks.Task Test_RuleTableView_InstantiationAndDataContext_DoesNotThrowXamlException()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var tagCatalog = new TagCatalogViewModel();
                var ruleTableVM = new RuleTableViewModel(tagCatalog);

                var view = new SimplePLC.Studio.Views.RuleTableView
                {
                    DataContext = ruleTableVM
                };

                // Cycle through all Sub-Tabs
                for (int i = 0; i < 4; i++)
                {
                    ruleTableVM.SelectSubTab(i);
                }

                tcs.SetResult(true);
            }
            catch (System.Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        await tcs.Task;
    }

    [Fact]
    public void Test_RuleTableViewModel_DeleteSubsystemItems_RemovesFromCollectionAndFiresCallback()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        var mbCmd = new ModbusCommandItemViewModel { Id = "MB1" };
        var mqttSvc = new MqttServiceItemViewModel { Id = "PUB1" };
        var fb = new FunctionBlockItemViewModel { Id = "T1" };

        vm.ModbusCommands.Add(mbCmd);
        vm.MqttServices.Add(mqttSvc);
        vm.FunctionBlocks.Add(fb);

        Assert.Equal(1, vm.ModbusCommandCount);
        Assert.Equal(1, vm.MqttServiceCount);
        Assert.Equal(1, vm.FunctionBlockCount);

        ModbusCommandItemViewModel? deletedMb = null;
        MqttServiceItemViewModel? deletedMqtt = null;
        FunctionBlockItemViewModel? deletedFb = null;

        vm.OnDeleteModbusCommandRequested = (c) => deletedMb = c;
        vm.OnDeleteMqttServiceRequested = (s) => deletedMqtt = s;
        vm.OnDeleteFunctionBlockRequested = (f) => deletedFb = f;

        // Delete Modbus
        vm.DeleteModbusCommand(mbCmd);
        Assert.Equal(0, vm.ModbusCommandCount);
        Assert.Same(mbCmd, deletedMb);

        // Delete MQTT
        vm.DeleteMqttService(mqttSvc);
        Assert.Equal(0, vm.MqttServiceCount);
        Assert.Same(mqttSvc, deletedMqtt);

        // Delete FB
        vm.DeleteFunctionBlock(fb);
        Assert.Equal(0, vm.FunctionBlockCount);
        Assert.Same(fb, deletedFb);
    }
}
