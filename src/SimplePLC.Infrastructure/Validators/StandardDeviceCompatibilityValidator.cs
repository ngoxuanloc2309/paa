using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Validators;

/// <summary>
/// Trình thẩm định tương thích sơ bộ cho kết nối vi điều khiển (Contract V1.9).
/// <para>
/// <b>PHÂN ĐỊNH RANH GIỚI KIẾN TRÚC:</b>
/// <list type="bullet">
/// <item><b>DeviceClass:</b> Định danh họ giao thức / phân loại chức năng (VD: REMOTE_IO = 0x0001).
/// Nếu MCU trả về DeviceClass chưa được App hỗ trợ (VD: 0xEEEE), kết nối sẽ bị TỪ CHỐI CỨNG vì các họ thiết bị khác nhau
/// có ngữ nghĩa vận hành, command set và vùng nhớ khác nhau.</item>
/// <item><b>ProductVariant:</b> Định danh biến thể phần cứng vật lý bên trong cùng một DeviceClass (VD: 8DI/8DO, 4DI/4DO, 16DI/16DO).
/// Với mô hình Self-Describing Device Profile V1, unknown ProductVariant ĐƯỢC CHẤP NHẬN tự động
/// mà không cần đăng ký trước trong C#, miễn là thiết bị tuân thủ các quy tắc của Wire Profile V1.</item>
/// </list>
/// </para>
/// </summary>
public sealed class StandardDeviceCompatibilityValidator : IDeviceCompatibilityValidator
{
    private readonly IReadOnlyList<SupportedDeviceProfile> _supportedProfiles;
    private readonly bool _allowUnknownVariants;

    public StandardDeviceCompatibilityValidator(
        IEnumerable<SupportedDeviceProfile>? supportedProfiles = null,
        bool allowUnknownVariants = true)
    {
        _supportedProfiles = supportedProfiles?.ToList().AsReadOnly()
            ?? new List<SupportedDeviceProfile> { SupportedDeviceProfile.RemoteIo8Di8Do4AiV1_7 }.AsReadOnly();
        _allowUnknownVariants = allowUnknownVariants;
    }

    public CompatibilityResult Validate(DeviceDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        foreach (var profile in _supportedProfiles)
        {
            if (profile.Matches(descriptor))
            {
                return CompatibilityResult.Ok();
            }
        }

        // Nếu không khớp profile nào, kiểm tra chi tiết để cung cấp thông báo rõ ràng
        bool classMatched = _supportedProfiles.Any(p => p.DeviceClass == (ushort)descriptor.DeviceClass);
        if (!classMatched)
        {
            return CompatibilityResult.Incompatible(
                CompatibilityStatus.UnsupportedDeviceClass,
                $"Device class 0x{(ushort)descriptor.DeviceClass:X4} is not supported.");
        }

        bool protocolMatched = _supportedProfiles.Any(p => p.ProtocolVersion == descriptor.ProtocolVersion);
        if (!protocolMatched)
        {
            return CompatibilityResult.Incompatible(
                CompatibilityStatus.UnsupportedProtocolVersion,
                $"Protocol version {descriptor.ProtocolVersion} is incompatible with host.");
        }

        bool ruleFormatMatched = _supportedProfiles.Any(p => p.RuleFormatVersion == descriptor.RuleFormatVersion);
        if (!ruleFormatMatched)
        {
            return CompatibilityResult.Incompatible(
                CompatibilityStatus.UnsupportedRuleFormatVersion,
                $"Rule format version {descriptor.RuleFormatVersion} is incompatible with host.");
        }

        // Với Wire Profile V1 tự mô tả: nếu class và protocol/rule_format hợp lệ thì chấp nhận unknown variant
        // (tài nguyên sẽ được thẩm định chi tiết qua DeviceResourceInfo tại 0x0020)
        if (_allowUnknownVariants)
        {
            return CompatibilityResult.Ok();
        }

        bool variantMatched = _supportedProfiles.Any(p => p.DeviceClass == (ushort)descriptor.DeviceClass && p.DeviceVariant == descriptor.DeviceVariant);
        if (!variantMatched)
        {
            return CompatibilityResult.Incompatible(
                CompatibilityStatus.UnsupportedDeviceVariant,
                $"Device variant 0x{descriptor.DeviceVariant:X4} is not supported for class 0x{(ushort)descriptor.DeviceClass:X4}.");
        }

        return CompatibilityResult.Ok();
    }
}
