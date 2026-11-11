using System.Linq;
using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử hợp đồng đồng bộ hóa thông minh giữa Kiểu dữ liệu và Danh sách Tag
/// (Contextual Type-Safe Tag Synchronization & Bi-directional Auto-Resolution).
/// Tuân thủ nguyên lý First Principles (Axiom 1: Hardware Ground Truth, Axiom 2: Visual Truth).
/// </summary>
public class TypeSafeTagBindingTests
{
    private readonly TagCatalogViewModel _tagCatalog;

    /// <summary>
    /// Khởi tạo TagCatalog với bộ tag mặc định cho việc kiểm thử.
    /// </summary>
    public TypeSafeTagBindingTests()
    {
        _tagCatalog = new TagCatalogViewModel();
    }

    /// <summary>
    /// Khẳng định khi chân Pin chọn kiểu BOOL, danh sách AvailableTags chỉ chứa tag dạng Digital/Boolean.
    /// </summary>
    [Fact]
    public void ModbusPin_WhenDataTypeIsBool_AvailableTagsContainsOnlyDigitalTags()
    {
        var node = new ModbusReadNodeViewModel();
        var dialogVm = new ModbusReadConfigDialogViewModel(node, _tagCatalog);
        var pin = new ModbusPinBindingViewModel { DataType = ModbusDataType.Bool };
        dialogVm.ConfigurePinTagProvider(pin);

        var available = pin.AvailableTags.ToList();

        Assert.NotEmpty(available);
        // Mọi tag trong danh sách (trừ tag None) phải là Digital
        Assert.All(available.Where(t => t.Kind != TagKind.None), t => Assert.True(t.IsDigital || t.DataType == TagDataType.Boolean));
        // Khẳng định không chứa bất kỳ thanh ghi VREG nào
        Assert.DoesNotContain(available, t => t.Kind == TagKind.VirtualRegister);
    }

    /// <summary>
    /// Khẳng định khi chân Pin chọn kiểu INT16, danh sách AvailableTags chỉ chứa tag dạng Register INT32.
    /// </summary>
    [Fact]
    public void ModbusPin_WhenDataTypeIsInt16_AvailableTagsContainsOnlyRegisterTags()
    {
        var node = new ModbusReadNodeViewModel();
        var dialogVm = new ModbusReadConfigDialogViewModel(node, _tagCatalog);
        var pin = new ModbusPinBindingViewModel { DataType = ModbusDataType.Int16 };
        dialogVm.ConfigurePinTagProvider(pin);

        var available = pin.AvailableTags.ToList();

        Assert.NotEmpty(available);
        // Không được chứa bất kỳ cờ nhớ boolean nào (VFLAG, DI, DO)
        Assert.DoesNotContain(available, t => t.Kind == TagKind.VirtualFlag || t.Kind == TagKind.DiscreteOutput || t.Kind == TagKind.DiscreteInput);
        // Phải chứa thanh ghi VREG
        Assert.Contains(available, t => t.Kind == TagKind.VirtualRegister);
    }

    /// <summary>
    /// Khẳng định khi chuyển DataType từ BOOL sang INT16, nếu TargetTag cũ là cờ boolean (VFLAG9)
    /// thì hệ thống tự động reset TargetTag về null (None) để tránh lỗi ghi lệch pha bộ nhớ.
    /// </summary>
    [Fact]
    public void ModbusPin_WhenSwitchingFromBoolToInt16_IncompatibleTargetTagAutoClears()
    {
        var vflag9 = _tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG9") ?? _tagCatalog.AllTags.First(t => t.Kind == TagKind.VirtualFlag);
        var pin = new ModbusPinBindingViewModel
        {
            DataType = ModbusDataType.Bool,
            TargetTag = vflag9
        };

        Assert.Equal(vflag9, pin.TargetTag);

        // Act: Người dùng đổi kiểu dữ liệu sang INT16
        pin.DataType = ModbusDataType.Int16;

        // Assert: TargetTag không tương thích phải tự động reset sạch về null
        Assert.Null(pin.TargetTag);
    }

    /// <summary>
    /// Khẳng định tương tác 2 chiều: Khi người dùng gán TargetTag là VFLAG1,
    /// chân Pin tự động nhảy DataType sang ModbusDataType.Bool.
    /// </summary>
    [Fact]
    public void ModbusPin_WhenSelectingBoolTag_AutoSwitchesDataTypeToBool()
    {
        var vflag1 = _tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG1") ?? _tagCatalog.AllTags.First(t => t.Kind == TagKind.VirtualFlag);
        var pin = new ModbusPinBindingViewModel
        {
            DataType = ModbusDataType.Int16
        };

        // Act: Chọn tag dạng cờ boolean
        pin.TargetTag = vflag1;

        // Assert: DataType tự động nhận diện và chuyển sang Bool
        Assert.Equal(ModbusDataType.Bool, pin.DataType);
    }

    /// <summary>
    /// Khẳng định khi ActionNode chọn hành vi TOGGLE_TAG (đảo bit), nếu trước đó đang chọn
    /// thanh ghi số nguyên (VREG0) thì TargetTag tự động xóa bỏ để tránh gán sai ngữ nghĩa.
    /// </summary>
    [Fact]
    public void ActionNode_WhenActionIsToggleTag_IncompatibleRegisterTagAutoClears()
    {
        var vreg0 = _tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG0") ?? _tagCatalog.AllTags.First(t => t.Kind == TagKind.VirtualRegister);
        var actNode = new ActionNodeViewModel(vreg0) { ActionType = ActionType.SET_TAG };
        Assert.Equal(vreg0, actNode.TargetTag);

        // Act: Chuyển sang hành vi TOGGLE_TAG (chỉ áp dụng cho boolean/coil)
        actNode.ActionType = ActionType.TOGGLE_TAG;

        // Assert: TargetTag thanh ghi không tương thích tự động reset
        Assert.Null(actNode.TargetTag);
    }

    /// <summary>
    /// Khẳng định khi ActionNode chọn hành vi INC_COUNTER (tăng số đếm), nếu trước đó đang chọn
    /// cờ boolean (DO0) thì TargetTag tự động xóa bỏ.
    /// </summary>
    [Fact]
    public void ActionNode_WhenActionIsIncCounter_IncompatibleDigitalTagAutoClears()
    {
        var do0 = _tagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0") ?? _tagCatalog.AllTags.First(t => t.Kind == TagKind.DiscreteOutput);
        var actNode = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG };
        Assert.Equal(do0, actNode.TargetTag);

        // Act: Chuyển sang hành vi INC_COUNTER (chỉ áp dụng cho register/counter)
        actNode.ActionType = ActionType.INC_COUNTER;

        // Assert: TargetTag digital không tương thích tự động reset
        Assert.Null(actNode.TargetTag);
    }
}
