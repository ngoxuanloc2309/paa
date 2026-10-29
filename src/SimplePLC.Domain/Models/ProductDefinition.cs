using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Định nghĩa cấu hình phần cứng và danh mục Tag chuẩn cho các biến thể sản phẩm SimplePLC.
/// Được sinh động tại runtime bởi DeviceProfileBuilder từ metadata do MCU báo về (Contract V1.9).
/// </summary>
public sealed class ProductDefinition
{
    public string ProductName { get; }
    public ushort DeviceClass { get; }
    public ushort ProductVariant { get; }
    public ushort MaxRules { get; }
    public ushort WireProfile { get; }
    public ProductResourceProfile Resources { get; }
    public IReadOnlyList<TagDefinition> Tags { get; }

    /// <summary>
    /// Năng lực Rule Engine được suy ra từ MaxRules > 0 (Contract V1.9).
    /// </summary>
    public bool HasRuleEngine => MaxRules > 0;

    /// <summary>
    /// Năng lực Retentive Memory được suy ra từ số lượng thanh ghi retain > 0 (Contract V1.9).
    /// </summary>
    public bool HasRetentiveMemory => Resources.RetentiveRegisters > 0;

    /// <summary>
    /// Năng lực Function Block chuyên dụng (0x0B00..0x0B7F) khi thiết bị hỗ trợ Wire Profile V2 trở lên.
    /// </summary>
    public bool SupportsDedicatedFunctionBlocks => WireProfile >= 2;

    /// <summary>
    /// Năng lực điều khiển chẩn đoán trực tiếp (Diagnostic Control 0x0A20) khi thiết bị hỗ trợ Wire Profile V2 trở lên.
    /// </summary>
    public bool SupportsDiagnosticControl => WireProfile >= 2;

    /// <summary>
    /// Năng lực đồng hồ thời gian thực (RTC Clock 0x0810..0x0813) khi thiết bị hỗ trợ Wire Profile V2 trở lên.
    /// </summary>
    public bool SupportsRtcClock => (HardwareCapabilities & SPLC_HardwareCapabilities.RTC) != 0 || WireProfile >= 2;

    /// <summary>
    /// Bitmask năng lực phần cứng do MCU khai báo tại 0x0A08 (hoặc suy diễn theo DeviceClass nếu firmware V1).
    /// </summary>
    public SPLC_HardwareCapabilities HardwareCapabilities { get; }

    /// <summary>
    /// Năng lực xuất bản viễn trắc MQTT Publish (0x0C00).
    /// </summary>
    public bool SupportsMqttPublish => (HardwareCapabilities & SPLC_HardwareCapabilities.MQTT_PUB) != 0;

    /// <summary>
    /// Năng lực nhận dữ liệu điều khiển MQTT Subscribe (0x0D80).
    /// </summary>
    public bool SupportsMqttSubscribe => (HardwareCapabilities & SPLC_HardwareCapabilities.MQTT_SUB) != 0;

    /// <summary>
    /// Năng lực cổng truyền thông mạng vật lý (Ethernet/WiFi).
    /// </summary>
    public bool SupportsEthernet => (HardwareCapabilities & SPLC_HardwareCapabilities.ETHERNET) != 0;

    /// <summary>
    /// Năng lực truyền thông không dây Wi-Fi (0x0200).
    /// </summary>
    public bool SupportsWifi => (HardwareCapabilities & SPLC_HardwareCapabilities.WIFI) != 0;

    /// <summary>
    /// Năng lực có kết nối mạng vật lý hoặc không dây (Ethernet hoặc Wi-Fi).
    /// </summary>
    public bool SupportsNetwork => SupportsEthernet || SupportsWifi;

    /// <summary>
    /// Năng lực lưu trữ thẻ nhớ MicroSD datalogger.
    /// </summary>
    public bool SupportsSdDatalog => (HardwareCapabilities & SPLC_HardwareCapabilities.SD_DATALOG) != 0;

    /// <summary>
    /// Năng lực khối hàm Modbus RTU Master Poller (0x1000).
    /// </summary>
    public bool SupportsModbusMaster => (HardwareCapabilities & SPLC_HardwareCapabilities.MODBUS_MASTER) != 0;

    /// <summary>
    /// Năng lực phân hệ Modbus RTU Slave & Sandbox (0x1500).
    /// </summary>
    public bool SupportsModbusSlave => (HardwareCapabilities & SPLC_HardwareCapabilities.MODBUS_SLAVE) != 0;

    /// <summary>
    /// Năng lực phân hệ REST API HTTP Client (0x1600..0x163F).
    /// </summary>
    public bool SupportsHttpClient => (HardwareCapabilities & SPLC_HardwareCapabilities.HTTP_CLIENT) != 0;

