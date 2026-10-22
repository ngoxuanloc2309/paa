using SimplePLC.Protocol.Constants;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// TC-HW-MB-001: Kiểm tra hằng số bản đồ thanh ghi Modbus cho việc khai báo số lượng cổng RS-485 vật lý.
/// </summary>
public sealed class HardwareAwareRs485ProtocolTests
{
    [Fact]
    public void VerifyRegisterMapConstants_Rs485PhysicalPortCount()
    {
        // 0x0A0A nằm ngay sau HardwareCapabilities (0x0A08..0x0A09)
        Assert.Equal(0x0A0A, ModbusRegisterMap.Rs485PhysicalPortCountAddress);
        Assert.Equal(1, ModbusRegisterMap.Rs485PhysicalPortCountLength);
    }
}
