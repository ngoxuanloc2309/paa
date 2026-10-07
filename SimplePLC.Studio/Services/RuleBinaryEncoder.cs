using System.Buffers.Binary;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Service mã hóa và giải mã bản ghi nhị phân Rule 32-byte chuẩn Data Contract V1.7 (16 thanh ghi Modbus).
/// Tuân thủ nghiêm ngặt:
/// - REQ-RUL-001: Kích thước cố định đúng 32 bytes (16 Modbus Holding Registers).
/// - REQ-RUL-002: reserved[0..5] (bytes 26-31) luôn ghi 0x00.
/// - REQ-RUL-003: guard_tag = (guard_index &amp; 0x7FFF) | (negate ? 0x8000 : 0); không có Guard thì dùng Sentinel 0x7FFF.
/// - REQ-SER-001 &amp; REQ-SER-002: Toàn bộ trường 16-bit và 32-bit ghi theo thứ tự Big-Endian chuẩn.
/// </summary>
public static class RuleBinaryEncoder
{
    public const int RuleRecordSize = 32;
    public const int RuleRecordSizeV17 = 32;

    /// <summary>
    /// Mã hóa một RuleItemModel thành mảng byte 32-byte Big-Endian theo chuẩn Data Contract V1.7.
    /// </summary>
    public static byte[] EncodeRule(RuleItemModel rule)
    {
        byte[] buffer = new byte[RuleRecordSize];
        EncodeRule(rule, buffer.AsSpan());
        return buffer;
    }

    /// <summary>
    /// Alias EncodeRuleV17 hỗ trợ tương thích mã gọi cũ.
    /// </summary>
    public static byte[] EncodeRuleV17(RuleItemModel rule) => EncodeRule(rule);

    /// <summary>
    /// Mã hóa RuleItemModel trực tiếp vào một Span 32-byte theo chuẩn Data Contract V1.7.
    /// </summary>
    public static void EncodeRule(RuleItemModel rule, Span<byte> destination)
    {
        if (destination.Length < RuleRecordSize)
            throw new ArgumentException($"Destination span must be at least {RuleRecordSize} bytes.", nameof(destination));

        // 00-03: threshold_lo (int32_t, BE)
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(0, 4), rule.ThresholdLo);

