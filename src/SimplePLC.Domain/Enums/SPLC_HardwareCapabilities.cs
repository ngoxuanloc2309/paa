namespace SimplePLC.Domain.Enums;

/// <summary>
/// Bitmask năng lực phần cứng do MCU khai báo tại địa chỉ 0x0A08 (32-bit uint32, Big-Endian).
/// </summary>
[Flags]
public enum SPLC_HardwareCapabilities : uint
{
    NONE               = 0x0000,
    BASE_LOGIC         = 0x0001, // Bit 0: Hỗ trợ Logic Engine FBD cơ bản (Rule Engine)
    RETENTIVE          = 0x0002, // Bit 1: Hỗ trợ lưu trữ Retentive Memory (Flash/NVRAM/FRAM)
    RTC                = 0x0004, // Bit 2: Hỗ trợ Real-Time Clock phần cứng (DS3231/LSE)
    ETHERNET           = 0x0008, // Bit 3: Có cổng truyền thông mạng vật lý (Ethernet/WiFi)
    MQTT_PUB           = 0x0010, // Bit 4: Hỗ trợ phân hệ xuất bản viễn trắc MQTT PUB (0x0C00)
    MQTT_SUB           = 0x0020, // Bit 5: Hỗ trợ phân hệ nhận bản tin điều khiển MQTT SUB
    SD_DATALOG         = 0x0040, // Bit 6: Hỗ trợ ghi chép dữ liệu cục bộ qua thẻ nhớ MicroSD
    MQTT_TLS_CERT      = 0x0080, // Bit 7: Hỗ trợ nạp chứng chỉ bảo mật TLS/SSL theo từng struct (0x0F00)
    MODBUS_MASTER      = 0x0100, // Bit 8: Hỗ trợ phân hệ Modbus RTU Master Poller (0x1000)
    WIFI               = 0x0200, // Bit 9: Hỗ trợ phân hệ truyền thông không dây Wi-Fi ST67 (0x1300..0x13FF)
    MODBUS_TCP         = 0x0400, // Bit 10: Hỗ trợ Modbus TCP Server qua Ethernet/Wi-Fi (Port 502)
    FLOAT32            = 0x0800, // Bit 11: Hỗ trợ tính toán phần cứng số thực Float32 / REAL
    HTTP_CLIENT        = 0x1000, // Bit 12: Hỗ trợ phân hệ xuất bản dữ liệu REST API HTTP/HTTPS Client (0x1600)
    TRANSPARENT_STREAM = 0x2000, // Bit 13: Hỗ trợ truyền trong suốt Serial-to-Network Passthrough
    MODBUS_SLAVE       = 0x4000, // Bit 14: Hỗ trợ phân hệ RS-485 Modbus RTU Slave & Sandbox (0x1500)
    ZIGBEE             = 0x8000  // Bit 15: Hỗ trợ phân hệ truyền thông không dây Zigbee Modbus (0x1700..0x173F)
}
