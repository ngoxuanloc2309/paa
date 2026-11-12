using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using SimplePLC.Studio.Views;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử các tính năng nâng cao của Tag Catalog (tìm kiếm, song ngữ, lọc nhóm).
/// </summary>
public class TagCatalogEnhancementTests
{
    [Fact]
    public void TagCatalog_DynamicCountAndTitle_Matches132Tags()
    {
        var vm = new TagCatalogViewModel();

        Assert.Equal(132, vm.TotalTagCount);
        Assert.Contains("132 TAGS", vm.TitleText);
    }

    [Fact]
    public void TagCatalog_BilingualSupport_SwitchesWithoutMixedStrings()
    {
        var vm = new TagCatalogViewModel();

        // 1. Vietnamese
        LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.Contains("DANH MỤC TAG I/O", vm.TitleText);
        Assert.Contains("132 TAGS", vm.TitleText);
        Assert.Equal("Mở rộng giải thích", vm.BannerToggleText);
        Assert.Contains("Không Gian Tag Hệ Thống", vm.CurrentGroupInfo.GroupName);

        vm.FilterByGroup("DI");
        Assert.Contains("Ngõ Vào Số", vm.CurrentGroupInfo.GroupName);

        // 2. English
        LocalizationService.Instance.CurrentLanguage = "en";
        Assert.Contains("TAG I/O & INTERNAL REGISTERS CATALOG", vm.TitleText);
        Assert.Contains("132 TAGS", vm.TitleText);
        Assert.Equal("Expand Info", vm.BannerToggleText);
        Assert.Contains("Opto-Isolated", vm.CurrentGroupInfo.GroupName);

        vm.FilterByGroup("ALL");
        Assert.Contains("System Tag Space", vm.CurrentGroupInfo.GroupName);

        // Reset to EN
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    [Fact]
    public void TagCatalog_ModbusAddressAndDataType_ComputesAccurately()
    {
        var vm = new TagCatalogViewModel();

        var di0 = vm.AllTags.First(t => t.Name == "DI0");
        var do0 = vm.AllTags.First(t => t.Name == "DO0");
        var ai0 = vm.AllTags.First(t => t.Name == "AI0");
        var vf0 = vm.AllTags.First(t => t.Name == "VFLAG0");
        var vr0 = vm.AllTags.First(t => t.Name == "VREG0");
        var vrr0 = vm.AllTags.First(t => t.Name == "VREG_RETAIN0");
        var c0 = vm.AllTags.First(t => t.Name == "COUNTER0");

        Assert.Equal("10001 (0x0000)", di0.ModbusAddressText);
        Assert.Equal("BOOL", di0.DataTypeText);
        Assert.True(di0.IsReadOnly);

        Assert.Equal("00001 (0x0008)", do0.ModbusAddressText);
        Assert.Equal("BOOL", do0.DataTypeText);
        Assert.False(do0.IsReadOnly);

        Assert.Equal("30001 (0x0010)", ai0.ModbusAddressText);
        Assert.Equal("INT32", ai0.DataTypeText);
        Assert.True(ai0.IsReadOnly);

        Assert.Equal("00021 (0x0014)", vf0.ModbusAddressText);
        Assert.Equal("BOOL", vf0.DataTypeText);

        Assert.Equal("40053 (0x0034)", vr0.ModbusAddressText);
        Assert.Equal("INT32", vr0.DataTypeText);

        Assert.Equal("40085 (0x0054)", vrr0.ModbusAddressText);
        Assert.Equal("INT32", vrr0.DataTypeText);

        Assert.Equal("40117 (0x0074)", c0.ModbusAddressText);
        Assert.Equal("INT32", c0.DataTypeText);
    }

    [Fact]
    public void TagCatalog_SearchFilter_MatchesModbusAndDataType()
    {
        var vm = new TagCatalogViewModel();

        // Match by Modbus address
        vm.SearchFilter = "10001";
        Assert.Single(vm.FilteredTags);
        Assert.Equal("DI0", vm.FilteredTags[0].Name);

        // Match by Data Type
        vm.SearchFilter = "BOOL";
        Assert.Equal(54, vm.FilteredTags.Count); // 8 DI + 8 DO + 32 VFLAG + 6 System Tags

        // Clear search
        vm.ClearSearch();
        Assert.Equal(132, vm.FilteredTags.Count);
        Assert.Equal(string.Empty, vm.SearchFilter);
    }

    [Fact]
    public void TagCatalog_QuickToggle_TogglesDigitalTagsOnly()
    {
        var vm = new TagCatalogViewModel();
        var di0 = vm.AllTags.First(t => t.Name == "DI0");
        var ai0 = vm.AllTags.First(t => t.Name == "AI0");

        int oldDiVal = di0.Value;
        vm.ToggleTagValue(di0);
        Assert.Equal(oldDiVal == 0 ? 1 : 0, di0.Value);

        // Analog tag should NOT toggle
        int oldAiVal = ai0.Value;
        vm.ToggleTagValue(ai0);
        Assert.Equal(oldAiVal, ai0.Value);
    }

    [Fact]
    public void TagCatalog_GroupFiltering_FiltersCorrectly()
    {
        var vm = new TagCatalogViewModel();

        vm.FilterByGroup("DI");
        Assert.Equal(8, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.DiscreteInput, t.Kind));

        vm.FilterByGroup("DO");
        Assert.Equal(8, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.DiscreteOutput, t.Kind));

        vm.FilterByGroup("AI");
        Assert.Equal(4, vm.FilteredTags.Count);
        Assert.All(vm.FilteredTags, t => Assert.Equal(TagKind.AnalogInput, t.Kind));