        // 04-07: threshold_hi (int32_t, BE)
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(4, 4), rule.ThresholdHi);

        // 08-11: for_ms (uint32_t, BE)
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(8, 4), rule.ForMs);

        // 12-15: action_param (int32_t, BE)
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(12, 4), rule.ActionParam);

        // 16-17: trigger_tag (uint16_t, BE)
        ushort triggerTagIdx = (ushort)(rule.TriggerTag?.Index ?? 0);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(16, 2), triggerTagIdx);

        // 18-19: action_tag (uint16_t, BE)
        ushort actionTagIdx = (ushort)(rule.ActionTag?.Index ?? 0);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(18, 2), actionTagIdx);

        // 20-21: guard_tag (uint16_t, BE: bit 0-14 index, bit 15 NEGATE, or 0x7FFF for NO GUARD)
        bool hasGuard = rule.GuardTag != null && 
                        rule.GuardTag.Kind != TagKind.None && 
                        !string.Equals(rule.GuardTag.Name, "NONE", StringComparison.OrdinalIgnoreCase);
        ushort guardTagPacked = hasGuard && rule.GuardTag != null
            ? (ushort)((rule.GuardTag.Index & 0x7FFF) | (rule.GuardNegated ? 0x8000 : 0))
            : (ushort)0x7FFF;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(20, 2), guardTagPacked);

        // 22: enabled (uint8_t)
        destination[22] = (byte)(rule.Enabled ? 1 : 0);

        // 23: trigger_type (uint8_t)
        destination[23] = (byte)rule.TriggerType;

        // 24: compare_op (uint8_t)
        destination[24] = (byte)rule.CompareOp;

        // 25: action_type (uint8_t)
        destination[25] = (byte)rule.ActionType;

        // 26-31: reserved[6] = 0x00 0x00 0x00 0x00 0x00 0x00 (R8)
        destination.Slice(26, 6).Clear();
    }

    /// <summary>
    /// Alias EncodeRuleV17 hỗ trợ tương thích mã gọi cũ.
    /// </summary>
    public static void EncodeRuleV17(RuleItemModel rule, Span<byte> destination) => EncodeRule(rule, destination);

    /// <summary>
    /// Mã hóa toàn bộ danh sách Rule thành payload liên tục (N * 32 bytes) để deploy hoặc nạp staging Modbus.
    /// </summary>
    public static byte[] EncodeProgram(IEnumerable<RuleItemModel> rules)
    {
        var ruleList = rules as IList<RuleItemModel> ?? rules.ToList();
        byte[] payload = new byte[ruleList.Count * RuleRecordSize];
        for (int i = 0; i < ruleList.Count; i++)
        {
            EncodeRule(ruleList[i], payload.AsSpan(i * RuleRecordSize, RuleRecordSize));
        }
        return payload;
    }

    /// <summary>
    /// Alias EncodeProgramV17 hỗ trợ tương thích mã gọi cũ.
    /// </summary>
    public static byte[] EncodeProgramV17(IEnumerable<RuleItemModel> rules) => EncodeProgram(rules);

    /// <summary>
    /// Định dạng chuỗi hex hiển thị nhóm theo các trường 32-byte cho kỹ sư dễ quan sát.
    /// </summary>
    public static string FormatDetailedBreakdown(byte[] bytes)
    {
        if (bytes.Length == RuleRecordSize)
        {
            // 00-03: ThLo | 04-07: ThHi | 08-11: ForMs | 12-15: ActParam | 16-17: TrgTag | 18-19: ActTag | 20-21: GrdTag | 22: En | 23: Trg | 24: Cmp | 25: Act | 26-31: Rsv[6]
            string thLo = $"{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}";
            string thHi = $"{bytes[4]:X2}{bytes[5]:X2}{bytes[6]:X2}{bytes[7]:X2}";
            string forMs = $"{bytes[8]:X2}{bytes[9]:X2}{bytes[10]:X2}{bytes[11]:X2}";
            string actP = $"{bytes[12]:X2}{bytes[13]:X2}{bytes[14]:X2}{bytes[15]:X2}";
            string trgTag = $"{bytes[16]:X2}{bytes[17]:X2}";
            string actTag = $"{bytes[18]:X2}{bytes[19]:X2}";
            string grdTag = $"{bytes[20]:X2}{bytes[21]:X2}";
            string flags = $"{bytes[22]:X2} {bytes[23]:X2} {bytes[24]:X2} {bytes[25]:X2}";
            string rsv = $"{bytes[26]:X2}{bytes[27]:X2}{bytes[28]:X2}{bytes[29]:X2}{bytes[30]:X2}{bytes[31]:X2}";

            return $"{thLo} {thHi} {forMs} {actP} | {trgTag} {actTag} {grdTag} | {flags} | {rsv}";
        }

        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Giải mã một record 32-byte thành dữ liệu thô theo chuẩn Data Contract V1.7.
    /// </summary>
    public static (int ThresholdLo, int ThresholdHi, uint ForMs, int ActionParam, 
                   ushort TriggerTag, ushort ActionTag, ushort GuardTag, bool GuardNegated,
                   bool Enabled, byte TriggerType, byte CompareOp, byte ActionType) 
        DecodeRecord(ReadOnlySpan<byte> source)
    {
        if (source.Length < RuleRecordSize)
            throw new ArgumentException($"Source span must be at least {RuleRecordSize} bytes.", nameof(source));

        int thresholdLo = BinaryPrimitives.ReadInt32BigEndian(source.Slice(0, 4));
        int thresholdHi = BinaryPrimitives.ReadInt32BigEndian(source.Slice(4, 4));
        uint forMs = BinaryPrimitives.ReadUInt32BigEndian(source.Slice(8, 4));
        int actionParam = BinaryPrimitives.ReadInt32BigEndian(source.Slice(12, 4));
        ushort triggerTag = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(16, 2));
        ushort actionTag = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(18, 2));
        ushort rawGuard = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(20, 2));
        ushort guardTag = (ushort)(rawGuard & 0x7FFF);
        bool guardNegated = (rawGuard & 0x8000) != 0;
        bool enabled = source[22] != 0;
        byte triggerType = source[23];
        byte compareOp = source[24];
        byte actionType = source[25];

        // bytes 26-31: reserved[6] (R8)

        return (thresholdLo, thresholdHi, forMs, actionParam, triggerTag, actionTag, 
                guardTag, guardNegated, enabled, triggerType, compareOp, actionType);
    }

    /// <summary>
    /// Alias DecodeRecordV17 hỗ trợ tương thích mã gọi cũ.
    /// </summary>
    public static (int ThresholdLo, int ThresholdHi, uint ForMs, int ActionParam, 
                   ushort TriggerTag, ushort ActionTag, ushort GuardTag, bool GuardNegated,
                   bool Enabled, byte TriggerType, byte CompareOp, byte ActionType) 
        DecodeRecordV17(ReadOnlySpan<byte> source) => DecodeRecord(source);

    /// <summary>
    /// Giải mã một mảng byte nhị phân chuẩn 32-byte (N * 32 bytes) thành danh sách RuleItemModel.
    /// Tự động dò tìm đối tượng Tag tương ứng trong danh mục Tag (nếu có).
    /// </summary>
    public static List<RuleItemModel> DecodeProgram(byte[] rawBytes, IEnumerable<TagModel>? availableTags = null)
    {
        if (rawBytes == null || rawBytes.Length == 0)
            return new List<RuleItemModel>();

        if (rawBytes.Length % RuleRecordSize != 0)
            throw new ArgumentException($"Buffer length ({rawBytes.Length}) must be a multiple of {RuleRecordSize} bytes.", nameof(rawBytes));

        int count = rawBytes.Length / RuleRecordSize;
        var rules = new List<RuleItemModel>(count);
        var tagList = availableTags?.ToList() ?? new List<TagModel>();

        TagModel GetOrCreateTag(ushort tagIndex, TagKind defaultKind, string defaultPrefix)
        {
            var match = tagList.FirstOrDefault(t => t.Index == tagIndex);
            if (match != null) return match;
            return new TagModel
            {
                Index = tagIndex,
                Name = $"{defaultPrefix}{tagIndex}",
                Alias = $"{defaultPrefix} #{tagIndex}",
                Kind = defaultKind
            };
        }

        for (int i = 0; i < count; i++)
        {
            var span = rawBytes.AsSpan(i * RuleRecordSize, RuleRecordSize);
            var rec = DecodeRecord(span);

            var rule = new RuleItemModel
            {
                Id = $"R{i + 1}",
                Index = i,
                ThresholdLo = rec.ThresholdLo,
                ThresholdHi = rec.ThresholdHi,
                ForMs = rec.ForMs,
                ActionParam = rec.ActionParam,
                TriggerTag = GetOrCreateTag(rec.TriggerTag, TagKind.DiscreteInput, "TAG_"),
                ActionTag = GetOrCreateTag(rec.ActionTag, TagKind.DiscreteOutput, "ACT_"),
                GuardTag = (rec.GuardTag != 0x7FFF) ? GetOrCreateTag(rec.GuardTag, TagKind.VirtualFlag, "GRD_") : null,
                GuardNegated = rec.GuardNegated,
                Enabled = rec.Enabled,
                TriggerType = Enum.IsDefined(typeof(TriggerType), (int)rec.TriggerType) ? (TriggerType)rec.TriggerType : TriggerType.ON_RISE,
                CompareOp = Enum.IsDefined(typeof(CompareOp), (int)rec.CompareOp) ? (CompareOp)rec.CompareOp : CompareOp.NONE,
                ActionType = Enum.IsDefined(typeof(ActionType), (int)rec.ActionType) ? (ActionType)rec.ActionType : ActionType.SET_TAG
            };

            rule.UpdateNarrative();
            rules.Add(rule);
        }

        return rules;
    }

    /// <summary>
    /// Alias DecodeProgramV17 hỗ trợ tương thích mã gọi cũ.
    /// </summary>
    public static List<RuleItemModel> DecodeProgramV17(byte[] rawBytes, IEnumerable<TagModel>? availableTags = null) => DecodeProgram(rawBytes, availableTags);
}