    /// <summary>
    /// Năng lực truyền trong suốt Serial-to-Network (Transparent Stream).
    /// </summary>
    public bool SupportsTransparentStream => (HardwareCapabilities & SPLC_HardwareCapabilities.TRANSPARENT_STREAM) != 0;

    /// <summary>
    /// Thiết bị có thể hoạt động ở vai trò DTU Gateway (hoặc là thiết bị thuần DTU, hoặc thiết bị có RS-485 và truyền thông mạng).
    /// </summary>
    public bool IsDtuCapable => DeviceClass == (ushort)SPLC_DeviceClass.DTU || (PhysicalRs485PortCount > 0 && SupportsNetwork);

    /// <summary>
    /// Số lượng cổng RS-485 vật lý hiện hữu trên bo mạch MCU (đọc động từ 0x0A0A hoặc suy diễn từ HardwareCapabilities).
    /// </summary>
    public ushort PhysicalRs485PortCount { get; }

    private readonly Dictionary<ushort, TagDefinition> _tagsByIndex;
    private readonly Dictionary<string, TagDefinition> _tagsByName;

    /// <summary>
    /// Constructor đầy đủ cho Self-Describing Device Profile V1.9 / V2.0.
    /// </summary>
    public ProductDefinition(
        string productName,
        ushort deviceClass,
        ushort productVariant,
        ushort maxRules,
        ProductResourceProfile resources,
        IEnumerable<TagDefinition> tags,
        ushort wireProfile = 1,
        SPLC_HardwareCapabilities hardwareCapabilities = SPLC_HardwareCapabilities.NONE,
        ushort? physicalRs485PortCount = null)
    {
        ProductName = productName ?? "Default";
        DeviceClass = deviceClass;
        ProductVariant = productVariant;
        MaxRules = maxRules;
        WireProfile = wireProfile;
        Resources = resources ?? ProductResourceProfile.DefaultRemoteIo;
        HardwareCapabilities = hardwareCapabilities != SPLC_HardwareCapabilities.NONE
            ? hardwareCapabilities
            : GetDefaultFallbackCapabilities(wireProfile);

        if (physicalRs485PortCount.HasValue)
        {
            PhysicalRs485PortCount = physicalRs485PortCount.Value;
        }
        else
        {
            // Tự động suy diễn: Nếu có năng lực MODBUS_MASTER thì mặc định 2 cổng, ngược lại 0
            bool hasModbus = (HardwareCapabilities & SPLC_HardwareCapabilities.MODBUS_MASTER) != 0;
            PhysicalRs485PortCount = hasModbus ? (ushort)2 : (ushort)0;
        }

        var tagList = tags.ToList();
        Tags = tagList.AsReadOnly();

        _tagsByIndex = tagList.ToDictionary(t => t.TagIndex);
        _tagsByName = tagList.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Năng lực dự phòng tối thiểu khi kết nối với firmware cũ (Legacy V1.x không hỗ trợ 0x0A08)
    /// hoặc khi khởi tạo đối tượng mock/test mà không chỉ định capabilities.
    /// <para>
    /// NGUYÊN TẮC KIẾN TRÚC: TUYỆT ĐỐI KHÔNG suy diễn năng lực ngoại vi (Ethernet, MQTT, RTC, SD)
    /// dựa trên DeviceClass vì mỗi phần cứng thực tế có BOM/board layout khác nhau.
    /// Nguồn sự thật duy nhất luôn là thanh ghi 0x0A08 do firmware MCU khai báo động.
    /// </para>
    /// </summary>
    public static SPLC_HardwareCapabilities GetDefaultFallbackCapabilities(ushort wireProfile = 2)
    {
        // Tối thiểu an toàn cho mọi SimplePLC engine là thực thi logic quy tắc và bộ nhớ duy trì
        return SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER;
    }

    /// <summary>
    /// Giữ lại để tương thích mã nguồn cũ, trả về cấu hình fallback an toàn.
    /// </summary>
    [Obsolete("Không suy diễn năng lực theo DeviceClass. Sử dụng thanh ghi 0x0A08 từ MCU hoặc GetDefaultFallbackCapabilities.")]
    public static SPLC_HardwareCapabilities GetDefaultCapabilitiesForClass(ushort deviceClass, ushort wireProfile)
    {
        return GetDefaultFallbackCapabilities(wireProfile);
    }

    /// <summary>
    /// Constructor tương thích ngược cho các unit test và mock legacy.
    /// </summary>
    public ProductDefinition(string productName, IEnumerable<TagDefinition> tags)
        : this(
            productName,
            deviceClass: 1, // REMOTE_IO
            productVariant: 1,
            maxRules: 100,
            resources: DeriveProfileFromTags(tags),
            tags: tags)
    {
    }

    public TagDefinition? FindTagByIndex(ushort index)
    {
        return _tagsByIndex.GetValueOrDefault(index);
    }

    public TagDefinition? FindTagByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _tagsByName.GetValueOrDefault(name);
    }

