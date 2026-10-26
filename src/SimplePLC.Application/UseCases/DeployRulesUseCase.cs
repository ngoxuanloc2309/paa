namespace SimplePLC.Application.UseCases;

using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Mapping;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Dto;

public sealed class DeployRulesUseCase
{
    private readonly IRuleTableWriter _ruleTableWriter;
    private readonly IDeviceOperationCoordinator? _coordinator;

    public DeployRulesUseCase(
        IRuleTableWriter ruleTableWriter,
        IDeviceOperationCoordinator? coordinator = null)
    {
        _ruleTableWriter = ruleTableWriter ?? throw new ArgumentNullException(nameof(ruleTableWriter));
        _coordinator = coordinator;
    }

    public Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        return ExecuteAsync(ruleTable, timers: null, counters: null, slaveId, ct);
    }

    public Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        return ExecuteAsync(ruleTable, timers, counters, null, slaveId, ct);
    }

    public Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        return ExecuteAsync(ruleTable, timers, counters, mqttConfigs, null, slaveId, ct);
    }

    public Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        byte slaveId = 1,
        CancellationToken ct = default)
    {
        return ExecuteAsync(ruleTable, timers, counters, mqttConfigs, brokerConfig, null, slaveId, ct);
    }

    public Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        byte slaveId = 1,
        CancellationToken ct = default) =>
        ExecuteAsync(ruleTable, timers, counters, mqttConfigs, brokerConfig, mqttSubConfigs, null, null, null, null, null, null, slaveId, ct);

    public async Task<DeployRulesResult> ExecuteAsync(
        RuleTable ruleTable,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs,
        IReadOnlyList<ModbusMasterWriteConfigDto>? modbusMasterWriteConfigs = null,
        IReadOnlyList<DirectWriteConfigDto>? directWriteConfigs = null,
        IReadOnlyList<ModbusPortConfigDto>? modbusPortConfigs = null,
        IReadOnlyList<ModbusSlaveMapEntryDto>? slaveMapConfigs = null,
        IReadOnlyList<byte>? tagDataTypes = null,
        byte slaveId = 1,
        CancellationToken ct = default,
        Action<string>? progressLogger = null)
    {
        ArgumentNullException.ThrowIfNull(ruleTable);

        // 1. Thẩm định Domain Invariants (R1)
        var validationResult = RuleTableValidator.Validate(ruleTable);
        if (!validationResult.IsValid)
        {
            return DeployRulesResult.DomainValidationError(validationResult.Errors);
        }

        // 2. Chuyển đổi Domain Rule sang Wire DTO (R4, R8, R9)
        var dtos = RuleMapper.ToDtoArray(ruleTable);

        // 3. Thực hiện nạp qua Staging/Commit (R10) với Exclusive Lease
        IAsyncDisposable? lease = null;
        if (_coordinator != null)
        {
            lease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DeployRules, ct).ConfigureAwait(false);
        }

        RuleDeployResult deployResult;
        try
        {
            deployResult = await _ruleTableWriter.DeployRulesAsync(
                slaveId,
                dtos,
                timers,
                counters,
                mqttConfigs,
                brokerConfig,
                mqttSubConfigs,
                modbusMasterConfigs,
                modbusMasterWriteConfigs,
                directWriteConfigs,
                modbusPortConfigs,
                slaveMapConfigs,
                tagDataTypes,
                ct,
                progressLogger).ConfigureAwait(false);
        }
        finally
        {
            if (lease != null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }

        // 4. Kiểm tra kết quả MCU phản hồi
        if (deployResult.IsSuccess)
        {
            return DeployRulesResult.Success((ushort)dtos.Length, deployResult.ActiveVersion);
        }

        return DeployRulesResult.HardwareError(
            deployResult.ErrorCode,
            string.IsNullOrWhiteSpace(deployResult.ErrorMessage)
                ? $"MCU returned error code {deployResult.ErrorCode}"
                : deployResult.ErrorMessage,
            deployResult.ActiveVersion);
    }
}
