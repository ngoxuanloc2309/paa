using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Biểu diễn một lệnh đọc/ghi Modbus RTU Master (Poller, Writer, DirectWrite) trong Bảng tổng hợp Modbus Master.
/// </summary>
public partial class ModbusCommandItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _blockType = string.Empty;

    [ObservableProperty]
    private string _portDisplay = string.Empty;

    [ObservableProperty]
    private byte _slaveId;

    [ObservableProperty]
    private string _functionCodeDisplay = string.Empty;

    [ObservableProperty]
    private string _addressDisplay = string.Empty;

    [ObservableProperty]
    private string _lengthDisplay = string.Empty;

    [ObservableProperty]
    private string _modeDisplay = string.Empty;

    [ObservableProperty]
    private string _pinsOrTargetDisplay = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private bool _isActive = true;
}

/// <summary>
/// Biểu diễn một dịch vụ MQTT IIoT (Telemetry Publish hoặc Inbound Subscription) trong Bảng tổng hợp MQTT Services.
/// </summary>
public partial class MqttServiceItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _serviceType = string.Empty;

    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private byte _qos;

    [ObservableProperty]
    private string _modeDisplay = string.Empty;

    [ObservableProperty]
    private string _payloadSummary = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private bool _isActive = true;
}

/// <summary>
/// Biểu diễn một khối hàm chuẩn IEC (Timer TON/TOF/TP hoặc Counter CTU/CTD) trong Bảng tổng hợp Function Blocks.
/// </summary>
public partial class FunctionBlockItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _blockType = string.Empty;

    [ObservableProperty]
    private string _presetDisplay = string.Empty;

    [ObservableProperty]
    private string _inputSignal = string.Empty;

    [ObservableProperty]
    private string _outputSignal = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private bool _isActive = true;
}
