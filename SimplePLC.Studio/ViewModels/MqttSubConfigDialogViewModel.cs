using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.ViewModels;

public record OptionItem<T>(T Value, string DisplayName);

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình khối Nhận dữ liệu MQTT (Được thiết kế tối giản, thân thiện cho kỹ sư vận hành).
/// </summary>
public partial class MqttSubConfigDialogViewModel : ObservableObject
{
    public MqttSubscribeNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }

    [ObservableProperty]
    private string _simulationInput = string.Empty;

    [ObservableProperty]
    private string _simulationStatusMessage = LocalizationService.Tr("MqttSubTestWaitingText");

    [ObservableProperty]
    private bool _isSimulationSuccess = true;

    [ObservableProperty]
    private bool _hasRunSimulation = false;

    public IReadOnlyList<OptionItem<MqttDataType>> AvailableDataTypes { get; } = new List<OptionItem<MqttDataType>>
    {
        new(MqttDataType.Int16, LocalizationService.Tr("MqttDataTypeInt16")),
        new(MqttDataType.Float, LocalizationService.Tr("MqttDataTypeFloat")),
        new(MqttDataType.Bool, LocalizationService.Tr("MqttDataTypeBool")),
        new(MqttDataType.String, LocalizationService.Tr("MqttDataTypeString")),
        new(MqttDataType.Int32, LocalizationService.Tr("MqttDataTypeInt32")),
    };

    public IReadOnlyList<OptionItem<MqttQueueMode>> AvailableQueueModes { get; } = new List<OptionItem<MqttQueueMode>>
    {
        new(MqttQueueMode.Fifo, LocalizationService.Tr("MqttSubQueueFifo")),
        new(MqttQueueMode.LatestWins, LocalizationService.Tr("MqttSubQueueLatest"))
    };

    public IReadOnlyList<OptionItem<MqttFailSafeAction>> AvailableFailSafeActions { get; } = new List<OptionItem<MqttFailSafeAction>>
    {
        new(MqttFailSafeAction.KeepLastValue, LocalizationService.Tr("MqttSubFailSafeKeep")),
        new(MqttFailSafeAction.ResetToDefault, LocalizationService.Tr("MqttSubFailSafeReset"))
    };

    public MqttSubConfigDialogViewModel(MqttSubscribeNodeViewModel node, TagCatalogViewModel tagCatalog)
    {
        Node = node;
        TagCatalog = tagCatalog;
        _simulationInput = GenerateSamplePayload();
    }

    public string GenerateSamplePayload()
    {
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var valuesDict = new Dictionary<string, object>();
        foreach (var pin in Node.Pins)
        {
            string key = string.IsNullOrWhiteSpace(pin.JsonPath) ? pin.Label : pin.JsonPath;
            valuesDict[key] = pin.DataType switch
            {
                MqttDataType.Bool => true,
                MqttDataType.String => "ON",
                MqttDataType.Float => 25.5,
                _ => key.Equals("grid", StringComparison.OrdinalIgnoreCase) ? 5 : (key.Equals("color", StringComparison.OrdinalIgnoreCase) ? 1 : 10)
            };
        }

        var envelope = new Dictionary<string, object>
        {
            ["device"] = "SynaptiX_Controller",
            ["ts"] = nowMillis,
            ["values"] = valuesDict
        };

        return System.Text.Json.JsonSerializer.Serialize(envelope, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    [RelayCommand]
    public void ResetSamplePayload()
    {
        SimulationInput = GenerateSamplePayload();
        RunSimulation();
    }

    [RelayCommand]
    public void AddPin()
    {
        int count = Node.Pins.Count;
        string defaultName = count switch
        {
            0 => "grid",
            1 => "color",
            _ => $"field_{count + 1}"
        };

        var newPin = new MqttPinBindingViewModel(defaultName, defaultName, MqttDataType.Int16);
        Node.Pins.Add(newPin);

        // Tự động đồng bộ thêm trường vào payload mẫu
        SimulationInput = GenerateSamplePayload();
        RunSimulation();
    }

    [RelayCommand]
    public void RemovePin(MqttPinBindingViewModel? pin)
    {
        if (pin != null && Node.Pins.Contains(pin))
        {
            Node.Pins.Remove(pin);
            // Tự động đồng bộ payload mẫu
            SimulationInput = GenerateSamplePayload();
            RunSimulation();
        }
    }

    [RelayCommand]
    public void RunSimulation()
    {
        if (string.IsNullOrWhiteSpace(SimulationInput)) return;

        HasRunSimulation = true;
        var result = Node.ProcessInboundPayload(SimulationInput);

        bool isVi = LocalizationService.Instance.IsVietnamese;
        if (result.IsSuccess)
        {
            IsSimulationSuccess = true;
            SimulationStatusMessage = isVi
                ? $"✔ Bản tin hợp lệ! Đã trích xuất thành công {Node.Pins.Count} trường dữ liệu."
                : $"✔ Valid payload! Successfully extracted {Node.Pins.Count} fields.";
        }
        else
        {
            IsSimulationSuccess = false;
            SimulationStatusMessage = result.ErrorCode switch
            {
                MqttErrorCode.JsonSyntaxError => isVi
                    ? "❌ Bản tin không đúng định dạng JSON (vui lòng kiểm tra dấu ngoặc {}, dấu phẩy hoặc dấu nháy kép)."
                    : "❌ Invalid JSON format (please check curly brackets {}, commas, or quotes).",
                MqttErrorCode.MissingField => isVi
                    ? $"❌ Bản tin thiếu trường dữ liệu: {result.ErrorMessage}"
                    : $"❌ Missing required data field: {result.ErrorMessage}",
                MqttErrorCode.OutOfRange => isVi
                    ? $"❌ Giá trị vượt giới hạn cho phép: {result.ErrorMessage}"
                    : $"❌ Value out of range: {result.ErrorMessage}",
                MqttErrorCode.TypeMismatch => isVi
                    ? $"❌ Sai kiểu dữ liệu: {result.ErrorMessage}"
                    : $"❌ Data type mismatch: {result.ErrorMessage}",
                _ => $"❌ Lỗi: {result.ErrorMessage}"
            };
        }
    }
}
