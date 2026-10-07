using System.IO.Ports;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Cài đặt thực tế truy vấn các cổng COM qua System.IO.Ports.SerialPort trên Windows.
/// </summary>
public sealed class SystemComPortDiscoveryService : IComPortDiscoveryService
{
    public IReadOnlyList<string> GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames()
                .Distinct()
                .OrderBy(p => p)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
