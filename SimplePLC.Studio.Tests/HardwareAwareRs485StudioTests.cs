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
}
