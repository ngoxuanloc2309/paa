using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Capability dành cho các Graph Node có liên kết với một PLC Tag thời gian thực.
/// Tách biệt hoàn toàn runtime state khỏi base GraphNodeViewModel.
/// Cho phép LogicEditorViewModel cập nhật trạng thái runtime theo cơ chế diffing thông minh.
/// </summary>
public interface ILiveTagBoundNode
{
    /// <summary>
    /// Chỉ số TagIndex được liên kết (theo ProductDefinition / TagCatalog).
    /// </summary>
    ushort? BoundTagIndex { get; }

    /// <summary>
    /// Giá trị thô nhận được từ MCU Modbus RTU.
    /// </summary>
    int LiveRawValue { get; }

    /// <summary>
    /// Chất lượng dữ liệu runtime (Good, Stale, Unknown).
    /// </summary>
    TagQuality LiveQuality { get; }

    /// <summary>
    /// Trạng thái kết nối trực tuyến tới MCU.
    /// </summary>
    bool IsLiveOnline { get; }

    /// <summary>
    /// Trạng thái kích hoạt (chỉ áp dụng cho tín hiệu Boolean/Digital: True khi RawValue != 0).
    /// </summary>
    bool IsLiveActive { get; }

    /// <summary>
    /// Chuỗi văn bản hiển thị giá trị thời gian thực.
    /// </summary>
    string LiveValueText { get; }

    /// <summary>
    /// Trạng thái hiển thị trực quan cho Converters (Active, Inactive, Stale, Offline).
    /// </summary>
    NodeLiveVisualState LiveVisualState { get; }

    /// <summary>
    /// Đã từng nhận được ít nhất 1 snapshot từ thiết bị (dùng cho Last Known).
    /// </summary>
    bool HasReceivedUpdate { get; }

    /// <summary>
    /// Áp dụng snapshot cập nhật mới từ Runtime State Store.
    /// </summary>
    void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline);

    /// <summary>
    /// Đặt trạng thái ngắt kết nối (bảo toàn Last Known RawValue).
    /// </summary>
    void SetOffline();
}
