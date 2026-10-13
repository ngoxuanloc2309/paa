using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Models;
using System;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho một trường dữ liệu trong bảng Visual Field Builder của khối MQTT.
/// Cho phép người dùng gán Key (tên trường JSON), chọn Tag dữ liệu và Đơn vị.
/// </summary>
public partial class MqttPayloadFieldViewModel : ObservableObject
{
    private readonly Action? _onChangedCallback;

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private TagModel? _tag;

    [ObservableProperty]
    private string _unit = string.Empty;

    public MqttPayloadFieldViewModel(string key = "", TagModel? tag = null, string unit = "", Action? onChanged = null)
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
