using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Models;
using System;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel đại diện cho một trường dữ liệu trong bảng Visual Field Builder của khối HTTP Client.
/// Cho phép ghép cặp Tên thuộc tính JSON (Key) với một Tag dữ liệu trong PLC (TagModel).
/// </summary>
public partial class HttpPayloadFieldViewModel : ObservableObject
{
    private readonly Action? _onChangedCallback;

    /// <summary>
    /// Định danh duy nhất của trường dữ liệu.
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Tên thuộc tính (JSON key name) được đóng gói vào payload gửi đi.
    /// </summary>
    [ObservableProperty]
    private string _key = string.Empty;

    /// <summary>
    /// Tag dữ liệu PLC nguồn cung cấp giá trị cho thuộc tính này.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    private TagModel? _tag;

    /// <summary>
    /// Đơn vị đo lường vật lý (tùy chọn).
    /// </summary>
    [ObservableProperty]
    private string _unit = string.Empty;

    /// <summary>
    /// Chuỗi hiển thị kiểu dữ liệu (BOOL, INT32, REAL...) của Tag đã chọn.
    /// </summary>
    public string DataTypeText => Tag?.DataTypeText ?? "—";

    /// <summary>
    /// Khởi tạo một đối tượng trường dữ liệu HTTP Payload.
    /// </summary>
    /// <param name="key">Tên trường JSON.</param>
    /// <param name="tag">Tag nguồn dữ liệu.</param>
    /// <param name="unit">Đơn vị đo lường.</param>
    /// <param name="onChanged">Callback kích hoạt khi thông số thay đổi.</param>
    public HttpPayloadFieldViewModel(string key = "", TagModel? tag = null, string unit = "", Action? onChanged = null)
    {
        _key = key;
        _tag = tag;
        _unit = unit;
        _onChangedCallback = onChanged;
    }

    partial void OnKeyChanged(string value) => _onChangedCallback?.Invoke();
    partial void OnTagChanged(TagModel? value) => _onChangedCallback?.Invoke();
    partial void OnUnitChanged(string value) => _onChangedCallback?.Invoke();
}
