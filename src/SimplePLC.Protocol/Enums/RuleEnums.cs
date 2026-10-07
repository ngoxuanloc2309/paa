namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Kiểu trigger của một rule theo Data Contract V1.7.
/// </summary>
public enum SPLC_TriggerType : byte
{
    ON_CHANGE = 0,     // Giá trị trigger_tag thay đổi
    ON_RISE = 1,       // Sườn lên / vượt ngưỡng theo hướng tăng
    ON_FALL = 2,       // Sườn xuống / vượt ngưỡng theo hướng giảm
    TIME_WINDOW = 3,   // Trigger theo thời gian trong ngày
    INTERVAL = 4       // Trigger định kỳ theo for_ms
}

/// <summary>
/// Phép so sánh bổ sung trên trigger value theo Data Contract V1.7.
/// </summary>
public enum SPLC_CompareOp : byte
{
    NONE = 0,
    EQ = 1,
    NEQ = 2,
    GT = 3,
    LT = 4,
    GTE = 5,
    LTE = 6,
    BETWEEN = 7
}

/// <summary>
/// Hành động được thực thi khi rule thỏa điều kiện theo Data Contract V1.7.
/// </summary>
public enum SPLC_ActionType : byte
{
    SET_TAG = 0,       // action_tag = action_param
    TOGGLE_TAG = 1,    // Đảo trạng thái action_tag
    INC_COUNTER = 2,   // action_tag += action_param
    WRITE_REMOTE = 3,  // Ghi giá trị sang remote tag
    LOG_EVENT = 4,     // Ghi event nội bộ
    SEND_ALARM = 5,    // Phát alarm code
    ADD_TAG = 6,       // action_tag += trigger_tag value
    SCALE_TAG = 7      // Scale trigger value sang action_tag
}
