using System.Collections.Generic;
using System.Linq;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TagCatalogSyncWithProductTests
{
    [Fact]
    public void SyncWithProductDefinition_SparseZigbeeProfile_RemovesAiAndTruncatesIo()
    {
        var vm = new TagCatalogViewModel();
        Assert.Equal(124, vm.TotalTagCount);

        // Đặt Alias trước khi sync
        var di0 = vm.AllTags.First(t => t.Name == "DI0");
        di0.Alias = "Nút Bắt Đầu (Start)";

        // Giả lập bo Zigbee 2DI / 2DO / 0AI / 32VFLAG / 32VREG / 32RETAIN / 8COUNTER
        var resources = new ProductResourceProfile(
            DigitalInputs: 2,
            DigitalOutputs: 2,
            AnalogInputs: 0,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 99,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 32,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);

        // Act: Sync
        vm.SyncWithProductDefinition(product);

        // Assert 1: Tổng số tag = 2 + 2 + 0 + 32 + 32 + 32 + 8 = 108 (không tính tag NONE)
        Assert.Equal(108, vm.TotalTagCount);

        // Assert 2: Không còn bất kỳ AI nào
        Assert.DoesNotContain(vm.AllTags, t => t.Kind == TagKind.AnalogInput);
        Assert.DoesNotContain(vm.AllTags, t => t.Name.StartsWith("AI"));

        // Assert 3: Chỉ còn đúng 2 DI (DI0, DI1) và 2 DO (DO0, DO1)
        var diTags = vm.AllTags.Where(t => t.Kind == TagKind.DiscreteInput).ToList();
        Assert.Equal(2, diTags.Count);
        Assert.Equal("DI0", diTags[0].Name);
        Assert.Equal("DI1", diTags[1].Name);

        var doTags = vm.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput).ToList();
        Assert.Equal(2, doTags.Count);
        Assert.Equal("DO0", doTags[0].Name);
        Assert.Equal("DO1", doTags[1].Name);

        // Assert 4: Tag NONE (Index 65535) vẫn được bảo toàn
        var noneTag = vm.AllTags.FirstOrDefault(t => t.Kind == TagKind.None);
        Assert.NotNull(noneTag);
        Assert.Equal(65535, noneTag.Index);

        // Assert 5: Alias đã đặt trước đó vẫn được bảo toàn nguyên vẹn
        Assert.Equal("Nút Bắt Đầu (Start)", diTags[0].Alias);

        // Assert 6: GroupButtons đã loại bỏ nút AI và cập nhật số lượng DI, DO
        Assert.DoesNotContain(vm.GroupButtons, g => g.Key == "AI");
        Assert.Contains(vm.GroupButtons, g => g.Key == "DI" && g.Label.Contains("(2)"));
        Assert.Contains(vm.GroupButtons, g => g.Key == "DO" && g.Label.Contains("(2)"));

        // Assert 7: DigitalTags được cập nhật đồng bộ
        Assert.DoesNotContain(vm.DigitalTags, t => t.Name == "DI2");
        Assert.Contains(vm.DigitalTags, t => t.Name == "DI0");
        Assert.Contains(vm.DigitalTags, t => t.Name == "NONE");
    }

    [Fact]
    public void SyncWithProductDefinition_ResetsSelectedGroup_WhenGroupNoLongerExists()
    {
        var vm = new TagCatalogViewModel();
        vm.FilterByGroup("AI");
        Assert.Equal("AI", vm.SelectedGroup);

        var resources = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 16,
            Counters: 4);

        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 50,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 16,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);

        vm.SyncWithProductDefinition(product);

        // Nhóm AI không còn -> tự động chuyển về ALL
        Assert.Equal("ALL", vm.SelectedGroup);
    }

    [Fact]
    public void UpdateTagValues_UpdatesLiveTagValuesCorrectly()
    {
        var vm = new TagCatalogViewModel();

        var liveValues = new List<RuntimeTagValue>
        {
            new() { TagIndex = 0, TagName = "DI0", Value = 1 },
            new() { TagIndex = 8, TagName = "DO0", Value = 1 },
            new() { TagIndex = 52, TagName = "VREG0", Value = 1234 }
        };

        vm.UpdateTagValues(liveValues);

        Assert.Equal(1, vm.AllTags.First(t => t.Index == 0).Value);
        Assert.Equal(1, vm.AllTags.First(t => t.Index == 8).Value);
        Assert.Equal(1234, vm.AllTags.First(t => t.Index == 52).Value);
    }

    [Fact]
    public void SyncWithProductDefinition_Zigbee4Di4DoProfile_SyncsProperly()
    {
        // Giả lập đúng cấu hình MCU:
        // #define ZIGBEE_IO_DI_NUM 4
        // #define ZIGBEE_IO_DO_NUM 4
        var vm = new TagCatalogViewModel();
        Assert.Equal(124, vm.TotalTagCount);

        var resources = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 2, // Zigbee IO
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 50,
            runtimeTagCount: (ushort)resources.TotalTags, // 4+4+0+32+32+32+8 = 112
            resources: resources);

        // Act
        vm.SyncWithProductDefinition(product);

        // Assert
        Assert.Equal(112, vm.TotalTagCount);

        // AI bị loại bỏ sạch
        Assert.DoesNotContain(vm.AllTags, t => t.Kind == TagKind.AnalogInput);
        Assert.DoesNotContain(vm.GroupButtons, b => b.Key == "AI");

        // DI chỉ có 4 tag: DI0, DI1, DI2, DI3
        var diTags = vm.AllTags.Where(t => t.Kind == TagKind.DiscreteInput).ToList();
        Assert.Equal(4, diTags.Count);
        Assert.Equal(new[] { "DI0", "DI1", "DI2", "DI3" }, diTags.Select(t => t.Name));
        var diBtn = vm.GroupButtons.First(b => b.Key == "DI");
        Assert.Contains("(4)", diBtn.Label);

        // DO chỉ có 4 tag: DO0, DO1, DO2, DO3
        var doTags = vm.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput).ToList();
        Assert.Equal(4, doTags.Count);
        Assert.Equal(new[] { "DO0", "DO1", "DO2", "DO3" }, doTags.Select(t => t.Name));
        var doBtn = vm.GroupButtons.First(b => b.Key == "DO");
        Assert.Contains("(4)", doBtn.Label);
    }
}

