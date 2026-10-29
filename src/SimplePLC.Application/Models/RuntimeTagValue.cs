namespace SimplePLC.Application.Models;

/// <summary>
/// Đại diện giá trị thời gian thực của một Tag.
/// Cho phép cập nhật thuộc tính tại chỗ (in-place mutation) để tái sử dụng buffer đệm trong vòng lặp quét,
/// giúp đạt chuẩn zero-allocation cho các ca vận hành dài 24/7.
/// </summary>
public sealed record RuntimeTagValue
{
    public ushort TagIndex { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int Value { get; set; }
    public ushort Quality { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
