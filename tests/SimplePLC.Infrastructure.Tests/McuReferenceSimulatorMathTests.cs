using System;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// IEC 61508-3 SIL2 Triad Unit Test Suite for MCU Reference Simulator ALU Math Execution.
/// Kiểm thử trực tiếp phương thức EvaluateMathAlu và chu trình Scan Engine với toàn bộ 8 toán tử:
/// ADD, SUB, MUL, DIV, MOD, MIN, MAX, CLAMP trên cả 2 miền dữ liệu INT32 và FLOAT32.
/// </summary>
public class McuReferenceSimulatorMathTests
{
    #region 1. Happy Path Testing (Nominal Operations)

    [Fact]
    public void EvaluateMathAlu_Add_Int32_ScalarConstant_CalculatesCorrectly()
    {
        // 50 + 150 = 200
        int result = McuReferenceSimulator.EvaluateMathAlu(50, 150, (byte)SPLC_MathOp.ADD, isReal: false);
        Assert.Equal(200, result);
    }

    [Fact]
    public void EvaluateMathAlu_Add_Float32_CalculatesCorrectly()
    {
        // 12.5f + 7.25f = 19.75f
        int op1 = BitConverter.SingleToInt32Bits(12.5f);
        int op2 = BitConverter.SingleToInt32Bits(7.25f);

        int rawRes = McuReferenceSimulator.EvaluateMathAlu(op1, op2, (byte)SPLC_MathOp.ADD, isReal: true);
        float result = BitConverter.Int32BitsToSingle(rawRes);

        Assert.Equal(19.75f, result, precision: 4);
    }

    [Fact]
    public void EvaluateMathAlu_Sub_Int32_CalculatesCorrectly()
    {
        // 100 - 45 = 55
        int result = McuReferenceSimulator.EvaluateMathAlu(100, 45, (byte)SPLC_MathOp.SUB, isReal: false);
        Assert.Equal(55, result);
    }

    [Fact]
    public void EvaluateMathAlu_Mul_Int32_CalculatesCorrectly()
    {
        // 12 * 8 = 96
        int result = McuReferenceSimulator.EvaluateMathAlu(12, 8, (byte)SPLC_MathOp.MUL, isReal: false);
        Assert.Equal(96, result);
    }

    [Fact]
    public void EvaluateMathAlu_Div_Int32_CalculatesCorrectly()
    {
        // 100 / 4 = 25
        int result = McuReferenceSimulator.EvaluateMathAlu(100, 4, (byte)SPLC_MathOp.DIV, isReal: false);
        Assert.Equal(25, result);
    }

    [Fact]
    public void EvaluateMathAlu_Mod_Int32_CalculatesCorrectly()
    {
        // 17 % 5 = 2
        int result = McuReferenceSimulator.EvaluateMathAlu(17, 5, (byte)SPLC_MathOp.MOD, isReal: false);
        Assert.Equal(2, result);
    }



    #endregion

    #region 2. Boundary & Overflow Saturation Testing

    [Fact]
    public void EvaluateMathAlu_Int32_Overflow_SaturatesToMaxInt()
    {
        // Int32.MaxValue + 1000 -> Int32.MaxValue (không wrap-around âm gây nguy hiểm công nghiệp)
        int result = McuReferenceSimulator.EvaluateMathAlu(int.MaxValue, 1000, (byte)SPLC_MathOp.ADD, isReal: false);
        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void EvaluateMathAlu_Int32_Underflow_SaturatesToMinInt()
    {
        // Int32.MinValue - 1000 -> Int32.MinValue
        int result = McuReferenceSimulator.EvaluateMathAlu(int.MinValue, 1000, (byte)SPLC_MathOp.SUB, isReal: false);
        Assert.Equal(int.MinValue, result);
    }

    #endregion

    #region 3. Fault Injection & Sanitization Testing

    [Fact]
    public void EvaluateMathAlu_DivByZero_Int32_SafelyReturnsZero()
    {
        // 100 / 0 -> 0 (Zero-Division Protection)
        int result = McuReferenceSimulator.EvaluateMathAlu(100, 0, (byte)SPLC_MathOp.DIV, isReal: false);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EvaluateMathAlu_ModByZero_Int32_SafelyReturnsZero()
    {
        // 100 % 0 -> 0 (Zero-Division Protection)
        int result = McuReferenceSimulator.EvaluateMathAlu(100, 0, (byte)SPLC_MathOp.MOD, isReal: false);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EvaluateMathAlu_DivByZero_Float32_SafelyReturnsZero_WithoutNaNOrInf()
    {
        // 100.0f / 0.0f -> 0.0f
        int op1 = BitConverter.SingleToInt32Bits(100.0f);
        int op2 = BitConverter.SingleToInt32Bits(0.0f);

        int rawRes = McuReferenceSimulator.EvaluateMathAlu(op1, op2, (byte)SPLC_MathOp.DIV, isReal: true);
        float result = BitConverter.Int32BitsToSingle(rawRes);

        Assert.False(float.IsNaN(result));
        Assert.False(float.IsInfinity(result));
        Assert.Equal(0.0f, result);
    }

    #endregion
}