        vm.FilterByGroup("VFLAG");
        Assert.Equal(32, vm.FilteredTags.Count);

        vm.FilterByGroup("VREG");
        Assert.Equal(32, vm.FilteredTags.Count);

        vm.FilterByGroup("VREG_R");
        Assert.Equal(32, vm.FilteredTags.Count);

        vm.FilterByGroup("COUNTER");
        Assert.Equal(8, vm.FilteredTags.Count);

        vm.FilterByGroup("ALL");
        Assert.Equal(132, vm.FilteredTags.Count);
    }

    [Fact]
    public void TagCatalog_BannerToggle_TogglesStateAndIcon()
    {
        var vm = new TagCatalogViewModel();
        Assert.False(vm.IsBannerExpanded);
        Assert.Equal("▼", vm.BannerToggleIcon);

        vm.ToggleBanner();
        Assert.True(vm.IsBannerExpanded);
        Assert.Equal("▲", vm.BannerToggleIcon);

        vm.ToggleBanner();
        Assert.False(vm.IsBannerExpanded);
        Assert.Equal("▼", vm.BannerToggleIcon);
    }

    [Fact]
    public void TagCatalog_IsDataTypeEditable_MatchesSpecification()
    {
        var vm = new TagCatalogViewModel();
        var di0 = vm.AllTags.First(t => t.Name == "DI0");
        var do0 = vm.AllTags.First(t => t.Name == "DO0");
        var ai0 = vm.AllTags.First(t => t.Name == "AI0");
        var vf0 = vm.AllTags.First(t => t.Name == "VFLAG0");
        var vr0 = vm.AllTags.First(t => t.Name == "VREG0");
        var vrr0 = vm.AllTags.First(t => t.Name == "VREG_RETAIN0");
        var c0 = vm.AllTags.First(t => t.Name == "COUNTER0");

        Assert.False(di0.IsDataTypeEditable);
        Assert.False(do0.IsDataTypeEditable);
        Assert.False(vf0.IsDataTypeEditable);
        Assert.False(c0.IsDataTypeEditable);

        Assert.True(ai0.IsDataTypeEditable);
        Assert.True(vr0.IsDataTypeEditable);
        Assert.True(vrr0.IsDataTypeEditable);
    }

    [Fact]
    public void GetFilteredTags_WriteTarget_Bool_ContainsOnlyDO_And_VFLAG_And_None()
    {
        var vm = new TagCatalogViewModel();
        var tags = vm.GetFilteredTags(TagAccessRole.WriteTarget, SimplePLC.Domain.Enums.MqttDataType.Bool).ToList();

        Assert.NotEmpty(tags);
        Assert.Contains(tags, t => t.Kind == TagKind.None);
        Assert.All(tags, t =>
        {
            Assert.True(t.Kind is TagKind.None or TagKind.DiscreteOutput or TagKind.VirtualFlag,
                $"Unexpected tag kind for WriteTarget BOOL: {t.Name} ({t.Kind})");
            Assert.False(t.Kind == TagKind.DiscreteInput, $"DiscreteInput {t.Name} must NOT be in WriteTarget!");
            Assert.False(t.Kind == TagKind.AnalogInput, $"AnalogInput {t.Name} must NOT be in WriteTarget!");
            Assert.False(t.Kind == TagKind.System, $"System tag {t.Name} must NOT be in WriteTarget!");
        });
    }

    [Fact]
    public void GetFilteredTags_WriteTarget_Float_ContainsOnlyFloat32_And_None()
    {
        var vm = new TagCatalogViewModel();
        // Set VREG0 to Float32
        var vr0 = vm.AllTags.First(t => t.Name == "VREG0");
        vr0.DataType = TagDataType.Float32;

        var tags = vm.GetFilteredTags(TagAccessRole.WriteTarget, SimplePLC.Domain.Enums.MqttDataType.Float).ToList();

        Assert.NotEmpty(tags);
        Assert.Contains(tags, t => t.Kind == TagKind.None);
        Assert.Contains(tags, t => t.Name == "VREG0");
        Assert.All(tags, t =>
        {
            if (t.Kind != TagKind.None)
            {
                Assert.Equal(TagDataType.Float32, t.DataType);
                Assert.True(t.IsWritable);
            }
        });
    }

    [Fact]
    public void GetFilteredTags_ReadSource_AllowsDI_And_AI()
    {
        var vm = new TagCatalogViewModel();
        var tags = vm.GetFilteredTags(TagAccessRole.ReadSource, SimplePLC.Domain.Enums.MqttDataType.Bool).ToList();

        Assert.Contains(tags, t => t.Kind == TagKind.DiscreteInput);
        Assert.Contains(tags, t => t.Kind == TagKind.DiscreteOutput);
        Assert.Contains(tags, t => t.Kind == TagKind.VirtualFlag);
    }

    [Fact]
    public void TagModel_DropdownDisplayName_FormatsRoleAndType()
    {
        var do0 = new SimplePLC.Studio.Models.TagModel
        {
            Name = "DO0",
            Alias = "FeedPump",
            Kind = TagKind.DiscreteOutput,
            DataType = TagDataType.Boolean
        };
        Assert.Equal("[DO0] FeedPump (BOOL · R/W)", do0.DropdownDisplayName);

        var di0 = new SimplePLC.Studio.Models.TagModel
        {
            Name = "DI0",
            Alias = "SensorProx",
            Kind = TagKind.DiscreteInput,
            DataType = TagDataType.Boolean
        };
        Assert.Equal("[DI0] SensorProx (BOOL · R/O)", di0.DropdownDisplayName);
    }
}