    public bool ContainsTag(ushort index) => _tagsByIndex.ContainsKey(index);
    public bool ContainsTag(string name) => !string.IsNullOrWhiteSpace(name) && _tagsByName.ContainsKey(name);

    /// <summary>
    /// Fallback helper tạo danh mục cấu hình chuẩn Remote I/O 8DI - 8DO - 4AI (124 tags)
    /// CHỈ dành cho demo offline, design-time placeholder, mock và test suite.
    /// <para>
    /// <b>CẢNH BÁO KIẾN TRÚC:</b> TUYỆT ĐỐI KHÔNG sử dụng phương thức này làm Source of Truth cho thiết bị đang kết nối (Connected Device).
    /// Khi kết nối với MCU thật hoặc simulator, Source of Truth duy nhất BẮT BUỘC phải được dựng động
    /// từ metadata thiết bị (0x0000 + 0x0020) thông qua <see cref="DeviceProfileBuilder"/>.
    /// </para>
    /// </summary>
    /// <summary>
    /// Fallback helper tạo danh mục cấu hình chuẩn SynaptiX DTU Gateway (RS485 to Network: MQTT, HTTP, TCP).
    /// </summary>
    public static ProductDefinition CreateDtuGateway(
        ushort wireProfile = 2,
        SPLC_HardwareCapabilities hardwareCapabilities = SPLC_HardwareCapabilities.NONE,
        ushort physicalRs485PortCount = 2)
    {
        var profile = new ProductResourceProfile(
            DigitalInputs: 0,
            DigitalOutputs: 0,
            AnalogInputs: 0,
            VirtualFlags: 32,
            VirtualRegisters: 64,
            RetentiveRegisters: 32,
            Counters: 4);

        var tags = DeviceProfileBuilder.GenerateTags(profile);
        var effectiveCaps = hardwareCapabilities != SPLC_HardwareCapabilities.NONE
            ? hardwareCapabilities
            : (SPLC_HardwareCapabilities.ETHERNET |
               SPLC_HardwareCapabilities.WIFI |
               SPLC_HardwareCapabilities.MQTT_PUB |
               SPLC_HardwareCapabilities.MQTT_SUB |
               SPLC_HardwareCapabilities.MODBUS_MASTER |
               SPLC_HardwareCapabilities.HTTP_CLIENT |
               SPLC_HardwareCapabilities.TRANSPARENT_STREAM);

        return new ProductDefinition(
            "SynaptiX DTU Gateway (RS-485)",
            deviceClass: (ushort)SPLC_DeviceClass.DTU,
            productVariant: (ushort)SPLC_DtuVariant.VARIANT_DTU_RS485_NET,
            maxRules: 50,
            resources: profile,
            tags: tags,
            wireProfile: wireProfile,
            hardwareCapabilities: effectiveCaps,
            physicalRs485PortCount: physicalRs485PortCount);
    }

    public static ProductDefinition CreateRemoteIo8Di8Do4Ai(
        ushort wireProfile = 2,
        SPLC_HardwareCapabilities hardwareCapabilities = SPLC_HardwareCapabilities.NONE,
        ushort? physicalRs485PortCount = null)
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(profile);
        var effectiveCaps = hardwareCapabilities != SPLC_HardwareCapabilities.NONE
            ? hardwareCapabilities
            : (SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER);
        return new ProductDefinition(
            "Remote I/O (8DI-8DO-4AI)",
            deviceClass: 1, // REMOTE_IO
            productVariant: 1,
            maxRules: 100,
            resources: profile,
            tags: tags,
            wireProfile: wireProfile,
            hardwareCapabilities: effectiveCaps,
            physicalRs485PortCount: physicalRs485PortCount);
    }

    private static ProductResourceProfile DeriveProfileFromTags(IEnumerable<TagDefinition> tags)
    {
        ushort di = 0, @do = 0, ai = 0, vflag = 0, vreg = 0, retain = 0, counter = 0;
        foreach (var t in tags)
        {
            switch (t.Kind)
            {
                case TagKind.DiscreteInput: di++; break;
                case TagKind.DiscreteOutput: @do++; break;
                case TagKind.AnalogInput: ai++; break;
                case TagKind.VirtualFlag: vflag++; break;
                case TagKind.VirtualRegister: vreg++; break;
                case TagKind.VirtualRegisterRetain: retain++; break;
                case TagKind.Counter: counter++; break;
            }
        }
        return new ProductResourceProfile(di, @do, ai, vflag, vreg, retain, counter);
    }
}
