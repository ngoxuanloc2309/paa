using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Phase E2: Domain Numeric Contract & Protocol 1:1 Mapping Freeze Tests.
/// Khóa cứng các giá trị số của Domain Enums và đảm bảo tính tương thích ép kiểu trực tiếp (cast)
/// giữa Domain Enums và Protocol Wire Enums (R4, R8).
/// </summary>
public class DomainNumericContractAndMappingFreezeTests
{
    [Fact]
    public void TagKind_Values_MatchExactPlatformV1Literals()
    {
        Assert.Equal((byte)0, (byte)TagKind.None);
        Assert.Equal((byte)1, (byte)TagKind.DiscreteInput);
        Assert.Equal((byte)2, (byte)TagKind.DiscreteOutput);
        Assert.Equal((byte)3, (byte)TagKind.AnalogInput);
        Assert.Equal((byte)4, (byte)TagKind.VirtualFlag);
        Assert.Equal((byte)5, (byte)TagKind.VirtualRegister);
        Assert.Equal((byte)6, (byte)TagKind.ModbusCoil);
        Assert.Equal((byte)7, (byte)TagKind.ModbusHolding);
        Assert.Equal((byte)8, (byte)TagKind.VirtualRegisterRetain);
        Assert.Equal((byte)9, (byte)TagKind.Counter);
    }

    [Fact]
    public void TriggerKind_Values_MatchExactLiterals_AndRoundtripToProtocol()
    {
        Assert.Equal(0, (int)TriggerKind.OnChange);
        Assert.Equal(1, (int)TriggerKind.OnRise);
        Assert.Equal(2, (int)TriggerKind.OnFall);
        Assert.Equal(3, (int)TriggerKind.TimeWindow);
        Assert.Equal(4, (int)TriggerKind.Interval);

        // 1:1 Cast Roundtrip Guarantee
        Assert.Equal(TriggerKind.OnChange, (TriggerKind)SPLC_TriggerType.ON_CHANGE);
        Assert.Equal(TriggerKind.OnRise, (TriggerKind)SPLC_TriggerType.ON_RISE);
        Assert.Equal(TriggerKind.OnFall, (TriggerKind)SPLC_TriggerType.ON_FALL);
        Assert.Equal(TriggerKind.TimeWindow, (TriggerKind)SPLC_TriggerType.TIME_WINDOW);
        Assert.Equal(TriggerKind.Interval, (TriggerKind)SPLC_TriggerType.INTERVAL);

        Assert.Equal(SPLC_TriggerType.ON_CHANGE, (SPLC_TriggerType)TriggerKind.OnChange);
        Assert.Equal(SPLC_TriggerType.ON_RISE, (SPLC_TriggerType)TriggerKind.OnRise);
        Assert.Equal(SPLC_TriggerType.ON_FALL, (SPLC_TriggerType)TriggerKind.OnFall);
        Assert.Equal(SPLC_TriggerType.TIME_WINDOW, (SPLC_TriggerType)TriggerKind.TimeWindow);
        Assert.Equal(SPLC_TriggerType.INTERVAL, (SPLC_TriggerType)TriggerKind.Interval);
    }

