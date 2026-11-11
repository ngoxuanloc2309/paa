using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị cho cơ chế Reactive Live Sync của Inspector và lọc Tag theo chuẩn phần cứng (IEC 61131-3 Axiom 1 &amp; Axiom 2).
/// </summary>
public sealed class ModbusChannelLiveSyncAndFilteringTests
{
    private static List<TagModel> CreateSampleTagCatalog()
    {
        return new List<TagModel>
        {
            new() { Name = "NONE", Kind = TagKind.None, Group = "NONE" },
            // Physical inputs (Read-Only)
            new() { Name = "DI0", Kind = TagKind.DiscreteInput, DataType = TagDataType.Boolean, Group = "DI" },
            new() { Name = "AI0", Kind = TagKind.AnalogInput, DataType = TagDataType.Int32, Group = "AI", Alias = "Sensor_Pressure" },
            new() { Name = "AI1", Kind = TagKind.AnalogInput, DataType = TagDataType.Float32, Group = "AI", Alias = "Sensor_Temp" },
            // Physical outputs (Writable)
            new() { Name = "DO0", Kind = TagKind.DiscreteOutput, DataType = TagDataType.Boolean, Group = "DO" },
            // Virtual memory (Writable)
            new() { Name = "VFLAG0", Kind = TagKind.VirtualFlag, DataType = TagDataType.Boolean, Group = "VFLAG" },
            new() { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Group = "VREG", Alias = "Target_Speed" },
            new() { Name = "VREG1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32, Group = "VREG", Alias = "Flow_Rate" },
            new() { Name = "VREG_RETAIN0", Kind = TagKind.VirtualRegisterRetain, DataType = TagDataType.Int32, Group = "VREG_R" },
            // System tags (Read-Only)
            new() { Name = "SYS_CLOCK_1S", Kind = TagKind.System, DataType = TagDataType.Boolean, Group = "SYSTEM" },
            new() { Name = "SYS_RTC_HOUR", Kind = TagKind.System, DataType = TagDataType.Int32, Group = "SYSTEM" }
        };
    }

    /// <summary>
    /// Kiểm thử: Khi người dùng sửa các trường trong kênh Modbus (TargetTag, Scale, Address), Inspector bên phải cập nhật tức thì.
    /// </summary>
    [Fact]
    public async Task ModbusLiveTestInspector_WhenChannelPropertiesChange_UpdatesInspectorBindingsImmediately()
    {
        var channel = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Int16,
            Scale = 1.0
        };

        await using var inspector = new ModbusLiveTestInspectorViewModel();
        inspector.TargetChannel = channel;

        // Ban đầu chưa gán Tag
        Assert.Equal("—", inspector.TargetTagDisplay);
        Assert.Equal("—", inspector.TargetTagAlias);
        Assert.Contains("40001", inspector.TargetSummary);

        // Thay đổi TargetTag trên channel
        var targetTag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Alias = "Target_Speed" };
        channel.TargetTag = targetTag;

        // Inspector phải cập nhật ngay lập tức mà không cần click lại dòng
        Assert.Contains("VREG0", inspector.TargetTagDisplay);
        Assert.Equal("Target_Speed", inspector.TargetTagAlias);
        Assert.Contains("VREG0", inspector.TargetSummary);

        // Thay đổi Scale
        channel.Scale = 2.5;
        Assert.Equal("× 2.5", inspector.TargetScaleDisplay);

        // Thay đổi DataType
        channel.DataType = ModbusDataType.Real;
        Assert.Contains("Real", inspector.TargetDataType);

