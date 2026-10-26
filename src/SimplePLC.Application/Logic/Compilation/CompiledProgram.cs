namespace SimplePLC.Application.Logic.Compilation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

public sealed class CompiledProgram
{
    public string ProgramId { get; }
    public IReadOnlyList<Rule> Rules { get; }
    public DateTimeOffset CompiledAt { get; }
    public ushort RuleCount => (ushort)Rules.Count;
    public IReadOnlyDictionary<int, GeneratedRuleOrigin> SourceMap { get; }
    public IReadOnlyList<FbTimerRecordDto> FunctionBlockTimers { get; }
    public IReadOnlyList<FbCounterRecordDto> FunctionBlockCounters { get; }
    public IReadOnlyList<MqttTelemetryConfigDto> MqttTelemetryConfigs { get; }
    public IReadOnlyList<MqttSubscriptionConfigDto> MqttSubscriptionConfigs { get; }
    public IReadOnlyList<ModbusMasterReadConfigDto> ModbusMasterConfigs { get; }
    public IReadOnlyList<ModbusMasterWriteConfigDto> ModbusMasterWriteConfigs { get; }
    public IReadOnlyList<DirectWriteConfigDto> DirectWriteConfigs { get; }
    public IReadOnlyList<ModbusSlaveMapEntryDto> ModbusSlaveMapConfigs { get; }

    public CompiledProgram(
        string programId,
        IReadOnlyList<Rule> rules,
        DateTimeOffset compiledAt,
        IReadOnlyDictionary<int, GeneratedRuleOrigin>? sourceMap = null,
        IReadOnlyList<FbTimerRecordDto>? fbTimers = null,
        IReadOnlyList<FbCounterRecordDto>? fbCounters = null,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs = null,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs = null,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs = null,
        IReadOnlyList<ModbusMasterWriteConfigDto>? modbusMasterWriteConfigs = null,
        IReadOnlyList<DirectWriteConfigDto>? directWriteConfigs = null,
        IReadOnlyList<ModbusSlaveMapEntryDto>? modbusSlaveMapConfigs = null)
    {
        ProgramId = programId ?? Guid.NewGuid().ToString("N");
        Rules = rules ?? Array.Empty<Rule>();
        CompiledAt = compiledAt;
        SourceMap = sourceMap ?? new Dictionary<int, GeneratedRuleOrigin>();
        FunctionBlockTimers = fbTimers ?? Array.Empty<FbTimerRecordDto>();
        FunctionBlockCounters = fbCounters ?? Array.Empty<FbCounterRecordDto>();
        MqttTelemetryConfigs = mqttConfigs ?? Array.Empty<MqttTelemetryConfigDto>();
        MqttSubscriptionConfigs = mqttSubConfigs ?? Array.Empty<MqttSubscriptionConfigDto>();
        ModbusMasterConfigs = modbusMasterConfigs ?? Array.Empty<ModbusMasterReadConfigDto>();
        ModbusMasterWriteConfigs = modbusMasterWriteConfigs ?? Array.Empty<ModbusMasterWriteConfigDto>();
        DirectWriteConfigs = directWriteConfigs ?? Array.Empty<DirectWriteConfigDto>();
        ModbusSlaveMapConfigs = modbusSlaveMapConfigs ?? Array.Empty<ModbusSlaveMapEntryDto>();
    }

    public RuleTable ToRuleTable()
    {
        var table = new RuleTable();
        foreach (var rule in Rules)
        {
            table.AddRule(rule);
        }
        return table;
    }

    private string? _cachedSemanticHash;

    /// <summary>
    /// Tính toán mã băm SHA-256 xác định trên toàn bộ cấu hình logic thực thi của chương trình.
    /// Không bao gồm bất kỳ thông tin vị trí đồ họa (X, Y) hay UI metadata nào.
    /// Nếu hai bản vẽ có vị trí các khối khác nhau nhưng cấu hình logic giống hệt nhau,
    /// mã băm này sẽ hoàn toàn trùng khớp (TIA Portal / CODESYS semantic parity).
    /// </summary>
    public string ComputeSemanticHash()
    {
        if (_cachedSemanticHash != null) return _cachedSemanticHash;

        using var sha256 = SHA256.Create();
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8);

