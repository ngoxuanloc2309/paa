using System.Linq;
using System.Threading.Tasks;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử tự động hóa tính năng Đọc Toàn Diện Thiết Bị (Full Device Image Upload & FBD Canvas Reconstruction):
/// 1. Nạp dự án đa khối IIoT (MODBUS_READ + MQTT_PUB + Status Action DO6/DO7 + Data Wires).
/// 2. Deploy xuống MCU Flash.
/// 3. Xóa sạch Canvas và Bảng Rule trên Studio.
/// 4. Thực hiện UploadFromDeviceAsync().
/// 5. Xác minh: Toàn bộ khối đồ họa FBD, Rule Table, cấu hình chi tiết và dây nối được phục hồi 100%.
/// </summary>
[Collection("AppServicesDeployTests")]
public class FullDeviceUploadRoundtripTests
{
    private async Task<(MainViewModel MainVM, FakeModbusClient Client)> CreateConnectedStudioFixtureAsync()
    {
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM_STUDIO_VIRTUAL", 115200);
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel(act => act());
        mainVM.LogicEditorVM.DebounceDelayMs = 0;
        mainVM.MessageBoxHandler = (msg, cap, btn, img) => { };
        mainVM.ConfirmBoxHandler = (msg, cap, btn, img) => System.Windows.MessageBoxResult.Yes;
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        mainVM.NewProject();
        if (AppServices.Instance.LifecycleManager.CurrentState != ConnectionLifecycleState.Disconnected)
        {
            await AppServices.Instance.LifecycleManager.DisconnectAsync();
        }
        await mainVM.ToggleConnect();
        Assert.True(mainVM.IsConnected, "MainViewModel must be connected to virtual simulator.");

        return (mainVM, fakeClient);
    }

    [Fact]
    public async Task Test_FullDeviceUpload_Reconstructs_Modbus_Mqtt_And_Rules_Completely()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            // 1. Áp dụng Blueprint SELEC MFM383A-C (RS-485 Modbus Read + MQTT Telemetry + DO6/DO7)
            var bp = mainVM.BlueprintsVM.Blueprints.First(b => b.Id == "mfm383a_energy_mqtt");
            mainVM.BlueprintsVM.ApplyBlueprint(bp);

            // 2. Biên dịch sơ đồ FBD thành 2 status macro rules và DTO cấu hình
            mainVM.LogicEditorVM.CompileNow();
            Assert.True(mainVM.LogicEditorVM.CompileState == CompileState.Valid);
            Assert.Equal(2, mainVM.RuleTableVM.Rules.Count);

            // 3. Deploy xuống MCU Simulator
            await mainVM.DeployVM.StartDeployAsync();
            Assert.True(mainVM.DeployVM.IsSuccess, $"Deploy failed: {mainVM.DeployVM.StatusMessage}");
            Assert.True(client.Simulator.Control.Flash.HasCommittedData);
            Assert.Equal(2, client.Simulator.Control.Flash.StoredRuleCount);

            // 4. Xóa trắng toàn bộ Canvas và Rule Table trên Studio (mô phỏng kỹ sư mở Studio trắng kết nối vào PLC)
            mainVM.RuleTableVM.Rules.Clear();
            mainVM.LogicEditorVM.Nodes.Clear();
            mainVM.LogicEditorVM.Connections.Clear();
            Assert.Empty(mainVM.RuleTableVM.Rules);
            Assert.Empty(mainVM.LogicEditorVM.Nodes);
            Assert.Empty(mainVM.LogicEditorVM.Connections);

            // 5. Gọi UploadFromDeviceAsync()
            await mainVM.UploadFromDeviceAsync();

