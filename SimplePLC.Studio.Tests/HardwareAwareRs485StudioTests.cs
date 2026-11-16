using System.Collections.Generic;
using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử độc lập cho tầng Studio: Co giãn động giao diện và Khóa an toàn biên dịch (Compiler Gate).
/// </summary>
public sealed class HardwareAwareRs485StudioTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void ModbusPortConfigViewModel_AdaptsToPhysicalPortCount(ushort physicalPortCount)
    {
        // TC-HW-MB-007: ViewModel co giãn số lượng cổng chính xác theo PhysicalRs485PortCount
        var metadata = new ProjectMetadata
        {
            PhysicalRs485PortCount = physicalPortCount
        };

        var vm = new ModbusPortConfigViewModel(metadata);

        Assert.Equal(physicalPortCount, vm.Ports.Count);
        Assert.True(vm.HasPhysicalPorts);
        Assert.NotNull(vm.SelectedPort);
        Assert.Equal((byte)0, vm.SelectedPort.PortIndex);
    }

    [Fact]
    public void ModbusPortConfigViewModel_WithZeroPhysicalPorts_SetsEmptyState()
    {
        var metadata = new ProjectMetadata
        {
            PhysicalRs485PortCount = 0
        };

        var vm = new ModbusPortConfigViewModel(metadata);

        Assert.Empty(vm.Ports);
        Assert.False(vm.HasPhysicalPorts);
        Assert.Null(vm.SelectedPort);
    }

    [Fact]
    public void RuleCompiler_EmitsError_WhenBlockUsesPortExceedingPhysicalCount()
    {
        // TC-HW-MB-008: MCU mục tiêu chỉ có 1 cổng RS-485, nhưng khối FBD gán vào Cổng 2 (PortIndex = 1) -> Trả về lỗi Error
        var compiler = new RuleCompiler();
        var tagCatalog = new TagCatalogViewModel();

        var resources = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(resources);
        var singlePortProduct = new ProductDefinition(
            "Micro PLC 1-Port",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 100,
            resources: resources,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: 1); // CHỈ CÓ 1 CỔNG

        var mbReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 1 // Gán vào Cổng 2 -> Vi phạm giới hạn phần cứng!
        };

        var nodes = new List<GraphNodeViewModel> { mbReadNode };
        var connections = new List<ConnectionViewModel>();

        var result = compiler.Compile(nodes, connections, tagCatalog, product: singlePortProduct);

        Assert.NotEmpty(result.Diagnostics);
        var err = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error && d.NodeId == mbReadNode.Id);
        Assert.NotNull(err);
        Assert.True(err.Message.Contains("vượt quá") || err.Message.Contains("exceeding"));
    }

    [Fact]
    public void RuleCompiler_Passes_WhenBlockUsesValidPhysicalPort()
    {
        var compiler = new RuleCompiler();
        var tagCatalog = new TagCatalogViewModel();

        var resources = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(resources);
        var dualPortProduct = new ProductDefinition(
            "Standard PLC 2-Port",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 100,
            resources: resources,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: 2); // 2 CỔNG

        var mbReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 1 // Gán vào Cổng 2 -> Hợp lệ trên bo mạch 2 cổng
        };

        var nodes = new List<GraphNodeViewModel> { mbReadNode };
        var connections = new List<ConnectionViewModel>();

        var result = compiler.Compile(nodes, connections, tagCatalog, product: dualPortProduct);

        // Không có lỗi vượt quá cổng vật lý
        var portError = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("vượt quá"));
        Assert.Null(portError);
    }

    [Fact]
    public void ModbusReadConfigDialogViewModel_WhenPhysicalPortCountIs1_ExposesOnlyPort1()
    {
        // TC-HW-MB-010: Bo mạch chỉ có 1 cổng RS-485, dù metadata có 4 cổng cấu hình, dropdown chỉ cho chọn đúng Cổng 1
        var prevProduct = AppServices.Instance.CurrentProduct;
        try
        {
            var resources = ProductResourceProfile.DefaultRemoteIo;
            var tags = DeviceProfileBuilder.GenerateTags(resources);
            var singlePortProduct = new ProductDefinition(
                "Micro PLC 1-Port",
                deviceClass: 1,
                productVariant: 1,
                maxRules: 100,
                resources: resources,
                tags: tags,
                wireProfile: 2,
                hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.MODBUS_MASTER,
                physicalRs485PortCount: 1);

            AppServices.Instance.CurrentProduct = singlePortProduct;

            var mbReadNode = new ModbusReadNodeViewModel { PortIndex = 0 };
            var tagCatalog = new TagCatalogViewModel();
            var ports = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Master" },
                new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Master" } // Cổng 2 vẫn Enabled trong metadata cũ
            };

            var dialogVm = new ModbusReadConfigDialogViewModel(mbReadNode, tagCatalog, ports);

            // Chỉ được phép hiển thị đúng 1 cổng vật lý
            Assert.Single(dialogVm.AvailablePorts);
            Assert.Equal((byte)0, dialogVm.AvailablePorts[0].Value);
            Assert.Equal((byte)0, dialogVm.PortIndex);
        }
        finally
        {
            AppServices.Instance.CurrentProduct = prevProduct;
        }
    }

    [Fact]
    public void MqttBrokerConfigViewModel_WhenUseTlsIsToggledOff_ResetsUseMtlsToFalse()
    {
        // TC-HW-TLS-001: Khi người dùng đang chọn mTLS (UseMtls = true), việc bỏ chọn UseTls (UseTls = false)
        // bắt buộc phải reset UseMtls về false và khi Apply() thì MqttTlsMtlsEnabled = false
        var metadata = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttTlsEnabled = true,
            MqttTlsMtlsEnabled = true
        };

        var vm = new MqttBrokerConfigViewModel(metadata)
        {
            UseTls = true,
            UseMtls = true
        };

        Assert.True(vm.UseTls);
        Assert.True(vm.UseMtls);

        // Act: Bỏ chọn UseTls
        vm.UseTls = false;

        // Assert: UseMtls phải tự động về false
        Assert.False(vm.UseMtls);

        vm.Apply();
        Assert.False(metadata.MqttTlsEnabled);
        Assert.False(metadata.MqttTlsMtlsEnabled);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenDeviceIsPlc_DoesNotEnableDtuByDefault()
    {
        // TC-HW-DTU-002: Với thiết bị PLC (DeviceClass != DTU), hàm SaveToMetadata() không tự động bật cờ dtu.Enabled
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)SimplePLC.Domain.Enums.SPLC_DeviceClass.CONTROLLER,
            DtuConfig = new DtuConfigModel { Enabled = false }
        };

        var vm = new DtuGatewayStudioViewModel(metadata);
        vm.SaveToMetadata();

        Assert.False(metadata.DtuConfig.Enabled);
    }

    [Fact]
    public void ModbusPortConfigViewModel_RequestClose_PassesTrueOnApply_AndFalseOnCancel()
    {
        var metadata = new ProjectMetadata { PhysicalRs485PortCount = 2 };
        var vm = new ModbusPortConfigViewModel(metadata);

        bool? closeResult = null;
        vm.RequestClose += success => closeResult = success;

        vm.Apply();
        Assert.True(closeResult);

        closeResult = null;
        vm.Cancel();
        Assert.False(closeResult);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_WhenAllPortsDisabled_AndThenReenabled_UpdatesAvailablePorts()
    {
        var node = new ModbusWriteNodeViewModel { PortIndex = 0 };
        var tagCatalog = new TagCatalogViewModel();
        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false, BaudRate = 9600 },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false, BaudRate = 19200 }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, ports);
        Assert.True(vm.HasNoAvailablePorts);
        Assert.False(vm.HasAvailablePorts);

        // Giả lập sau khi người dùng mở popup cấu hình và bật 1 cổng
        ports[0].Enabled = true;
        vm.RefreshAvailablePorts(ports);

        Assert.False(vm.HasNoAvailablePorts);
        Assert.True(vm.HasAvailablePorts);
        Assert.Single(vm.AvailablePorts);
        Assert.Equal((byte)0, vm.AvailablePorts[0].Value);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_ReloadFromMetadata_RefreshesPortStateFromProjectMetadata()
    {
        // TC-HW-MB-012: Kiểm tra khi cổng RS-485 được bật từ popup cấu hình ngoài Canvas, Tab System cập nhật chuẩn xác
        var metadata = new ProjectMetadata
        {
            PhysicalRs485PortCount = 2,
            ModbusPorts = new List<ModbusPortConfigModel>
            {
                new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false },
                new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false }
            }
        };

        var dtuVm = new DtuGatewayStudioViewModel(metadata);
        Assert.False(dtuVm.ModbusPortConfigVM.Ports[0].Enabled);

        // Giả lập popup cấu hình bật cổng 1 và Apply vào metadata
        metadata.ModbusPorts[0].Enabled = true;

        // Khi người dùng chuyển sang Tab System, ReloadFromMetadata được gọi
        dtuVm.ReloadFromMetadata();

        Assert.True(dtuVm.ModbusPortConfigVM.Ports[0].Enabled);
    }
}
