using SimplePLC.Application.Models;
using SimplePLC.Infrastructure.Discovery;

namespace SimplePLC.Infrastructure.Tests;

public sealed class SerialPortDeviceDetectorTests
{
    [Fact]
    public async Task FindCandidatesAsync_DeduplicatesAndSortsPorts()
    {
        var detector = new SerialPortDeviceDetector(() => new[] { "COM4", "COM1", "com4", "COM3" });

        var candidates = await detector.FindCandidatesAsync();

        Assert.Equal(3, candidates.Count);
        Assert.Equal(new UsbCdcEndpoint("COM1"), candidates[0]);
        Assert.Equal(new UsbCdcEndpoint("COM3"), candidates[1]);
        Assert.Equal(new UsbCdcEndpoint("COM4"), candidates[2]);
    }

    [Fact]
    public async Task FindCandidatesAsync_FiltersEmptyAndWhitespacePorts()
    {
        var detector = new SerialPortDeviceDetector(() => new[] { "COM3", "", "   ", null!, "COM5" });

        var candidates = await detector.FindCandidatesAsync();

        Assert.Equal(2, candidates.Count);
        Assert.Equal(new UsbCdcEndpoint("COM3"), candidates[0]);
        Assert.Equal(new UsbCdcEndpoint("COM5"), candidates[1]);
    }

    [Fact]
    public async Task FindCandidatesAsync_ProviderThrows_ReturnsEmptyListSafely()
    {
        var detector = new SerialPortDeviceDetector(() => throw new InvalidOperationException("OS serial subsystem failure"));

        var candidates = await detector.FindCandidatesAsync();

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var detector = new SerialPortDeviceDetector(() => new[] { "COM3" });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => detector.FindCandidatesAsync(cts.Token));
    }

    [Fact]
    public async Task FindCandidatesAsync_DefaultConstructor_DoesNotThrow()
    {
        var detector = new SerialPortDeviceDetector();

        var candidates = await detector.FindCandidatesAsync();

        Assert.NotNull(candidates);
    }
}
