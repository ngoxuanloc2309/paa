namespace SimplePLC.Application.Logic.Graph;

using SimplePLC.Domain.Enums;

public enum LogicNodeKind
{
    Input = 0,
    Trigger = 1,
    Guard = 2,
    Action = 3,
    Timer = 4,
    Counter = 5,
    Scale = 6,
    Comparator = 7,
    MqttPublish = 8,
    MqttSubscribe = 9,
    ModbusRead = 10,
    ModbusWrite = 11,
    DirectWrite = 12
}

public sealed class ScaleNodeData
{
    public ushort InTagIndex { get; set; }
    public ushort? OutTagIndex { get; set; }
    public double Gain { get; set; } = 0.01;
    public double Offset { get; set; } = 0.0;
    public bool IsClamped { get; set; } = true;
    public double ClampMin { get; set; } = 0.0;
    public double ClampMax { get; set; } = 100.0;
    public int DecimalPlaces { get; set; } = 1;
    public string Unit { get; set; } = "bar";
}

public enum TimerMacroType
{
    Ton = 0,
    Tof = 1,
    Tp = 2,
    Cycle = 3
}

public enum CounterMacroType
{
    Ctu = 0, // Count Up
    Ctd = 1  // Count Down
}

public sealed class CounterNodeData
{
    public CounterMacroType Type { get; set; } = CounterMacroType.Ctu;
    public ushort CuTagIndex { get; set; }
    public ushort? ResetTagIndex { get; set; } // null if none
    public ushort CvTagIndex { get; set; }
    public int PresetValue { get; set; } = 10;
    public ushort QTagIndex { get; set; }
}

public sealed class TimerNodeData
{
    public TimerMacroType Type { get; set; } = TimerMacroType.Ton;
    public ushort InTagIndex { get; set; }
    public uint PresetMs { get; set; }
    public ushort QTagIndex { get; set; }
    public uint OnTimeMs { get; set; } = 1000;
    public uint OffTimeMs { get; set; } = 1000;
    public bool StartWithOn { get; set; } = true;
}

public sealed class InputNodeData
{
    public ushort TagIndex { get; set; }
}

public sealed class TriggerNodeData
{
    public TriggerKind Type { get; set; } = TriggerKind.OnRise;
    public CompareOperator CompareOp { get; set; } = CompareOperator.None;
    public double ThresholdLo { get; set; }
    public double ThresholdHi { get; set; }
    public uint ForMs { get; set; }
}

public sealed class GuardNodeData
{
    public ushort TagIndex { get; set; }
    public bool Negated { get; set; }
}

public sealed class ComparatorNodeData
{
    public ushort InTagIndex { get; set; }
    public CompareOperator Operator { get; set; } = CompareOperator.GreaterThan;
    public double ThresholdLo { get; set; } = 50;
    public double ThresholdHi { get; set; } = 100;
}

public sealed class ActionNodeData
{
    public ushort TargetTagIndex { get; set; }
    public ActionKind Type { get; set; } = ActionKind.SetTag;
    public int Parameter { get; set; } = 1;
}

public sealed class MqttPublishNodeData
{
    public ushort? EnTagIndex { get; set; }
    public ushort? DataTagIndex { get; set; }
    public string Topic { get; set; } = "synaptix/telemetry/data";
    public string PayloadMode { get; set; } = "JSON";
    public string PublishMode { get; set; } = "Event";
    public double Deadband { get; set; } = 0.5;
    public uint IntervalMs { get; set; } = 1000;
    public int Qos { get; set; } = 0;
    public bool Retain { get; set; } = false;
    public string Unit { get; set; } = "bar";
    public bool IncludeDevice { get; set; } = true;
    public bool IncludeTimestamp { get; set; } = true;
    public bool WrapValues { get; set; } = true;
    public List<SimplePLC.Protocol.Dto.MqttPayloadFieldDto> Fields { get; set; } = new();
    public string CustomTemplate { get; set; } = string.Empty;
}

public sealed class MqttSubscribeNodeData
{
    public ushort? EnTagIndex { get; set; }
    public string Topic { get; set; } = "synaptix/commands/inbound";
    public int Qos { get; set; } = 1;
    public SimplePLC.Protocol.Enums.MqttQueueMode QueueMode { get; set; } = SimplePLC.Protocol.Enums.MqttQueueMode.Fifo;
    public int QueueCapacity { get; set; } = 16;
    public bool IgnoreRetained { get; set; } = true;
    public uint TimeoutMs { get; set; } = 0;
    public SimplePLC.Protocol.Enums.MqttFailSafeAction FailSafeAction { get; set; } = SimplePLC.Protocol.Enums.MqttFailSafeAction.KeepLastValue;
    public List<SimplePLC.Protocol.Dto.MqttPinBindingDto> Pins { get; set; } = new();
}

