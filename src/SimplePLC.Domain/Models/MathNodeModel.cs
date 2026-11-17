using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ khối tính toán toán học Math.
/// </summary>
public sealed class MathNodeModel
{
    /// <summary>
    /// Định danh duy nhất của khối.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Nhãn hiển thị của khối trên Canvas.
    /// </summary>
    public string Label { get; set; } = "MATH";

    /// <summary>
    /// Toán tử số học thực thi.
    /// </summary>
    public MathOperator Operator { get; set; } = MathOperator.Add;

    /// <summary>
    /// Chế độ xử lý kiểu dữ liệu (Auto, ForceInt32, ForceReal).
    /// </summary>
    public MathTypeMode TypeMode { get; set; } = MathTypeMode.Auto;

    /// <summary>
    /// Danh sách các chân ngõ vào toán hạng (tối thiểu 2 chân, tối đa 8 chân).
    /// </summary>
    public List<MathPinModel> Pins { get; init; } = new();

    /// <summary>
    /// Tag đích nhận kết quả tính toán.
    /// </summary>
    public TagDefinition? TargetTag { get; set; }
}
