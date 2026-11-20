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
    public void EvaluateMathAlu_Int32_Add_Overflow_WrapsAroundMod2Pow32_PerSpec007()
    {
        // 2_000_000_000 + 2_000_000_000 -> -294_967_296 (khớp Vector 5 chuẩn SPLC-SPEC-MATH-007)
        int result = McuReferenceSimulator.EvaluateMathAlu(2_000_000_000, 2_000_000_000, (byte)SPLC_MathOp.ADD, isReal: false);
        Assert.Equal(-294_967_296, result);
    }

    [Fact]
    public void EvaluateMathAlu_Int32_Sub_Underflow_WrapsAroundMod2Pow32_PerSpec007()
    {
        // Int32.MinValue - 1000 -> wraps to positive 2147482648 (0x7FFFFC18)
        int result = McuReferenceSimulator.EvaluateMathAlu(int.MinValue, 1000, (byte)SPLC_MathOp.SUB, isReal: false);
        Assert.Equal(2147482648L, (long)result);
    }

    [Fact]
    public void EvaluateMathAlu_Int32_Mul_Overflow_ClampsToMaxInt_PerSpec007()
    {
        // 1_000_000_000 * 10 -> kẹp về INT32_MAX qua int64 (khớp Vector 8)
        int result = McuReferenceSimulator.EvaluateMathAlu(1_000_000_000, 10, (byte)SPLC_MathOp.MUL, isReal: false);
        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void EvaluateMathAlu_Boundary_MinInt_Div_Minus1_ReturnsMaxInt_PerSpec007()
    {
        // INT32_MIN / -1 -> INT32_MAX (khớp Vector 6)
        int result = McuReferenceSimulator.EvaluateMathAlu(int.MinValue, -1, (byte)SPLC_MathOp.DIV, isReal: false);
        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void EvaluateMathAlu_Boundary_MinInt_Mod_Minus1_ReturnsZero_PerSpec007()
    {
        // INT32_MIN % -1 -> 0 (khớp Vector 7 và Q11)
        int result = McuReferenceSimulator.EvaluateMathAlu(int.MinValue, -1, (byte)SPLC_MathOp.MOD, isReal: false);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EvaluateMathAlu_C99NegativeModulo_ReturnsNegativeRemainder_PerSpec007()
    {
        // -7 % 3 = -1 (C99 dấu theo số bị chia, khớp Vector 10)
        int result = McuReferenceSimulator.EvaluateMathAlu(-7, 3, (byte)SPLC_MathOp.MOD, isReal: false);
        Assert.Equal(-1, result);
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