    [Fact]
    public void CompareOperator_Values_MatchExactLiterals_AndRoundtripToProtocol()
    {
        Assert.Equal(0, (int)CompareOperator.None);
        Assert.Equal(1, (int)CompareOperator.Equal);
        Assert.Equal(2, (int)CompareOperator.NotEqual);
        Assert.Equal(3, (int)CompareOperator.GreaterThan);
        Assert.Equal(4, (int)CompareOperator.LessThan);
        Assert.Equal(5, (int)CompareOperator.GreaterThanOrEqual);
        Assert.Equal(6, (int)CompareOperator.LessThanOrEqual);
        Assert.Equal(7, (int)CompareOperator.Between);

        // 1:1 Cast Roundtrip Guarantee
        Assert.Equal(CompareOperator.None, (CompareOperator)SPLC_CompareOp.NONE);
        Assert.Equal(CompareOperator.Equal, (CompareOperator)SPLC_CompareOp.EQ);
        Assert.Equal(CompareOperator.NotEqual, (CompareOperator)SPLC_CompareOp.NEQ);
        Assert.Equal(CompareOperator.GreaterThan, (CompareOperator)SPLC_CompareOp.GT);
        Assert.Equal(CompareOperator.LessThan, (CompareOperator)SPLC_CompareOp.LT);
        Assert.Equal(CompareOperator.GreaterThanOrEqual, (CompareOperator)SPLC_CompareOp.GTE);
        Assert.Equal(CompareOperator.LessThanOrEqual, (CompareOperator)SPLC_CompareOp.LTE);
        Assert.Equal(CompareOperator.Between, (CompareOperator)SPLC_CompareOp.BETWEEN);

        Assert.Equal(SPLC_CompareOp.NONE, (SPLC_CompareOp)CompareOperator.None);
        Assert.Equal(SPLC_CompareOp.EQ, (SPLC_CompareOp)CompareOperator.Equal);
        Assert.Equal(SPLC_CompareOp.NEQ, (SPLC_CompareOp)CompareOperator.NotEqual);
        Assert.Equal(SPLC_CompareOp.GT, (SPLC_CompareOp)CompareOperator.GreaterThan);
        Assert.Equal(SPLC_CompareOp.LT, (SPLC_CompareOp)CompareOperator.LessThan);
        Assert.Equal(SPLC_CompareOp.GTE, (SPLC_CompareOp)CompareOperator.GreaterThanOrEqual);
        Assert.Equal(SPLC_CompareOp.LTE, (SPLC_CompareOp)CompareOperator.LessThanOrEqual);
        Assert.Equal(SPLC_CompareOp.BETWEEN, (SPLC_CompareOp)CompareOperator.Between);
    }

    [Fact]
    public void ActionKind_Values_MatchExactLiterals_AndRoundtripToProtocol()
    {
        Assert.Equal(0, (int)ActionKind.SetTag);
        Assert.Equal(1, (int)ActionKind.ToggleTag);
        Assert.Equal(2, (int)ActionKind.IncrementCounter);
        Assert.Equal(3, (int)ActionKind.WriteRemote);
        Assert.Equal(4, (int)ActionKind.LogEvent);
        Assert.Equal(5, (int)ActionKind.SendAlarm);
        Assert.Equal(6, (int)ActionKind.AddTag);
        Assert.Equal(7, (int)ActionKind.ScaleTag);

        // 1:1 Cast Roundtrip Guarantee
        Assert.Equal(ActionKind.SetTag, (ActionKind)SPLC_ActionType.SET_TAG);
        Assert.Equal(ActionKind.ToggleTag, (ActionKind)SPLC_ActionType.TOGGLE_TAG);
        Assert.Equal(ActionKind.IncrementCounter, (ActionKind)SPLC_ActionType.INC_COUNTER);
        Assert.Equal(ActionKind.WriteRemote, (ActionKind)SPLC_ActionType.WRITE_REMOTE);
        Assert.Equal(ActionKind.LogEvent, (ActionKind)SPLC_ActionType.LOG_EVENT);
        Assert.Equal(ActionKind.SendAlarm, (ActionKind)SPLC_ActionType.SEND_ALARM);
        Assert.Equal(ActionKind.AddTag, (ActionKind)SPLC_ActionType.ADD_TAG);
        Assert.Equal(ActionKind.ScaleTag, (ActionKind)SPLC_ActionType.SCALE_TAG);

        Assert.Equal(SPLC_ActionType.SET_TAG, (SPLC_ActionType)ActionKind.SetTag);
        Assert.Equal(SPLC_ActionType.TOGGLE_TAG, (SPLC_ActionType)ActionKind.ToggleTag);
        Assert.Equal(SPLC_ActionType.INC_COUNTER, (SPLC_ActionType)ActionKind.IncrementCounter);
        Assert.Equal(SPLC_ActionType.WRITE_REMOTE, (SPLC_ActionType)ActionKind.WriteRemote);
        Assert.Equal(SPLC_ActionType.LOG_EVENT, (SPLC_ActionType)ActionKind.LogEvent);
        Assert.Equal(SPLC_ActionType.SEND_ALARM, (SPLC_ActionType)ActionKind.SendAlarm);
        Assert.Equal(SPLC_ActionType.ADD_TAG, (SPLC_ActionType)ActionKind.AddTag);
        Assert.Equal(SPLC_ActionType.SCALE_TAG, (SPLC_ActionType)ActionKind.ScaleTag);
    }
}
