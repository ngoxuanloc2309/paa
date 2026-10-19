using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Models;
using System;
using System.Collections.Generic;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho một trường dữ liệu trong bảng Visual Field Builder của khối MQTT.
/// Cho phép người dùng gán Key (tên trường JSON), chọn Tag dữ liệu, Đơn vị và Số chữ số lẻ (Decimals 0..3).
/// </summary>
public partial class MqttPayloadFieldViewModel : ObservableObject
{
    private readonly Action? _onChangedCallback;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    private TagModel? _tag;

    [ObservableProperty]
    private string _unit = string.Empty;

    public string DataTypeText => Tag?.DataTypeText ?? "-";

    public ConnectorViewModel? Connector { get; set; }

    public MqttPayloadFieldViewModel(string key = "", TagModel? tag = null, string unit = "", Action? onChanged = null)
    {
        _key = key;
        _tag = tag;
        _unit = unit;
        _onChangedCallback = onChanged;
    }

    partial void OnKeyChanged(string value)
    {
        if (Connector != null)
        {
            Connector.Title = value;
            Connector.SetBilingualDescription(
                $"Trường dữ liệu JSON '{value}' đóng gói vào bản tin MQTT",
                $"JSON payload field '{value}' to be published via MQTT");
        }
        _onChangedCallback?.Invoke();
    }

    partial void OnTagChanged(TagModel? value)
    {
        _onChangedCallback?.Invoke();
    }

    partial void OnUnitChanged(string value) => _onChangedCallback?.Invoke();
}