        // 1. Rules
        bw.Write(Rules.Count);
        foreach (var rule in Rules)
        {
            bw.Write(rule.RuleIndex);
            bw.Write(rule.Enabled);

            // Trigger
            bw.Write(rule.Trigger.Tag.Name);
            bw.Write(rule.Trigger.Tag.TagIndex);
            bw.Write((int)rule.Trigger.Type);
            bw.Write(rule.Trigger.ThresholdLo);
            bw.Write(rule.Trigger.ThresholdHi);
            bw.Write(rule.Trigger.ForMs);
            bw.Write((int)rule.Trigger.CompareOp);
            bw.Write(rule.Trigger.IsFloat);

            // Guard
            bw.Write(rule.Guard.HasGuard);
            bw.Write(rule.Guard.Negated);
            bw.Write(rule.Guard.Tag?.Name ?? string.Empty);
            bw.Write(rule.Guard.Tag?.TagIndex ?? 0);

            // Action
            bw.Write(rule.Action.TargetTag.Name);
            bw.Write(rule.Action.TargetTag.TagIndex);
            bw.Write((int)rule.Action.Type);
            bw.Write(rule.Action.Parameter);
            bw.Write(rule.Action.IsFloat);
        }

        // 2. FunctionBlockTimers
        bw.Write(FunctionBlockTimers.Count);
        foreach (var t in FunctionBlockTimers)
        {
            bw.Write((ushort)t.Mode);
            bw.Write(t.PresetMs);
            bw.Write(t.RuleRef);
            bw.Write(t.StatusBits);
        }

        // 3. FunctionBlockCounters
        bw.Write(FunctionBlockCounters.Count);
        foreach (var c in FunctionBlockCounters)
        {
            bw.Write((ushort)c.Mode);
            bw.Write(c.PresetValue);
            bw.Write(c.RetainTagIndex);
            bw.Write(c.StatusBits);
        }

        // 4. MqttTelemetryConfigs
        bw.Write(MqttTelemetryConfigs.Count);
        foreach (var m in MqttTelemetryConfigs)
        {
            bw.Write(m.DataTagIndex ?? 0xFFFF);
            bw.Write(m.EnTagIndex ?? 0xFFFF);
            bw.Write(m.Topic ?? string.Empty);
            bw.Write(m.PayloadMode ?? string.Empty);
            bw.Write(m.PublishMode ?? string.Empty);
            bw.Write(m.Qos);
            bw.Write(m.Retain);
            bw.Write(m.Deadband);
            bw.Write(m.IntervalMs);
            bw.Write(m.IncludeDevice);
            bw.Write(m.IncludeTimestamp);
            bw.Write(m.CustomTemplate ?? string.Empty);
            bw.Write(m.Fields?.Count ?? 0);
            if (m.Fields != null)
            {
                foreach (var f in m.Fields)
                {
                    bw.Write(f.Key ?? string.Empty);
                    bw.Write(f.TagIndex);
                    bw.Write(f.Unit ?? string.Empty);
                }
            }
        }

        // 5. MqttSubscriptionConfigs
        bw.Write(MqttSubscriptionConfigs.Count);
        foreach (var msSub in MqttSubscriptionConfigs)
        {
            bw.Write(msSub.Topic ?? string.Empty);
            bw.Write(msSub.Qos);
            bw.Write((int)msSub.QueueMode);
            bw.Write(msSub.QueueCapacity);
            bw.Write(msSub.IgnoreRetainedOnBoot);
            bw.Write((int)msSub.FailSafeAction);
            bw.Write(msSub.TimeoutMs);
            bw.Write(msSub.Pins?.Count ?? 0);
            if (msSub.Pins != null)
            {
                foreach (var p in msSub.Pins)
                {
                    bw.Write(p.Id ?? string.Empty);
                    bw.Write(p.Label ?? string.Empty);
                    bw.Write(p.JsonPath ?? string.Empty);
                    bw.Write((int)p.DataType);
                    bw.Write(p.TargetTagIndex ?? 0xFFFF);
                    bw.Write(p.DefaultValue);
                }
            }
        }

