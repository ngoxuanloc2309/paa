using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Compilation.MacroLowering;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// IEC 61508-3 SIL2/3 Triad Test Suite kiểm chứng toàn diện 12 Vector kiểm thử
/// theo chuẩn đồng bộ Firmware SPLC-SPEC-MATH-007 v2.1.0 và SPLC-SPEC-SCALE-002.
/// Kiểm chứng End-to-End từ Logic Graph -> Macro Lowering -> Modbus Wire Contract (Reg 0..15) -> MCU Reference Simulator.
/// </summary>
public class MathFirmwareSyncSpec007Tests
{
    /// <summary>
    /// Tạo cấu hình sản phẩm chuẩn cho board WIFI_IO (meshwf) với 112 tags:
    /// DI: 0..3, DO: 4..7, VFLAG: 8..39, VREG: 40..71, VREG_RETAIN: 72..103, COUNTER: 104..111.
    /// HardwareCapabilities có MATH_ALU (Bit 16) và FLOAT32.
    /// </summary>
    private static ProductDefinition CreateWifiIoProduct()
    {
        var profile = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var tags = DeviceProfileBuilder.GenerateTags(profile);
        var caps = SPLC_HardwareCapabilities.BASE_LOGIC |
                   SPLC_HardwareCapabilities.RETENTIVE |
                   SPLC_HardwareCapabilities.WIFI |
                   SPLC_HardwareCapabilities.FLOAT32 |
                   SPLC_HardwareCapabilities.MATH_ALU;

        return new ProductDefinition(
            "SynaptiX WIFI_IO (meshwf)",
            deviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 2,
            maxRules: 50,
            resources: profile,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: caps);
    }

    private static ProductDefinition CreateWifiIoProductFloat()
    {
        var profile = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var tags = DeviceProfileBuilder.GenerateTags(profile);
        for (ushort i = 40; i < 40 + 32; i++)
        {
            tags[i] = new TagDefinition(i, $"VREG{i - 40}", TagKind.VirtualRegister, TagDataType.Float32, isReadOnly: false);
        }

        var caps = SPLC_HardwareCapabilities.BASE_LOGIC |
                   SPLC_HardwareCapabilities.RETENTIVE |
                   SPLC_HardwareCapabilities.WIFI |
                   SPLC_HardwareCapabilities.FLOAT32 |
                   SPLC_HardwareCapabilities.MATH_ALU;

        return new ProductDefinition(
            "SynaptiX WIFI_IO Float (meshwf)",
            deviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            productVariant: 2,
            maxRules: 50,
            resources: profile,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: caps);
    }

    private readonly ProductDefinition _wifiProduct = CreateWifiIoProduct();
    private readonly ProductDefinition _wifiProductFloat = CreateWifiIoProductFloat();
    private readonly RuleCompiler _compiler = new();

    #region 1. Verification of 12 Standard Test Vectors (SPLC-SPEC-MATH-007)

    [Fact]
    public void Vector01_Nominal_Add_Int32_TagAndConstant_ComputesCorrectly()
    {
        // Vector 1: VREG0 (Tag 40) + Constant 100 -> VREG2 (Tag 42)
        // Khởi tạo: VREG0 = 25, VREG2 = 0. Kỳ vọng: VREG2 = 125, Bit 3 = 0, Reg 6..7 = 0, AuxParam = 100
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, 25);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V01",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 100.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.Rules);

        var rule = result.Program.Rules[0];
        var dto = RuleMapper.ToDto(rule);

        // Kiểm tra Hợp đồng Wire: Reg 6..7 = 0, Reg 14..15 = 100, GuardTag = 0x7FFF
        Assert.Equal(0, dto.ActionParam);
        Assert.Equal(100, dto.AuxParam);
        Assert.Equal(ModbusRegisterMap.GuardTagNone, dto.GuardTag);
        Assert.Equal((byte)SPLC_RuleFlags.NONE, dto.Flags);

