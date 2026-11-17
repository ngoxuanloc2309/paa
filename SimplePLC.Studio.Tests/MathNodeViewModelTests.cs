using System;
using System.Linq;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit Test Suite for MathNodeViewModel & MathPinBindingViewModel.
/// Kiểm thử mô hình FSM Drafting/Committed (Axiom 3), giới hạn chân (2..8),
/// công thức hiển thị, và tính toán mô phỏng Canvas an toàn.
/// </summary>
public class MathNodeViewModelTests
{
    #region 1. Pin Management & Operator Constraints

    [Fact]
    public void MathNode_InitializesWithTwoPins()
    {
        var vm = new MathNodeViewModel();

        Assert.Equal(2, vm.Pins.Count);
        Assert.Equal(2, vm.InputConnectors.Count);
        Assert.Single(vm.OutputConnectors);
        Assert.Equal("IN1", vm.Pins[0].Label);
        Assert.Equal("IN2", vm.Pins[1].Label);
    }

    [Fact]
    public void MathNode_AddPin_EnforcesMaximumEightPins()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Add };

        // Thêm từ 2 lên đến 8 pins
        for (int i = 0; i < 10; i++)
        {
            if (vm.CanAddPin) vm.AddPin();
        }

        Assert.Equal(8, vm.Pins.Count);
        Assert.False(vm.CanAddPin);
    }

    [Fact]
    public void MathNode_RemovePin_EnforcesMinimumTwoPins()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Add };

        // Thêm 2 chân thành 4
        vm.AddPin();
        vm.AddPin();
        Assert.Equal(4, vm.Pins.Count);

        // Xóa nhiều lần
        while (vm.Pins.Count > 0)
        {
            vm.RemovePin(vm.Pins.LastOrDefault());
            if (vm.Pins.Count == 2) break;
        }

        // Cố xóa tiếp khi đã ở mức 2
        vm.RemovePin(vm.Pins.Last());
        Assert.Equal(2, vm.Pins.Count);
    }

    [Fact]
    public void MathNode_SwitchToSub_PreservesMultiplePins()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Add };
        vm.AddPin();
        vm.AddPin();
        Assert.Equal(4, vm.Pins.Count);

        // Chuyển sang SUB -> giữ nguyên 4 pins và cho phép thêm/xóa chân bình thường
        vm.Operator = MathOperator.Sub;
        Assert.Equal(4, vm.Pins.Count);
        Assert.True(vm.CanAddPin);
    }

    [Fact]
    public void MathNode_SwitchToDivide_ReducesPinsToTwo()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Add };
        vm.AddPin();
        vm.AddPin();
        Assert.Equal(4, vm.Pins.Count);

        // Chuyển sang DIV -> phải tự cắt giảm về 2 chân
        vm.Operator = MathOperator.Div;
        Assert.Equal(2, vm.Pins.Count);
        Assert.False(vm.CanAddPin);
    }

    #endregion

    #region 2. Formula Expression Generation

    [Fact]
    public void MathNode_FormulaExpression_FormatsAddCorrectly()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Add };
        vm.Pins[0].IsConstant = true;
        vm.Pins[0].ConstantValue = 10;
        vm.Pins[1].IsConstant = true;
        vm.Pins[1].ConstantValue = 25;

        Assert.Contains("10 + 25", vm.FormulaExpression);
    }

    [Fact]
    public void MathNode_FormulaExpression_FormatsMulCorrectly()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Mul };
        vm.Pins[0].IsConstant = true;
        vm.Pins[0].ConstantValue = 5;
        vm.Pins[1].IsConstant = true;
        vm.Pins[1].ConstantValue = 8;

        Assert.Contains("5 * 8", vm.FormulaExpression);
    }

    [Fact]
    public void MathNode_FormulaExpression_FormatsSubCorrectly()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Sub };
        vm.AddPin(); // 3 pins
        vm.Pins[0].IsConstant = true;
        vm.Pins[0].ConstantValue = 100;
        vm.Pins[1].IsConstant = true;
        vm.Pins[1].ConstantValue = 30;
        vm.Pins[2].IsConstant = true;
        vm.Pins[2].ConstantValue = 15;

        Assert.Contains("100 - 30 - 15", vm.FormulaExpression);
    }

    #endregion

    #region 3. Axiom 3 - FSM Drafting vs Committed State Machine

    [Fact]
    public void MathPin_NominalInput_CommitsCorrectly()
    {
        var pin = new MathPinBindingViewModel("IN1", isConstant: true, constantValue: 0.0);

        pin.ConstantValueText = "42.5";
        pin.CommitConstantValue();

        Assert.Equal(42.5, pin.ConstantValue);
        Assert.Equal("42.5", pin.ConstantValueText);
    }

    [Fact]
    public void MathPin_TolerantNormalization_CommitsWithCorrectSeparator()
    {
        var pin = new MathPinBindingViewModel("IN1", isConstant: true, constantValue: 0.0);

        // Nhập dấu phẩy văn hóa Việt Nam "100,5" -> tự normalize thành "100.5"
        pin.ConstantValueText = "100,5";
        pin.CommitConstantValue();

        Assert.Equal(100.5, pin.ConstantValue);
        Assert.Equal("100.5", pin.ConstantValueText);
    }

    [Fact]
    public void MathPin_AdversarialInput_AutoRevertsToLastValidValue()
    {
        var pin = new MathPinBindingViewModel("IN1", isConstant: true, constantValue: 50.0);
        Assert.Equal("50", pin.ConstantValueText);

        // Người dùng nhập chuỗi rác "invalid_input"
        pin.ConstantValueText = "invalid_input";
        pin.CommitConstantValue();

        // Phải tự động revert về 50, không làm hỏng mô hình bên dưới
        Assert.Equal(50.0, pin.ConstantValue);
        Assert.Equal("50", pin.ConstantValueText);
    }

    #endregion

    #region 4. Local Simulation & Zero Division Protection

    [Fact]
    public void MathNode_Calculate_DivByZero_ProtectsAgainstCrash()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Div };

        double res = vm.Calculate(new double[] { 100, 0 });

        Assert.Equal(0, res);
        Assert.True(vm.HasMathError);
        Assert.Equal("Div by Zero", vm.MathErrorMessage);
    }

    [Fact]
    public void MathNode_Calculate_Mod_CalculatesCorrectly()
    {
        var vm = new MathNodeViewModel { Operator = MathOperator.Mod };

        double res = vm.Calculate(new double[] { 17, 5 });
        Assert.Equal(2, res);
    }

    #endregion
}
