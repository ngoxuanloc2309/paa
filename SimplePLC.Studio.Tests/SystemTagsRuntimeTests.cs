using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử hành vi Runtime Engine cho 8 System Tags chuẩn quốc tế
/// đảm bảo tính xác định (determinism) và chính xác theo chu kỳ quét (scan cycles).
/// </summary>
public sealed class SystemTagsRuntimeTests
{
    private readonly TagCatalogViewModel _catalog = new();

    /// <summary>
    /// [Happy Path]: TagCatalog tự động nạp đủ 8 System Tags chuẩn quốc tế thuộc nhóm "SYSTEM" và đánh dấu Read-Only.
    /// </summary>
    [Fact]
    public void TagCatalog_InitializesAll8SystemTags_AsReadOnlyInSystemGroup()
    {
        // Act
        var sysTags = _catalog.AllTags.Where(t => t.Kind == TagKind.System).ToList();

        // Assert
        Assert.Equal(8, sysTags.Count);
        Assert.All(sysTags, t => Assert.Equal("SYSTEM", t.Group));
        Assert.All(sysTags, t => Assert.True(t.IsReadOnly));

        var firstScan = sysTags.FirstOrDefault(t => t.Index == ModbusRegisterMap.SysIndexFirstScan);
        Assert.NotNull(firstScan);
        Assert.Equal("SYS_FIRST_SCAN", firstScan.Name);
        Assert.True(firstScan.IsDigital);

        var rtcHour = sysTags.FirstOrDefault(t => t.Index == ModbusRegisterMap.SysIndexRtcHour);
        Assert.NotNull(rtcHour);
        Assert.Equal("SYS_RTC_HOUR", rtcHour.Name);
        Assert.False(rtcHour.IsDigital);
        Assert.Equal(TagDataType.Int32, rtcHour.DataType);
    }

