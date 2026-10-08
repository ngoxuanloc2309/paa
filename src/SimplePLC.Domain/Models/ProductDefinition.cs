using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;

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
    public bool SupportsRtcClock => WireProfile >= 2;

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
        ushort wireProfile = 1)
    {
        ProductName = productName ?? "Default";
        DeviceClass = deviceClass;
        ProductVariant = productVariant;
        MaxRules = maxRules;
        WireProfile = wireProfile;
        Resources = resources ?? ProductResourceProfile.DefaultRemoteIo;

        var tagList = tags.ToList();
        Tags = tagList.AsReadOnly();

        _tagsByIndex = tagList.ToDictionary(t => t.TagIndex);
        _tagsByName = tagList.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
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
    public static ProductDefinition CreateRemoteIo8Di8Do4Ai(ushort wireProfile = 1)
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(profile);
        return new ProductDefinition(
            "Remote I/O (8DI-8DO-4AI)",
            deviceClass: 1, // REMOTE_IO
            productVariant: 1,
            maxRules: 100,
            resources: profile,
            tags: tags,
            wireProfile: wireProfile);
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
