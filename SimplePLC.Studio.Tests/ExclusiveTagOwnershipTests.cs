using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử học thuyết kiểm soát quyền sở hữu ghi độc quyền dữ liệu truyền thông ngoại vi
/// (Exclusive Ingestion Ownership) theo chuẩn First Principles & IEC 61131-3.
/// </summary>
public class ExclusiveTagOwnershipTests : IDisposable
{
    private readonly TagCatalogViewModel _tagCatalog;

    public ExclusiveTagOwnershipTests()
    {
        TagWriteOwnershipService.Instance.Reset();
        LocalizationService.Instance.CurrentLanguage = "vi";
        _tagCatalog = new TagCatalogViewModel();
    }

    public void Dispose()
    {
        TagWriteOwnershipService.Instance.Reset();
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    [Fact]
    public void ModbusReadChannel_WhenTagClaimed_DisablesTagInOtherChannelsDropdown()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        meta.ModbusPorts.Add(portConfig);

        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        // Channel 1: Đọc FC03 gán vào VREG0
        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3, // Read Holding Registers
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        // Channel 2: Kênh đọc thứ hai
        var ch2 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40002,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch2);

        // Act
        var ch2Options = ch2.TagOptions.ToList();
        var vreg0OptionInCh2 = ch2Options.FirstOrDefault(o => o.Tag.Name == "VREG0");