public sealed class ModbusReadNodeData
{
    public byte PortIndex { get; set; } = 0;
    public ushort? EnTagIndex { get; set; }
    public ushort? TrigTagIndex { get; set; }
    public ushort? ValidTagIndex { get; set; }
    public ushort? ErrorTagIndex { get; set; }
    public ushort? ErrCodeTagIndex { get; set; }
    public byte SlaveId { get; set; } = 1;
    public SimplePLC.Protocol.Enums.ModbusMasterFunctionCode FunctionCode { get; set; } = SimplePLC.Protocol.Enums.ModbusMasterFunctionCode.ReadHoldingRegisters;
    public SimplePLC.Protocol.Enums.ModbusReadMode ReadMode { get; set; } = SimplePLC.Protocol.Enums.ModbusReadMode.Cyclic;
    public ushort StartAddress { get; set; } = 0;
    public ushort RegCount { get; set; } = 2;
    public uint PollIntervalMs { get; set; } = 500;
    public uint TimeoutMs { get; set; } = 300;
    public List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto> Pins { get; set; } = new();
}

public sealed class ModbusWriteNodeData
{
    public byte PortIndex { get; set; } = 0;
    public ushort? TrigTagIndex { get; set; }
    public byte SlaveId { get; set; } = 1;
    public SimplePLC.Protocol.Enums.ModbusWriteMode WriteMode { get; set; } = SimplePLC.Protocol.Enums.ModbusWriteMode.ChannelIndexed;
    public SimplePLC.Protocol.Enums.ModbusMasterWriteFunctionCode FunctionCode { get; set; } = SimplePLC.Protocol.Enums.ModbusMasterWriteFunctionCode.WriteSingleRegister;
    public ushort StartAddress { get; set; } = 0;
    public ushort PointCount { get; set; } = 1;
    public ushort BaseAddressCommand { get; set; } = 0x0100;
    public ushort BaseAddressParam { get; set; } = 0x0120;
    public ushort ChannelCount { get; set; } = 9;
    public bool IsExclusiveChannel { get; set; } = true;
    public SimplePLC.Protocol.Enums.ModbusWriteTriggerMode TriggerMode { get; set; } = SimplePLC.Protocol.Enums.ModbusWriteTriggerMode.OnTriggerRisingEdge;
    public uint CyclicIntervalMs { get; set; } = 500;
    public uint TimeoutMs { get; set; } = 300;
    public ushort? ChannelTagIndex { get; set; }
    public ushort? CommandTagIndex { get; set; }
    public ushort? ParamTagIndex { get; set; }
    public List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto> Pins { get; set; } = new();
}

public sealed class DirectWriteNodeData
{
    public ushort? TrigTagIndex { get; set; }
    public SimplePLC.Protocol.Enums.DirectWriteTargetPin TargetPin { get; set; } = SimplePLC.Protocol.Enums.DirectWriteTargetPin.Do0;
    public SimplePLC.Protocol.Enums.DirectWriteMode Mode { get; set; } = SimplePLC.Protocol.Enums.DirectWriteMode.DigitalOutput;
    public ushort ChannelCount { get; set; } = 18;
    public bool IsExclusiveChannel { get; set; } = true;
    public uint PulseWidthUs { get; set; } = 50;
    public uint PwmFrequencyHz { get; set; } = 1000;
    public SimplePLC.Protocol.Enums.DirectWriteTriggerMode TriggerMode { get; set; } = SimplePLC.Protocol.Enums.DirectWriteTriggerMode.OnTriggerRisingEdge;
    public uint TimeoutMs { get; set; } = 300;
    public ushort? ChannelTagIndex { get; set; }
    public ushort? ParamTagIndex { get; set; }
    public uint DefaultColorRgb { get; set; } = 0x00FF00;
}

public sealed class LogicNode
{
    public string Id { get; set; } = string.Empty;
    public LogicNodeKind Kind { get; set; }
    public int ExecutionOrder { get; set; } = 0;
    public string Label { get; set; } = string.Empty;

