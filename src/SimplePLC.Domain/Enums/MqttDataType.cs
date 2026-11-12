namespace SimplePLC.Domain.Enums;

/// <summary>
/// Kiểu dữ liệu phân giải từ gói tin JSON trong giao thức MQTT Subscribe.
/// </summary>
public enum MqttDataType
{
    Int16  = 0,
    Int32  = 1,
    Float  = 2,
    Bool   = 3,
    String = 4
}