        // Assert
        Assert.NotNull(vreg0OptionInCh2);
        Assert.False(vreg0OptionInCh2.IsSelectable, "Tag VREG0 phải bị vô hiệu hóa trong dropdown của Kênh 2 vì Kênh 1 đã chiếm giữ.");
        Assert.True(vreg0OptionInCh2.HasConflict);
        Assert.Contains("Modbus Kênh 1", vreg0OptionInCh2.ConflictBadge);
        Assert.Contains("Modbus Master Kênh 1", vreg0OptionInCh2.ConflictTooltip);
    }

    [Fact]
    public void ModbusReadChannel_SelfExemption_AllowsCurrentChannelToKeepTag()
    {
        // Arrange
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        // Act - Kiểm tra TagOptions của chính Kênh 1
        var ch1Options = ch1.TagOptions.ToList();
        var vreg0OptionInCh1 = ch1Options.FirstOrDefault(o => o.Tag.Name == "VREG0");

        // Assert
        Assert.NotNull(vreg0OptionInCh1);
        Assert.True(vreg0OptionInCh1.IsSelectable, "Chính Kênh 1 phải được tự miễn trừ (Self-Exemption) để hiển thị VREG0 là hợp lệ.");
        Assert.False(vreg0OptionInCh1.HasConflict);
    }

    [Fact]
    public void ModbusChannels_CrossSelection_PreservesBothChannelsTags()
    {
        // Arrange
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = _tagCatalog.AllTags.First(t => t.Name == "VREG1");
        var vreg2 = _tagCatalog.AllTags.First(t => t.Name == "VREG2");

        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        var ch2 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40002,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch2);

        // Act 1: Kênh 2 chọn VREG1
        ch2.TargetTag = vreg1;

        // Assert 1: Kênh 1 vẫn phải giữ VREG0, không được biến mất hay bị reset
        Assert.NotNull(ch1.TargetTag);
        Assert.Equal("VREG0", ch1.TargetTag.Name);
        Assert.Equal("VREG0", ch1.TargetTagName);

        // Kênh 1's TagOptions phải thấy VREG1 bị disabled với badge Kênh 2
        var vreg1InCh1 = ch1.TagOptions.First(o => o.Tag.Name == "VREG1");
        Assert.False(vreg1InCh1.IsSelectable);
        Assert.Contains("Modbus Kênh 2", vreg1InCh1.ConflictBadge);

        // Act 2: Kênh 1 chuyển sang chọn VREG2
        ch1.TargetTag = vreg2;

        // Assert 2: Kênh 2 vẫn phải giữ nguyên VREG1, không bị reset!
        Assert.NotNull(ch2.TargetTag);
        Assert.Equal("VREG1", ch2.TargetTag.Name);
        Assert.Equal("VREG1", ch2.TargetTagName);

        // Kênh 2's TagOptions: VREG2 bị disabled với badge Kênh 1, còn VREG1 (chính nó) là selectable
        var vreg2InCh2 = ch2.TagOptions.First(o => o.Tag.Name == "VREG2");
        var vreg1InCh2 = ch2.TagOptions.First(o => o.Tag.Name == "VREG1");
        Assert.False(vreg2InCh2.IsSelectable);
        Assert.Contains("Modbus Kênh 1", vreg2InCh2.ConflictBadge);
        Assert.True(vreg1InCh2.IsSelectable);
    }

    [Fact]
    public void ModbusWriteChannel_DoesNotClaimTag_AllowsOtherWriters()
    {
        // Arrange
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        // Kênh 1: Lệnh GHI FC06 (Egress reader - đọc giá trị VREG0 từ PLC gửi ra thiết bị ngoài)
        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 6, // Write Single Register
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        // Kênh 2: Lệnh đọc hoặc ghi khác
        var ch2 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40002,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch2);

        // Act
        var ch2Options = ch2.TagOptions.ToList();
        var vreg0OptionInCh2 = ch2Options.FirstOrDefault(o => o.Tag.Name == "VREG0");

        // Assert: Vì FC06 là lệnh GHI (Egress Reader từ PLC), nó không chiếm quyền ghi độc quyền vào PLC
        Assert.NotNull(vreg0OptionInCh2);
        Assert.True(vreg0OptionInCh2.IsSelectable, "Lệnh Ghi Modbus (FC06) không chiếm giữ độc quyền Tag đích.");
    }

    [Fact]
    public void CrossProtocol_ModbusReadAndMqttSub_EnforcesExclusiveOwnership()
    {
        // Arrange
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        var modbusCh1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(modbusCh1);
        modbusCh1.TargetTag = vreg0;

        // Tạo chân MQTT Subscribe
        var mqttNode = new MqttSubscribeNodeViewModel();
        var mqttPin = new MqttPinBindingViewModel("temperature", "temperature", MqttDataType.Int16);
        mqttPin.TagProvider = dt => _tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);
        mqttNode.Pins.Add(mqttPin);

        // Act 1: Kiểm tra danh sách tùy chọn của chân MQTT
        var mqttOptions = mqttPin.TagOptions.ToList();
        var vreg0OptionInMqtt = mqttOptions.FirstOrDefault(o => o.Tag.Name == "VREG0");

        // Assert 1: Chân MQTT không được phép chọn VREG0 vì Modbus Read đang ghi độc quyền
        Assert.NotNull(vreg0OptionInMqtt);
        Assert.False(vreg0OptionInMqtt.IsSelectable, "Chân MQTT không được chọn VREG0 khi Modbus Read đang ghi vào nó.");
        Assert.Contains("Modbus Kênh 1", vreg0OptionInMqtt.ConflictBadge);

        // Act 2: Giải phóng Tag ở kênh Modbus
        var noneTag = _tagCatalog.AllTags.First(t => t.Kind == TagKind.None);
        modbusCh1.TargetTag = noneTag;

        // Assert 2: Chân MQTT ngay lập tức thấy VREG0 khả dụng trở lại
        var updatedMqttOptions = mqttPin.TagOptions.ToList();
        var updatedVreg0Option = updatedMqttOptions.FirstOrDefault(o => o.Tag.Name == "VREG0");
        Assert.NotNull(updatedVreg0Option);
        Assert.True(updatedVreg0Option.IsSelectable, "Sau khi Modbus giải phóng, VREG0 phải khả dụng lại cho MQTT.");
    }

    [Fact]
    public void DeploySafetyGate_WhenMultipleIngestionWritersTargetSameTag_RejectsDeployment()
    {
        // Arrange
        var deployVM = new DeployViewModel();
        ushort sharedTagIndex = 10; // VREG10

        var modbusReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                PortIndex = 0,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { Id = "P1", Label = "Val1", TargetTagIndex = sharedTagIndex }
                }
            }
        };

        var mqttSubs = new List<MqttSubscriptionConfigDto>
        {
            new()
            {
                Topic = "factory/env",
                Pins = new List<MqttPinBindingDto>
                {
                    new() { Id = "P2", Label = "humidity", TargetTagIndex = sharedTagIndex }
                }
            }
        };

        // Act
        var (isValid, errorMessage) = deployVM.ValidateIngestionOwnership(modbusReads, mqttSubs);

        // Assert
        Assert.False(isValid, "DeploySafetyGate phải từ chối khi nhiều luồng ngoại vi cùng ghi vào 1 Tag.");
        Assert.NotNull(errorMessage);
        Assert.Contains("Xung đột sở hữu ghi độc quyền", errorMessage);
        Assert.Contains("Modbus Master", errorMessage);
        Assert.Contains("MQTT Subscribe", errorMessage);
    }

    [Fact]
    public void DeploySafetyGate_WhenLogicEditorRulesTargetSameTag_AllowsDeployment()
    {
        // Arrange
        // Mô phỏng 2 quy tắc Logic Editor (FBD / Rule Table) điều khiển cùng chân DO0:
        // Rule 1: DI0 = 1 -> DO0 = 1 (Nút Start)
        // Rule 2: DI1 = 1 -> DO0 = 0 (Nút Stop)
        var ruleTableVM = new RuleTableViewModel(_tagCatalog);
        var do0 = _tagCatalog.AllTags.First(t => t.Name == "DO0");
        var di0 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var di1 = _tagCatalog.AllTags.First(t => t.Name == "DI1");

        ruleTableVM.Rules.Add(new RuleItemModel
        {
            Id = "R1",
            Enabled = true,
            TriggerTag = di0,
            ActionTag = do0,
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        });
        ruleTableVM.Rules.Add(new RuleItemModel
        {
            Id = "R2",
            Enabled = true,
            TriggerTag = di1,
            ActionTag = do0,
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        });

        // Act: Chỉ kiểm tra ValidateIngestionOwnership với các luồng truyền thông hợp lệ riêng rẽ
        var deployVM = new DeployViewModel();
        var modbusReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                PortIndex = 0,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { Id = "P1", Label = "Val1", TargetTagIndex = 1 }
                }
            }
        };

        var mqttSubs = new List<MqttSubscriptionConfigDto>
        {
            new()
            {
                Topic = "factory/sensor",
                Pins = new List<MqttPinBindingDto>
                {
                    new() { Id = "P2", Label = "temp", TargetTagIndex = 2 }
                }
            }
        };

        var (isValid, errorMessage) = deployVM.ValidateIngestionOwnership(modbusReads, mqttSubs);

        // Assert: Bảng Logic Editor được phép có nhiều luật ghi vào cùng Tag (Start/Stop state latch),
        // và các luồng truyền thông ngoại vi khi không trùng Tag sẽ hợp lệ hoàn toàn.
        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Equal(2, ruleTableVM.Rules.Count);
    }

    private static void RunOnSta(Action action)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.ResourceAssembly == null)
                {
                    System.Windows.Application.ResourceAssembly = typeof(SimplePLC.Studio.App).Assembly;
                }
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    [Fact]
    public void ModbusPortConfigView_WhenAddMasterChannelExecuted_DoesNotCrash()
    {
        RunOnSta(() =>
        {
            var meta = new ProjectMetadata();
            var portConfig = new ModbusPortConfigModel
            {
                PortIndex = 0,
                PortName = "RS-485 #1",
                Enabled = true,
                Role = "Master"
            };
            meta.ModbusPorts.Add(portConfig);

            var tagCatalog = new TagCatalogViewModel();
            tagCatalog.SyncWithProductDefinition(ProductDefinition.CreateRemoteIo8Di8Do4Ai());

            var vm = new ModbusPortConfigViewModel(meta, tagCatalog);
            var control = new SimplePLC.Studio.Controls.ModbusPortConfigControl { DataContext = vm };
            var window = new System.Windows.Window { Content = control, Width = 1000, Height = 700 };
            window.Show();

            Assert.NotNull(vm.SelectedPort);
            vm.SelectedPort.AddMasterChannelCommand.Execute(null);

            control.Measure(new System.Windows.Size(1000, 700));
            control.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
            control.UpdateLayout();

            // Chọn tag VREG0 cho channel vừa tạo
            var ch = vm.SelectedPort.MasterChannels.First();
            var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
            ch.TargetTag = vreg0;

            control.UpdateLayout();

            // Thêm channel thứ 2
            vm.SelectedPort.AddMasterChannelCommand.Execute(null);
            control.UpdateLayout();

            // Channel 2 chọn VREG1
            var ch2 = vm.SelectedPort.MasterChannels[1];
            var vreg1 = tagCatalog.AllTags.First(t => t.Name == "VREG1");
            ch2.TargetTag = vreg1;
            control.UpdateLayout();

            // Kiểm tra Channel 1 có bị mất tag hay không!
            Assert.NotNull(ch.TargetTag);
            Assert.Equal("VREG0", ch.TargetTag.Name);
            Assert.Equal("VREG0", ch.TargetTagName);

            window.Close();
        });
    }

    [Fact]
    public void MqttCommandSubscribeControl_WhenAddPinExecuted_DoesNotCrash()
    {
        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            tagCatalog.SyncWithProductDefinition(ProductDefinition.CreateRemoteIo8Di8Do4Ai());

            var node = new MqttSubscribeNodeViewModel();
            var vm = new MqttSubConfigDialogViewModel(node, tagCatalog);
            var control = new SimplePLC.Studio.Controls.MqttCommandSubscribeControl { DataContext = vm };
            var window = new System.Windows.Window { Content = control, Width = 1000, Height = 700 };
            window.Show();

            vm.AddPinCommand.Execute(null);

            control.Measure(new System.Windows.Size(1000, 700));
            control.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
            control.UpdateLayout();

            // Chọn tag cho pin 1
            var pin1 = node.Pins.First();
            var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
            pin1.TargetTag = vreg0;

            control.UpdateLayout();

            // Thêm pin 2
            vm.AddPinCommand.Execute(null);
            control.UpdateLayout();

            var pin2 = node.Pins[1];
            var vreg1 = tagCatalog.AllTags.First(t => t.Name == "VREG1");
            pin2.TargetTag = vreg1;
            control.UpdateLayout();

            // Kiểm tra Pin 1 không bị mất tag
            Assert.NotNull(pin1.TargetTag);
            Assert.Equal("VREG0", pin1.TargetTag.Name);
            Assert.Equal("VREG0", pin1.TargetTagName);

            window.Close();
        });
    }

    [Fact]
    public void TagWriteOwnershipService_IsSameTag_BoundaryAndEdgeCases()
    {
        var noneTag1 = new TagModel { Kind = TagKind.None, Name = "NONE", Index = 0 };
        var noneTag2 = new TagModel { Kind = TagKind.None, Name = "NONE", Index = 0 };
        var vreg0 = new TagModel { Kind = TagKind.VirtualRegister, Name = "VREG0", Index = 0 };
        var vreg0Lower = new TagModel { Kind = TagKind.VirtualRegister, Name = "vreg0", Index = 0 };
        var vreg1 = new TagModel { Kind = TagKind.VirtualRegister, Name = "VREG1", Index = 1 };

        Assert.False(TagWriteOwnershipService.IsSameTag(null, null));
        Assert.False(TagWriteOwnershipService.IsSameTag(vreg0, null));
        Assert.False(TagWriteOwnershipService.IsSameTag(null, vreg0));
        Assert.False(TagWriteOwnershipService.IsSameTag(noneTag1, noneTag2), "Hai Tag Kind.None không được xem là xung đột chiếm giữ.");
        Assert.True(TagWriteOwnershipService.IsSameTag(vreg0, vreg0Lower), "Khớp Tag không phân biệt hoa thường theo chuẩn IEC.");
        Assert.False(TagWriteOwnershipService.IsSameTag(vreg0, vreg1));
    }

    [Fact]
    public void ModbusReadChannel_WhenChannelRemoved_ReleasesTagOwnership()
    {
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        var ch2 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40002,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch2);

        // Ban đầu Kênh 2 thấy VREG0 bị chiếm dụng
        var optCh2Initial = ch2.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.False(optCh2Initial.IsSelectable);

        // Xóa Kênh 1
        portVM.MasterChannels.Remove(ch1);
        ch1.TargetTag = null;
        TagWriteOwnershipService.Instance.NotifyOwnershipChanged();

        // Kênh 2 thấy VREG0 tự do trở lại
        var optCh2After = ch2.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(optCh2After.IsSelectable);
        Assert.False(optCh2After.HasConflict);
    }

    [Fact]
    public void ModbusPortItemViewModel_WhenPortDisabled_ReleasesAllClaims()
    {
        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };
        var portVM = new ModbusPortItemViewModel(portConfig, _tagCatalog);
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");

        var ch1 = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog.AllTags,
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16
        };
        portVM.MasterChannels.Add(ch1);
        ch1.TargetTag = vreg0;

        // Tạo chân MQTT kiểm tra
        var mqttPin = new MqttPinBindingViewModel("temp", "temp", MqttDataType.Int16);
        mqttPin.TagProvider = dt => _tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);

        // Khi cổng bật: MQTT thấy VREG0 bị chiếm
        var mqttOptBefore = mqttPin.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.False(mqttOptBefore.IsSelectable);

        // Tắt cổng RS-485
        portVM.Enabled = false;

        // Khi cổng tắt: Quyền chiếm giữ được giải phóng tự động
        var mqttOptAfter = mqttPin.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(mqttOptAfter.IsSelectable);
    }

    [Fact]
    public void MqttPinBindingViewModel_SelfExemption_AllowsCurrentPinToKeepTag()
    {
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var mqttPin = new MqttPinBindingViewModel("pressure", "pressure", MqttDataType.Int16);
        mqttPin.TagProvider = dt => _tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);

        // Gán VREG0 cho pin này
        mqttPin.TargetTag = vreg0;

        // Kiểm tra options của chính pin này: VREG0 phải selectable
        var opt = mqttPin.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(opt.IsSelectable, "Tự miễn trừ (Self-Exemption): Chân hiện tại phải luôn được phép giữ Tag của chính nó.");
        Assert.False(opt.HasConflict);
    }

    [Fact]
    public void CrossProtocol_TwoMqttSubscribePins_EnforcesExclusiveOwnership()
    {
        var vreg0 = _tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var node = new MqttSubscribeNodeViewModel();

        var pin1 = new MqttPinBindingViewModel("pin1", "field1", MqttDataType.Int16);
        pin1.TagProvider = dt => _tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);
        node.Pins.Add(pin1);

        var pin2 = new MqttPinBindingViewModel("pin2", "field2", MqttDataType.Int16);
        pin2.TagProvider = dt => _tagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dt, includeNone: true);
        node.Pins.Add(pin2);

        // Pin 1 gán VREG0
        pin1.TargetTag = vreg0;

        // Pin 2 kiểm tra options
        var pin2Opt = pin2.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.False(pin2Opt.IsSelectable);
        Assert.True(pin2Opt.HasConflict);
        Assert.Contains("MQTT", pin2Opt.ConflictBadge);
        Assert.Contains("MQTT", pin2Opt.ConflictTooltip);
    }

    [Fact]
    public async Task DeploySafetyGate_WhenConflictExists_AbortsPreflightInDeployViewModel()
    {
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();

        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");

        var logicEditorVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: a => a());

        // Tạo metadata có 2 kênh Modbus Read cùng ghi vào VREG0
        var meta = new ProjectMetadata
        {
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new()
                {
                    PortIndex = 0,
                    PortName = "RS-485 #1",
                    Enabled = true,
                    Role = "Master",
                    MasterChannels = new List<ModbusMasterChannelModel>
                    {
                        new()
                        {
                            SlaveId = 1,
                            FunctionCode = 3,
                            Address = 40001,
                            TargetTagName = vreg0.Name
                        },
                        new()
                        {
                            SlaveId = 2,
                            FunctionCode = 3,
                            Address = 40002,
                            TargetTagName = vreg0.Name
                        }
                    }
                }
            }
        };

        var deployVM = new DeployViewModel(
            AppServices.Instance.DeployUseCase,
            ruleTableVM,
            logicEditorVM,
            getMetadata: () => meta);

        // Act
        await deployVM.StartDeployAsync();

        // Assert
        Assert.False(deployVM.IsSuccess);
        Assert.Equal(DeployState.Failed, deployVM.DeployState);
        Assert.Equal(StepStatus.Failed, deployVM.Steps[0].Status);
        Assert.Contains("[SAFETY ABORT]", deployVM.ConsoleLog);
        Assert.Contains("Xung đột sở hữu ghi độc quyền", deployVM.StatusMessage);
    }

    [Fact]
    public void Deploy_WhenModbusReadNodeSynchronizedToTab0_DoesNotDuplicateChannelsAndPassesValidation()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = tagCatalog.AllTags.First(t => t.Name == "VREG1");

        var logicEditorVM = new LogicEditorViewModel(tagCatalog);
        var mbReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 0,
            SlaveId = 1,
            FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters,
            StartAddress = 0,
            RegCount = 4
        };
        mbReadNode.Pins.Add(new ModbusPinBindingViewModel
        {
            Id = "P1",
            Label = "v1",
            RegOffset = 0,
            DataType = ModbusDataType.Real,
            TargetTag = vreg0
        });
        mbReadNode.Pins.Add(new ModbusPinBindingViewModel
        {
            Id = "P2",
            Label = "v2",
            RegOffset = 2,
            DataType = ModbusDataType.Real,
            TargetTag = vreg1
        });
        logicEditorVM.Nodes.Add(mbReadNode);

        // Mô phỏng Tab Cấu hình Cổng RS-485 (Port 0) đã được đồng bộ 2 kênh từ MB_READ
        var meta = new ProjectMetadata
        {
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new()
                {
                    PortIndex = 0,
                    PortName = "RS-485 #1",
                    Enabled = true,
                    Role = "Master",
                    MasterChannels = new List<ModbusMasterChannelModel>
                    {
                        new()
                        {
                            SlaveId = 1,
                            FunctionCode = 4,
                            Address = 30001,
                            DataType = ModbusDataType.Real,
                            TargetTagName = "VREG0",
                            LinkedNodeId = mbReadNode.Id // Kênh đã liên kết với khối MB_READ
                        },
                        new()
                        {
                            SlaveId = 1,
                            FunctionCode = 4,
                            Address = 30003,
                            DataType = ModbusDataType.Real,
                            TargetTagName = "VREG1",
                            LinkedNodeId = mbReadNode.Id // Kênh đã liên kết với khối MB_READ
                        }
                    }
                }
            }
        };

        var deployVM = new DeployViewModel(
            AppServices.Instance.DeployUseCase,
            logicEditorVM: logicEditorVM,
            getMetadata: () => meta);

        // Act: Trích xuất các kênh nạp từ Tab 0 khi đã có canvasReads từ logicEditorVM
        var canvasReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                NodeId = mbReadNode.Id,
                PortIndex = 0,
                SlaveId = 1,
                FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters,
                StartAddress = 0,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { Id = "P1", Label = "v1", TargetTagIndex = (ushort)vreg0.Index },
                    new() { Id = "P2", Label = "v2", TargetTagIndex = (ushort)vreg1.Index }
                }
            }
        };

        var (tab0Reads, _) = deployVM.ExtractTab0ModbusChannels(meta, canvasReads, null);

        // Assert 1: tab0Reads KHÔNG được nhân bản các kênh đã gắn LinkedNodeId hoặc đã có trên Canvas
        Assert.Empty(tab0Reads);

        // Assert 2: Khi gộp danh sách nạp, ValidateIngestionOwnership phải hợp lệ (không bị SAFETY ABORT)
        var mergedReads = new List<ModbusMasterReadConfigDto>(canvasReads);
        mergedReads.AddRange(tab0Reads);
        var (isValid, errorMsg) = deployVM.ValidateIngestionOwnership(mergedReads, null);
        Assert.True(isValid, $"Kỳ vọng kiểm tra hợp lệ nhưng bị lỗi: {errorMsg}");
        Assert.Null(errorMsg);
    }

    [Fact]
    public void Deploy_WhenConflictOccursInEnglish_OutputsEnglishErrorMessage()
    {
        // Arrange
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            LocalizationService.Instance.CurrentLanguage = "en";
            var deployVM = new DeployViewModel();

            var conflictingReads = new List<ModbusMasterReadConfigDto>
            {
                new()
                {
                    NodeId = "MB_1",
                    PortIndex = 0,
                    StartAddress = 0,
                    Pins = new List<ModbusMasterPinBindingDto>
                    {
                        new() { TargetTagIndex = 40 }
                    }
                },
                new()
                {
                    NodeId = "MB_2",
                    PortIndex = 0,
                    StartAddress = 10,
                    Pins = new List<ModbusMasterPinBindingDto>
                    {
                        new() { TargetTagIndex = 40 }
                    }
                }
            };

            // Act
            var (isValid, errorMessage) = deployVM.ValidateIngestionOwnership(conflictingReads, null);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.StartsWith("Exclusive write ownership conflict:", errorMessage);
            Assert.DoesNotContain("Xung đột sở hữu ghi độc quyền", errorMessage);
            Assert.Contains("Tag 40", errorMessage);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void Deploy_WhenConflictOccursInVietnamese_OutputsVietnameseErrorMessage()
    {
        // Arrange
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            LocalizationService.Instance.CurrentLanguage = "vi";
            var deployVM = new DeployViewModel();

            var conflictingReads = new List<ModbusMasterReadConfigDto>
            {
                new()
                {
                    NodeId = "MB_1",
                    PortIndex = 0,
                    StartAddress = 0,
                    Pins = new List<ModbusMasterPinBindingDto>
                    {
                        new() { TargetTagIndex = 40 }
                    }
                },
                new()
                {
                    NodeId = "MB_2",
                    PortIndex = 0,
                    StartAddress = 10,
                    Pins = new List<ModbusMasterPinBindingDto>
                    {
                        new() { TargetTagIndex = 40 }
                    }
                }
            };

            // Act
            var (isValid, errorMessage) = deployVM.ValidateIngestionOwnership(conflictingReads, null);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.StartsWith("Xung đột sở hữu ghi độc quyền:", errorMessage);
            Assert.DoesNotContain("Exclusive write ownership conflict", errorMessage);
            Assert.Contains("Tag 40", errorMessage);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void ModbusReadConfigDialog_AutoSequenceOffsets_Aligns32BitOffsetsCorrectly()
    {
        // Arrange
        var tagCatalog = new TagCatalogViewModel();
        var mbNode = new ModbusReadNodeViewModel();
        var dialogVM = new ModbusReadConfigDialogViewModel(mbNode, tagCatalog);

        mbNode.Pins.Clear();
        mbNode.Pins.Add(new ModbusPinBindingViewModel { Label = "v1", RegOffset = 0, DataType = ModbusDataType.Real });
        mbNode.Pins.Add(new ModbusPinBindingViewModel { Label = "v2", RegOffset = 1, DataType = ModbusDataType.Real }); // ban đầu bị chồng lấn offset 1
        mbNode.Pins.Add(new ModbusPinBindingViewModel { Label = "v3", RegOffset = 2, DataType = ModbusDataType.Real });
        mbNode.Pins.Add(new ModbusPinBindingViewModel { Label = "Vavg", RegOffset = 3, DataType = ModbusDataType.Real });

        // Act
        dialogVM.AutoSequenceOffsets();

        // Assert: 4 chân REAL 32-bit phải có offset lần lượt 0, 2, 4, 6 và RegCount tự động nâng lên 8
        Assert.Equal(0, mbNode.Pins[0].RegOffset);
        Assert.Equal(2, mbNode.Pins[1].RegOffset);
        Assert.Equal(4, mbNode.Pins[2].RegOffset);
        Assert.Equal(6, mbNode.Pins[3].RegOffset);
        Assert.True(dialogVM.RegCount >= 8, $"RegCount phải >= 8 nhưng thực tế là {dialogVM.RegCount}");
    }

    [Fact]
    public void TagCatalog_GetFilteredTags_IncludesAllVregs_RegardlessOfModbusDataType()
    {
        // Arrange: Đảm bảo có một số VREG được đặt là Float32 và một số là Int32
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        vreg0.DataType = TagDataType.Float32;
        vreg1.DataType = TagDataType.Int32;

        // Act: Lấy danh sách tag cho ModbusDataType.Int16
        var filteredForInt16 = catalog.GetFilteredTags(TagAccessRole.WriteTarget, ModbusDataType.Int16).ToList();
        // Lấy danh sách tag cho ModbusDataType.Real
        var filteredForReal = catalog.GetFilteredTags(TagAccessRole.WriteTarget, ModbusDataType.Real).ToList();

        // Assert: Cả VREG0 và VREG1 đều phải xuất hiện trong cả 2 danh sách vì chúng có IsDataTypeEditable
        Assert.Contains(filteredForInt16, t => t.Name == "VREG0");
        Assert.Contains(filteredForInt16, t => t.Name == "VREG1");
        Assert.Contains(filteredForReal, t => t.Name == "VREG0");
        Assert.Contains(filteredForReal, t => t.Name == "VREG1");
    }

    [Fact]
    public void ModbusPinBinding_WhenTargetTagSelected_AutoAdaptsDataType()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Float32;

        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        vreg1.DataType = TagDataType.Int32;

        var do0 = catalog.AllTags.First(t => t.Name == "DO0");

        var pin = new ModbusPinBindingViewModel
        {
            DataType = ModbusDataType.Int16,
            TagProvider = dt => catalog.GetFilteredTags(TagAccessRole.WriteTarget, dt)
        };

        // Act & Assert 1: Gán tag Float32 -> DataType tự động chuyển sang Real
        pin.TargetTag = vreg0;
        Assert.Equal(ModbusDataType.Real, pin.DataType);

        // Act & Assert 2: Gán tag Boolean -> DataType tự động chuyển sang Bool
        pin.TargetTag = do0;
        Assert.Equal(ModbusDataType.Bool, pin.DataType);

        // Act & Assert 3: Gán tag Int32 -> DataType tự động chuyển sang Int16/Int32
        pin.TargetTag = vreg1;
        Assert.Equal(ModbusDataType.Int16, pin.DataType);
    }

    [Fact]
    public void ModbusPinBinding_WhenDataTypeChanged_SynchronizesTargetTagDataTypeInCatalog()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Int32;

        var pin = new ModbusPinBindingViewModel
        {
            DataType = ModbusDataType.Int16,
            TagProvider = dt => catalog.GetFilteredTags(TagAccessRole.WriteTarget, dt)
        };
        pin.TargetTag = vreg0;

        // Act 1: Đổi DataType của pin sang Real
        pin.DataType = ModbusDataType.Real;

        // Assert 1: TargetTag.DataType trong TagCatalog phải đồng bộ sang Float32
        Assert.Equal(TagDataType.Float32, vreg0.DataType);

        // Act 2: Đổi DataType của pin lại sang Int16
        pin.DataType = ModbusDataType.Int16;

        // Assert 2: TargetTag.DataType trong TagCatalog phải đồng bộ về Int32
        Assert.Equal(TagDataType.Int32, vreg0.DataType);
    }

    [Fact]
    public void TagWriteOwnershipService_KeyedProvider_ReplacesPreviousInstanceAndReleasesDeletedChannelTags()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        var vreg5 = catalog.AllTags.First(t => t.Name == "VREG5");
        var vreg13 = catalog.AllTags.First(t => t.Name == "VREG13");

        var portConfig = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master"
        };

        // Bước 1: Instance 1 của Port 0 (chứa VREG0 ở Kênh 1 và VREG5 ở Kênh 2)
        var portVM1 = new ModbusPortItemViewModel(portConfig, catalog);
        var ch1 = new ModbusMasterChannelItemViewModel { TargetTag = vreg0, FunctionCode = 3, Address = 40001, DataType = ModbusDataType.Real };
        var ch2 = new ModbusMasterChannelItemViewModel { TargetTag = vreg5, FunctionCode = 3, Address = 40002, DataType = ModbusDataType.Int16 };
        portVM1.MasterChannels.Add(ch1);
        portVM1.MasterChannels.Add(ch2);

        // Khẳng định ban đầu VREG0 và VREG5 bị chiếm dụng
        var claimVreg0 = TagWriteOwnershipService.Instance.GetClaimant(vreg0, null);
        var claimVreg5 = TagWriteOwnershipService.Instance.GetClaimant(vreg5, null);
        Assert.NotNull(claimVreg0);
        Assert.NotNull(claimVreg5);

        // Bước 2: Instance 2 của Port 0 (mô phỏng mở lại dialog hoặc tải lại sau khi xóa hết các kênh, chỉ giữ lại VREG13)
        var portVM2 = new ModbusPortItemViewModel(portConfig, catalog);
        var chOnly13 = new ModbusMasterChannelItemViewModel { TargetTag = vreg13, FunctionCode = 3, Address = 40001, DataType = ModbusDataType.Real };
        portVM2.MasterChannels.Add(chOnly13);

        // Act: Đăng ký của PortVM2 phải ghi đè hoàn toàn PortVM1 cho Port 0 (Keyed Provider)
        var claimVreg0After = TagWriteOwnershipService.Instance.GetClaimant(vreg0, null);
        var claimVreg5After = TagWriteOwnershipService.Instance.GetClaimant(vreg5, null);
        var claimVreg13After = TagWriteOwnershipService.Instance.GetClaimant(vreg13, null);

        // Assert: VREG0 và VREG5 phải được giải phóng 100% (null claim), VREG13 bị chiếm dụng
        Assert.Null(claimVreg0After);
        Assert.Null(claimVreg5After);
        Assert.NotNull(claimVreg13After);
        Assert.Contains("Modbus Kênh 1", claimVreg13After.DisplayBadge);

        portVM1.Dispose();
        portVM2.Dispose();
    }

    [Fact]
    public void ModbusMasterChannel_WhenTargetTagIsNone_DataTypeIsNullAndEndiannessIsDash()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var noneTag = catalog.AllTags.First(t => t.Kind == TagKind.None);

        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            TargetTag = noneTag
        };

        // Assert: Khi TargetTag là NONE thì DataType phải là null và IsEndiannessApplicable là false (hiển thị "—")
        Assert.Null(channel.DataType);
        Assert.False(channel.IsEndiannessApplicable);
        Assert.Equal("—", channel.RegisterSizeDisplay);
    }

    [Fact]
    public void ModbusMasterChannel_WhenTargetTagSelected_SetsDataType_AndRevertingToNoneClearsDataType()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Float32;

        var noneTag = catalog.AllTags.First(t => t.Kind == TagKind.None);

        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            TargetTag = noneTag
        };

        Assert.Null(channel.DataType);

        // Act 1: Chọn Tag VREG0 (REAL)
        channel.TargetTag = vreg0;

        // Assert 1: DataType tự động nhảy sang Real, Endianness có hiệu lực
        Assert.Equal(ModbusDataType.Real, channel.DataType);
        Assert.True(channel.IsEndiannessApplicable);

        // Act 2: Đổi lại Tag về NONE
        channel.TargetTag = noneTag;

        // Assert 2: DataType tự động trở về null và Endianness không áp dụng ("—")
        Assert.Null(channel.DataType);
        Assert.False(channel.IsEndiannessApplicable);
        Assert.Equal("—", channel.DataTypeText);
    }

    [Fact]
    public void ModbusMasterChannel_WhenTargetTagIsDO0_DataTypeTextIsBool_AndTagsListIsNotRestricted()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");

        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 1 // FC01: Read Coils (Bit Operation)
        };

        // Act: Chọn DO0
        channel.TargetTag = do0;

        // Assert: DataTypeText là BOOL, không áp dụng Endianness, không cho phép đổi width 16/32
        Assert.Equal("BOOL", channel.DataTypeText);
        Assert.Equal(ModbusDataType.Bool, channel.DataType);
        Assert.False(channel.IsEndiannessApplicable);
        Assert.False(channel.CanToggleRegisterWidth);

        // FilteredAvailableTags lọc các Tag phù hợp với thao tác Bit (chỉ bao gồm Digital tags)
        var availableTags = channel.FilteredAvailableTags.ToList();
        Assert.Contains(availableTags, t => t.Name == "DO0");
        Assert.DoesNotContain(availableTags, t => t.Name == "VREG0");
    }

    [Fact]
    public void ModbusMasterChannel_WhenTargetTagIsIntegerVREG_AllowsToggleRegisterWidth()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        vreg1.DataType = TagDataType.Int32;

        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 3,
            TargetTag = vreg1
        };

        // Assert ban đầu: Mặc định là INT16 (1 thanh ghi)
        Assert.Equal("INT16", channel.DataTypeText);
        Assert.Equal(ModbusDataType.Int16, channel.DataType);
        Assert.False(channel.IsEndiannessApplicable);
        Assert.True(channel.CanToggleRegisterWidth);

        // Act 1: Click toggle register width sang 32-bit
        channel.ToggleRegisterWidthCommand.Execute(null);

        // Assert 1: Chuyển sang INT32, kích hoạt Endianness
        Assert.Equal("INT32", channel.DataTypeText);
        Assert.Equal(ModbusDataType.Int32, channel.DataType);
        Assert.True(channel.IsEndiannessApplicable);

        // Act 2: Click toggle register width trở lại 16-bit
        channel.ToggleRegisterWidthCommand.Execute(null);

        // Assert 2: Quay về INT16, Endianness tắt
        Assert.Equal("INT16", channel.DataTypeText);
        Assert.Equal(ModbusDataType.Int16, channel.DataType);
        Assert.False(channel.IsEndiannessApplicable);
    }

    [Fact]
    public void ModbusPinBinding_DataTypeText_AdaptsToTagAndAllowsToggleRegisterWidth()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Float32;
        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        vreg1.DataType = TagDataType.Int32;

        var pin = new ModbusPinBindingViewModel();

        // Act 1: Gán DO0
        pin.TargetTag = do0;
        Assert.Equal("BOOL", pin.DataTypeText);
        Assert.False(pin.CanToggleRegisterWidth);

        // Act 2: Gán VREG0 (REAL)
        pin.TargetTag = vreg0;
        Assert.Equal("REAL", pin.DataTypeText);
        Assert.False(pin.CanToggleRegisterWidth);

        // Act 3: Gán VREG1 (INT)
        pin.TargetTag = vreg1;
        Assert.Equal("INT16", pin.DataTypeText);
        Assert.True(pin.CanToggleRegisterWidth);

        // Act 4: Toggle sang INT32
        pin.ToggleRegisterWidthCommand.Execute(null);
        Assert.Equal("INT32", pin.DataTypeText);
        Assert.Equal(ModbusDataType.Int32, pin.DataType);

        // Act 5: Toggle lại INT16
        pin.ToggleRegisterWidthCommand.Execute(null);
        Assert.Equal("INT16", pin.DataTypeText);
        Assert.Equal(ModbusDataType.Int16, pin.DataType);
    }

    [Fact]
    public void MqttPinBinding_DataTypeText_ReflectsTargetTagAccurately()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        vreg0.DataType = TagDataType.Float32;
        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        vreg1.DataType = TagDataType.Int32;

        var pin = new MqttPinBindingViewModel();

        // Ban đầu chưa có Tag
        Assert.Equal("—", pin.DataTypeText);

        // Gán DO0
        pin.TargetTag = do0;
        Assert.Equal("BOOL", pin.DataTypeText);

        // Chân Float gán VREG0 (REAL)
        var pinFloat = new MqttPinBindingViewModel("Analog", "val", MqttDataType.Float);
        pinFloat.TargetTag = vreg0;
        Assert.Equal("REAL", pinFloat.DataTypeText);

        // Chân Int32 gán VREG1 (INT32)
        var pinInt = new MqttPinBindingViewModel("Digital", "val", MqttDataType.Int32);
        pinInt.TargetTag = vreg1;
        Assert.Equal("INT32", pinInt.DataTypeText);
    }

    [Fact]
    public void ModbusMasterChannel_WhenFC03_FilteredTagsOnlyContainRegisters_NeverContainsVflagOrDo()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 3 // FC03: Read Holding Registers
        };

        // Act
        var filtered = channel.FilteredAvailableTags.ToList();

        // Assert: FC03 chỉ chứa NONE và các thanh ghi (VREG), tuyệt đối KHÔNG có VFLAG hoặc DO
        Assert.Contains(filtered, t => t.Name == "NONE");
        Assert.Contains(filtered, t => t.Name == "VREG0");
        Assert.Contains(filtered, t => t.Name == "VREG_RETAIN0");
        Assert.DoesNotContain(filtered, t => t.Name == "VFLAG0");
        Assert.DoesNotContain(filtered, t => t.Name == "VFLAG31");
        Assert.DoesNotContain(filtered, t => t.Name == "DO0");
        Assert.DoesNotContain(filtered, t => t.Name == "DI0");
    }

    [Fact]
    public void ModbusMasterChannel_WhenFC01_FilteredTagsOnlyContainDigitalTags_NeverContainsVreg()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 1 // FC01: Read Coils
        };

        // Act
        var filtered = channel.FilteredAvailableTags.ToList();

        // Assert: FC01 chỉ chứa NONE và các cờ bit (DO, VFLAG), tuyệt đối KHÔNG có VREG
        Assert.Contains(filtered, t => t.Name == "NONE");
        Assert.Contains(filtered, t => t.Name == "DO0");
        Assert.Contains(filtered, t => t.Name == "VFLAG0");
        Assert.Contains(filtered, t => t.Name == "VFLAG31");
        Assert.DoesNotContain(filtered, t => t.Name == "VREG0");
        Assert.DoesNotContain(filtered, t => t.Name == "VREG_RETAIN0");
    }

    [Fact]
    public void ModbusMasterChannel_WhenSwitchingFromFC03ToFC01_AutoRevertsIncompatibleRegisterTagToNone()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 3,
            TargetTag = vreg0
        };

        Assert.Equal("VREG0", channel.TargetTag.Name);

        // Act: Đổi Function Code sang FC01 (Read Coils)
        channel.FunctionCode = 1;

        // Assert: VREG0 không tương thích với thao tác Bit -> Tự động revert về NONE, DataType = Bool
        Assert.Equal("NONE", channel.TargetTag.Name);
        Assert.Equal(ModbusDataType.Bool, channel.DataType);
        Assert.Equal("BOOL", channel.DataTypeText);
    }

    [Fact]
    public void ModbusMasterChannel_ScrollingThroughTags_DoesNotAlterFilteredTagsCollection()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var vreg0 = catalog.AllTags.First(t => t.Name == "VREG0");
        var vreg1 = catalog.AllTags.First(t => t.Name == "VREG1");
        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => catalog.AllTags,
            FunctionCode = 3
        };

        var initialCount = channel.FilteredAvailableTags.Count();

        // Act: Mô phỏng người dùng di chuột qua các item khác nhau trong dropdown
        channel.TargetTag = vreg0;
        var countAfterVreg0 = channel.FilteredAvailableTags.Count();

        channel.TargetTag = vreg1;
        var countAfterVreg1 = channel.FilteredAvailableTags.Count();

        // Assert: Danh sách Tag không bị co rút hay biến động (Zero Feedback Loop)
        Assert.Equal(initialCount, countAfterVreg0);
        Assert.Equal(initialCount, countAfterVreg1);
    }
}
