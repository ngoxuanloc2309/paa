using System.Collections.Generic;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests verifying compliance of Toolbox category names and logic block naming
/// with the IEC 61131-3 standard (Issue #15).
/// </summary>
public class ToolboxIecNamingTests
{
    [Fact]
    public void EnglishCategories_MatchIec61131StandardNames()
    {
        var loc = LocalizationService.Instance;
        string originalLang = loc.CurrentLanguage;

        try
        {
            loc.CurrentLanguage = "en";

            // Verify main categories (Issue #15 & #17)
            Assert.Equal("PROCESS I/O", loc["ToolboxIoCategory"]);
            Assert.Equal("LOGIC", loc["ToolboxLogicCategory"]);
            Assert.Equal("TIMERS", loc["ToolboxTimerCategory"]);
            Assert.Equal("COUNTERS", loc["ToolboxCounterCategory"]);
            Assert.Equal("ANALOG & ACTUATORS", loc["ToolboxAnalogCategory"]);
            Assert.Equal("ANALOG", loc["ToolboxAnalogOnlyCategory"]);
            Assert.Equal("MODBUS", loc["ToolboxModbusCategory"]);
            Assert.Equal("MQTT", loc["ToolboxMqttCategory"]);
            Assert.Equal("GATEWAY", loc["ToolboxGatewayCategory"]);
            Assert.Equal("COMMUNICATION", loc["ToolboxCommCategory"]);

            // Verify aliases / fallback keys
            Assert.Equal("PROCESS I/O", loc["ToolboxTagsCategory"]);
            Assert.Equal("LOGIC", loc["ToolboxCoreCategory"]);
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void VietnameseCategories_MatchStandardVietnameseNames()
    {
        var loc = LocalizationService.Instance;
        string originalLang = loc.CurrentLanguage;

        try
        {
            loc.CurrentLanguage = "vi";

            // Verify main categories in Vietnamese (Issue #15 & #17)
            Assert.Equal("NGÕ VÀO / RA", loc["ToolboxIoCategory"]);
            Assert.Equal("CỔNG LOGIC", loc["ToolboxLogicCategory"]);
            Assert.Equal("BỘ ĐỊNH THỜI", loc["ToolboxTimerCategory"]);
            Assert.Equal("BỘ ĐẾM", loc["ToolboxCounterCategory"]);
            Assert.Equal("XỬ LÝ TƯƠNG TỰ & CHẤP HÀNH", loc["ToolboxAnalogCategory"]);
            Assert.Equal("XỬ LÝ TƯƠNG TỰ", loc["ToolboxAnalogOnlyCategory"]);
            Assert.Equal("MODBUS", loc["ToolboxModbusCategory"]);
            Assert.Equal("MQTT", loc["ToolboxMqttCategory"]);
            Assert.Equal("GATEWAY", loc["ToolboxGatewayCategory"]);
            Assert.Equal("TRUYỀN THÔNG", loc["ToolboxCommCategory"]);

            // Verify aliases / fallback keys
            Assert.Equal("NGÕ VÀO / RA", loc["ToolboxTagsCategory"]);
            Assert.Equal("CỔNG LOGIC", loc["ToolboxCoreCategory"]);
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void CategoryDynamicVisibility_RespectsHardwareCapabilities()
    {
        // Issue #17: Đối với các thiết bị không có kết nối thì không hiển thị
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);

        // 1. Device with RS-485 Modbus Master but NO Network (e.g. Remote I/O)
        var modbusOnly = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai(
            hardwareCapabilities: SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: 1);
        tagCatalog.SyncWithProductDefinition(modbusOnly);

        Assert.True(vm.IsModbusCategoryVisible, "Modbus category must be visible for devices with Modbus/RS-485");
        Assert.False(vm.IsMqttCategoryVisible, "MQTT category must be hidden for devices without network");
        Assert.False(vm.IsDtuGatewayCategoryVisible, "DTU category must be hidden for non-DTU devices");

        // 2. Pure Discrete Mini PLC without Modbus and without Network
        var discreteOnly = new SimplePLC.Domain.Models.ProductDefinition(
            "Discrete Mini",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 50,
            resources: SimplePLC.Domain.Models.ProductResourceProfile.DefaultRemoteIo,
            tags: System.Array.Empty<SimplePLC.Domain.Models.TagDefinition>(),
            wireProfile: 2,
            hardwareCapabilities: SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.BASE_LOGIC,
            physicalRs485PortCount: 0);
        tagCatalog.SyncWithProductDefinition(discreteOnly);

        Assert.False(vm.IsModbusCategoryVisible, "Modbus category must be hidden when device lacks RS-485 / Modbus");
        Assert.False(vm.IsMqttCategoryVisible, "MQTT category must be hidden when device lacks Network");
        Assert.False(vm.IsDtuGatewayCategoryVisible, "DTU category must be hidden when device is not DTU capable");

        // 3. IIoT Gateway / Network PLC with Ethernet, WiFi, MQTT and Modbus
        var iiotPlc = new SimplePLC.Domain.Models.ProductDefinition(
            "SynaptiX IIoT PLC",
            deviceClass: 1,
            productVariant: 2,
            maxRules: 200,
            resources: SimplePLC.Domain.Models.ProductResourceProfile.DefaultRemoteIo,
            tags: System.Array.Empty<SimplePLC.Domain.Models.TagDefinition>(),
            wireProfile: 2,
            hardwareCapabilities: SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.BASE_LOGIC
                | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.ETHERNET
                | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.MQTT_PUB
                | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.MQTT_SUB
                | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.MODBUS_MASTER
                | SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.TRANSPARENT_STREAM,
            physicalRs485PortCount: 2);
        tagCatalog.SyncWithProductDefinition(iiotPlc);

        Assert.True(vm.IsModbusCategoryVisible, "Modbus category must be visible on IIoT PLC with Modbus");
        Assert.True(vm.IsMqttCategoryVisible, "MQTT category must be visible on IIoT PLC with Network + MQTT");
        Assert.True(vm.IsDtuGatewayCategoryVisible, "DTU category must be visible on device with Transparent Stream");
    }

    [Fact]
    public void LogicBlocks_PreserveIecStandardAcronyms()
    {
        var loc = LocalizationService.Instance;
        string originalLang = loc.CurrentLanguage;

        try
        {
            foreach (var lang in new[] { "en", "vi" })
            {
                loc.CurrentLanguage = lang;

                // Timers
                Assert.StartsWith("TON", loc["ToolboxTonTitle"]);
                Assert.StartsWith("TOF", loc["ToolboxTofTitle"]);
                Assert.StartsWith("TP", loc["ToolboxTpTitle"]);
                Assert.StartsWith("CYCLE", loc["ToolboxCycleTitle"]);

                // Counters
                Assert.StartsWith("CTU", loc["ToolboxCtuTitle"]);
                Assert.StartsWith("CTD", loc["ToolboxCtdTitle"]);

                // Analog / Math
                Assert.StartsWith("SCALE", loc["ToolboxScaleTitle"]);
            }
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void LanguageToggle_ReactivityUpdatesToolboxCategories()
    {
        var loc = LocalizationService.Instance;
        string originalLang = loc.CurrentLanguage;

        try
        {
            bool propertyChangedFired = false;
            loc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                    propertyChangedFired = true;
            };

            // Switch to Vietnamese
            loc.CurrentLanguage = "vi";
            Assert.True(propertyChangedFired);
            Assert.Equal("BỘ ĐỊNH THỜI", loc["ToolboxTimerCategory"]);

            // Switch to English
            propertyChangedFired = false;
            loc.CurrentLanguage = "en";
            Assert.True(propertyChangedFired);
            Assert.Equal("TIMERS", loc["ToolboxTimerCategory"]);
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }
}
