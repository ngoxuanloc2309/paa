namespace SimplePLC.Protocol.Dto;

using System.Collections.Generic;
using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO đóng gói toàn bộ cấu hình khối MQTT Subscribe.
/// Lưu trữ cùng file dự án (.synx) và cấu hình nạp xuống Runtime/MCU.
/// </summary>
public sealed class MqttSubscriptionConfigDto
{
    public string NodeId { get; set; } = string.Empty;
    public string Topic { get; set; } = "synaptix/commands/inbound";
    public int Qos { get; set; } = 1;

    // Quản lý hàng đợi
    public MqttQueueMode QueueMode { get; set; } = MqttQueueMode.Fifo;
    public int QueueCapacity { get; set; } = 16;

    // Chính sách an toàn & Fail-safe
    public bool IgnoreRetainedOnBoot { get; set; } = true;
    public uint TimeoutMs { get; set; } = 0; // 0 = vô hiệu hóa
    public MqttFailSafeAction FailSafeAction { get; set; } = MqttFailSafeAction.KeepLastValue;

    // Danh sách các chân ngõ ra động (Dynamic Output Pins)
    public List<MqttPinBindingDto> Pins { get; set; } = new();
}
