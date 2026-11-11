using System.Linq;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho TcpClientNodeViewModel theo học thuyết IEC 61508-3 và AGENTS.md.
/// </summary>
public class TcpClientNodeViewModelTests
{
    [Fact]
    public void Constructor_DefaultValues_InitializesCorrectly()
    {
        var node = new TcpClientNodeViewModel();

        Assert.Equal("TCP_CLIENT", node.Title);
        Assert.Equal("192.168.1.100", node.RemoteHost);
        Assert.Equal((ushort)8888, node.RemotePort);
        Assert.Equal((ushort)5, node.ReconnectDelaySec);
        Assert.Equal((ushort)30, node.HeartbeatIntervalSec);
        Assert.Equal("None", node.RegistrationType);
        Assert.Equal("PING", node.HeartbeatPayload);
        Assert.Equal("CRLF", node.FramingDelimiter);
        Assert.Equal("{val}", node.CustomTemplate);
        Assert.Equal("Raw", node.PayloadMode);
        Assert.True(node.IsRawMode);
        Assert.False(node.IsCustomMode);

        // Connectors verification
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Contains(node.InputConnectors, c => c.Title == "Send" && c.IsInput && c.IsEvent);
        Assert.Contains(node.InputConnectors, c => c.Title == "DataIn" && c.IsInput);

        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Conn" && !c.IsInput);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Done" && !c.IsInput && c.IsEvent);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Err" && !c.IsInput && c.IsEvent);
        Assert.Contains(node.OutputConnectors, c => c.Title == "DataOut" && !c.IsInput);
    }

    [Fact]
    public void RemoteHost_ExceedingLimit_AutoTruncatesTo48Chars()
    {
        var node = new TcpClientNodeViewModel();
        string longHost = "extremely-long-hostname-for-remote-industrial-cloud-server-exceeding-limit.company.com";

        node.RemoteHost = longHost;

        Assert.True(node.RemoteHost.Length <= 48);
        Assert.Equal(longHost[..48], node.RemoteHost);
    }

    [Fact]
    public void RemoteHost_Empty_ReportsValidationError()
    {
        var node = new TcpClientNodeViewModel();

        node.RemoteHost = "";
        Assert.True(node.HasHostError);
        Assert.NotEmpty(node.HostValidationError);

        node.RemoteHost = "10.0.0.5";
        Assert.False(node.HasHostError);
        Assert.Empty(node.HostValidationError);
    }

    [Fact]
    public void RemotePort_Zero_AutoClampsToOne()
    {
        var node = new TcpClientNodeViewModel();

        node.RemotePort = 0;
        Assert.Equal((ushort)1, node.RemotePort);
    }

    [Fact]
    public void PayloadMode_ToggleRawAndCustom_UpdatesProperties()
    {
        var node = new TcpClientNodeViewModel();

        node.PayloadMode = "Custom";
        Assert.False(node.IsRawMode);
        Assert.True(node.IsCustomMode);

        node.PayloadMode = "Raw";
        Assert.True(node.IsRawMode);
        Assert.False(node.IsCustomMode);
    }

    [Fact]
    public void StatusPillText_ReflectsSocketState()
    {
        var node = new TcpClientNodeViewModel();

        Assert.True(node.StatusPillText.ToUpperInvariant().Contains("DISCONNECTED") || node.StatusPillText.ToUpperInvariant().Contains("MẤT KẾT NỐI"));

        node.IsConnectedActive = true;
        Assert.True(node.StatusPillText.ToUpperInvariant().Contains("CONN") || node.StatusPillText.ToUpperInvariant().Contains("KẾT NỐI"));

        node.SendCount = 5;
        Assert.Contains("TX:5", node.StatusPillText.ToUpperInvariant());

        node.IsErrorActive = true;
        node.LastErrorMessage = "RESET";
        Assert.True(node.StatusPillText.ToUpperInvariant().Contains("ERR") || node.StatusPillText.ToUpperInvariant().Contains("LỖI"));
    }
}
