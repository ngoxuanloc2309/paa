using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Gateways;

/// <summary>
/// Cổng giao tiếp toàn diện cho bảng quy tắc (Active Table & Staging Table).
/// Hợp nhất IRuleTableReader và IRuleTableWriter theo Clean Architecture.
/// </summary>
public sealed class RuleTableGateway : IRuleTableGateway
{
    private readonly IRuleTableReader _reader;
    private readonly IRuleTableWriter _writer;

    public RuleTableGateway(IRuleTableReader reader, IRuleTableWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public RuleTableGateway(IModbusClient client)
        : this(new RuleTableReader(client), new RuleTableWriter(client))
    {
    }

    public Task<IReadOnlyList<RuleRecordDto>> ReadActiveRulesAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveRulesAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<FbTimerRecordDto>> ReadActiveTimersAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveTimersAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<FbCounterRecordDto>> ReadActiveCountersAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveCountersAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<ModbusMasterReadConfigDto>> ReadActiveModbusMasterConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveModbusMasterConfigsAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<ModbusMasterWriteConfigDto>> ReadActiveModbusMasterWriteConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveModbusMasterWriteConfigsAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<DirectWriteConfigDto>> ReadActiveDirectWriteConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveDirectWriteConfigsAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<MqttTelemetryConfigDto>> ReadActiveMqttTelemetryConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveMqttTelemetryConfigsAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<MqttSubscriptionConfigDto>> ReadActiveMqttSubscribeConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveMqttSubscribeConfigsAsync(slaveId, cancellationToken);

    public Task<MqttBrokerConfigDto?> ReadActiveMqttBrokerConfigAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveMqttBrokerConfigAsync(slaveId, cancellationToken);

    public Task<IReadOnlyList<ModbusPortConfigDto>> ReadActiveModbusPortsAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadActiveModbusPortsAsync(slaveId, cancellationToken);

    public Task<FullDeviceConfigurationDto> ReadFullDeviceConfigurationAsync(byte slaveId = 1, CancellationToken cancellationToken = default) =>
        _reader.ReadFullDeviceConfigurationAsync(slaveId, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(byte slaveId, IReadOnlyList<RuleRecordDto> rules, CancellationToken cancellationToken = default) =>
        _writer.DeployRulesAsync(slaveId, rules, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default) =>
        _writer.DeployRulesAsync(slaveId, rules, timers, counters, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers = null,
        IReadOnlyList<FbCounterRecordDto>? counters = null,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs = null,
        MqttBrokerConfigDto? brokerConfig = null,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs = null,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs = null,
        IReadOnlyList<ModbusMasterWriteConfigDto>? modbusMasterWriteConfigs = null,
        IReadOnlyList<DirectWriteConfigDto>? directWriteConfigs = null,
        IReadOnlyList<ModbusPortConfigDto>? modbusPortConfigs = null,
        IReadOnlyList<byte>? tagDataTypes = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressLogger = null) =>
        _writer.DeployRulesAsync(
            slaveId,
            rules,
            timers,
            counters,
            mqttConfigs,
            brokerConfig,
            mqttSubConfigs,
            modbusMasterConfigs,
            modbusMasterWriteConfigs,
            directWriteConfigs,
            modbusPortConfigs,
            tagDataTypes,
            cancellationToken,
            progressLogger);
}