    /// <summary>
    /// [Happy Path]: SYS_FIRST_SCAN chỉ tích cực bằng 1 ở chu kỳ quét #1, và tự động trở về 0 ở chu kỳ quét #2 trở đi.
    /// </summary>
    [Fact]
    public void RuntimeEngine_FirstScanTag_ActiveOnlyOnFirstScan()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags);
        var rules = Array.Empty<Rule>();
        var firstScanTag = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexFirstScan);

        // Act & Assert - Scan 1
        var snap1 = engine.Scan(rules, tickMs: 0);
        Assert.Equal(1, snap1.ScanNumber);
        Assert.Equal(1, snap1.Values[ModbusRegisterMap.SysIndexFirstScan]);
        Assert.Equal(1, firstScanTag.Value);

        // Act & Assert - Scan 2
        var snap2 = engine.Scan(rules, tickMs: 10);
        Assert.Equal(2, snap2.ScanNumber);
        Assert.Equal(0, snap2.Values[ModbusRegisterMap.SysIndexFirstScan]);
        Assert.Equal(0, firstScanTag.Value);

        // Act & Assert - Scan 3
        var snap3 = engine.Scan(rules, tickMs: 20);
        Assert.Equal(3, snap3.ScanNumber);
        Assert.Equal(0, snap3.Values[ModbusRegisterMap.SysIndexFirstScan]);
        Assert.Equal(0, firstScanTag.Value);
    }

    /// <summary>
    /// [Happy Path]: SYS_ON luôn giữ giá trị 1 và SYS_OFF luôn giữ giá trị 0 xuyên suốt các chu kỳ quét.
    /// </summary>
    [Fact]
    public void RuntimeEngine_AlwaysOnAndAlwaysOff_MaintainConstantValues()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags);
        var rules = Array.Empty<Rule>();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var snap = engine.Scan(rules, tickMs: i * 50);
            Assert.Equal(1, snap.Values[ModbusRegisterMap.SysIndexAlwaysOn]);
            Assert.Equal(0, snap.Values[ModbusRegisterMap.SysIndexAlwaysOff]);
        }
    }

    /// <summary>
    /// [Timing & Boundary]: SYS_CLOCK_1S đổi trạng thái mỗi 500ms (1.0 Hz, 500ms ON / 500ms OFF).
    /// </summary>
    [Fact]
    public void RuntimeEngine_Clock1S_TogglesEvery500Ms()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags);
        var rules = Array.Empty<Rule>();

        // Act & Assert
        // 0ms -> ON
        var snap0 = engine.Scan(rules, tickMs: 0);
        Assert.Equal(1, snap0.Values[ModbusRegisterMap.SysIndexClock1S]);

        // 490ms -> still ON
        var snap490 = engine.Scan(rules, tickMs: 490);
        Assert.Equal(1, snap490.Values[ModbusRegisterMap.SysIndexClock1S]);

        // 500ms -> OFF
        var snap500 = engine.Scan(rules, tickMs: 500);
        Assert.Equal(0, snap500.Values[ModbusRegisterMap.SysIndexClock1S]);

        // 990ms -> still OFF
        var snap990 = engine.Scan(rules, tickMs: 990);
        Assert.Equal(0, snap990.Values[ModbusRegisterMap.SysIndexClock1S]);

        // 1000ms -> ON again
        var snap1000 = engine.Scan(rules, tickMs: 1000);
        Assert.Equal(1, snap1000.Values[ModbusRegisterMap.SysIndexClock1S]);
    }

    /// <summary>
    /// [Timing & Boundary]: SYS_CLOCK_100MS đổi trạng thái mỗi 50ms (10.0 Hz, 50ms ON / 50ms OFF).
    /// </summary>
    [Fact]
    public void RuntimeEngine_Clock100Ms_TogglesEvery50Ms()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags);
        var rules = Array.Empty<Rule>();

        // Act & Assert
        var snap0 = engine.Scan(rules, tickMs: 0);
        Assert.Equal(1, snap0.Values[ModbusRegisterMap.SysIndexClock100Ms]);

        var snap40 = engine.Scan(rules, tickMs: 40);
        Assert.Equal(1, snap40.Values[ModbusRegisterMap.SysIndexClock100Ms]);

        var snap50 = engine.Scan(rules, tickMs: 50);
        Assert.Equal(0, snap50.Values[ModbusRegisterMap.SysIndexClock100Ms]);

        var snap100 = engine.Scan(rules, tickMs: 100);
        Assert.Equal(1, snap100.Values[ModbusRegisterMap.SysIndexClock100Ms]);
    }

    /// <summary>
    /// [Happy Path]: SYS_RTC_HOUR và SYS_RTC_MINUTE phản ánh chính xác đồng hồ hệ thống được tiêm (TimeProvider).
    /// </summary>
    [Fact]
    public void RuntimeEngine_RtcHourAndMinute_ReflectTimeProvider()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags)
        {
            TimeProvider = () => new DateTime(2026, 11, 3, 14, 45, 30) // 14:45
        };
        var rules = Array.Empty<Rule>();

        // Act
        var snap = engine.Scan(rules, tickMs: 100);

        // Assert
        Assert.Equal(14, snap.Values[ModbusRegisterMap.SysIndexRtcHour]);
        Assert.Equal(45, snap.Values[ModbusRegisterMap.SysIndexRtcMinute]);
    }

    /// <summary>
    /// [Fault & Lifecycle]: Khi Reset() được gọi, scanCount trở về 0 và chu kỳ quét tiếp theo sẽ kích hoạt lại SYS_FIRST_SCAN = 1.
    /// </summary>
    [Fact]
    public void RuntimeEngine_Reset_RestoresFirstScanPulse()
    {
        // Arrange
        var engine = new RuntimeEngine(_catalog.AllTags);
        var rules = Array.Empty<Rule>();

        // Chạy qua 2 chu kỳ scan
        engine.Scan(rules, tickMs: 0);
        var snap2 = engine.Scan(rules, tickMs: 10);
        Assert.Equal(0, snap2.Values[ModbusRegisterMap.SysIndexFirstScan]);

        // Act - Reset
        engine.Reset();

        // Chu kỳ scan kế tiếp sau reset là chu kỳ #1 mới
        var snapAfterReset = engine.Scan(rules, tickMs: 20);

        // Assert
        Assert.Equal(1, snapAfterReset.ScanNumber);
        Assert.Equal(1, snapAfterReset.Values[ModbusRegisterMap.SysIndexFirstScan]);
    }

    /// <summary>
    /// [UI / Presentation]: TagCatalog sắp xếp User Tags (DI, DO...) lên đầu và 8 System Tags ở cuối danh sách.
    /// </summary>
    [Fact]
    public void TagCatalog_PlacesSystemTagsAtTheEnd_AfterUserTags()
    {
        // Tag 0 is NONE
        Assert.Equal("NONE", _catalog.AllTags[0].Name);
        Assert.Equal(TagKind.None, _catalog.AllTags[0].Kind);

        // Tag 1 should be DI0
        Assert.Equal("DI0", _catalog.AllTags[1].Name);
        Assert.Equal(TagKind.DiscreteInput, _catalog.AllTags[1].Kind);

        // The last 8 tags should be System Tags (1000..1007)
        var last8 = _catalog.AllTags.TakeLast(8).ToList();
        Assert.All(last8, t => Assert.Equal(TagKind.System, t.Kind));
        Assert.Equal("SYS_FIRST_SCAN", last8[0].Name);
        Assert.Equal(1000, last8[0].Index);
        Assert.Equal("SYS_COMM_OK", last8[7].Name);
        Assert.Equal(1007, last8[7].Index);
    }

    /// <summary>
    /// [Modbus & Presentation]: ModbusAddressText và DataTypeText hiển thị chính xác theo chuẩn Holding Register (42081..42088) và INT16 cho RTC.
    /// </summary>
    [Fact]
    public void TagModel_SystemTags_DisplayCorrectModbusAddress_AndInt16ForRtc()
    {
        var firstScan = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexFirstScan);
        Assert.Equal("42081 (0x0820)", firstScan.ModbusAddressText);
        Assert.Equal("BOOL", firstScan.DataTypeText);
        Assert.False(firstScan.IsAliasEditable);

        var rtcHour = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexRtcHour);
        Assert.Equal("42086 (0x0825)", rtcHour.ModbusAddressText);
        Assert.Equal("INT16", rtcHour.DataTypeText);
        Assert.False(rtcHour.IsAliasEditable);

        var rtcMinute = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexRtcMinute);
        Assert.Equal("42087 (0x0826)", rtcMinute.ModbusAddressText);
        Assert.Equal("INT16", rtcMinute.DataTypeText);
        Assert.False(rtcMinute.IsAliasEditable);

        var commOk = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexCommOk);
        Assert.Equal("42088 (0x0827)", commOk.ModbusAddressText);
        Assert.Equal("BOOL", commOk.DataTypeText);
        Assert.False(commOk.IsAliasEditable);
    }

    /// <summary>
    /// [Localization]: Chuyển đổi ngôn ngữ cập nhật tức thời Alias của System Tags thuần Việt hoặc thuần Anh, không pha trộn song ngữ.
    /// </summary>
    [Fact]
    public void TagCatalog_SystemTagAliases_UpdateOnLanguageChange_WithoutMixedStrings()
    {
        // 1. Vietnamese
        LocalizationService.Instance.CurrentLanguage = "vi";
        var firstScanVi = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexFirstScan);
        var sysOnVi = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexAlwaysOn);
        Assert.Equal("Xung quét đầu tiên", firstScanVi.Alias);
        Assert.Equal("Xung quét đầu tiên (SYS_FIRST_SCAN)", firstScanVi.DisplayName);
        Assert.Equal("Luôn Bật (TRUE)", sysOnVi.Alias);
        Assert.Equal("Luôn Bật (TRUE) (SYS_ON)", sysOnVi.DisplayName);
        Assert.DoesNotContain("First Scan", firstScanVi.DisplayName);

        // 2. English
        LocalizationService.Instance.CurrentLanguage = "en";
        var firstScanEn = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexFirstScan);
        var sysOnEn = _catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexAlwaysOn);
        Assert.Equal("First Scan Pulse", firstScanEn.Alias);
        Assert.Equal("First Scan Pulse (SYS_FIRST_SCAN)", firstScanEn.DisplayName);
        Assert.Equal("Always ON (TRUE)", sysOnEn.Alias);
        Assert.Equal("Always ON (TRUE) (SYS_ON)", sysOnEn.DisplayName);
        Assert.DoesNotContain("Xung quét", firstScanEn.DisplayName);

        // Reset to EN
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    /// <summary>
    /// [Cold Boot / Power On]: Khi kết nối trực tiếp SYS_ON tới ngõ ra (ví dụ DO0 = 1),
    /// tại vòng quét đầu tiên (Scan #1), SYS_ON chuyển mức từ 0 (chưa cấp nguồn) sang 1 (sườn lên bật nguồn)
    /// và kích hoạt bật đèn ngõ ra DO0 = 1.
    /// </summary>
    [Fact]
    public void RuntimeEngine_SysOnDirectConnection_TriggersActionOnFirstScan()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var engine = new RuntimeEngine(catalog.AllTags);
        var sysOnTag = catalog.AllTags.First(t => t.Index == ModbusRegisterMap.SysIndexAlwaysOn);
        var do0Tag = catalog.AllTags.First(t => t.Name == "DO0");
        do0Tag.Value = 0;

        // Rule: Trigger = SYS_ON OnRise, Action = DO0 Set Value 1
        var rule = new RuleItemModel
        {
            Index = 0,
            Narrative = "SYS_ON to DO0",
            TriggerTag = sysOnTag,
            TriggerType = TriggerType.ON_RISE,
            ActionTag = do0Tag,
            ActionType = ActionType.SET_TAG,
            ActionParam = 1,
            Enabled = true
        };

        // Act - Scan #1 (Power-On edge)
        var snap1 = engine.Scan(new[] { rule }, tickMs: 0);

        // Assert
        Assert.Equal(1, snap1.ScanNumber);
        Assert.Equal(1, do0Tag.Value); // DO0 phải bật sáng 1!

        // Act - Scan #2
        var snap2 = engine.Scan(new[] { rule }, tickMs: 10);
        Assert.Equal(2, snap2.ScanNumber);
        Assert.Equal(1, do0Tag.Value); // DO0 tiếp tục duy trì sáng 1!
    }
}
