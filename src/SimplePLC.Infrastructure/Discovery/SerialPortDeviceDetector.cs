using System.IO.Ports;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;

namespace SimplePLC.Infrastructure.Discovery;

/// <summary>
/// Hiện thực IDeviceDetector quét các cổng Virtual COM vật lý của hệ thống (Discovery only).
/// Tuyệt đối không mở port, không đọc register hay handshake.
/// Cho phép inject portProvider để phục vụ Unit Test mà không phụ thuộc OS.
/// </summary>
public sealed class SerialPortDeviceDetector : IDeviceDetector
{
    private readonly Func<string[]> _portProvider;

    public SerialPortDeviceDetector(Func<string[]>? portProvider = null)
    {
        _portProvider = portProvider ?? SerialPort.GetPortNames;
    }

    public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var ports = _portProvider()
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(p => (DeviceEndpoint)new UsbCdcEndpoint(p.Trim()))
                .ToList();

            return Task.FromResult<IReadOnlyList<DeviceEndpoint>>(ports.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Xử lý lỗi khi OS không đọc được danh sách serial port
            return Task.FromResult<IReadOnlyList<DeviceEndpoint>>(Array.Empty<DeviceEndpoint>());
        }
    }
}
