using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Compilation.MacroLowering;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// IEC 61508-3 Triad Unit Test Suite for MATH Macro Lowering & Compilation.
/// Kiểm chứng toàn diện quy trình phân giải (lowering) khối MATH đa chân (2..8 inputs)
/// thành chuỗi micro-rules 32-byte nguyên tử thực thi trên firmware STM32/MCU.
/// </summary>
public class MathMacroLoweringTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    #region 1. Happy Path Testing (Nominal Execution)

    [Fact]
    public void Math_TwoPin_TagAndScalarConstant_CompilesToDirectRuleWithSentinel()
    {
        // Arrange: AI0 + 100 -> VREG0
        var ai0Sensor = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_1",
            Kind = LogicNodeKind.Math,
            Label = "Add100",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0Sensor.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 100.0 }
                }
            }
        };

        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(ai0Sensor.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(vreg0.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.MathOp, rule.Action.Type);
        Assert.Equal(100, rule.Action.Parameter);
        Assert.False(rule.Guard.HasGuard);
        var dto = RuleMapper.ToDto(rule);
        Assert.Equal(0x7FFF, dto.GuardTag); // sentinel for scalar
        Assert.Equal(100, dto.AuxParam);
    }

    [Fact]
    public void Math_TwoPin_TwoTags_CompilesToDirectRuleWithoutSentinel()
    {
        // Arrange: AI0 + AI1 -> VREG0
        var ai0 = _product.FindTagByName("AI0")!;
        var ai1 = _product.FindTagByName("AI1")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_1",
            Kind = LogicNodeKind.Math,
            Label = "AddSensors",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = ai1.TagIndex }
                }
            }
        };

        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(ai1.TagIndex, rule.Guard.Tag!.TagIndex); // dynamic tag operand 2
        Assert.Equal(vreg0.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.MathOp, rule.Action.Type);
    }

    [Fact]
    public void Math_FourPins_LowersToChainedMicroRules_WithReverseScratchpads()
    {
        // Arrange: AI0 + AI1 + AI2 + 50 -> VREG0 (4 inputs -> 3 micro-rules)
        var ai0 = _product.FindTagByName("AI0")!;
        var ai1 = _product.FindTagByName("AI1")!;
        var ai2 = _product.FindTagByName("AI2")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_CHAIN_1",
            Kind = LogicNodeKind.Math,
            Label = "Sum4",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = ai1.TagIndex },
                    new() { Label = "IN3", IsConstant = false, TagIndex = ai2.TagIndex },
                    new() { Label = "IN4", IsConstant = true, ConstantValue = 50.0 }
                }
            }
        };

        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert: 4 inputs phải sinh ra đúng N-1 = 3 micro-rules
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.Rules.Count);

        // Micro-rule 1: AI0 + AI1 -> VREG15 (scratchpad cao nhất)
        var rule1 = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, rule1.Trigger.Tag.TagIndex);
        Assert.Equal(ai1.TagIndex, rule1.Guard.Tag!.TagIndex);
        Assert.Contains("VREG", rule1.Action.TargetTag.Name);
        Assert.NotEqual(vreg0.TagIndex, rule1.Action.TargetTag.TagIndex); // Phải ghi vào scratchpad VREG
        ushort scratch1 = rule1.Action.TargetTag.TagIndex;

        // Micro-rule 2: scratch1 + AI2 -> VREG14 (scratchpad tiếp theo)
        var rule2 = result.Program.Rules[1];
        Assert.Equal(scratch1, rule2.Trigger.Tag.TagIndex);
        Assert.Equal(ai2.TagIndex, rule2.Guard.Tag!.TagIndex);
        ushort scratch2 = rule2.Action.TargetTag.TagIndex;
        Assert.NotEqual(scratch1, scratch2);

        // Micro-rule 3: scratch2 + 50 -> VREG0 (kết quả cuối cùng tới đích)
        var rule3 = result.Program.Rules[2];
        Assert.Equal(scratch2, rule3.Trigger.Tag.TagIndex);
        Assert.False(rule3.Guard.HasGuard);
        var dto3 = RuleMapper.ToDto(rule3);
        Assert.Equal(0x7FFF, dto3.GuardTag); // scalar constant 50
        Assert.Equal(50, dto3.AuxParam);
        Assert.Equal(vreg0.TagIndex, rule3.Action.TargetTag.TagIndex); // đích cuối
    }

    #endregion

    #region 2. Boundary & Operator Coverage Testing

    [Theory]
    [InlineData(MathOperator.Sub)]
    [InlineData(MathOperator.Mul)]
    [InlineData(MathOperator.Div)]
    [InlineData(MathOperator.Mod)]
    public void Math_AllBinaryOperators_LowerCorrectly(MathOperator op)
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var ai1 = _product.FindTagByName("AI1")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = $"MATH_{op}",
            Kind = LogicNodeKind.Math,
            Label = $"Op_{op}",
            MathData = new MathNodeData
            {
                Operator = op,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = ai1.TagIndex }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.Rules);
        var rule = result.Program.Rules[0];
        Assert.Equal(ActionKind.MathOp, rule.Action.Type);
    }

    [Fact]
    public void Math_TypePromotion_MixedIntAndFloat_PromotesToReal()
    {
        // Arrange: AI0 (Int32) + 2.5 (Float scalar) with Auto type mode
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_FLOAT_PROMOTE",
            Kind = LogicNodeKind.Math,
            Label = "FloatPromote",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 2.5 } // Có phần thập phân -> ép kiểu float
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        // Option B: AI0 (INT32) được tự động thăng hạng sang Float32 qua Rule 0 (SCALE)
        // Rule 1 thực hiện MATH_OP cộng với hằng số 2.5f
        Assert.Equal(2, result.Program!.Rules.Count);

        var promoRule = result.Program.Rules[0];
        Assert.Equal(ActionKind.ScaleTag, promoRule.Action.Type);
        Assert.True(promoRule.Action.IsFloat);

        var rule = result.Program.Rules[1];
        Assert.Equal(ActionKind.MathOp, rule.Action.Type);
        Assert.True(rule.Action.IsFloat);
        var dto = RuleMapper.ToDto(rule);
        Assert.Equal(0x7FFF, dto.GuardTag);

        // IEEE 754 32-bit float của 2.5f là 0x40200000 = 1075838976
        int expectedBits = BitConverter.SingleToInt32Bits(2.5f);
        Assert.Equal(expectedBits, dto.AuxParam);
    }

    #endregion

    #region 3. Fault Injection & Resilience Testing

    [Fact]
    public void Math_MissingOutputTag_EmitsDiagnosticError()
    {
        var ai0 = _product.FindTagByName("AI0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_NO_OUT",
            Kind = LogicNodeKind.Math,
            Label = "NoOutput",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = null, // Thiếu output tag
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 10.0 }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrMathNoOutput);
    }

    [Fact]
    public void Math_LessThanTwoPins_EmitsDiagnosticError()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_ONE_PIN",
            Kind = LogicNodeKind.Math,
            Label = "OnePin",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex } // Chỉ có 1 chân
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrMathNoInput);
    }

    #endregion
    [Fact]
    public void Math_Subtraction_WithThreePins_CompilesToChainedRulesSuccessfully()
    {
        // Arrange: AI0 - AI1 - AI2 -> VREG0 (Left-associative: (AI0 - AI1) - AI2)
        var ai0 = _product.FindTagByName("AI0")!;
        var ai1 = _product.FindTagByName("AI1")!;
        var ai2 = _product.FindTagByName("AI2")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_SUB_3",
            Kind = LogicNodeKind.Math,
            Label = "Sub3Inputs",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Sub,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = ai1.TagIndex },
                    new() { Label = "IN3", IsConstant = false, TagIndex = ai2.TagIndex }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert: 3 inputs phải sinh ra đúng 2 micro-rules (AI0 - AI1 -> Scratchpad, Scratchpad - AI2 -> VREG0)
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        var rule1 = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, rule1.Trigger.Tag.TagIndex);
        Assert.Equal(ai1.TagIndex, rule1.Guard.Tag!.TagIndex);
        Assert.Equal((CompareOperator)MathOperator.Sub, rule1.Trigger.CompareOp);

        var rule2 = result.Program.Rules[1];
        Assert.Equal(rule1.Action.TargetTag.TagIndex, rule2.Trigger.Tag.TagIndex);
        Assert.Equal(ai2.TagIndex, rule2.Guard.Tag!.TagIndex);
        Assert.Equal(vreg0.TagIndex, rule2.Action.TargetTag.TagIndex);
        Assert.Equal((CompareOperator)MathOperator.Sub, rule2.Trigger.CompareOp);
    }

    [Theory]
    [InlineData(MathOperator.Div)]
    [InlineData(MathOperator.Mod)]
    public void Math_NonAssociativeOperator_WithMoreThanTwoPins_EmitsDiagnosticError(MathOperator op)
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var ai1 = _product.FindTagByName("AI1")!;
        var ai2 = _product.FindTagByName("AI2")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = $"MATH_INVALID_{op}",
            Kind = LogicNodeKind.Math,
            Label = "InvalidNonAssoc",
            MathData = new MathNodeData
            {
                Operator = op,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = ai1.TagIndex },
                    new() { Label = "IN3", IsConstant = false, TagIndex = ai2.TagIndex }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrMathNoInput);
    }

    [Theory]
    [InlineData(MathOperator.Div, 0.0)]
    [InlineData(MathOperator.Div, 1e-10)]
    [InlineData(MathOperator.Mod, 0.0)]
    public void Math_DivOrMod_WithConstantZero_EmitsDivZeroDiagnosticError(MathOperator op, double zeroVal)
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_DIV_ZERO",
            Kind = LogicNodeKind.Math,
            Label = "DivZeroTest",
            MathData = new MathNodeData
            {
                Operator = op,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = vreg0.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = zeroVal }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "SPLC-COMP-MATH-DIV-ZERO");
    }

    [Fact]
    public void Math_MixedIntAndFloatTags_AutomaticallyPromotesWithImplicitScaleRule()
    {
        var tags = new List<TagDefinition>(_product.Tags)
        {
            new TagDefinition(300, "FLOAT_TAG", TagKind.VirtualRegister, TagDataType.Float32, isReadOnly: false, "Float Tag"),
            new TagDefinition(301, "FLOAT_OUT", TagKind.VirtualRegister, TagDataType.Float32, isReadOnly: false, "Float Out")
        };
        var productWithFloat = new ProductDefinition(
            _product.ProductName,
            _product.DeviceClass,
            _product.ProductVariant,
            _product.MaxRules,
            _product.Resources,
            tags,
            _product.WireProfile,
            hardwareCapabilities: _product.HardwareCapabilities);

        var ai0 = productWithFloat.FindTagByName("AI0")!; // Int32
        var floatTag = productWithFloat.FindTagByName("FLOAT_TAG")!; // Float32
        var floatOut = productWithFloat.FindTagByName("FLOAT_OUT")!; // Float32

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_MIXED_TYPE",
            Kind = LogicNodeKind.Math,
            Label = "MixedTypePromoTest",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = floatOut.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = floatTag.TagIndex }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, productWithFloat);

        // Assert: Option B - Tự động thăng hạng kiểu, biên dịch thành công!
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        // Sinh ra 2 rules: Rule 0 là SCALE ngầm cho AI0, Rule 1 là MATH_OP cộng float
        Assert.Equal(2, result.Program.Rules.Count);

        var promoRule = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, promoRule.Trigger.Tag.TagIndex);
        Assert.Equal(ActionKind.ScaleTag, promoRule.Action.Type);
        Assert.True(promoRule.Action.IsFloat);
        Assert.Equal(1.0, promoRule.Action.Parameter);

        var mathRule = result.Program.Rules[1];
        // Op1 của MathRule chính là target của promoRule
        Assert.Equal(promoRule.Action.TargetTag.TagIndex, mathRule.Trigger.Tag.TagIndex);
        Assert.Equal(floatTag.TagIndex, mathRule.Guard.Tag!.TagIndex);
        Assert.Equal(floatOut.TagIndex, mathRule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.MathOp, mathRule.Action.Type);
        Assert.True(mathRule.Action.IsFloat);
    }

    [Fact]
    public void Math_ThreePin_MixedIntAndFloatTags_GeneratesChainedPromotionAndMathRules()
    {
        var tags = new List<TagDefinition>(_product.Tags)
        {
            new TagDefinition(300, "FLOAT_TAG", TagKind.VirtualRegister, TagDataType.Float32, isReadOnly: false, "Float Tag"),
            new TagDefinition(301, "FLOAT_OUT", TagKind.VirtualRegister, TagDataType.Float32, isReadOnly: false, "Float Out")
        };
        var productWithFloat = new ProductDefinition(
            _product.ProductName,
            _product.DeviceClass,
            _product.ProductVariant,
            _product.MaxRules,
            _product.Resources,
            tags,
            _product.WireProfile,
            hardwareCapabilities: _product.HardwareCapabilities);

        var ai0 = productWithFloat.FindTagByName("AI0")!; // Int32
        var floatTag = productWithFloat.FindTagByName("FLOAT_TAG")!; // Float32
        var floatOut = productWithFloat.FindTagByName("FLOAT_OUT")!; // Float32

        var graph = new LogicGraph();
        var mathNode = new LogicNode
        {
            Id = "MATH_3PIN_MIXED",
            Kind = LogicNodeKind.Math,
            Label = "ThreePinMixedTest",
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.Auto,
                OutTagIndex = floatOut.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = ai0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 10.0 },
                    new() { Label = "IN3", IsConstant = false, TagIndex = floatTag.TagIndex }
                }
            }
        };
        graph.Nodes.Add(mathNode);

        // Act
        var result = _compiler.Compile(graph, productWithFloat);

        // Assert: 1 promo rule (cho AI0) + 2 math step rules = 3 rules tổng cộng
        Assert.True(result.IsSuccess, $"Compilation must succeed: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.Rules.Count);

        var promoRule = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, promoRule.Trigger.Tag.TagIndex);
        Assert.Equal(ActionKind.ScaleTag, promoRule.Action.Type);
    }
}