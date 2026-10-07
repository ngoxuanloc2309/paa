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
    ScaleTag = 7         // Chuyển đổi tuyến tính tỉ lệ giá trị TriggerTag sang ActionTag
}