        // Chạy qua MCU Simulator
        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(125, sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.Equal(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector02_Nominal_Sub_Float32_TagAndTag_ComputesCorrectly()
    {
        // Vector 2: VREG0 (Tag 40: 50.5f) - VREG1 (Tag 41: 20.2f) -> VREG2 (Tag 42: 0.0f)
        // Kỳ vọng: VREG2 = 30.3f, Bit 3 = 0, Reg 6..7 = 0, AuxParam = 0
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProductFloat.FindTagByIndex(40)!;
        var vreg1 = _wifiProductFloat.FindTagByIndex(41)!;
        var vreg2 = _wifiProductFloat.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, BitConverter.SingleToInt32Bits(50.5f));
        sim.Control.SetTagValue(vreg1.TagIndex, BitConverter.SingleToInt32Bits(20.2f));
        sim.Control.SetTagValue(vreg2.TagIndex, BitConverter.SingleToInt32Bits(0.0f));

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V02",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Subtract,
                TypeMode = MathTypeMode.ForceReal,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = vreg1.TagIndex }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProductFloat);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.Rules);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        Assert.Equal(0, dto.ActionParam);
        Assert.Equal(0, dto.AuxParam);
        Assert.Equal(vreg1.TagIndex, dto.GuardTagIndex);
        Assert.Equal((byte)(SPLC_RuleFlags.TRIGGER_FLOAT | SPLC_RuleFlags.ACTION_FLOAT), dto.Flags);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        float resF = BitConverter.Int32BitsToSingle(sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.Equal(30.3f, resF, precision: 3);
        Assert.Equal(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector03_Runtime_DivByZero_Int32_Tag_FreezesOutput_SetsHealthBit3()
    {
        // Vector 3: VREG0 (100) / VREG1 (0) -> VREG2 (khởi tạo 999)
        // Kỳ vọng: VREG2 giữ nguyên 999 (đóng băng), Bit 3 (MATH_FAULT) được kích hoạt
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg1 = _wifiProduct.FindTagByIndex(41)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, 100);
        sim.Control.SetTagValue(vreg1.TagIndex, 0); // Tag Op2 bằng 0 lúc runtime
        sim.Control.SetTagValue(vreg2.TagIndex, 999);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V03",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Divide,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = vreg1.TagIndex }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        // Ngõ ra giữ nguyên giá trị 999, KHÔNG bị đè
        Assert.Equal(999, sim.Control.GetTagValue(vreg2.TagIndex));
        // Health Flags (0x0803) có Bit 3 = 1 (MATH_FAULT)
        Assert.NotEqual(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector04_Runtime_DivByZero_Float32_Tag_FreezesOutput_SetsHealthBit3()
    {
        // Vector 4: VREG0 (10.0f) / VREG1 (0.0f) -> VREG2 (khởi tạo 77.7f)
        // Kỳ vọng: VREG2 giữ nguyên 77.7f, Bit 3 = 1
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProductFloat.FindTagByIndex(40)!;
        var vreg1 = _wifiProductFloat.FindTagByIndex(41)!;
        var vreg2 = _wifiProductFloat.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, BitConverter.SingleToInt32Bits(10.0f));
        sim.Control.SetTagValue(vreg1.TagIndex, BitConverter.SingleToInt32Bits(0.0f));
        sim.Control.SetTagValue(vreg2.TagIndex, BitConverter.SingleToInt32Bits(77.7f));

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V04",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Divide,
                TypeMode = MathTypeMode.ForceReal,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = vreg1.TagIndex }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProductFloat);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.Rules);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        float resF = BitConverter.Int32BitsToSingle(sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.Equal(77.7f, resF, precision: 2);
        Assert.NotEqual(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector05_Add_Int32_Overflow_WrapsAroundMod2Pow32()
    {
        // Vector 5: 2.000.000.000 + 2.000.000.000 -> -294.967.296 (0xEE6B2800)
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, 2_000_000_000);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V05",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 2_000_000_000.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(-294_967_296, sim.Control.GetTagValue(vreg2.TagIndex));
    }

    [Fact]
    public void Vector06_Boundary_Div_MinInt_By_Minus1_WritesMaxInt_SetsHealthBit3()
    {
        // Vector 6: INT32_MIN / -1 -> ghi INT32_MAX và bật Bit 3
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, int.MinValue);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V06",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Divide,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = -1.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(int.MaxValue, sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.NotEqual(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector07_Boundary_Mod_MinInt_By_Minus1_WritesZero_NoHealthFault()
    {
        // Vector 7: INT32_MIN % -1 -> ghi 0, KHÔNG bật cờ lỗi (khớp Q11)
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, int.MinValue);
        sim.Control.SetTagValue(vreg2.TagIndex, 555);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V07",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Modulo,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = -1.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(0, sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.Equal(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector08_Mul_Int32_Saturation_ClampsToMaxInt()
    {
        // Vector 8: 1.000.000.000 * 10 -> kẹp về INT32_MAX
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, 1_000_000_000);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V08",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Multiply,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 10.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(int.MaxValue, sim.Control.GetTagValue(vreg2.TagIndex));
    }

    [Fact]
    public void Vector09_Real_Overflow_ToInfinity_FreezesOutput_SetsHealthBit3()
    {
        // Vector 9: 1.0e30f * 1.0e30f -> +Infinity -> Giữ nguyên ngõ ra cũ 12.34f, bật Bit 3
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProductFloat.FindTagByIndex(40)!;
        var vreg2 = _wifiProductFloat.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, BitConverter.SingleToInt32Bits(1.0e30f));
        sim.Control.SetTagValue(vreg2.TagIndex, BitConverter.SingleToInt32Bits(12.34f));

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V09",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Multiply,
                TypeMode = MathTypeMode.ForceReal,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 1.0e30 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProductFloat);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.Rules);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        float resF = BitConverter.Int32BitsToSingle(sim.Control.GetTagValue(vreg2.TagIndex));
        Assert.Equal(12.34f, resF, precision: 2);
        Assert.NotEqual(0, sim.Control.ReadHoldingRegister(0x0803) & (ushort)SPLC_HealthFlags.MATH_FAULT);
    }

    [Fact]
    public void Vector10_C99_NegativeModulo_ComputesCorrectRemainder()
    {
        // Vector 10: -7 % 3 = -1 (dấu theo số bị chia)
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, -7);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V10",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Modulo,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 3.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        Assert.Equal(-1, sim.Control.GetTagValue(vreg2.TagIndex));
    }

    [Fact]
    public void Vector11_Op2_Is_SystemTag_ComputesCorrectly()
    {
        // Vector 11: VREG0 (10) + SystemTag 1000 (SYS_FIRST_SCAN: 1 ở scan 1, 0 ở scan 2+) -> VREG2
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var sysTag1000 = new TagDefinition(1000, "SYS_FIRST_SCAN", TagKind.System, TagDataType.Int32, isReadOnly: true);
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        sim.Control.SetTagValue(vreg0.TagIndex, 10);
        sim.Control.SetTagValue(vreg2.TagIndex, 0);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_V11",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = false, TagIndex = sysTag1000.TagIndex }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);
        Assert.True(result.IsSuccess);
        var dto = RuleMapper.ToDto(result.Program!.Rules[0]);

        Assert.Equal(sysTag1000.TagIndex, dto.GuardTagIndex);

        sim.Control.SetActiveRule(0, dto);

        // Scan pass 1: SYS_FIRST_SCAN == 1 -> 10 + 1 = 11
        sim.Control.ExecuteScanPass();
        Assert.Equal(11, sim.Control.GetTagValue(vreg2.TagIndex));

        // Scan pass 2: SYS_FIRST_SCAN == 0 -> 10 + 0 = 10
        sim.Control.ExecuteScanPass();
        Assert.Equal(10, sim.Control.GetTagValue(vreg2.TagIndex));
    }

    [Fact]
    public void Vector12_Scale_Int32_To_Real_TypePromotion_ProducesFlag0x07()
    {
        // Vector 12: SCALE_TAG nguồn Int32 sang Real sinh cờ flags = 0x07 và tính toán chuẩn xác
        var sim = new McuReferenceSimulator();
        var vreg0 = _wifiProduct.FindTagByIndex(40)!; // INT32 (100)
        var vreg1 = _wifiProduct.FindTagByIndex(41)!; // REAL (0.0f)

        sim.Control.SetTagValue(vreg0.TagIndex, 100);
        sim.Control.SetTagValue(vreg1.TagIndex, 0);

        var trigger = new TriggerModel(vreg0, TriggerKind.OnChange) { IsFloat = false };
        var action = new ActionModel(vreg1, ActionKind.ScaleTag, parameter: 1.0, auxParameter: 0.0) { IsFloat = true };
        var domainRule = new Rule(0, "ScalePromo", trigger, action, GuardModel.Empty, enabled: true);

        var dto = RuleMapper.ToDto(domainRule);

        // Kiểm tra cờ Wire: 0x01 | 0x02 | 0x04 = 0x07 (INT_TO_FLOAT)
        Assert.Equal(0x07, dto.Flags);
        Assert.True(dto.IsIntToFloat);

        sim.Control.SetActiveRule(0, dto);
        sim.Control.ExecuteScanPass();

        float resF = BitConverter.Int32BitsToSingle(sim.Control.GetTagValue(vreg1.TagIndex));
        Assert.Equal(100.0f, resF, precision: 2);
    }

    #endregion

    #region 2. Compiler Defensive Guards Testing

    [Fact]
    public void Compiler_Rejects_ReadOnly_OutputTag_For_MathNode()
    {
        // Thử cấu hình OUT là DI0 (chỉ số 0, Tag chỉ đọc)
        var di0 = _wifiProduct.FindTagByIndex(0)!;
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_READONLY",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = di0.TagIndex, // Cố tình chọn Tag chỉ đọc
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 10.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MathMacroLowering.ErrMathReadOnlyTarget);
    }

    [Fact]
    public void Compiler_Rejects_MathNode_When_Device_Lacks_MathAlu_Capability()
    {
        // Tạo sản phẩm thiếu năng lực MATH_ALU (Bit 16 = 0)
        var profile = new ProductResourceProfile(4, 4, 0, 16, 16, 16, 4);
        var tags = DeviceProfileBuilder.GenerateTags(profile);
        var oldDeviceNoMath = new ProductDefinition(
            "Legacy Device No Math",
            1, 1, 50, profile, tags, 2,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE);

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_OLD_DEV",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Add,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = 40,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = true, ConstantValue = 1.0 },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 2.0 }
                }
            }
        });

        var result = _compiler.Compile(graph, oldDeviceNoMath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MathMacroLowering.ErrMathUnsupported);
    }

    [Fact]
    public void Compiler_Rejects_Constant_Zero_Divisor_At_Compile_Time()
    {
        var vreg0 = _wifiProduct.FindTagByIndex(40)!;
        var vreg2 = _wifiProduct.FindTagByIndex(42)!;

        var graph = new LogicGraph();
        graph.Nodes.Add(new LogicNode
        {
            Id = "MATH_DIV_ZERO_CONST",
            Kind = LogicNodeKind.Math,
            MathData = new MathNodeData
            {
                Operator = MathOperator.Divide,
                TypeMode = MathTypeMode.ForceInt32,
                OutTagIndex = vreg2.TagIndex,
                Pins = new List<MathPinData>
                {
                    new() { Label = "IN1", IsConstant = false, TagIndex = vreg0.TagIndex },
                    new() { Label = "IN2", IsConstant = true, ConstantValue = 0.0 } // Hằng số 0
                }
            }
        });

        var result = _compiler.Compile(graph, _wifiProduct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MathMacroLowering.ErrMathDivZero);
    }

    #endregion
}
