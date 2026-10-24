using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Bộ kiểm thử độc lập xác thực tính tuân thủ theo Datasheet đồng hồ đo đa năng 3 pha SELEC MFM383A-C
/// Tài liệu tham chiếu: 1028-mfm383a.pdf (Doc name: OP INST MFM383A SERIES OP623-V01).
/// Đặc tả truyền thông: RS-485 Modbus RTU, FC04 (Read Input Registers 30000), Float 32-bit (IEEE-754 Big-Endian ABCD).
/// </summary>
public class SelecMfm383aDatasheetTests
{
    private static (ushort Reg0, ushort Reg1) FloatToBigEndianRegisters(float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        ushort reg0 = (ushort)(bits >> 16);   // High word (MSW)
        ushort reg1 = (ushort)(bits & 0xFFFF); // Low word (LSW)
        return (reg0, reg1);
    }

    [Fact]
    public void Verify_DatasheetRegisterMap_HexAndOffsetAlignment()
    {
        // Trang 5/6 datasheet: Bảng "Readable parameters from MFM383A-C : [ Length (Register) : 2 ; Data Structure : Float ]"
        // Kiểm tra đối chiếu chính xác giữa Modbus 30000 Address, Hex Address và 0-based Register Offset.
        var expectedMappings = new (ushort Offset, ushort ModbusAddress, ushort HexAddress, string ParamName)[]
        {
            (0,  30000, 0x00, "Voltage V1N"),
            (2,  30002, 0x02, "Voltage V2N"),
            (4,  30004, 0x04, "Voltage V3N"),
            (6,  30006, 0x06, "Average Voltage LN"),
            (8,  30008, 0x08, "Voltage V12"),
            (10, 30010, 0x0A, "Voltage V23"),
            (12, 30012, 0x0C, "Voltage V31"),
            (14, 30014, 0x0E, "Average Voltage LL"),
            (16, 30016, 0x10, "Current I1"),
            (18, 30018, 0x12, "Current I2"),
            (20, 30020, 0x14, "Current I3"),
            (22, 30022, 0x16, "Average Current"),
            (24, 30024, 0x18, "kW1"),
            (26, 30026, 0x1A, "kW2"),
            (28, 30028, 0x1C, "kW3"),
            (30, 30030, 0x1E, "Total kW"),
            (32, 30032, 0x20, "kVA1"),
            (34, 30034, 0x22, "kVA2"),
            (36, 30036, 0x24, "kVA3"),
            (38, 30038, 0x26, "kVAr1"),
            (40, 30040, 0x28, "kVAr2"),
            (42, 30042, 0x2A, "kVAr3"),
            (44, 30044, 0x2C, "Total kVA"),
            (46, 30046, 0x2E, "Total kVAr"),
            (48, 30048, 0x30, "PF1"),
            (50, 30050, 0x32, "PF2"),
            (52, 30052, 0x34, "PF3"),
            (54, 30054, 0x36, "Average PF"),
            (56, 30056, 0x38, "Frequency"),
            (58, 30058, 0x3A, "Total Active Energy (kWh)"),
            (60, 30060, 0x3C, "Total Apparent Energy (kVAh)"),
            (62, 30062, 0x3E, "Total Reactive Energy (kVArh)")
        };

        foreach (var (offset, modbusAddr, hexAddr, paramName) in expectedMappings)
        {
            Assert.Equal(offset, hexAddr);
            Assert.Equal(30000 + offset, modbusAddr);
            Assert.False(string.IsNullOrWhiteSpace(paramName));
        }
    }

    [Theory]
    [InlineData(230.45f, 0x4366, 0x7333)]  // Điện áp pha trung bình V_avg (Offset 6)
    [InlineData(5.25f,   0x40A8, 0x0000)]  // Dòng điện trung bình I_avg (Offset 22)
    [InlineData(12.75f,  0x414C, 0x0000)]  // Tổng công suất tác dụng Total_kW (Offset 30)
    [InlineData(12345.5f,0x4640, 0xE600)]  // Tổng điện năng tiêu thụ Total_kWh (Offset 58)
    [InlineData(50.0f,   0x4248, 0x0000)]  // Tần số lưới điện 50Hz (Offset 56)
    [InlineData(0.95f,   0x3F73, 0x3333)]  // Hệ số công suất trung bình PF_avg (Offset 54)
    [InlineData(0.0f,    0x0000, 0x0000)]  // Trạng thái nghỉ / 0
    [InlineData(-15.5f,  0xC178, 0x0000)]  // Công suất phát ngược lưới (Dấu âm IEEE-754)
    public void Verify_BigEndian_ABCD_IEEE754_SinglePrecisionDecoding(float expectedValue, ushort expectedReg0, ushort expectedReg1)
    {
        // 1. Kiểm tra mã hóa bit IEEE-754 Float 32-bit
        var (actualReg0, actualReg1) = FloatToBigEndianRegisters(expectedValue);
        Assert.Equal(expectedReg0, actualReg0);
        Assert.Equal(expectedReg1, actualReg1);

        // 2. Kiểm tra bộ giải mã ModbusMasterDataParser với chế độ BigEndian_ABCD
        ushort[] registers = [actualReg0, actualReg1];
        bool success = ModbusMasterDataParser.TryParsePinValue(
            registers,
            regOffset: 0,
            dataType: ModbusDataType.Real,
            endianness: ModbusEndianness.BigEndian_ABCD,
            multiplier: 1.0f,
            out double parsedVal);

        Assert.True(success);
        Assert.Equal(expectedValue, (float)parsedVal, precision: 3);
    }

