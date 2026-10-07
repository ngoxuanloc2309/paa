using BenchmarkDotNet.Attributes;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class SimulatorScanEngineBenchmarks
{
    private McuReferenceSimulator _simulator = null!;
    private RuleRecordDto[] _activeRules = null!;
    private ushort[] _rtcBuffer = null!;
    private ushort[] _timerBuffer = null!;
    private ushort[] _counterBuffer = null!;
    private RtcClockDto _sampleRtc = null!;
    private FbTimerRecordDto[] _sampleTimers = null!;
    private FbCounterRecordDto[] _sampleCounters = null!;
    private int _stepCounter;

    [GlobalSetup]
    public void Setup()
    {
        _simulator = new McuReferenceSimulator();
        _activeRules = new RuleRecordDto[ModbusRegisterMap.MaxRules];

        for (int i = 0; i < ModbusRegisterMap.MaxRules; i++)
        {
            _activeRules[i] = new RuleRecordDto
            {
                ThresholdLo = i * 10,
                ThresholdHi = i * 20 + 50,
                ForMs = (uint)(i % 5 * 20),
                ActionParam = i + 1,
                TriggerTag = (ushort)(i % 8),          // DI0..DI7
                ActionTag = (ushort)(8 + (i % 8)),     // DO0..DO7
                GuardTag = (ushort)(16 + (i % 16)),    // VFLAG0..VFLAG15
                Enabled = true,
                TriggerType = (SPLC_TriggerType)(1 + (i % 5)), // ON_RISE, ON_FALL, ON_CHANGE, INTERVAL, ALWAYS
                CompareOp = SPLC_CompareOp.GT,
                ActionType = SPLC_ActionType.SET_TAG
            };
        }

        // Set active rules in simulator
        for (int i = 0; i < ModbusRegisterMap.MaxRules; i++)
        {
            _simulator.Control.SetActiveRule(i, _activeRules[i]);
        }

        // RTC & Timer & Counter buffers
        _rtcBuffer = new ushort[ModbusRegisterMap.RtcClockLength];
        _timerBuffer = new ushort[ModbusRegisterMap.FbTimerTableLength];
        _counterBuffer = new ushort[ModbusRegisterMap.FbCounterTableLength];

        _sampleRtc = new RtcClockDto
        {
            EpochUtcSeconds = 1790825000,
            TimezoneOffsetMinutes = 420,
            IsSynced = true,
            HasHardwareRtc = true,
            IsBatteryLow = false
        };

        _sampleTimers = new FbTimerRecordDto[ModbusRegisterMap.FbMaxTimers];
        for (int i = 0; i < ModbusRegisterMap.FbMaxTimers; i++)
        {
            _sampleTimers[i] = new FbTimerRecordDto
            {
                StatusBits = (ushort)(i % 2 == 0 ? ModbusRegisterMap.FbTimerStatusBitIn : 0),
                Mode = (SPLC_TimerMode)(1 + (i % 3)),
                PresetMs = 5000,
                ElapsedMs = (uint)(i * 500)
            };
        }

        _sampleCounters = new FbCounterRecordDto[ModbusRegisterMap.FbMaxCounters];
        for (int i = 0; i < ModbusRegisterMap.FbMaxCounters; i++)
        {
            _sampleCounters[i] = new FbCounterRecordDto
            {
                StatusBits = (ushort)(i % 2 == 0 ? ModbusRegisterMap.FbCounterStatusBitCu : 0),
                Mode = (SPLC_CounterMode)(1 + (i % 4)),
                PresetValue = 100,
                CurrentValue = i * 10,
                RetainTagIndex = ModbusRegisterMap.FbCounterRetainNone
            };
        }
    }

    [Benchmark(Description = "Execute Full PLC Scan Pass (100 Active Rules)")]
    public void ExecuteScanPass_100Rules()
    {
        _stepCounter++;
        // Toggle input tag to trigger edge detectors every alternating run
        _simulator.Control.SetTagValue((ushort)(_stepCounter % 8), _stepCounter % 2);
        _simulator.ExecuteScanPass(20);
    }

    [Benchmark(Description = "Encode & Decode RTC Real-Time Clock")]
    public RtcClockDto RtcClock_Codec()
    {
        RegisterCodec.EncodeRtcClock(_sampleRtc, _rtcBuffer.AsSpan());
        return RegisterCodec.DecodeRtcClock(_rtcBuffer.AsSpan());
    }

    [Benchmark(Description = "Encode & Decode 8 Function Block Timers")]
    public int FbTimers_Codec()
    {
        for (int i = 0; i < _sampleTimers.Length; i++)
        {
            var slice = _timerBuffer.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock);
            FunctionBlockCodec.EncodeTimer(_sampleTimers[i], slice);
            _ = FunctionBlockCodec.DecodeTimer(slice);
        }
        return _timerBuffer[0];
    }

    [Benchmark(Description = "Encode & Decode 8 Function Block Counters")]
    public int FbCounters_Codec()
    {
        for (int i = 0; i < _sampleCounters.Length; i++)
        {
            var slice = _counterBuffer.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock);
            FunctionBlockCodec.EncodeCounter(_sampleCounters[i], slice);
            _ = FunctionBlockCodec.DecodeCounter(slice);
        }
        return _counterBuffer[0];
    }
}
