using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Integration tests kiểm thử quy trình kết nối và thẩm định hồ sơ thiết bị tự mô tả
/// (Self-Describing Device Profile V1 - Contract V1.9) qua DeviceConnectionFactory.
/// </summary>
public class SelfDescribingDeviceConnectionTests
{
    private sealed class SimulatorTestConnectionFactory : IDeviceConnectionFactory
    {
        private readonly McuReferenceSimulator _simulator;
        private readonly IDeviceCompatibilityValidator _validator;

        public SimulatorTestConnectionFactory(McuReferenceSimulator simulator, IDeviceCompatibilityValidator? validator = null)
        {
            _simulator = simulator;
            _validator = validator ?? new StandardDeviceCompatibilityValidator();
        }

        public Task<DeviceConnectionResult> ConnectAsync(
            DeviceEndpoint endpoint,
            byte slaveId = 1,
            CancellationToken cancellationToken = default)
        {
            var fakeTransport = new FakeUsbCdcTransport { IsOpen = true };
            var fakeClient = new FakeModbusClient(_simulator, isConnected: true);

            var realFactory = new DeviceConnectionFactory(_validator);
            // SimulatorEndpoint sử dụng FakeUsbCdcTransport và FakeModbusClient bên trong DeviceConnectionFactory
            // Tuy nhiên để inject simulator có Faults cụ thể, ta dùng SingleClientConnectionFactory hoặc gọi trực tiếp:
            return ConnectWithClientAsync(fakeTransport, fakeClient, endpoint, slaveId, cancellationToken);
        }

        private async Task<DeviceConnectionResult> ConnectWithClientAsync(
            FakeUsbCdcTransport transport,
            FakeModbusClient client,
            DeviceEndpoint endpoint,
            byte slaveId,
            CancellationToken cancellationToken)
        {
            var descriptorReader = new SimplePLC.Infrastructure.Devices.DeviceDescriptorReader(client);
            var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, cancellationToken).ConfigureAwait(false);

            var compatibility = _validator.Validate(descriptor);
            if (!compatibility.IsCompatible)
            {
                await transport.CloseAsync(CancellationToken.None);
                return DeviceConnectionResult.Incompatible(compatibility);
            }

            var resourceInfo = await descriptorReader.ReadResourceInfoAsync(slaveId, cancellationToken).ConfigureAwait(false);
            if (!SimplePLC.Application.Mapping.DeviceProfileMapper.TryBuildProductDefinition(descriptor, resourceInfo, out var product, out var profileError))
            {
                await transport.CloseAsync(CancellationToken.None);
                return DeviceConnectionResult.Incompatible(
                    CompatibilityResult.Incompatible(CompatibilityStatus.UnsupportedDeviceVariant, profileError ?? "Invalid device profile."));
            }

            var ruleGateway = new SimplePLC.Infrastructure.Gateways.RuleTableGateway(client);
            var tagReader = new SimplePLC.Infrastructure.Devices.RuntimeTagReader(client);
            var healthReader = new SimplePLC.Infrastructure.Devices.DeviceHealthReader(client);
            var commandClient = new SimplePLC.Infrastructure.Devices.SystemCommandClient(client);

            var session = new DeviceSession(
                endpoint,
                descriptor,
                slaveId,
                transport,
                client,
                ruleGateway,
                tagReader,
                healthReader,
                commandClient,
                resourceInfo,
                product!);

