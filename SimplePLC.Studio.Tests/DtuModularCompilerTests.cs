using System.Linq;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit and E2E Integration tests cho kiến trúc khối DTU Gateway độc lập (HTTP_CLIENT & TCP_CLIENT).
/// </summary>
public class DtuModularCompilerTests
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly LogicEditorViewModel _vm;
    private readonly RuleCompiler _compiler;

    public DtuModularCompilerTests()
    {
        _tagCatalog = new TagCatalogViewModel();
        _vm = new LogicEditorViewModel(_tagCatalog);
        _compiler = new RuleCompiler();
    }

    [Fact]
    public void GraphGrammar_CanConnect_AllowsModbusReadToHttpClientAndTcpClient()
    {
        bool canConnectHttp = GraphGrammarV1.CanConnect(LogicNodeKind.ModbusRead, LogicNodeKind.HttpClient, out string errHttp, out _);
        Assert.True(canConnectHttp);
        Assert.Empty(errHttp);

        bool canConnectTcp = GraphGrammarV1.CanConnect(LogicNodeKind.ModbusRead, LogicNodeKind.TcpClient, out string errTcp, out _);
        Assert.True(canConnectTcp);
        Assert.Empty(errTcp);
    }

    [Fact]
    public void GraphGrammar_CanConnect_AllowsHttpClientAndTcpClientToFeedActions()
    {
        bool canConnectHttp = GraphGrammarV1.CanConnect(LogicNodeKind.HttpClient, LogicNodeKind.Action, out string errHttp, out _);
        Assert.True(canConnectHttp);
        Assert.Empty(errHttp);

        bool canConnectTcp = GraphGrammarV1.CanConnect(LogicNodeKind.TcpClient, LogicNodeKind.Action, out string errTcp, out _);
        Assert.True(canConnectTcp);
        Assert.Empty(errTcp);
    }

    [Fact]
    public void RuleCompiler_GraphWithHttpClient_CompilesSuccessfullyAndProducesDtuHttpConfig()
    {
        // Arrange
        var mbNode = new ModbusReadNodeViewModel
        {
            PortIndex = 0,
            SlaveId = 1,
            StartAddress = 100,
            RegCount = 2
        };

        var httpNode = new HttpClientNodeViewModel
        {
            Url = "https://cloud.myserver.com/api/v1/telemetry",
            Method = "POST",
            CustomHeaders = "Authorization: Bearer 12345",
            TimeoutSec = 15,
            TriggerMode = "Periodic",
            PeriodicIntervalSec = 30,
            PayloadMode = "Custom"
        };

        _vm.Nodes.Add(mbNode);
        _vm.Nodes.Add(httpNode);

        // Act
        var result = _compiler.Compile(_vm.Nodes, _vm.Connections, _tagCatalog);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.NotNull(result.Program.DtuConfig);
        Assert.NotNull(result.Program.DtuHttpConfig);
        Assert.Equal(DtuUplinkMode.Http, result.Program.DtuConfig.UplinkMode);
        Assert.Equal("https://cloud.myserver.com/api/v1/telemetry", result.Program.DtuHttpConfig.Url);
        Assert.Equal(DtuHttpMethod.Post, result.Program.DtuHttpConfig.Method);
        Assert.Equal((ushort)15, result.Program.DtuHttpConfig.TimeoutSec);
        Assert.Equal(DtuHttpTriggerMode.Periodic, result.Program.DtuHttpConfig.TriggerMode);
        Assert.Equal((ushort)30, result.Program.DtuHttpConfig.PeriodicIntervalSec);
    }

    [Fact]
    public void RuleCompiler_GraphWithTcpClient_CompilesSuccessfullyAndProducesDtuTcpConfig()
    {
        // Arrange
        var tcpNode = new TcpClientNodeViewModel
        {
            RemoteHost = "192.168.1.250",
            RemotePort = 9000,
            ReconnectDelaySec = 10,
            HeartbeatIntervalSec = 60,
            HeartbeatPayload = "PING",
            RegistrationType = "OnConnect",
            RegistrationPayload = "DEVICE_ID_001",
            PayloadMode = "Raw"
        };

        _vm.Nodes.Add(tcpNode);

        // Act
        var result = _compiler.Compile(_vm.Nodes, _vm.Connections, _tagCatalog);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.NotNull(result.Program.DtuConfig);
        Assert.NotNull(result.Program.DtuTcpConfig);
        Assert.Equal(DtuUplinkMode.TcpClient, result.Program.DtuConfig.UplinkMode);
        Assert.Equal("192.168.1.250", result.Program.DtuTcpConfig.RemoteHost);
        Assert.Equal((ushort)9000, result.Program.DtuTcpConfig.RemotePort);
        Assert.Equal((ushort)10, result.Program.DtuTcpConfig.ReconnectDelaySec);
        Assert.Equal((ushort)60, result.Program.DtuTcpConfig.HeartbeatIntervalSec);
        Assert.Equal("PING", result.Program.DtuTcpConfig.HeartbeatPayload);
        Assert.Equal(DtuTcpRegistrationType.OnConnect, result.Program.DtuTcpConfig.RegistrationType);
        Assert.Equal("DEVICE_ID_001", result.Program.DtuTcpConfig.RegistrationPayload);
    }

    [Fact]
    public void RuleCompiler_HardwareWithoutHttpClientCapability_EmitsWarningDiagnostic()
    {
        // Arrange product without HTTP_CLIENT capability
        var restrictedProduct = new ProductDefinition(
            productName: "RestrictedRemoteIO",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 64,
            resources: ProductResourceProfile.DefaultRemoteIo,
            tags: _tagCatalog.CurrentProduct.Tags,
            wireProfile: 1,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC,
            physicalRs485PortCount: 1);

        _tagCatalog.SyncWithProductDefinition(restrictedProduct);

        var httpNode = new HttpClientNodeViewModel();
        _vm.Nodes.Add(httpNode);

        // Act
        var result = _compiler.Compile(_vm.Nodes, _vm.Connections, _tagCatalog);

        // Assert
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("HTTP Client"));
    }
}
