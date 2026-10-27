using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa IIoT Data Transfer Unit (DTU Gateway).
/// Cho phép điều khiển luồng truyền thông cầu nối RS-485 lên Internet (MQTT, HTTP, TCP).
/// </summary>
public partial class DtuGatewayNodeViewModel : GraphNodeViewModel
{
    public ConnectorViewModel EnConnector { get; }
    public ConnectorViewModel TrigConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel BusyConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }
    public ConnectorViewModel ErrCodeConnector { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(PortDisplayName))]
    private byte _portIndex = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private string _serialMode = "Transference"; // Transference, ModbusMaster, ModbusSlave

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private string _uplinkMode = "Mqtt"; // Mqtt, Http, TcpClient, TcpServer

    [ObservableProperty]
    private string _customJsonTemplate = string.Empty;

    public ObservableCollection<DtuHttpFieldModel> HttpFields { get; } = new();

    public string PortDisplayName => $"RS-485 #{PortIndex + 1}";

    public string BadgeText => $"{SerialMode.ToUpperInvariant()} ➔ {UplinkMode.ToUpperInvariant()}";

    public override string SummaryText
    {
        get
        {
            var uplink = UplinkMode switch
            {
                "Mqtt" => "MQTT",
                "Http" => "HTTP REST",
                "TcpClient" => "TCP Client",
                "TcpServer" => "TCP Server",
                _ => UplinkMode
            };
            var serial = SerialMode switch
            {
                "Transference" => LocalizationService.Instance.IsVietnamese ? "Trong suốt" : "Transparent",
                "ModbusMaster" => "Modbus Master",
                "ModbusSlave" => "Modbus Slave",
                _ => SerialMode
            };
            return $"{PortDisplayName} · {serial} ➔ {uplink}";
        }
    }

    public DtuGatewayNodeViewModel()
    {
        Title = LocalizationService.Tr("ToolboxDtuGateway", "📡 DTU Gateway");

        EnConnector = new ConnectorViewModel
        {
            Title = "EN",
            TooltipKey = "PinTooltipDtuEn",
            IsInput = true,
            IsEvent = false,
            Node = this
        };

        TrigConnector = new ConnectorViewModel
        {
            Title = "TRIG",
            TooltipKey = "PinTooltipDtuTrig",
            IsInput = true,
            IsEvent = true,
            Node = this
        };

        DoneConnector = new ConnectorViewModel
        {
            Title = "DONE",
            TooltipKey = "PinTooltipDtuDone",
            IsInput = false,
            IsEvent = true,
            Node = this
        };

        BusyConnector = new ConnectorViewModel
        {
            Title = "BUSY",
            TooltipKey = "PinTooltipDtuBusy",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        ErrorConnector = new ConnectorViewModel
        {
            Title = "ERR",
            TooltipKey = "PinTooltipDtuErr",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        ErrCodeConnector = new ConnectorViewModel
        {
            Title = "CODE",
            TooltipKey = "PinTooltipDtuErrCode",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        InputConnectors.Add(EnConnector);
        InputConnectors.Add(TrigConnector);

        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(BusyConnector);
        OutputConnectors.Add(ErrorConnector);
        OutputConnectors.Add(ErrCodeConnector);
    }

    public Action<DtuGatewayNodeViewModel>? ConfigureHandler { get; set; }

    [RelayCommand]
    private void Configure()
    {
        ConfigureHandler?.Invoke(this);
    }
}
