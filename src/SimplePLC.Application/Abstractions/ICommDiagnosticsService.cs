namespace SimplePLC.Application.Abstractions;

using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Dịch vụ phân tích và giải mã nguyên nhân gốc của trạng thái truyền thông (Tier 2 Forensics).
/// </summary>
public interface ICommDiagnosticsService
{
    /// <summary>
    /// Phân tích nguyên nhân gốc dựa trên trạng thái tổng thể của các kênh truyền thông.
    /// </summary>
    /// <param name="sysCommOk">Giá trị cờ SYS_COMM_OK từ PLC (True = 1, False = 0).</param>
    /// <param name="wifiConfig">Thông tin trạng thái phân vùng Wi-Fi (nếu có).</param>
    /// <param name="mqttBrokerConfig">Thông tin cấu hình và trạng thái MQTT Broker (nếu có).</param>
    /// <param name="mqttLastErrorCode">Mã lỗi trả về từ thanh ghi MQTT Broker offset +90 (nếu có).</param>
    /// <param name="lastModbusRxElapsed">Thời gian đã trôi qua kể từ frame Modbus hợp lệ gần nhất.</param>
    /// <param name="isStandaloneDevice">Cờ chỉ định thiết bị được cấu hình chạy độc lập không dùng truyền thông.</param>
    /// <returns>Đối tượng kết quả chẩn đoán với thông tin nguyên nhân gốc chi tiết.</returns>
    CommDiagnosticResult Analyze(
        bool sysCommOk,
        WifiConfigDto? wifiConfig = null,
        MqttBrokerConfigDto? mqttBrokerConfig = null,
        ushort? mqttLastErrorCode = null,
        TimeSpan? lastModbusRxElapsed = null,
        bool isStandaloneDevice = false);
}
