using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests verifying compliance of the standardized Lucide Icons vector system (Issue #14):
/// 1. Validation of all IEC 61131-3 and System StreamGeometry definitions in Icons.xaml.
/// 2. Elimination of 100% raw OS emoji characters across critical UI views and viewmodels.
/// 3. Verification of clean industrial typography in LocalizationService dictionaries.
/// </summary>
public class LucideIconsTests
{
    private static readonly Regex EmojiPattern = new(
        @"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]|[\u2300-\u23FF]|[\u2B50]|⭐|☆|★",
        RegexOptions.Compiled);

    [Fact]
    public void IconsResourceDictionary_ContainsAllRegisteredGeometries()
    {
        // Path to Styles/Icons.xaml in SimplePLC.Studio
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimplePLC.Studio", "Styles", "Icons.xaml");
        if (!File.Exists(xamlPath))
        {
            xamlPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SimplePLC.Studio", "Styles", "Icons.xaml"));
        }

        Assert.True(File.Exists(xamlPath), $"Icons.xaml not found at: {xamlPath}");
        string xamlContent = File.ReadAllText(xamlPath);

        // Verify MIT License header is present
        Assert.Contains("Lucide Icons", xamlContent);
        Assert.Contains("License: MIT", xamlContent);

        // Required IEC category & block keys
        string[] requiredIecKeys =
        {
            "IconIecProcessIo",
            "IconIecInputPort",
            "IconIecActionCoil",
            "IconIecLogic",
            "IconIecTriggerPulse",
            "IconIecGuardInterlock",
            "IconIecComparator",
            "IconIecTimers",
            "IconIecTimerTon",
            "IconIecTimerTof",
            "IconIecTimerTp",
            "IconIecTimerCycle",
            "IconIecCounters",
            "IconIecCounterCtu",
            "IconIecCounterCtd",
            "IconIecAnalog",
            "IconIecAnalogScale",
            "IconIecCommunication",
            "IconIecMqtt",
            "IconIecMqttPublish",
            "IconIecMqttSubscribe",
            "IconIecModbus"
        };

        foreach (var key in requiredIecKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", xamlContent);
        }

        // Required system workbench keys
        string[] requiredSysKeys =
        {
            "IconSysRun",
            "IconSysStop",
            "IconSysCompile",
            "IconSysFlashDownload",
            "IconSysConnect",
            "IconSysDisconnect",
            "IconSysLiveWatch",
            "IconSysTagTable",
            "IconSysLogicEditor",
            "IconSysBlueprints",
            "IconSysSearch",
            "IconSysClear",
            "IconSysStar",
            "IconSysLock",
            "IconSysUnlock",
            "IconSysPlus",
            "IconSysPencil",
            "IconSysTrash",
            "IconSysCopy",
            "IconSysSave",
            "IconSysFolderOpen",
            "IconSysSettings",
            "IconSysAlertTriangle",
            "IconSysCheck"
        };

        foreach (var key in requiredSysKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", xamlContent);
        }
    }

    [Fact]
    public void LogicEditorToolbox_ContainsNoRawEmojis()
    {
        string viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimplePLC.Studio", "Views", "LogicEditorView.xaml");
        if (!File.Exists(viewPath))
        {
            viewPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SimplePLC.Studio", "Views", "LogicEditorView.xaml"));
        }

        Assert.True(File.Exists(viewPath), $"LogicEditorView.xaml not found at: {viewPath}");
        string content = File.ReadAllText(viewPath);

        // Isolate the Toolbox section (ToolboxItemsHost)
        int hostStart = content.IndexOf("x:Name=\"ToolboxItemsHost\"", StringComparison.Ordinal);
        int hostEnd = content.IndexOf("</ScrollViewer>", hostStart, StringComparison.Ordinal);

        Assert.True(hostStart > 0 && hostEnd > hostStart);
        string toolboxChunk = content.Substring(hostStart, hostEnd - hostStart);

        var matches = EmojiPattern.Matches(toolboxChunk);
        Assert.True(matches.Count == 0, $"Found {matches.Count} emojis in Toolbox: {string.Join(", ", matches)}");
    }

    [Fact]
    public void LiveWatchView_ContainsNoRawEmojis()
    {
        string viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimplePLC.Studio", "Views", "LiveWatchView.xaml");
        if (!File.Exists(viewPath))
        {
            viewPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SimplePLC.Studio", "Views", "LiveWatchView.xaml"));
        }

        Assert.True(File.Exists(viewPath), $"LiveWatchView.xaml not found at: {viewPath}");
        string content = File.ReadAllText(viewPath);

        var matches = EmojiPattern.Matches(content);
        Assert.True(matches.Count == 0, $"Found {matches.Count} emojis in LiveWatchView.xaml: {string.Join(", ", matches)}");
    }

    [Fact]
    public void LocalizationService_ToolboxAndTabKeys_ContainNoEmojis()
    {
        var loc = LocalizationService.Instance;

        string[] checkKeys =
        {
            "TabLogicGraph", "TabTagCatalog", "TabRuleTable", "TabBlueprints", "TabLiveWatch",
            "ToolboxIoCategory", "ToolboxLogicCategory", "ToolboxTimerCategory", "ToolboxCounterCategory",
            "ToolboxAnalogCategory", "ToolboxMqttCategory",
            "ToolboxTonTitle", "ToolboxTofTitle", "ToolboxTpTitle", "ToolboxCycleTitle",
            "ToolboxCtuTitle", "ToolboxCtdTitle", "ToolboxScaleTitle",
            "LiveWatchModeSafe", "LiveWatchModeManual", "FilterWatchlist"
        };

        string origLang = loc.CurrentLanguage;
        try
        {
            foreach (var lang in new[] { "en", "vi" })
            {
                loc.CurrentLanguage = lang;
                foreach (var key in checkKeys)
                {
                    string val = loc[key];
                    var matches = EmojiPattern.Matches(val);
                    Assert.True(matches.Count == 0, $"Key '{key}' in lang '{lang}' contains emoji '{val}'");
                }
            }
        }
        finally
        {
            loc.CurrentLanguage = origLang;
        }
    }
}
