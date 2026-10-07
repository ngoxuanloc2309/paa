using SimplePLC.Protocol.Dto;

namespace SimplePLC.Application.Models;

public enum ConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Incompatible = 3,
    Faulted = 4
}

public sealed record DeviceConnectionInfo
{
    public ConnectionStatus Status { get; init; } = ConnectionStatus.Disconnected;
    public DeviceEndpoint? Endpoint { get; init; }
    public string? PortName { get; init; }
    public CompatibilityStatus? Compatibility { get; init; }
    public DeviceDescriptorDto? Descriptor { get; init; }
    public DeviceHealthDto? Health { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static DeviceConnectionInfo DisconnectedState() =>
        new() { Status = ConnectionStatus.Disconnected };

    public static DeviceConnectionInfo ConnectingState(DeviceEndpoint endpoint) =>
        new()
        {
            Status = ConnectionStatus.Connecting,
            Endpoint = endpoint,
            PortName = endpoint is UsbCdcEndpoint u ? u.PortName : endpoint.ToString()
        };

    public static DeviceConnectionInfo ConnectedState(DeviceEndpoint endpoint, DeviceDescriptorDto descriptor, DeviceHealthDto health) =>
        new()
        {
            Status = ConnectionStatus.Connected,
            Endpoint = endpoint,
            PortName = endpoint is UsbCdcEndpoint u ? u.PortName : endpoint.ToString(),
            Compatibility = CompatibilityStatus.Compatible,
            Descriptor = descriptor,
            Health = health
        };

    public static DeviceConnectionInfo IncompatibleState(DeviceEndpoint endpoint, CompatibilityStatus status, string reason) =>
        new()
        {
            Status = ConnectionStatus.Incompatible,
            Endpoint = endpoint,
            PortName = endpoint is UsbCdcEndpoint u ? u.PortName : endpoint.ToString(),
            Compatibility = status,
            ErrorMessage = reason
        };

    public static DeviceConnectionInfo FaultedState(DeviceEndpoint endpoint, string error) =>
        new()
        {
            Status = ConnectionStatus.Faulted,
            Endpoint = endpoint,
            PortName = endpoint is UsbCdcEndpoint u ? u.PortName : endpoint.ToString(),
            ErrorMessage = error
        };

    // Overloads cho string port name (tiện lợi cho backward compatibility)
    public static DeviceConnectionInfo ConnectedState(string port, DeviceDescriptorDto descriptor, DeviceHealthDto health) =>
        ConnectedState(new UsbCdcEndpoint(port), descriptor, health);

    public static DeviceConnectionInfo FaultedState(string? port, string error) =>
        new()
        {
            Status = ConnectionStatus.Faulted,
            Endpoint = port != null ? new UsbCdcEndpoint(port) : null,
            PortName = port,
            ErrorMessage = error
        };
}
