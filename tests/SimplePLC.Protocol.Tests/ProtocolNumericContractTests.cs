using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Phase E2: Numeric Contract Freeze Tests (Protocol Layer).
/// Khóa cứng các giá trị số (integer literals) của toàn bộ Protocol Enums.
/// Đảm bảo tính tương thích nhị phân 100% giữa C# App, C Header và Modbus Wire.
/// </summary>
public class ProtocolNumericContractTests
{
    [Fact]
    public void DeviceClass_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_DeviceClass.UNKNOWN);
        Assert.Equal((ushort)1, (ushort)SPLC_DeviceClass.REMOTE_IO);
        Assert.Equal((ushort)2, (ushort)SPLC_DeviceClass.DATALOGGER);
        Assert.Equal((ushort)3, (ushort)SPLC_DeviceClass.GATEWAY);
        Assert.Equal((ushort)4, (ushort)SPLC_DeviceClass.CONTROLLER);
    }

    [Fact]
    public void RemoteIoVariant_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_RemoteIoVariant.UNKNOWN);
        Assert.Equal((ushort)1, (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI);
        Assert.Equal((ushort)2, (ushort)SPLC_RemoteIoVariant.VARIANT_16DI_16DO);
    }

    [Fact]
    public void ResetReason_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_ResetReason.UNKNOWN);
        Assert.Equal((ushort)1, (ushort)SPLC_ResetReason.POWER_ON);
        Assert.Equal((ushort)2, (ushort)SPLC_ResetReason.SOFTWARE);
        Assert.Equal((ushort)3, (ushort)SPLC_ResetReason.WATCHDOG);
        Assert.Equal((ushort)4, (ushort)SPLC_ResetReason.BROWNOUT);
        Assert.Equal((ushort)5, (ushort)SPLC_ResetReason.EXTERNAL);
    }

    [Fact]
    public void HealthFlags_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_HealthFlags.NONE);
        Assert.Equal((ushort)1, (ushort)SPLC_HealthFlags.CPU_HIGH);
        Assert.Equal((ushort)2, (ushort)SPLC_HealthFlags.RAM_HIGH);
        Assert.Equal((ushort)4, (ushort)SPLC_HealthFlags.SCAN_OVERRUN);
    }

    [Fact]
    public void SystemCommand_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_SystemCommand.NONE);
        Assert.Equal((ushort)1, (ushort)SPLC_SystemCommand.REBOOT);
        Assert.Equal((ushort)2, (ushort)SPLC_SystemCommand.FACTORY_RESET);
        Assert.Equal((ushort)3, (ushort)SPLC_SystemCommand.CLEAR_RULES);
        Assert.Equal((ushort)4, (ushort)SPLC_SystemCommand.CLEAR_RETAIN);
    }

    [Fact]
    public void CommandStatus_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_CommandStatus.IDLE);
        Assert.Equal((ushort)1, (ushort)SPLC_CommandStatus.ACCEPTED);
        Assert.Equal((ushort)2, (ushort)SPLC_CommandStatus.BUSY);
        Assert.Equal((ushort)3, (ushort)SPLC_CommandStatus.DONE);
        Assert.Equal((ushort)4, (ushort)SPLC_CommandStatus.ERROR);
    }

    [Fact]
    public void ConfigStatus_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_ConfigStatus.IDLE);
        Assert.Equal((ushort)1, (ushort)SPLC_ConfigStatus.RECEIVING);
        Assert.Equal((ushort)2, (ushort)SPLC_ConfigStatus.VERIFYING);
        Assert.Equal((ushort)3, (ushort)SPLC_ConfigStatus.READY);
        Assert.Equal((ushort)4, (ushort)SPLC_ConfigStatus.ERROR);
    }

    [Fact]
    public void ErrorCode_Values_MatchExactLiterals()
    {
        Assert.Equal((ushort)0, (ushort)SPLC_ErrorCode.NONE);
        Assert.Equal((ushort)1, (ushort)SPLC_ErrorCode.INVALID_COMMAND);
        Assert.Equal((ushort)2, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
        Assert.Equal((ushort)3, (ushort)SPLC_ErrorCode.BUSY);
        Assert.Equal((ushort)4, (ushort)SPLC_ErrorCode.CRC_MISMATCH);
        Assert.Equal((ushort)5, (ushort)SPLC_ErrorCode.UNSUPPORTED);
        Assert.Equal((ushort)6, (ushort)SPLC_ErrorCode.FLASH);
    }

    [Fact]
    public void TriggerType_Values_MatchExactLiterals()
    {
        Assert.Equal((byte)0, (byte)SPLC_TriggerType.ON_CHANGE);
        Assert.Equal((byte)1, (byte)SPLC_TriggerType.ON_RISE);
        Assert.Equal((byte)2, (byte)SPLC_TriggerType.ON_FALL);
        Assert.Equal((byte)3, (byte)SPLC_TriggerType.TIME_WINDOW);
        Assert.Equal((byte)4, (byte)SPLC_TriggerType.INTERVAL);
    }

    [Fact]
    public void CompareOp_Values_MatchExactLiterals()
    {
        Assert.Equal((byte)0, (byte)SPLC_CompareOp.NONE);
        Assert.Equal((byte)1, (byte)SPLC_CompareOp.EQ);
        Assert.Equal((byte)2, (byte)SPLC_CompareOp.NEQ);
        Assert.Equal((byte)3, (byte)SPLC_CompareOp.GT);
        Assert.Equal((byte)4, (byte)SPLC_CompareOp.LT);
        Assert.Equal((byte)5, (byte)SPLC_CompareOp.GTE);
        Assert.Equal((byte)6, (byte)SPLC_CompareOp.LTE);
        Assert.Equal((byte)7, (byte)SPLC_CompareOp.BETWEEN);
    }

    [Fact]
    public void ActionType_Values_MatchExactLiterals()
    {
        Assert.Equal((byte)0, (byte)SPLC_ActionType.SET_TAG);
        Assert.Equal((byte)1, (byte)SPLC_ActionType.TOGGLE_TAG);
        Assert.Equal((byte)2, (byte)SPLC_ActionType.INC_COUNTER);
        Assert.Equal((byte)3, (byte)SPLC_ActionType.WRITE_REMOTE);
        Assert.Equal((byte)4, (byte)SPLC_ActionType.LOG_EVENT);
        Assert.Equal((byte)5, (byte)SPLC_ActionType.SEND_ALARM);
        Assert.Equal((byte)6, (byte)SPLC_ActionType.ADD_TAG);
        Assert.Equal((byte)7, (byte)SPLC_ActionType.SCALE_TAG);
    }

    [Fact]
    public void ModbusExceptionCode_Values_MatchStandardModbusProtocol()
    {
        Assert.Equal((byte)0x00, (byte)ModbusExceptionCode.None);
        Assert.Equal((byte)0x01, (byte)ModbusExceptionCode.IllegalFunction);
        Assert.Equal((byte)0x02, (byte)ModbusExceptionCode.IllegalDataAddress);
        Assert.Equal((byte)0x03, (byte)ModbusExceptionCode.IllegalDataValue);
        Assert.Equal((byte)0x04, (byte)ModbusExceptionCode.SlaveDeviceFailure);
        Assert.Equal((byte)0x05, (byte)ModbusExceptionCode.Acknowledge);
        Assert.Equal((byte)0x06, (byte)ModbusExceptionCode.SlaveDeviceBusy);
        Assert.Equal((byte)0x07, (byte)ModbusExceptionCode.NegativeAcknowledge);
        Assert.Equal((byte)0x08, (byte)ModbusExceptionCode.MemoryParityError);
        Assert.Equal((byte)0x0A, (byte)ModbusExceptionCode.GatewayPathUnavailable);
        Assert.Equal((byte)0x0B, (byte)ModbusExceptionCode.GatewayTargetDeviceFailedToRespond);
    }
}