    [Fact]
    public void Verify_FullBulkReadFrame_60Registers_ExtractsAllTelemetryAccurately()
    {
        // Giả lập khung phản hồi 60 thanh ghi (120 bytes) từ đồng hồ SELEC MFM383A-C
        // Đọc liên tục từ StartAddress = 0, RegCount = 60 theo Blueprint SimplePLC
        ushort[] responseRegisters = new ushort[60];

        // Điền các giá trị đo thực tế theo đúng Register Offset trong datasheet:
        float simV1 = 229.8f;
        float simV2 = 230.1f;
        float simV3 = 231.4f;
        float simVavg = 230.43f; // Offset 6
        float simIavg = 5.25f;   // Offset 22
        float simTotalKw = 3.63f;// Offset 30
        float simPfAvg = 0.994f; // Offset 54
        float simFreq = 50.01f;  // Offset 56
        float simKwh = 87654.3f; // Offset 58

        void SetFloat(ushort offset, float val)
        {
            var (r0, r1) = FloatToBigEndianRegisters(val);
            responseRegisters[offset] = r0;
            responseRegisters[offset + 1] = r1;
        }

        SetFloat(0, simV1);
        SetFloat(2, simV2);
        SetFloat(4, simV3);
        SetFloat(6, simVavg);
        SetFloat(22, simIavg);
        SetFloat(30, simTotalKw);
        SetFloat(54, simPfAvg);
        SetFloat(56, simFreq);
        SetFloat(58, simKwh);

        // Trích xuất 4 thông số chính của Blueprint: V_avg (6), I_avg (22), Total_kW (30), Total_kWh (58)
        bool okV = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 6, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double vAvg);
        bool okI = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 22, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double iAvg);
        bool okP = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 30, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double totalKw);
        bool okE = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 58, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double totalKwh);

        // Trích xuất các thông số bổ trợ khác để kiểm tra tính toàn vẹn của bảng thanh ghi
        bool okF = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 56, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double freq);
        bool okPf = ModbusMasterDataParser.TryParsePinValue(responseRegisters, 54, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double pf);

        Assert.True(okV && okI && okP && okE && okF && okPf);

        Assert.Equal(simVavg, (float)vAvg, precision: 2);
        Assert.Equal(simIavg, (float)iAvg, precision: 2);
        Assert.Equal(simTotalKw, (float)totalKw, precision: 2);
        Assert.Equal(simKwh, (float)totalKwh, precision: 1);
        Assert.Equal(simFreq, (float)freq, precision: 2);
        Assert.Equal(simPfAvg, (float)pf, precision: 3);
    }

    [Fact]
    public void Verify_EndiannessMismatchDetection_ProvesOnlyBigEndianABCDIsDatasheetCompliant()
    {
        // Khi đồng hồ gửi 230.5V (Big-Endian ABCD: [0x4366, 0x8000]):
        ushort[] registers = [0x4366, 0x8000];

        // 1. Đúng định dạng BigEndian_ABCD:
        bool okAbcd = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double valAbcd);
        Assert.True(okAbcd);
        Assert.Equal(230.5f, (float)valAbcd, precision: 2);

        // 2. Nếu nhầm sang Word-Swap (CDAB): giá trị bị đảo sai hoàn toàn
        bool okCdab = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.Real, ModbusEndianness.MidBig_CDAB, 1.0f, out double valCdab);
        Assert.True(okCdab);
        Assert.NotEqual(230.5f, (float)valCdab, precision: 1);

        // 3. Nếu nhầm sang Byte-Swap (BADC): giá trị bị đảo sai hoàn toàn
        bool okBadc = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.Real, ModbusEndianness.ByteSwap_BADC, 1.0f, out double valBadc);
        Assert.True(okBadc);
        Assert.NotEqual(230.5f, (float)valBadc, precision: 1);
    }
}
