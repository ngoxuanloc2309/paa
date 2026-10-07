namespace SimplePLC.Application.Models;

/// <summary>
/// Điểm cuối (Endpoint) kết nối vật lý hoặc logic tới vi điều khiển PLC.
/// </summary>
public abstract record DeviceEndpoint
{
    public abstract string DisplayName { get; }
}

/// <summary>
/// Kết nối trực tiếp qua cổng Native USB CDC (TinyUSB) trên vi điều khiển STM32.
/// </summary>
public sealed record UsbCdcEndpoint(string PortName) : DeviceEndpoint
{
    public override string DisplayName => $"USB CDC ({PortName})";

    public override string ToString() => PortName;
}

/// <summary>
/// Kết nối tới thiết bị giả lập trong bộ nhớ (In-memory Simulator).
/// </summary>
public sealed record SimulatorEndpoint : DeviceEndpoint
{
    public override string DisplayName => "SIMULATOR (VIRTUAL)";

    public override string ToString() => "SIMULATOR (VIRTUAL)";
}
