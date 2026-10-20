using System.IO.Ports;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class HardwareVirtualComEdgeDebugTests : IDisposable
{
    public HardwareVirtualComEdgeDebugTests()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    [Fact]
    public async Task EdgeTriggerRules_LiveDebug_OverVirtualComOrHardware_OnlySatisfiedRuleFires()
    {
        // 1. Detect if COM7 and COM9 pair exists on the system
        var ports = SerialPort.GetPortNames();
        bool hasComPair = ports.Contains("COM7", StringComparer.OrdinalIgnoreCase) &&
                          ports.Contains("COM9", StringComparer.OrdinalIgnoreCase);

        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);

        using var cts = new CancellationTokenSource();
        SerialPort? serverPort = null;
        IUsbCdcTransport? testTransport = null;
        DeviceEndpoint endpoint;

        if (hasComPair)
        {
            // Run MCU on COM9, App on COM7
            serverPort = new SerialPort("COM9", 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            serverPort.Open();
            _ = Task.Run(() => mcuServer.RunAsync(serverPort.BaseStream, serverPort.BaseStream, cts.Token));

            endpoint = new UsbCdcEndpoint("COM7");
        }
        else
        {
            // Fallback in-memory Virtual COM transport if physical COM7/COM9 loopback is absent
            var vTransport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };
            testTransport = vTransport;
            endpoint = new UsbCdcEndpoint("COM_VIRTUAL");
        }

        try
        {
            var factory = new DeviceConnectionFactory(transportFactory: testTransport != null ? _ => testTransport : null);
            var connectResult = await factory.ConnectAsync(endpoint, slaveId: 1);
            Assert.True(connectResult.IsSuccess, $"Failed to connect: {connectResult.FailureReason}");
            var session = connectResult.Session!;

            var coordinator = new DeviceOperationCoordinator();
            var deployUseCase = new DeployRulesUseCase(session.Rules, coordinator);

            // 2. Build and Deploy the 2 exact rules from user screenshot:
            // Rule 1: DI0 Rising Edge (0 -> 1) -> DO0 = 1
            // Rule 2: DI0 Falling Edge (1 -> 0) -> DO0 = 0
            var di0Domain = session.Product.FindTagByName("DI0")!;
            var do0Domain = session.Product.FindTagByName("DO0")!;

            var ruleTable = new RuleTable();
            ruleTable.AddRule(new Rule(
                0,
                "Rule1_RisingEdge",
                new TriggerModel(di0Domain, TriggerKind.OnRise),
                new ActionModel(do0Domain, ActionKind.SetTag, 1)));

            ruleTable.AddRule(new Rule(
                1,
                "Rule2_FallingEdge",
                new TriggerModel(di0Domain, TriggerKind.OnFall),
                new ActionModel(do0Domain, ActionKind.SetTag, 0)));

            var deployResult = await deployUseCase.ExecuteAsync(ruleTable);
            Assert.True(deployResult.IsSuccess, $"Deploy failed: {deployResult.ErrorMessage}");

            // 3. Setup Studio LogicEditorViewModel
            var catalog = new TagCatalogViewModel();
            var di0Tag = catalog.AllTags.First(t => t.Name == "DI0");
            var do0Tag = catalog.AllTags.First(t => t.Name == "DO0");

            var stateStore = new RuntimeStateStore();
            stateStore.UpdateConnection(endpoint, ConnectionStatus.Connected);

            var editor = new LogicEditorViewModel(catalog, stateStore: stateStore, uiDispatcher: a => a());
            editor.Nodes.Clear();
            editor.Connections.Clear();

            // Pipeline 1
            var in1 = new InputNodeViewModel(di0Tag) { Id = "in1" };
            var trg1 = new TriggerNodeViewModel { Id = "trg1", TriggerType = TriggerType.ON_RISE };
            var act1 = new ActionNodeViewModel(do0Tag) { Id = "act1", ActionType = ActionType.SET_TAG, ActionParam = 1 };
            editor.Nodes.Add(in1);
            editor.Nodes.Add(trg1);
            editor.Nodes.Add(act1);
            var connIn1Trg1 = new ConnectionViewModel(in1.OutputConnectors[0], trg1.InputConnectors[0]);
            var connTrg1Act1 = new ConnectionViewModel(trg1.OutputConnectors[0], act1.InputConnectors[0]);
            editor.Connections.Add(connIn1Trg1);
            editor.Connections.Add(connTrg1Act1);

            // Pipeline 2
            var in2 = new InputNodeViewModel(di0Tag) { Id = "in2" };
            var trg2 = new TriggerNodeViewModel { Id = "trg2", TriggerType = TriggerType.ON_FALL };
            var act2 = new ActionNodeViewModel(do0Tag) { Id = "act2", ActionType = ActionType.SET_TAG, ActionParam = 0 };
            editor.Nodes.Add(in2);
            editor.Nodes.Add(trg2);
            editor.Nodes.Add(act2);
            var connIn2Trg2 = new ConnectionViewModel(in2.OutputConnectors[0], trg2.InputConnectors[0]);
            var connTrg2Act2 = new ConnectionViewModel(trg2.OutputConnectors[0], act2.InputConnectors[0]);
            editor.Connections.Add(connIn2Trg2);
            editor.Connections.Add(connTrg2Act2);

            editor.MarkGraphDeployed();
            editor.IsLiveDebugActive = true;

            // 4. Initial state: DI0 = 0
            mcuSimulator.Control.SetTagValue((ushort)di0Domain.TagIndex, 0);
            di0Tag.Value = 0;
            editor.ApplyLiveOnlinePropagation();

            Assert.False(trg1.IsSimTriggered);
            Assert.False(trg2.IsSimTriggered);
            Assert.False(connTrg1Act1.IsActive);
            Assert.False(connTrg2Act2.IsActive);

            // 5. Test Step A: Transition DI0: 0 -> 1 (Rising Edge)
            mcuSimulator.Control.SetTagValue((ushort)di0Domain.TagIndex, 1);
            // Run scan cycles on MCU simulator to execute the rule
            for (int i = 0; i < 5; i++)
            {
                mcuSimulator.ExecuteScanPass(20);
            }

            // MCU executed Rule 1 -> DO0 must be 1 on MCU!
            int mcuDo0 = mcuSimulator.Control.GetTagValue((ushort)do0Domain.TagIndex);
            Assert.Equal(1, mcuDo0);

            // Live Debug evaluation in Studio
            di0Tag.Value = 1;
            do0Tag.Value = mcuDo0;
            editor.ApplyLiveOnlinePropagation();

            // CRITICAL CHECK:
            // Rule 1 (Rising Edge) SHOULD trigger!
            // Rule 2 (Falling Edge) MUST NOT trigger! (This was the reported bug!)
            Assert.True(trg1.IsSimTriggered, "Rule 1 (Rising Edge) must trigger on 0 -> 1");
            Assert.True(connTrg1Act1.IsActive, "Wire 1 must be active (green)");
            Assert.True(act1.IsSimActive, "Action 1 must be active (green)");

            Assert.False(trg2.IsSimTriggered, "Rule 2 (Falling Edge) must NOT trigger when DI0 is 1");
            Assert.False(connTrg2Act2.IsActive, "Wire 2 must NOT be active when DI0 is 1");
            Assert.False(act2.IsSimActive, "Action 2 must NOT be active when DI0 is 1");

            // Wait 300ms for pulse window to clear
            Thread.Sleep(300);
            editor.ApplyLiveOnlinePropagation();
            Assert.False(trg1.IsSimTriggered, "Rule 1 pulse should clear after pulse duration");

            // 6. Test Step B: Transition DI0: 1 -> 0 (Falling Edge)
            mcuSimulator.Control.SetTagValue((ushort)di0Domain.TagIndex, 0);
            for (int i = 0; i < 5; i++)
            {
                mcuSimulator.ExecuteScanPass(20);
            }

            // MCU executed Rule 2 -> DO0 must be 0 on MCU!
            mcuDo0 = mcuSimulator.Control.GetTagValue((ushort)do0Domain.TagIndex);
            Assert.Equal(0, mcuDo0);

            // Live Debug evaluation in Studio
            di0Tag.Value = 0;
            do0Tag.Value = mcuDo0;
            editor.ApplyLiveOnlinePropagation();

            // CRITICAL CHECK:
            // Rule 1 (Rising Edge) MUST NOT trigger!
            // Rule 2 (Falling Edge) SHOULD trigger!
            Assert.False(trg1.IsSimTriggered, "Rule 1 must NOT trigger on falling edge");
            Assert.False(connTrg1Act1.IsActive, "Wire 1 must NOT be active on falling edge");

            Assert.True(trg2.IsSimTriggered, "Rule 2 (Falling Edge) must trigger on 1 -> 0");
            Assert.True(connTrg2Act2.IsActive, "Wire 2 must be active (green) on Falling Edge");
            Assert.True(act2.IsSimActive, "Action 2 must be active (green) on Falling Edge");

            await session.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
            serverPort?.Close();
            serverPort?.Dispose();
        }
    }
}