    public InputNodeData? InputData { get; set; }
    public TriggerNodeData? TriggerData { get; set; }
    public GuardNodeData? GuardData { get; set; }
    public ActionNodeData? ActionData { get; set; }
    public TimerNodeData? TimerData { get; set; }
    public CounterNodeData? CounterData { get; set; }
    public ScaleNodeData? ScaleData { get; set; }
    public ComparatorNodeData? ComparatorData { get; set; }
    public MqttPublishNodeData? MqttPublishData { get; set; }
    public MqttSubscribeNodeData? MqttSubscribeData { get; set; }
    public ModbusReadNodeData? ModbusReadData { get; set; }
    public ModbusWriteNodeData? ModbusWriteData { get; set; }
    public DirectWriteNodeData? DirectWriteData { get; set; }

    public static LogicNode CreateInput(string id, ushort tagIndex, string label = "") =>
        new() { Id = id, Kind = LogicNodeKind.Input, Label = label, InputData = new InputNodeData { TagIndex = tagIndex } };

    public static LogicNode CreateTrigger(
        string id,
        TriggerKind type = TriggerKind.OnRise,
        CompareOperator compareOp = CompareOperator.None,
        double thresholdLo = 0,
        double thresholdHi = 0,
        uint forMs = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Trigger,
            Label = label,
            TriggerData = new TriggerNodeData
            {
                Type = type,
                CompareOp = compareOp,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi,
                ForMs = forMs
            }
        };

    public static LogicNode CreateGuard(string id, ushort tagIndex, bool negated = false, string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Guard,
            Label = label,
            GuardData = new GuardNodeData { TagIndex = tagIndex, Negated = negated }
        };

    public static LogicNode CreateAction(
        string id,
        ushort targetTagIndex,
        ActionKind type = ActionKind.SetTag,
        int parameter = 1,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Action,
            Label = label,
            ExecutionOrder = executionOrder,
            ActionData = new ActionNodeData
            {
                TargetTagIndex = targetTagIndex,
                Type = type,
                Parameter = parameter
            }
        };

