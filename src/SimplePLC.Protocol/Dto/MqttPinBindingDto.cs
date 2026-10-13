namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO đóng gói cấu hình một chân động trong phân hệ MQTT IIoT.
/// Dùng chung cho cả Subscribe (chiều vào) và Publish (chiều ra).
/// </summary>
public sealed class MqttPinBindingDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public MqttDataType DataType { get; set; } = MqttDataType.Int16;
    public ushort? TargetTagIndex { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double DefaultValue { get; set; } = 0.0;
}
