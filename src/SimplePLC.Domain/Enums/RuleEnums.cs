namespace SimplePLC.Domain.Enums;

/// <summary>
/// Kiểu kích hoạt nghiệp vụ của một Trigger.
/// </summary>
public enum TriggerKind
{
    OnChange = 0,    // Khi giá trị tag thay đổi
    OnRise = 1,      // Khi giá trị tag chuyển từ 0 lên 1 hoặc vượt ngưỡng theo chiều tăng
    OnFall = 2,      // Khi giá trị tag chuyển từ 1 về 0 hoặc vượt ngưỡng theo chiều giảm
    TimeWindow = 3,  // Kích hoạt theo khung giờ trong ngày
    Interval = 4     // Kích hoạt lặp lại định kỳ theo chu kỳ thời gian (ForMs)
}

/// <summary>
/// Toán tử so sánh điều kiện giá trị.
/// </summary>
public enum CompareOperator
{
    None = 0,
    Equal = 1,
    NotEqual = 2,
    GreaterThan = 3,
    LessThan = 4,
    GreaterThanOrEqual = 5,
    LessThanOrEqual = 6,
    Between = 7
}

/// <summary>
/// Hành động nghiệp vụ được thực thi khi Rule kích hoạt.
/// </summary>
public enum ActionKind
{
    SetTag = 0,          // Gán giá trị cụ thể cho ActionTag
    ToggleTag = 1,       // Đảo trạng thái (0 -> 1 hoặc 1 -> 0) cho ActionTag kiểu Boolean
    IncrementCounter = 2,// Tăng giá trị ActionTag một lượng tham số (action_param)
    WriteRemote = 3,     // Ghi giá trị sang remote tag qua mạng
    LogEvent = 4,        // Ghi nhật ký sự kiện nội bộ
    SendAlarm = 5,       // Kích hoạt cảnh báo hệ thống
    AddTag = 6,          // Cộng giá trị TriggerTag vào ActionTag
    ScaleTag = 7,        // Chuyển đổi tuyến tính tỉ lệ giá trị TriggerTag sang ActionTag
    MathOp = 8          // Phép tính số học 2 toán hạng tổng quát (Op1 [Op] Op2 -> ActionTag)
}

/// <summary>
/// Các phép toán số học và đại số nhị phân được hỗ trợ bởi khối Math (IEC 61131-3).
/// </summary>
public enum MathOperator : byte
{
    /// <summary>Phép cộng đại số: Out = Op1 + Op2</summary>
    Add = 0,

    /// <summary>Phép trừ đại số: Out = Op1 - Op2</summary>
    Subtract = 1,
    Sub = 1,

    /// <summary>Phép nhân đại số: Out = Op1 * Op2</summary>
    Multiply = 2,
    Mul = 2,

    /// <summary>Phép chia đại số: Out = Op1 / Op2 (có phòng vệ chống chia cho 0)</summary>
    Divide = 3,
    Div = 3,

    /// <summary>Phép chia lấy phần dư: Out = Op1 % Op2</summary>
    Modulo = 4,
    Mod = 4
}

/// <summary>
/// Chế độ xử lý kiểu dữ liệu tính toán cho khối Math.
/// </summary>
public enum MathTypeMode : byte
{
    /// <summary>Tự động phát hiện (Nâng kiểu lên REAL nếu có bất kỳ toán hạng nào là REAL)</summary>
    Auto = 0,

    /// <summary>Cưỡng bức tính toán số nguyên bù hai 32-bit (INT32)</summary>
    ForceInt32 = 1,

    /// <summary>Cưỡng bức tính toán số thực dấu chấm động IEEE-754 (REAL / Float32)</summary>
    ForceReal = 2
}