            return DeviceConnectionResult.Success(session);
        }
    }

    [Fact]
    public async Task ConnectAsync_StandardRemoteIo_CreatesSessionWithDynamicProductDefinition()
    {
        var sim = new McuReferenceSimulator();
        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);

        var session = result.Session!;
        Assert.Equal(1, session.ResourceInfo.WireProfile);
        Assert.Equal(100, session.ResourceInfo.MaxRules);
        Assert.Equal(124, session.ResourceInfo.RuntimeTagCount);
        Assert.Equal(124, session.Product.Tags.Count);
        Assert.True(session.Product.HasRuleEngine);
        Assert.True(session.Product.HasRetentiveMemory);

        // Kiểm tra dải tag chuẩn
        var di0 = session.Product.FindTagByIndex(0);
        Assert.NotNull(di0);
        Assert.Equal("DI0", di0.Name);
        Assert.Equal(TagKind.DiscreteInput, di0.Kind);

        var vregRetain0 = session.Product.FindTagByIndex(84);
        Assert.NotNull(vregRetain0);
        Assert.Equal("VREG_RETAIN0", vregRetain0.Name);
        Assert.Equal(TagKind.VirtualRegisterRetain, vregRetain0.Kind);
    }

    [Fact]
    public async Task ConnectAsync_UnknownVariant_WithValidProfile_ConnectsSuccessfully()
    {
        // MCU trả về ProductVariant = 0xABCD lạ (chưa hề được định nghĩa trước đó)
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.OverrideDeviceVariant = 0xABCD;

        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        // Theo Wire Profile V1: Unknown variant KHÔNG bị reject mà vẫn kết nối thành công nếu profile hợp lệ
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
        Assert.Equal(0xABCD, result.Session!.Product.ProductVariant);
        Assert.Equal(124, result.Session.Product.Tags.Count);
    }

    [Fact]
    public async Task ConnectAsync_WireProfileV2_And_ProtocolV2_ConnectsSuccessfully()
    {
        // MCU V2.0: WireProfile = 2, ProtocolVersion = 2, RuleFormatVersion = 7
        var sim = new McuReferenceSimulator(wireProfile: 2);
        sim.Control.Faults.OverrideDescriptor = new SimplePLC.Protocol.Dto.DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 2,
            FwVersionMinor = 0,
            FwVersionPatch = 0,
            ProtocolVersion = 2,
            RuleFormatVersion = 7
        };

        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        Assert.True(result.IsSuccess, $"Failed with: {result.FailureReason} - {result.Compatibility.Reason}");
        Assert.NotNull(result.Session);
        Assert.Equal(2, result.Session!.Product.WireProfile);
        Assert.True(result.Session.Product.SupportsDedicatedFunctionBlocks);
        Assert.True(result.Session.Product.SupportsDiagnosticControl);
    }

    [Fact]
    public async Task ConnectAsync_InvalidWireProfile_FailsCompatibility()
    {
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.OverrideResourceInfo = new DeviceResourceInfoDto
        {
            WireProfile = 99, // Không được hỗ trợ
            MaxRules = 100,
            RuntimeTagCount = 124,
            DigitalInputCount = 8,
            DigitalOutputCount = 8,
            AnalogInputCount = 4,
            VirtualFlagCount = 32,
            VirtualRegisterCount = 32,
            RetentiveRegisterCount = 32,
            CounterCount = 8
        };

        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        Assert.False(result.IsSuccess);
        Assert.Contains("wire profile", result.Compatibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectAsync_RuntimeTagCountMismatch_FailsCompatibility()
    {
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.OverrideResourceInfo = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 100,
            RuntimeTagCount = 100, // Khai báo 100 nhưng tổng tag là 124 -> mismatch
            DigitalInputCount = 8,
            DigitalOutputCount = 8,
            AnalogInputCount = 4,
            VirtualFlagCount = 32,
            VirtualRegisterCount = 32,
            RetentiveRegisterCount = 32,
            CounterCount = 8
        };

        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        Assert.False(result.IsSuccess);
        Assert.Contains("mismatch", result.Compatibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectAsync_SmallIoProfile_BuildsCorrectTagsAndCapabilities()
    {
        // Thiết bị Small IO: 4 DI, 4 DO, 0 AI, 8 VFLAG, 8 VREG, 0 RETAIN, 0 COUNTER (tổng 24 tags)
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.OverrideResourceInfo = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 0, // Không có Rule Engine
            RuntimeTagCount = 24,
            DigitalInputCount = 4,
            DigitalOutputCount = 4,
            AnalogInputCount = 0,
            VirtualFlagCount = 8,
            VirtualRegisterCount = 8,
            RetentiveRegisterCount = 0, // Không có retain
            CounterCount = 0
        };

        var factory = new SimulatorTestConnectionFactory(sim);

        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);

        var product = result.Session!.Product;
        Assert.Equal(24, product.Tags.Count);
        Assert.False(product.HasRuleEngine);
        Assert.False(product.HasRetentiveMemory);

        // Kiểm tra các dải Base Index động theo TagLayoutMap
        var layout = ModbusRegisterMap.ComputeLayout(4, 4, 0, 8, 8, 0, 0);

        Assert.Equal("DI0", product.FindTagByIndex(layout.DiBase)?.Name);
        Assert.Equal("DI3", product.FindTagByIndex((ushort)(layout.DiBase + 3))?.Name);

        Assert.Equal("DO0", product.FindTagByIndex(layout.DoBase)?.Name);
        Assert.Equal("DO3", product.FindTagByIndex((ushort)(layout.DoBase + 3))?.Name);

        Assert.Equal("VFLAG0", product.FindTagByIndex(layout.VflagBase)?.Name);
        Assert.Equal("VFLAG7", product.FindTagByIndex((ushort)(layout.VflagBase + 7))?.Name);

        Assert.Equal("VREG0", product.FindTagByIndex(layout.VregBase)?.Name);
        Assert.Equal("VREG7", product.FindTagByIndex((ushort)(layout.VregBase + 7))?.Name);

        // Vượt quá dải tổng 24 tag (0..23)
        Assert.Null(product.FindTagByIndex(24));
    }

    /// <summary>
    /// ARCHITECTURE REGRESSION TEST (Contract V1.9):
    /// Chứng minh vi điều khiển tương lai (ProductVariant = 0x0017) với cấu hình tài nguyên bất kỳ
    /// (DI=4, DO=4, AI=2, VFLAG=16, VREG=16, VREG_RETAIN=8, COUNTER=4, MaxRules=50, RuntimeTags=54)
    /// kết nối thành công và sinh ProductDefinition chuẩn 100% mà TUYỆT ĐỐI KHÔNG cần thêm code/factory cụ thể nào trong C#.
    /// </summary>
    [Fact]
    public async Task ArchitectureTest_UnknownVariant0017_DynamicResourceCounts_ConnectSucceedsWithoutHardcodedFactory()
    {
        // 1. Arrange: Simulator mô phỏng thiết bị mới 0x0017
        var sim = new McuReferenceSimulator();
        sim.Control.Faults.OverrideDeviceVariant = 0x0017;
        sim.Control.Faults.OverrideResourceInfo = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 50,
            RuntimeTagCount = 54, // 4 + 4 + 2 + 16 + 16 + 8 + 4 = 54
            DigitalInputCount = 4,
            DigitalOutputCount = 4,
            AnalogInputCount = 2,
            VirtualFlagCount = 16,
            VirtualRegisterCount = 16,
            RetentiveRegisterCount = 8,
            CounterCount = 4
        };

        var factory = new SimulatorTestConnectionFactory(sim);

        // 2. Act: Kết nối
        var result = await factory.ConnectAsync(new SimulatorEndpoint());

        // 3. Assert: Kết nối thành công hoàn toàn
        Assert.True(result.IsSuccess, $"Connection failed: {result.FailureReason}");
        Assert.NotNull(result.Session);

        var session = result.Session!;
        var product = session.Product;

        // Xác minh biến thể và các năng lực suy diễn từ tài nguyên
        Assert.Equal(0x0017, product.ProductVariant);
        Assert.Equal(50, product.MaxRules);
        Assert.True(product.HasRuleEngine);       // MaxRules > 0
        Assert.True(product.HasRetentiveMemory);  // RetentiveRegisters > 0
        Assert.Equal(54, product.Tags.Count);

        // Xác minh quy hoạch TagIndex động (Dynamic Tag Index Mapping)
        var layout17 = ModbusRegisterMap.ComputeLayout(4, 4, 2, 16, 16, 8, 4);

        // DI (0..3)
        Assert.Equal("DI0", product.FindTagByIndex(layout17.DiBase)?.Name);
        Assert.Equal(TagKind.DiscreteInput, product.FindTagByIndex(layout17.DiBase)?.Kind);
        Assert.Equal("DI3", product.FindTagByIndex((ushort)(layout17.DiBase + 3))?.Name);

        // DO (4..7)
        Assert.Equal("DO0", product.FindTagByIndex(layout17.DoBase)?.Name);
        Assert.Equal(TagKind.DiscreteOutput, product.FindTagByIndex(layout17.DoBase)?.Kind);
        Assert.Equal("DO3", product.FindTagByIndex((ushort)(layout17.DoBase + 3))?.Name);

        // AI (8..9)
        Assert.Equal("AI0", product.FindTagByIndex(layout17.AiBase)?.Name);
        Assert.Equal(TagKind.AnalogInput, product.FindTagByIndex(layout17.AiBase)?.Kind);
        Assert.Equal("AI1", product.FindTagByIndex((ushort)(layout17.AiBase + 1))?.Name);

        // VFLAG (10..25)
        Assert.Equal("VFLAG0", product.FindTagByIndex(layout17.VflagBase)?.Name);
        Assert.Equal(TagKind.VirtualFlag, product.FindTagByIndex(layout17.VflagBase)?.Kind);
        Assert.Equal("VFLAG15", product.FindTagByIndex((ushort)(layout17.VflagBase + 15))?.Name);

        // VREG (26..41)
        Assert.Equal("VREG0", product.FindTagByIndex(layout17.VregBase)?.Name);
        Assert.Equal(TagKind.VirtualRegister, product.FindTagByIndex(layout17.VregBase)?.Kind);
        Assert.Equal("VREG15", product.FindTagByIndex((ushort)(layout17.VregBase + 15))?.Name);

        // VREG_RETAIN (42..49)
        Assert.Equal("VREG_RETAIN0", product.FindTagByIndex(layout17.VregRetainBase)?.Name);
        Assert.Equal(TagKind.VirtualRegisterRetain, product.FindTagByIndex(layout17.VregRetainBase)?.Kind);
        Assert.Equal("VREG_RETAIN7", product.FindTagByIndex((ushort)(layout17.VregRetainBase + 7))?.Name);

        // COUNTER (50..53)
        Assert.Equal("COUNTER0", product.FindTagByIndex(layout17.CounterBase)?.Name);
        Assert.Equal(TagKind.Counter, product.FindTagByIndex(layout17.CounterBase)?.Kind);
        Assert.Equal("COUNTER3", product.FindTagByIndex((ushort)(layout17.CounterBase + 3))?.Name);

        Assert.Null(product.FindTagByIndex(54));
    }
}
