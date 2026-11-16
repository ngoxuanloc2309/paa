namespace SimplePLC.Application.Services;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Triển khai dịch vụ chẩn đoán phân tầng Two-Tier Diagnostics cho cờ SYS_COMM_OK.
/// Cung cấp thuật toán phân tích cây quyết định (Decision Tree) để tìm nguyên nhân gốc.
/// </summary>
public sealed class CommDiagnosticsService : ICommDiagnosticsService
{
    /// <summary>
    /// Thời gian timeout danh định của Modbus Master Watchdog (3.0 giây).
    /// </summary>
    public static readonly TimeSpan DefaultModbusTimeout = TimeSpan.FromMilliseconds(3000);

    /// <summary>
    /// Ngưỡng RSSI tối thiểu khuyến nghị cho liên kết Wi-Fi ổn định (-80 dBm).
    /// </summary>
    public const short CriticalLowRssiDbm = -80;

    /// <inheritdoc />
    public CommDiagnosticResult Analyze(
        bool sysCommOk,
        WifiConfigDto? wifiConfig = null,
        MqttBrokerConfigDto? mqttBrokerConfig = null,
        ushort? mqttLastErrorCode = null,
        TimeSpan? lastModbusRxElapsed = null,
        bool isStandaloneDevice = false)
    {
        // 1. Trường hợp truyền thông hoàn toàn thông suốt
        if (sysCommOk)
        {
            return new CommDiagnosticResult(
                isHealthy: true,
                category: CommRootCauseCategory.Healthy,
                code: "COMM_OK",
                messageEn: "Communication links healthy and active.",
                messageVi: "Đường truyền kết nối thông suốt và đang hoạt động tốt.",
                recommendationVi: "Không có lỗi. Thiết bị đang trao đổi dữ liệu bình thường.");
        }

        // 2. Trường hợp thiết bị chạy Offline độc lập trong tủ điện
        if (isStandaloneDevice)
        {
            return new CommDiagnosticResult(
                isHealthy: false,
                category: CommRootCauseCategory.OfflineStandalone,
                code: "OFFLINE_STANDALONE",
                messageEn: "Device is operating in autonomous standalone mode without supervisory links.",
                messageVi: "Thiết bị đang chạy ở chế độ tủ điện độc lập (Offline) - Không có kết nối điều khiển ngoại vi.",
                recommendationVi: "Hành vi hoàn toàn bình thường khi chạy độc lập. Không đưa cờ SYS_COMM_OK vào Guard vận hành máy.");
        }

        // 3. Kiểm tra tầng Wi-Fi nếu tính năng Wi-Fi được bật
        if (wifiConfig != null && wifiConfig.Enabled)
        {
            switch (wifiConfig.ConnectionStatus)
            {
                case ModbusRegisterMap.WifiConnAuthFailed:
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.WifiAuthFailed,
                        code: "WIFI_AUTH_FAILED",
                        messageEn: "Wi-Fi authentication failed. Incorrect password or incompatible WPA security mode.",
                        messageVi: "Xác thực Wi-Fi thất bại. Sai mật khẩu hoặc không khớp chế độ bảo mật WPA2/WPA3.",
                        recommendationVi: "Vui lòng kiểm tra và cập nhật lại mật khẩu Wi-Fi tại phân vùng cấu hình 0x1300.");

                case ModbusRegisterMap.WifiConnSsidNotFound:
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.WifiSsidNotFound,
                        code: "WIFI_SSID_NOT_FOUND",
                        messageEn: $"Wi-Fi SSID '{wifiConfig.Ssid}' was not found. Router may be powered off or out of coverage.",
                        messageVi: $"Không tìm thấy điểm truy cập Wi-Fi '{wifiConfig.Ssid}'. Router có thể bị tắt hoặc ngoài vùng phủ sóng.",
                        recommendationVi: "Kiểm tra nguồn router phát Wi-Fi, ăng-ten thiết bị và khoảng cách truyền sóng.");

                case ModbusRegisterMap.WifiConnDisconnected:
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.WifiDisconnected,
                        code: "WIFI_DISCONNECTED",
                        messageEn: "Wi-Fi subsystem is enabled but currently disconnected.",
                        messageVi: "Phân vùng Wi-Fi đã kích hoạt nhưng hiện đang ở trạng thái ngắt kết nối.",
                        recommendationVi: "Gửi lệnh kết nối Wi-Fi (CmdConnect = 2) qua thanh ghi điều khiển 0x1340.");
            }

            // Kiểm tra cảnh báo sóng yếu
            if (wifiConfig.RssiDbm < CriticalLowRssiDbm && wifiConfig.RssiDbm > -120 && wifiConfig.ConnectionStatus == ModbusRegisterMap.WifiConnGotIp)
            {
                return new CommDiagnosticResult(
                    isHealthy: false,
                    category: CommRootCauseCategory.WifiLowSignal,
                    code: "WIFI_LOW_SIGNAL",
                    messageEn: $"Wi-Fi signal strength is critically low ({wifiConfig.RssiDbm} dBm). Packet loss likely.",
                    messageVi: $"Cường độ tín hiệu Wi-Fi quá yếu ({wifiConfig.RssiDbm} dBm), có nguy cơ mất gói dữ liệu cao.",
                    recommendationVi: "Lắp thêm ăng-ten kéo dài hoặc di chuyển thiết bị lại gần bộ phát Wi-Fi.");
            }
        }

        // 4. Kiểm tra tầng MQTT nếu tính năng MQTT được bật
        if (mqttBrokerConfig != null && mqttBrokerConfig.Enabled && mqttLastErrorCode.HasValue)
        {
            switch (mqttLastErrorCode.Value)
            {
                case 1: // SPLC_MQTT_ERR_DNS_FAIL
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.MqttDnsFailed,
                        code: "MQTT_DNS_FAILED",
                        messageEn: $"Failed to resolve MQTT broker hostname '{mqttBrokerConfig.Host}'. No internet connection.",
                        messageVi: $"Không phân giải được tên miền MQTT Broker '{mqttBrokerConfig.Host}'. Mạng không có Internet.",
                        recommendationVi: "Kiểm tra đường truyền Internet của router hoặc cấu hình DNS máy chủ tại phân vùng 0x1300.");

                case 2: // SPLC_MQTT_ERR_TCP_RESET
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.MqttTcpConnectFailed,
                        code: "MQTT_TCP_CONNECT_FAILED",
                        messageEn: $"Failed to establish TCP socket with MQTT broker '{mqttBrokerConfig.Host}:{mqttBrokerConfig.Port}'.",
                        messageVi: $"Không thể thiết lập kết nối TCP tới Broker '{mqttBrokerConfig.Host}:{mqttBrokerConfig.Port}'.",
                        recommendationVi: "Kiểm tra trạng thái máy chủ Broker và đảm bảo cổng 1883/8883 không bị tường lửa chặn.");

                case 3: // SPLC_MQTT_ERR_AUTH_FAIL
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.MqttAuthRejected,
                        code: "MQTT_AUTH_REJECTED",
                        messageEn: "MQTT broker rejected credentials. Invalid username, password, or client ID.",
                        messageVi: "MQTT Broker từ chối tài khoản hoặc thông tin đăng nhập. Sai Username, Password hoặc Client ID.",
                        recommendationVi: "Kiểm tra lại thông tin tài khoản xác thực tại phân vùng cấu hình 0x0E00.");

                case 4: // SPLC_MQTT_ERR_TIMEOUT
                    return new CommDiagnosticResult(
                        isHealthy: false,
                        category: CommRootCauseCategory.MqttTimeout,
                        code: "MQTT_TIMEOUT",
                        messageEn: "MQTT broker keepalive ping timeout. Server is unresponsive.",
                        messageVi: "Quá thời gian chờ phản hồi Ping từ MQTT Broker. Máy chủ không phản hồi.",
                        recommendationVi: "Kiểm tra tải của máy chủ Broker hoặc tăng chu kỳ KeepAlive.");
            }
        }

        // 5. Kiểm tra thời gian tĩnh lặng của Modbus Master
        if (lastModbusRxElapsed.HasValue && lastModbusRxElapsed.Value > DefaultModbusTimeout)
        {
            return new CommDiagnosticResult(
                isHealthy: false,
                category: CommRootCauseCategory.ModbusTimeout,
                code: "MODBUS_TIMEOUT",
                messageEn: $"Modbus master silence timeout. {lastModbusRxElapsed.Value.TotalSeconds:F1}s elapsed since last valid frame.",
                messageVi: $"Mất liên lạc Modbus trạm chủ quá {DefaultModbusTimeout.TotalSeconds:F0}s (đã trôi qua {lastModbusRxElapsed.Value.TotalSeconds:F1}s từ gói tin cuối).",
                recommendationVi: "Kiểm tra cáp truyền thông RS-485/USB hoặc đảm bảo phần mềm SCADA/Studio đang bật chu kỳ quét.");
        }

        // 6. Trường hợp mặc định: Không có bất kỳ kênh truyền thông nào hoạt động
        return new CommDiagnosticResult(
            isHealthy: false,
            category: CommRootCauseCategory.OfflineStandalone,
            code: "COMM_INACTIVE",
            messageEn: "No active communication link detected. Master is silent and network is unconfigured.",
            messageVi: "Không có luồng truyền thông nào đang hoạt động. Trạm chủ im lặng và mạng chưa được cấu hình.",
            recommendationVi: "Kết nối cáp Modbus hoặc cấu hình Wi-Fi/MQTT nếu cần liên động điều khiển từ xa.");
    }
}
