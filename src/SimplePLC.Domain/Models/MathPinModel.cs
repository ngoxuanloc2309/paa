namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình chân toán hạng đầu vào cho khối Math.
/// </summary>
public sealed class MathPinModel
{
    /// <summary>
    /// Định danh duy nhất của chân.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Nhãn hiển thị của chân (ví dụ: IN1, IN2, IN3...).
    /// </summary>
    public string Label { get; set; } = "IN1";

    /// <summary>
    /// Tag liên kết với chân (nếu nhận giá trị động từ Tag).
    /// </summary>
    public TagDefinition? BoundTag { get; set; }

    /// <summary>
    /// Cờ xác định chân này nhận giá trị hằng số cố định thay vì Tag.
    /// </summary>
    public bool IsConstant { get; set; }

    /// <summary>
    /// Giá trị hằng số khi IsConstant = true.
    /// </summary>
    public double ConstantValue { get; set; }
}
