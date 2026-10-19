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

        // Giữ nguyên chuỗi payload người dùng đã nhập, chỉ tạo mẫu mặc định nếu ô nhập hoàn toàn trống
        if (string.IsNullOrWhiteSpace(SimulationInput))
        {
            SimulationInput = GenerateSamplePayload();
        }

        // Tự động thử nghiệm lại trên payload hiện tại nếu đã từng chạy test
        if (HasRunSimulation && !string.IsNullOrWhiteSpace(SimulationInput))
        {
            RunSimulation();
        }
    }

    [RelayCommand]
    public void RemovePin(MqttPinBindingViewModel? pin)
    {
        if (pin != null && Node.Pins.Contains(pin))
        {
            Node.Pins.Remove(pin);

            // Giữ nguyên chuỗi payload hiện tại và tự động thử nghiệm lại nếu đang ở trạng thái xem kết quả
            if (HasRunSimulation && !string.IsNullOrWhiteSpace(SimulationInput))
            {
                RunSimulation();
            }
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
            string field = !string.IsNullOrEmpty(result.FaultyField) ? $"'{result.FaultyField}'" : string.Empty;
            SimulationStatusMessage = result.ErrorCode switch
            {
                MqttErrorCode.JsonSyntaxError => isVi
                    ? "❌ Bản tin không đúng định dạng JSON (vui lòng kiểm tra dấu ngoặc {}, dấu phẩy hoặc dấu nháy kép)."
                    : "❌ Invalid JSON format (please check curly brackets {}, commas, or quotes).",
                MqttErrorCode.MissingField => isVi
                    ? (!string.IsNullOrEmpty(field)
                        ? $"❌ Bản tin thiếu trường dữ liệu: Không tìm thấy trường {field} trong gói tin."
                        : "❌ Bản tin thiếu trường dữ liệu bắt buộc.")
                    : (!string.IsNullOrEmpty(field)
                        ? $"❌ Missing required data field: Field {field} was not found in the payload."
                        : "❌ Missing required data field in payload."),
                MqttErrorCode.OutOfRange => isVi
                    ? (!string.IsNullOrEmpty(field)
                        ? $"❌ Giá trị vượt giới hạn cho phép tại trường {field}."
                        : "❌ Giá trị vượt giới hạn cho phép.")
                    : (!string.IsNullOrEmpty(field)
                        ? $"❌ Value out of range for field {field}."
                        : "❌ Value out of range."),
                MqttErrorCode.TypeMismatch => isVi
                    ? (!string.IsNullOrEmpty(field)
                        ? $"❌ Sai kiểu dữ liệu tại trường {field}."
                        : "❌ Sai kiểu dữ liệu.")
                    : (!string.IsNullOrEmpty(field)
                        ? $"❌ Data type mismatch for field {field}."
                        : "❌ Data type mismatch."),
                _ => isVi ? $"❌ Lỗi: {result.ErrorMessage}" : $"❌ Error: {result.ErrorMessage}"
            };
        }
    }
}