        // 6. ModbusMasterConfigs
        bw.Write(ModbusMasterConfigs.Count);
        foreach (var mb in ModbusMasterConfigs)
        {
            bw.Write(mb.BlockIndex);
            bw.Write(mb.PortIndex);
            bw.Write(mb.Enabled);
            bw.Write(mb.SlaveId);
            bw.Write((int)mb.FunctionCode);
            bw.Write(mb.StartAddress);
            bw.Write(mb.RegCount);
            bw.Write(mb.PollIntervalMs);
            bw.Write(mb.TimeoutMs);
            bw.Write((int)mb.ReadMode);
            bw.Write(mb.EnTagIndex ?? 0xFFFF);
            bw.Write(mb.TrigTagIndex ?? 0xFFFF);
            bw.Write(mb.ValidTagIndex ?? 0xFFFF);
            bw.Write(mb.ErrorTagIndex ?? 0xFFFF);
            bw.Write(mb.ErrCodeTagIndex ?? 0xFFFF);
            bw.Write(mb.Pins?.Count ?? 0);
            if (mb.Pins != null)
            {
                foreach (var p in mb.Pins)
                {
                    bw.Write(p.Id ?? string.Empty);
                    bw.Write(p.Label ?? string.Empty);
                    bw.Write(p.RegOffset);
                    bw.Write((int)p.DataType);
                    bw.Write(p.TargetTagIndex ?? 0xFFFF);
                }
            }
        }

        // 7. ModbusMasterWriteConfigs
        bw.Write(ModbusMasterWriteConfigs.Count);
        foreach (var mbw in ModbusMasterWriteConfigs)
        {
            bw.Write(mbw.BlockIndex);
            bw.Write(mbw.PortIndex);
            bw.Write(mbw.Enabled);
            bw.Write(mbw.SlaveId);
            bw.Write((int)mbw.WriteMode);
            bw.Write((int)mbw.FunctionCode);
            bw.Write(mbw.StartAddress);
            bw.Write(mbw.PointCount);
            bw.Write(mbw.BaseAddressCommand);
            bw.Write(mbw.BaseAddressParam);
            bw.Write(mbw.ChannelCount);
            bw.Write(mbw.IsExclusiveChannel);
            bw.Write((int)mbw.TriggerMode);
            bw.Write(mbw.CyclicIntervalMs);
            bw.Write(mbw.TimeoutMs);
            bw.Write(mbw.Deadband);
            bw.Write(mbw.TrigTagIndex ?? 0xFFFF);
            bw.Write(mbw.DoneTagIndex ?? 0xFFFF);
            bw.Write(mbw.BusyTagIndex ?? 0xFFFF);
            bw.Write(mbw.ErrorTagIndex ?? 0xFFFF);
            bw.Write(mbw.ErrCodeTagIndex ?? 0xFFFF);
            bw.Write(mbw.ChannelTagIndex ?? 0xFFFF);
            bw.Write(mbw.CommandTagIndex ?? 0xFFFF);
        }

        // 8. DirectWriteConfigs
        bw.Write(DirectWriteConfigs.Count);
        foreach (var dw in DirectWriteConfigs)
        {
            bw.Write((int)dw.TargetPin);
            bw.Write((int)dw.Mode);
            bw.Write(dw.ChannelCount);
            bw.Write(dw.IsExclusiveChannel);
            bw.Write(dw.PulseWidthUs);
            bw.Write(dw.PwmFrequencyHz);
            bw.Write((int)dw.TriggerMode);
        }

        bw.Flush();
        ms.Position = 0;
        var hashBytes = sha256.ComputeHash(ms);
        _cachedSemanticHash = Convert.ToHexString(hashBytes);
        return _cachedSemanticHash;
    }
}

public sealed class CompileResult
{
    public CompiledProgram? Program { get; }
    public IReadOnlyList<CompileDiagnostic> Diagnostics { get; }

    public bool IsSuccess => Program != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public CompileResult(CompiledProgram? program, IEnumerable<CompileDiagnostic>? diagnostics = null)
    {
        Program = program;
        Diagnostics = diagnostics?.ToList().AsReadOnly() ?? (IReadOnlyList<CompileDiagnostic>)Array.Empty<CompileDiagnostic>();
    }

    public static CompileResult Success(CompiledProgram program, IEnumerable<CompileDiagnostic>? diagnostics = null) =>
        new(program, diagnostics);

    public static CompileResult Failure(IEnumerable<CompileDiagnostic> diagnostics) =>
        new(null, diagnostics);
}
