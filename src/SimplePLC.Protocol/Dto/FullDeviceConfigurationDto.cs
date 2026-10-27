namespace SimplePLC.Protocol.Dto;

/// <summary>
/// Hình ảnh cấu hình hoàn chỉnh của thiết bị PLC được đọc ngược từ MCU.
/// Bao gồm Bảng Rule cơ sở, các khối Function Block Timer/Counter, Modbus Master Poller & Writer,
/// MQTT Telemetry & Subscription, MQTT Broker và cấu hình cổng RS-485.
/// </summary>
public sealed class FullDeviceConfigurationDto
{
    public IReadOnlyList<RuleRecordDto> Rules { get; init; } = Array.Empty<RuleRecordDto>();
    public IReadOnlyList<FbTimerRecordDto> Timers { get; init; } = Array.Empty<FbTimerRecordDto>();
    public IReadOnlyList<FbCounterRecordDto> Counters { get; init; } = Array.Empty<FbCounterRecordDto>();
    public IReadOnlyList<ModbusMasterReadConfigDto> ModbusMasterReads { get; init; } = Array.Empty<ModbusMasterReadConfigDto>();
    public IReadOnlyList<ModbusMasterWriteConfigDto> ModbusMasterWrites { get; init; } = Array.Empty<ModbusMasterWriteConfigDto>();
    public IReadOnlyList<DirectWriteConfigDto> DirectWrites { get; init; } = Array.Empty<DirectWriteConfigDto>();
    public IReadOnlyList<MqttTelemetryConfigDto> MqttTelemetries { get; init; } = Array.Empty<MqttTelemetryConfigDto>();
    public IReadOnlyList<MqttSubscriptionConfigDto> MqttSubscriptions { get; init; } = Array.Empty<MqttSubscriptionConfigDto>();
    public MqttBrokerConfigDto? BrokerConfig { get; init; }
    public MqttWillConfigDto? MqttWillConfig { get; init; }
    public DtuConfigDto? DtuConfig { get; init; }
    public IReadOnlyList<ModbusPortConfigDto> ModbusPorts { get; init; } = Array.Empty<ModbusPortConfigDto>();
    public IReadOnlyList<ModbusSlaveMapEntryDto> ModbusSlaveMaps { get; init; } = Array.Empty<ModbusSlaveMapEntryDto>();
}
