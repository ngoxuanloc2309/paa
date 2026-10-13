namespace SimplePLC.Domain.Models;

using SimplePLC.Protocol.Enums;

/// <summary>
/// Đại diện cho cấu hình một chân động (Dynamic Pin) của khối MQTT Subscribe.
/// Tách biệt hoàn toàn giữa Tên hiển thị trên Canvas và Khóa bóc tách trong JSON.
/// </summary>
public sealed class MqttPinBinding
{
    /// <summary>
    /// Mã định danh duy nhất của chân để đảm bảo không đứt dây nối khi đổi tên nhãn.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Tên nhãn hiển thị trực quan trên chân Canvas (ví dụ: "grid", "speed", "target_temp").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn trường trong gói JSON (ví dụ: "grid" hoặc lồng nhau "data.speed").
    /// Nếu để trống, coi như lấy toàn bộ payload (Raw mode).
    /// </summary>
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>
    /// Kiểu dữ liệu mong muốn của chân.
    /// </summary>
    public MqttDataType DataType { get; set; } = MqttDataType.Int16;

    /// <summary>
    /// Tùy chọn: Ánh xạ ghi thêm giá trị vào một Tag trong Tag Store (VREG/VFLAG),
    /// cho phép HMI/SCADA hoặc Live Watch đọc được song song với dây nối FBD.
    /// </summary>
    public ushort? TargetTagIndex { get; set; }

    /// <summary>
    /// Ngưỡng lọc giá trị tối thiểu (null nếu không kiểm tra).
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Ngưỡng lọc giá trị tối đa (null nếu không kiểm tra).
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// Giá trị mặc định khi kích hoạt Fail-safe hoặc khi khởi tạo.
    /// </summary>
    public double DefaultValue { get; set; } = 0.0;
}
