namespace SimplePLC.Studio.Services;

/// <summary>
/// Giao diện dịch vụ quét và phát hiện các cổng nối tiếp khả dụng trên hệ thống.
/// Giúp tách biệt lời gọi tĩnh SerialPort.GetPortNames() khỏi ViewModel (DIP / Clean Architecture).
/// </summary>
public interface IComPortDiscoveryService
{
    IReadOnlyList<string> GetAvailablePorts();
}
