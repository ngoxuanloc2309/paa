using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn chính xác wire contract 32 byte (16 registers) của một Rule theo Data Contract V1.7.
/// Tuân thủ quy tắc DTO != Domain Rule (R4, R8).
/// </summary>
public sealed class RuleRecordDto
{
    public int ThresholdLo { get; set; }
    public int ThresholdHi { get; set; }
    public uint ForMs { get; set; }
    public int ActionParam { get; set; }
    public ushort TriggerTag { get; set; }
    public ushort ActionTag { get; set; }
    
    /// <summary>
    /// Bit 0..14 là tag index (0..127) hoặc 0x7FFF (Không có Guard); Bit 15 là cờ phủ định NEGATE.
    /// Mặc định: 0x7FFF (GuardTagNone).
    /// </summary>
    public ushort GuardTag { get; set; } = SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagNone;
    
    public bool Enabled { get; set; }
    public SPLC_TriggerType TriggerType { get; set; }
    public SPLC_CompareOp CompareOp { get; set; }
    public SPLC_ActionType ActionType { get; set; }

    /// <summary>
    /// Helper lấy tag index thuần từ GuardTag (bỏ bit 15 NEGATE).
    /// </summary>
    public ushort GuardTagIndex => (ushort)(GuardTag & SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagIndexMask);

    /// <summary>
    /// Helper kiểm tra cờ NEGATE (bit 15 của GuardTag).
    /// </summary>
    public bool GuardNegated => (GuardTag & SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagNegateMask) != 0;

    /// <summary>
    /// Helper kiểm tra Rule có cấu hình Guard hay không (GuardTagIndex != GuardTagNone).
    /// </summary>
    public bool HasGuard => GuardTagIndex != SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagNone;

    /// <summary>
    /// Đặt GuardTag kết hợp giữa tag index và cờ phủ định negate.
    /// </summary>
    public void SetGuardTag(ushort tagIndex, bool negate)
    {
        GuardTag = (ushort)((tagIndex & SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagIndexMask) | 
                            (negate ? SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagNegateMask : 0));
    }

    /// <summary>
    /// Xóa cấu hình Guard (đặt về sentinel 0x7FFF).
    /// </summary>
    public void ClearGuard()
    {
        GuardTag = SimplePLC.Protocol.Constants.ModbusRegisterMap.GuardTagNone;
    }
}