    public static LogicNode CreateTimer(
        string id,
        TimerMacroType type,
        ushort inTagIndex,
        uint presetMs,
        ushort qTagIndex,
        int executionOrder = 0,
        string label = "",
        uint onTimeMs = 1000,
        uint offTimeMs = 1000,
        bool startWithOn = true) =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Timer,
            Label = label,
            ExecutionOrder = executionOrder,
            TimerData = new TimerNodeData
            {
                Type = type,
                InTagIndex = inTagIndex,
                PresetMs = presetMs,
                QTagIndex = qTagIndex,
                OnTimeMs = onTimeMs,
                OffTimeMs = offTimeMs,
                StartWithOn = startWithOn
            }
        };

    public static LogicNode CreateCounter(
        string id,
        CounterMacroType type,
        ushort cuTagIndex,
        ushort? resetTagIndex,
        ushort cvTagIndex,
        int presetValue,
        ushort qTagIndex,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Counter,
            Label = label,
            ExecutionOrder = executionOrder,
            CounterData = new CounterNodeData
            {
                Type = type,
                CuTagIndex = cuTagIndex,
                ResetTagIndex = resetTagIndex,
                CvTagIndex = cvTagIndex,
                PresetValue = presetValue,
                QTagIndex = qTagIndex
            }
        };

    public static LogicNode CreateScale(
        string id,
        ushort inTagIndex,
        ushort? outTagIndex,
        double gain = 0.01,
        double offset = 0.0,
        bool isClamped = true,
        double clampMin = 0.0,
        double clampMax = 100.0,
        int decimalPlaces = 1,
        string unit = "bar",
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Scale,
            Label = label,
            ExecutionOrder = executionOrder,
            ScaleData = new ScaleNodeData
            {
                InTagIndex = inTagIndex,
                OutTagIndex = outTagIndex,
                Gain = gain,
                Offset = offset,
                IsClamped = isClamped,
                ClampMin = clampMin,
                ClampMax = clampMax,
                DecimalPlaces = decimalPlaces,
                Unit = unit
            }
        };

    public static LogicNode CreateComparator(
        string id,
        ushort inTagIndex,
        CompareOperator op = CompareOperator.GreaterThan,
        double thresholdLo = 50,
        double thresholdHi = 100,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Comparator,
            Label = label,
            ExecutionOrder = executionOrder,
            ComparatorData = new ComparatorNodeData
            {
                InTagIndex = inTagIndex,
                Operator = op,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi
            }
        };

    public static LogicNode CreateMqttPublish(
        string id,
        ushort? enTagIndex,
        ushort? dataTagIndex,
        string topic = "synaptix/telemetry/data",
        string publishMode = "Event",
        double deadband = 0.5,
        uint intervalMs = 1000,
        int qos = 0,
        bool retain = false,
        string payloadMode = "JSON",
        string unit = "bar",
        bool includeDevice = true,
        bool includeTimestamp = true,
        IEnumerable<SimplePLC.Protocol.Dto.MqttPayloadFieldDto>? fields = null,
        string customTemplate = "",
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.MqttPublish,
            Label = label,
            ExecutionOrder = executionOrder,
            MqttPublishData = new MqttPublishNodeData
            {
                EnTagIndex = enTagIndex,
                DataTagIndex = dataTagIndex,
                Topic = topic,
                PublishMode = publishMode,
                Deadband = deadband,
                IntervalMs = intervalMs,
                Qos = qos,
                Retain = retain,
                PayloadMode = payloadMode,
                Unit = unit,
                IncludeDevice = includeDevice,
                IncludeTimestamp = includeTimestamp,
                Fields = fields != null ? new List<SimplePLC.Protocol.Dto.MqttPayloadFieldDto>(fields) : new(),
                CustomTemplate = customTemplate
            }
        };

    public static LogicNode CreateMqttSubscribe(
        string id,
        ushort? enTag = null,
        string topic = "synaptix/commands/inbound",
        int qos = 1,
        SimplePLC.Protocol.Enums.MqttQueueMode queueMode = SimplePLC.Protocol.Enums.MqttQueueMode.Fifo,
        int queueCapacity = 16,
        bool ignoreRetained = true,
        uint timeoutMs = 0,
        SimplePLC.Protocol.Enums.MqttFailSafeAction failSafeAction = SimplePLC.Protocol.Enums.MqttFailSafeAction.KeepLastValue,
        IEnumerable<SimplePLC.Protocol.Dto.MqttPinBindingDto>? pins = null,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.MqttSubscribe,
            ExecutionOrder = executionOrder,
            Label = label,
            MqttSubscribeData = new MqttSubscribeNodeData
            {
                EnTagIndex = enTag,
                Topic = topic,
                Qos = qos,
                QueueMode = queueMode,
                QueueCapacity = queueCapacity,
                IgnoreRetained = ignoreRetained,
                TimeoutMs = timeoutMs,
                FailSafeAction = failSafeAction,
                Pins = pins != null ? new List<SimplePLC.Protocol.Dto.MqttPinBindingDto>(pins) : new()
            }
        };

    public static LogicNode CreateModbusRead(
        string id,
        ushort? enTag = null,
        byte slaveId = 1,
        SimplePLC.Protocol.Enums.ModbusMasterFunctionCode functionCode = SimplePLC.Protocol.Enums.ModbusMasterFunctionCode.ReadHoldingRegisters,
        ushort startAddress = 0,
        ushort regCount = 2,
        uint pollIntervalMs = 500,
        uint timeoutMs = 300,
        IEnumerable<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>? pins = null,
        byte portIndex = 0,
        int executionOrder = 0,
        string label = "",
        ushort? validTag = null,
        ushort? errorTag = null,
        ushort? errCodeTag = null) =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.ModbusRead,
            ExecutionOrder = executionOrder,
            Label = label,
            ModbusReadData = new ModbusReadNodeData
            {
                PortIndex = portIndex,
                EnTagIndex = enTag,
                ValidTagIndex = validTag,
                ErrorTagIndex = errorTag,
                ErrCodeTagIndex = errCodeTag,
                SlaveId = slaveId,
                FunctionCode = functionCode,
                StartAddress = startAddress,
                RegCount = regCount,
                PollIntervalMs = pollIntervalMs,
                TimeoutMs = timeoutMs,
                Pins = pins != null ? new List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>(pins) : new()
            }
        };

    public static LogicNode CreateModbusWrite(
        string id,
        ushort? trigTag = null,
        byte slaveId = 1,
        SimplePLC.Protocol.Enums.ModbusWriteMode writeMode = SimplePLC.Protocol.Enums.ModbusWriteMode.ChannelIndexed,
        SimplePLC.Protocol.Enums.ModbusMasterWriteFunctionCode functionCode = SimplePLC.Protocol.Enums.ModbusMasterWriteFunctionCode.WriteSingleRegister,
        ushort startAddress = 0,
        ushort pointCount = 1,
        ushort baseAddressCommand = 0x0100,
        ushort baseAddressParam = 0x0120,
        ushort channelCount = 9,
        bool isExclusiveChannel = true,
        SimplePLC.Protocol.Enums.ModbusWriteTriggerMode triggerMode = SimplePLC.Protocol.Enums.ModbusWriteTriggerMode.OnTriggerRisingEdge,
        uint cyclicIntervalMs = 500,
        uint timeoutMs = 300,
        ushort? channelTag = null,
        ushort? commandTag = null,
        ushort? paramTag = null,
        byte portIndex = 0,
        int executionOrder = 0,
        string label = "",
        IEnumerable<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>? pins = null) =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.ModbusWrite,
            ExecutionOrder = executionOrder,
            Label = label,
            ModbusWriteData = new ModbusWriteNodeData
            {
                PortIndex = portIndex,
                TrigTagIndex = trigTag,
                SlaveId = slaveId,
                WriteMode = writeMode,
                FunctionCode = functionCode,
                StartAddress = startAddress,
                PointCount = pointCount,
                BaseAddressCommand = baseAddressCommand,
                BaseAddressParam = baseAddressParam,
                ChannelCount = channelCount,
                IsExclusiveChannel = isExclusiveChannel,
                TriggerMode = triggerMode,
                CyclicIntervalMs = cyclicIntervalMs,
                TimeoutMs = timeoutMs,
                ChannelTagIndex = channelTag,
                CommandTagIndex = commandTag,
                ParamTagIndex = paramTag,
                Pins = pins != null ? new List<SimplePLC.Protocol.Dto.ModbusMasterPinBindingDto>(pins) : new()
            }
        };

    public static LogicNode CreateDirectWrite(
        string id,
        ushort? trigTag = null,
        SimplePLC.Protocol.Enums.DirectWriteTargetPin targetPin = SimplePLC.Protocol.Enums.DirectWriteTargetPin.Do0,
        SimplePLC.Protocol.Enums.DirectWriteMode mode = SimplePLC.Protocol.Enums.DirectWriteMode.DigitalOutput,
        ushort channelCount = 18,
        bool isExclusiveChannel = true,
        uint pulseWidthUs = 50,
        uint pwmFrequencyHz = 1000,
        SimplePLC.Protocol.Enums.DirectWriteTriggerMode triggerMode = SimplePLC.Protocol.Enums.DirectWriteTriggerMode.OnTriggerRisingEdge,
        uint timeoutMs = 300,
        ushort? channelTag = null,
        ushort? paramTag = null,
        uint defaultColorRgb = 0x00FF00,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.DirectWrite,
            ExecutionOrder = executionOrder,
            Label = label,
            DirectWriteData = new DirectWriteNodeData
            {
                TrigTagIndex = trigTag,
                TargetPin = targetPin,
                Mode = mode,
                ChannelCount = channelCount,
                IsExclusiveChannel = isExclusiveChannel,
                PulseWidthUs = pulseWidthUs,
                PwmFrequencyHz = pwmFrequencyHz,
                TriggerMode = triggerMode,
                TimeoutMs = timeoutMs,
                ChannelTagIndex = channelTag,
                ParamTagIndex = paramTag,
                DefaultColorRgb = defaultColorRgb
            }
        };
}

public sealed class LogicEdge
{
    public string Id { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePort { get; set; } = "Out";
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPort { get; set; } = "In";

    public LogicEdge() { }

    public LogicEdge(string sourceNodeId, string targetNodeId, string sourcePort = "Out", string targetPort = "In", string id = "")
    {
        Id = string.IsNullOrEmpty(id) ? $"{sourceNodeId}->{targetNodeId}" : id;
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        SourcePort = sourcePort;
        TargetPort = targetPort;
    }
}

public sealed class LogicGraph
{
    public List<LogicNode> Nodes { get; } = new();
    public List<LogicEdge> Edges { get; } = new();

    public LogicNode? FindNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public IEnumerable<LogicEdge> GetIncomingEdges(string targetNodeId) => Edges.Where(e => e.TargetNodeId == targetNodeId);
    public IEnumerable<LogicEdge> GetOutgoingEdges(string sourceNodeId) => Edges.Where(e => e.SourceNodeId == sourceNodeId);
}
