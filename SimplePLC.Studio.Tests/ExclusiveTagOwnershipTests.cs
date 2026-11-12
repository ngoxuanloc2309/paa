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
}