        // Thay đổi Address
        channel.Address = 40010;
        Assert.Contains("40010", inspector.TargetSummary);
    }

    /// <summary>
    /// Kiểm thử: Khi đổi sang channel khác hoặc Dispose, channel cũ không còn phát sự kiện tới Inspector.
    /// </summary>
    [Fact]
    public async Task ModbusLiveTestInspector_WhenChannelSwitchedOrDisposed_UnsubscribesCleanly()
    {
        var channel1 = new ModbusMasterChannelItemViewModel { SlaveId = 1, Address = 40001 };
        var channel2 = new ModbusMasterChannelItemViewModel { SlaveId = 2, Address = 40002 };

        var inspector = new ModbusLiveTestInspectorViewModel();
        inspector.TargetChannel = channel1;
        Assert.Contains("40001", inspector.TargetSummary);

        // Chuyển sang channel2
        inspector.TargetChannel = channel2;
        Assert.Contains("40002", inspector.TargetSummary);

        // Sửa channel1 cũ: Inspector không được đổi theo channel1
        channel1.Address = 49999;
        Assert.Contains("40002", inspector.TargetSummary);
        Assert.DoesNotContain("49999", inspector.TargetSummary);

        // Dispose inspector
        await inspector.DisposeAsync();

        // Sửa channel2 sau khi Dispose: không gây ngoại lệ
        channel2.Address = 48888;
        Assert.Null(inspector.TargetChannel);
    }

    /// <summary>
    /// Kiểm thử Axiom 1 (Hardware Ground Truth):
    /// Thao tác ĐỌC (FC01..FC04) TUYỆT ĐỐI không cho phép chọn chân phần cứng chỉ đọc (AI, DI, SYSTEM) làm đích.
    /// </summary>
    [Theory]
    [InlineData(ModbusDataType.Bool)]   // FC01/FC02 Read Bit
    [InlineData(ModbusDataType.Int16)]  // FC03/FC04 Read Register
    public void FilterTagsByPhysicalDataType_ForReadOperations_ExcludesPhysicalInputsAndSystemTags(ModbusDataType dt)
    {
        var allTags = CreateSampleTagCatalog();
        var filtered = ModbusMasterChannelItemViewModel.FilterTagsByPhysicalDataType(allTags, dt, isReadOperation: true).ToList();

        // Không bao giờ chứa DI, AI hoặc SYS tags vì phần cứng ADC/GPIO sẽ ghi đè sau mỗi 10ms
        Assert.DoesNotContain(filtered, t => t.Kind == TagKind.AnalogInput);
        Assert.DoesNotContain(filtered, t => t.Kind == TagKind.DiscreteInput);
        Assert.DoesNotContain(filtered, t => t.Kind == TagKind.System);

        // Tag NONE luôn luôn có mặt
        Assert.Contains(filtered, t => t.Kind == TagKind.None || t.Name == "NONE");

        if (dt == ModbusDataType.Bool)
        {
            // Chỉ chứa biến nhớ bit ghi được: VFLAG, DO
            Assert.Contains(filtered, t => t.Name == "VFLAG0");
            Assert.Contains(filtered, t => t.Name == "DO0");
        }
        else
        {
            // Chỉ chứa biến nhớ ghi được: VREG, VREG_R
            Assert.Contains(filtered, t => t.Name == "VREG0");
            Assert.Contains(filtered, t => t.Name == "VREG_RETAIN0");
        }
    }

    /// <summary>
    /// Kiểm thử: Thao tác GHI (FC05..FC16) cho phép đọc từ chân phần cứng (AI, DI) để truyền ra Modbus slave.
    /// </summary>
    [Fact]
    public void FilterTagsByPhysicalDataType_ForWriteOperations_AllowsPhysicalInputsWhenTypeMatches()
    {
        var allTags = CreateSampleTagCatalog();

        // FC06 / FC16 (Write Register) với kiểu INT16/INT32
        var filteredInt = ModbusMasterChannelItemViewModel.FilterTagsByPhysicalDataType(allTags, ModbusDataType.Int16, isReadOperation: false).ToList();
        Assert.Contains(filteredInt, t => t.Name == "AI0"); // AI0 có kiểu Int32 -> hợp lệ làm nguồn ghi

        // FC06 / FC16 (Write Register) với kiểu REAL
        var filteredReal = ModbusMasterChannelItemViewModel.FilterTagsByPhysicalDataType(allTags, ModbusDataType.Real, isReadOperation: false).ToList();
        Assert.Contains(filteredReal, t => t.Name == "AI1"); // AI1 có kiểu Float32 -> hợp lệ làm nguồn ghi
        Assert.DoesNotContain(filteredReal, t => t.Name == "AI0"); // AI0 là Int32 -> không được lẫn vào REAL
    }

    /// <summary>
    /// Kiểm thử tính nghiêm ngặt về kiểu dữ liệu:
    /// REAL không bao giờ chứa tag digital (DI, DO, VFLAG); và trong chế độ Ghi, AI0 (Int32) không bao giờ lọt vào REAL.
    /// </summary>
    [Fact]
    public void FilterTagsByPhysicalDataType_StrictRealAndIntMatching_DoesNotLeakAcrossDataTypes()
    {
        var allTags = CreateSampleTagCatalog();

        // Lọc REAL cho lệnh Đọc (FC03)
        var realReadTags = ModbusMasterChannelItemViewModel.FilterTagsByPhysicalDataType(allTags, ModbusDataType.Real, isReadOperation: true).ToList();
        Assert.DoesNotContain(realReadTags, t => t.IsDigital);
        Assert.DoesNotContain(realReadTags, t => t.Name == "AI0");
        Assert.DoesNotContain(realReadTags, t => t.Name == "DI0");

        // Lọc REAL cho lệnh Ghi (FC06/16)
        var realWriteTags = ModbusMasterChannelItemViewModel.FilterTagsByPhysicalDataType(allTags, ModbusDataType.Real, isReadOperation: false).ToList();
        Assert.Contains(realWriteTags, t => t.Name == "AI1"); // AI1 là Float32 -> hợp lệ
        Assert.DoesNotContain(realWriteTags, t => t.Name == "AI0"); // AI0 là Int32 -> tuyệt đối không được lẫn vào REAL
        Assert.DoesNotContain(realWriteTags, t => t.IsDigital);
    }

    /// <summary>
    /// Kiểm thử Axiom 3 (Input State Machine Auto-Revert):
    /// Khi kênh đang chọn AI0 ở lệnh Ghi (FC06), người dùng đổi FunctionCode sang FC03 (Đọc),
    /// kênh phải tự động reset TargetTag về NONE để bảo vệ phần cứng.
    /// </summary>
    [Fact]
    public void ModbusMasterChannel_WhenFunctionCodeChangesToRead_CoercesInvalidReadOnlyTagToNone()
    {
        var allTags = CreateSampleTagCatalog();
        var aiTag = allTags.First(t => t.Name == "AI0");
        var noneTag = allTags.First(t => t.Name == "NONE");

        var channel = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => allTags,
            FunctionCode = 6, // Write Register
            DataType = ModbusDataType.Int16,
            TargetTag = aiTag
        };

        Assert.Equal("AI0", channel.TargetTag?.Name);

        // Chuyển sang FC03 (Read Holding Registers)
        channel.FunctionCode = 3;

        // TargetTag phải tự động hoàn nguyên về NONE
        Assert.True(channel.TargetTag == null || channel.TargetTag.Kind == TagKind.None);
        Assert.Equal(string.Empty, channel.TargetTagName);
    }
}