            // Đảm bảo chuyển hướng trực quan sang màn hình Logic Canvas (FBD Graph) để kỹ sư lập trình
            Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);

            // 6. Kiểm tra Bảng Rule đã phục hồi đủ 2 rules
            Assert.Equal(2, mainVM.RuleTableVM.Rules.Count);

            // 7. Kiểm tra Canvas FBD đã tự động dựng lại 8 khối chuẩn kiến trúc PLC:
            // - Khối MODBUS_READ
            // - 2 Khối Action đèn báo (DO6 và DO7)
            // - 4 Khối Action thanh ghi nội tại PLC (VREG0, VREG1, VREG2, VREG_RETAIN0) kết nối từ chân Modbus
            // - Khối MQTT_PUB
            Assert.Equal(8, mainVM.LogicEditorVM.Nodes.Count);

            var mbRead = mainVM.LogicEditorVM.Nodes.OfType<ModbusReadNodeViewModel>().Single();
            Assert.Equal(1, mbRead.SlaveId);
            Assert.Equal(ModbusMasterFunctionCode.ReadInputRegisters, mbRead.FunctionCode);
            Assert.Equal((ushort)0, mbRead.StartAddress);
            Assert.Equal((ushort)60, mbRead.RegCount);
            Assert.Equal(4, mbRead.Pins.Count);
            Assert.Equal("V_avg", mbRead.Pins[0].Label);
            Assert.Equal("VREG0", mbRead.Pins[0].TargetTag?.Name);
            Assert.Equal("V_avg", mbRead.Pins[0].DisplayTitle);
            Assert.Equal("Total_kWh", mbRead.Pins[3].Label);
            Assert.Equal("VREG_RETAIN0", mbRead.Pins[3].TargetTag?.Name);
            Assert.Equal("Total_kWh", mbRead.Pins[3].DisplayTitle);

            var mqttPub = mainVM.LogicEditorVM.Nodes.OfType<MqttPublishNodeViewModel>().Single();
            Assert.Equal("factory/energy/meter1", mqttPub.Topic);
            Assert.Equal(1, mqttPub.Qos);
            Assert.Equal(4, mqttPub.Fields.Count);
            Assert.Equal("v_avg", mqttPub.Fields[0].Key);
            Assert.Equal("VREG0", mqttPub.Fields[0].Tag?.Name);
            Assert.Equal("VREG_RETAIN0", mqttPub.Fields[3].Tag?.Name);

            var actNodes = mainVM.LogicEditorVM.Nodes.OfType<ActionNodeViewModel>().ToList();
            Assert.Equal(6, actNodes.Count);
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "DO6");
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "DO7");
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "VREG0");
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "VREG1");
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "VREG2");
            Assert.Contains(actNodes, a => a.TargetTag?.Name == "VREG_RETAIN0");

            var inpNodes = mainVM.LogicEditorVM.Nodes.OfType<InputNodeViewModel>().ToList();
            Assert.Empty(inpNodes);

            // 8. Kiểm tra toàn bộ 6 kết nối dây (Connections) đã được tái dựng hoàn chỉnh:
            // 2 dây Valid/Error sang 2 Action đèn báo + 4 dây nối trực tiếp từ chân Modbus Read sang 4 Action VREG
            Assert.Equal(6, mainVM.LogicEditorVM.Connections.Count);

            // 9. Biên dịch lại sơ đồ vừa tái tạo: Đảm bảo tính nhất quán (Roundtrip Fidelity)
            mainVM.LogicEditorVM.CompileNow();
            Assert.True(mainVM.LogicEditorVM.CompileState == CompileState.Valid);
            Assert.Equal(2, mainVM.RuleTableVM.Rules.Count);

            // 10. Kiểm tra đồng bộ Phân hệ Thiết bị (Device Layer / ProjectMetadata) và Bảng Sub-Tabs
            Assert.NotEmpty(mainVM.CurrentProjectMetadata.ModbusPorts);
            Assert.Single(mainVM.RuleTableVM.ModbusCommands);
            Assert.Equal("MB1", mainVM.RuleTableVM.ModbusCommands[0].Id);
            Assert.Equal((byte)1, mainVM.RuleTableVM.ModbusCommands[0].SlaveId);

            Assert.Single(mainVM.RuleTableVM.MqttServices);
            Assert.Equal("PUB1", mainVM.RuleTableVM.MqttServices[0].Id);
            Assert.Equal("factory/energy/meter1", mainVM.RuleTableVM.MqttServices[0].Topic);

            // Kiểm tra chuyển đổi Sub-Tabs mượt mà
            mainVM.RuleTableVM.SelectSubTab(1);
            Assert.Equal(1, mainVM.RuleTableVM.SelectedSubTabIndex);
            mainVM.RuleTableVM.SelectSubTab(2);
            Assert.Equal(2, mainVM.RuleTableVM.SelectedSubTabIndex);
            mainVM.RuleTableVM.SelectSubTab(0);
            Assert.Equal(0, mainVM.RuleTableVM.SelectedSubTabIndex);

            // Kiểm tra song ngữ Anh - Việt trên nhãn Sub-Tabs
            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Contains("Modbus Master (1)", mainVM.RuleTableVM.SubTabModbusLabel);
            Assert.Contains("MQTT (1)", mainVM.RuleTableVM.SubTabMqttLabel);
            Assert.Contains("Logic (2)", mainVM.RuleTableVM.SubTabLogicLabel);

            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.Contains("Modbus Master (1)", mainVM.RuleTableVM.SubTabModbusLabel);
            Assert.Contains("MQTT Services (1)", mainVM.RuleTableVM.SubTabMqttLabel);
            Assert.Contains("Logic Rules (2)", mainVM.RuleTableVM.SubTabLogicLabel);
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }

    [Fact]
    public async Task Test_UploadFromDevice_WhenDirty_PromptsUser_AndRespectsCancellation()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            // Dự án có thay đổi chưa lưu
            mainVM.IsProjectDirty = true;
            bool confirmPrompted = false;

            // Người dùng chọn "No" (Từ chối ghi đè)
            mainVM.ConfirmBoxHandler = (msg, cap, btn, img) =>
            {
                confirmPrompted = true;
                Assert.Equal(System.Windows.MessageBoxButton.YesNo, btn);
                return System.Windows.MessageBoxResult.No;
            };

            await mainVM.UploadFromDeviceAsync();

            Assert.True(confirmPrompted, "Confirmation dialog must be prompted when project is dirty.");
            Assert.Empty(mainVM.RuleTableVM.Rules); // Không bị ghi đè

            // Bây giờ người dùng chọn "Yes" (Đồng ý ghi đè)
            mainVM.ConfirmBoxHandler = (msg, cap, btn, img) => System.Windows.MessageBoxResult.Yes;
            await mainVM.UploadFromDeviceAsync();

            Assert.False(mainVM.IsProjectDirty, "IsProjectDirty must be reset to false after successful upload.");
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }

    [Fact]
    public async Task Test_FullDeviceUpload_Synchronizes_Wifi_And_Zigbee_Subsystems()
    {
        var (mainVM, client) = await CreateConnectedStudioFixtureAsync();

        try
        {
            // Seed Wi-Fi registers in simulator: 0x1300..0x133F
            var mem = client.Simulator.Memory;

            // Wi-Fi at 0x1300: Seed using official Protocol Codec
            var wifiDto = new WifiConfigDto
            {
                Enabled = true,
                AutoReconnect = true,
                Ssid = "Factory_IoT",
                IsDhcp = true
            };
            var wifiRegs = new ushort[ModbusRegisterMap.WifiConfigRegisters];
            SimplePLC.Protocol.Codec.WifiCodec.EncodeConfig(wifiDto, wifiRegs);
            for (int i = 0; i < wifiRegs.Length; i++)
            {
                mem.WriteSingleRegister((ushort)(0x1300 + i), wifiRegs[i]);
            }

            // Zigbee at 0x1700: Seed using official Protocol Codec
            var zbDto = new ZigbeeConfigDto
            {
                Enabled = true,
                Role = ZigbeeRole.Master,
                SlaveId = 5,
                PanId = 0xABCD,
                Channel = 20,
                Antenna = ZigbeeAntenna.External,
                SecurityEnabled = true
            };
            var zbRegs = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];
            SimplePLC.Protocol.Codec.ZigbeeConfigCodec.Encode(zbDto, zbRegs);
            for (int i = 0; i < zbRegs.Length; i++)
            {
                mem.WriteSingleRegister((ushort)(0x1700 + i), zbRegs[i]);
            }

            // Execute Upload
            await mainVM.UploadFromDeviceAsync();

            // Verify Wi-Fi metadata synchronized
            Assert.True(mainVM.CurrentProjectMetadata.WifiEnabled);
            Assert.True(mainVM.CurrentProjectMetadata.WifiAutoReconnect);
            Assert.Equal("Factory_IoT", mainVM.CurrentProjectMetadata.WifiSsid);

            // Verify Zigbee metadata synchronized
            Assert.True(mainVM.CurrentProjectMetadata.ZigbeeConfig.Enabled);
            Assert.Equal("Master", mainVM.CurrentProjectMetadata.ZigbeeConfig.Role);
            Assert.Equal((byte)5, mainVM.CurrentProjectMetadata.ZigbeeConfig.SlaveId);
            Assert.Equal("ABCD", mainVM.CurrentProjectMetadata.ZigbeeConfig.PanIdHex);
            Assert.Equal((ushort)20, mainVM.CurrentProjectMetadata.ZigbeeConfig.Channel);
            Assert.Equal("External", mainVM.CurrentProjectMetadata.ZigbeeConfig.Antenna);
            Assert.True(mainVM.CurrentProjectMetadata.ZigbeeConfig.SecurityEnabled);

            // Verify DTU Gateway Studio ViewModel reflects changes
            Assert.Equal("Factory_IoT", mainVM.DtuGatewayStudioVM.WifiVM.Ssid);
            Assert.Equal("ABCD", mainVM.DtuGatewayStudioVM.ZigbeeVM.PanIdHex);
            Assert.Equal((ushort)20, mainVM.DtuGatewayStudioVM.ZigbeeVM.Channel);
        }
        finally
        {
            if (mainVM.IsConnected)
                await mainVM.ToggleConnect();
        }
    }
}
