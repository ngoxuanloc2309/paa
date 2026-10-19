using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class ModbusMasterCodecTests
{
    [Fact]
    public void ModbusRegisterMap_Constants_MatchWireContract()
    {
        Assert.Equal(0x1000, ModbusRegisterMap.ModbusMasterTableBaseAddress);
        Assert.Equal(64, ModbusRegisterMap.ModbusMasterRegistersPerBlock);
        Assert.Equal(128, ModbusRegisterMap.ModbusMasterBytesPerBlock);
        Assert.Equal(4, ModbusRegisterMap.ModbusMasterMaxBlocks);
        Assert.Equal(256, ModbusRegisterMap.ModbusMasterTableTotalLength);
        Assert.Equal(4, ModbusRegisterMap.ModbusMasterMaxPinsPerBlock);

        Assert.Equal(0x1000, ModbusRegisterMap.GetModbusMasterBlockAddress(0));
        Assert.Equal(0x1040, ModbusRegisterMap.GetModbusMasterBlockAddress(1));
        Assert.Equal(0x1080, ModbusRegisterMap.GetModbusMasterBlockAddress(2));
        Assert.Equal(0x10C0, ModbusRegisterMap.GetModbusMasterBlockAddress(3));
    }

    [Fact]
    public void EncodeAndDecodeBlock_Roundtrip_PreservesAllFieldsAndPins()
    {
        var config = new ModbusMasterReadConfigDto
        {
            BlockIndex = 1,
            Enabled = true,
            SlaveId = 5,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = 100,
            RegCount = 4,
            PollIntervalMs = 500,
            TimeoutMs = 1000,
            EnTagIndex = 12,
            ValidTagIndex = 13,
            ErrorTagIndex = 14,
            ErrCodeTagIndex = 15,
            Pins = new List<ModbusMasterPinBindingDto>
            {
                new ModbusMasterPinBindingDto
                {
                    Label = "Temp",
                    RegOffset = 0,
                    DataType = ModbusDataType.Real,
                    Endianness = ModbusEndianness.MidBig_CDAB,
                    Multiplier = 0.1f,
                    TargetTagIndex = 20
                },
                new ModbusMasterPinBindingDto
                {
                    Label = "Humidity",
                    RegOffset = 2,
                    DataType = ModbusDataType.UInt16,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 1.0f,
                    TargetTagIndex = 21
                }
            }
        };

        var span = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeBlock(config, span);

        var decoded = ModbusMasterCodec.DecodeBlock(span, 1);

        Assert.Equal(1, decoded.BlockIndex);
        Assert.True(decoded.Enabled);
        Assert.Equal(5, decoded.SlaveId);
        Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, decoded.FunctionCode);
        Assert.Equal(100, decoded.StartAddress);
        Assert.Equal(4, decoded.RegCount);
        Assert.Equal((uint)500, decoded.PollIntervalMs);
        Assert.Equal((uint)1000, decoded.TimeoutMs);
        Assert.Equal((ushort)13, decoded.ValidTagIndex);
        Assert.Equal((ushort)14, decoded.ErrorTagIndex);
        Assert.Equal((ushort)15, decoded.ErrCodeTagIndex);

        Assert.Equal(2, decoded.Pins.Count);

        Assert.Equal("Temp", decoded.Pins[0].Label);
        Assert.Equal(0, decoded.Pins[0].RegOffset);
        Assert.Equal(ModbusDataType.Real, decoded.Pins[0].DataType);
        Assert.Equal(ModbusEndianness.MidBig_CDAB, decoded.Pins[0].Endianness);
        Assert.Equal(0.1f, decoded.Pins[0].Multiplier, 3);
        Assert.Equal((ushort)20, decoded.Pins[0].TargetTagIndex);

        Assert.Equal("Humidity", decoded.Pins[1].Label);
        Assert.Equal(2, decoded.Pins[1].RegOffset);
        Assert.Equal(ModbusDataType.UInt16, decoded.Pins[1].DataType);
        Assert.Equal(ModbusEndianness.BigEndian_ABCD, decoded.Pins[1].Endianness);
        Assert.Equal(1.0f, decoded.Pins[1].Multiplier, 3);
        Assert.Equal((ushort)21, decoded.Pins[1].TargetTagIndex);
    }

    [Fact]
    public void ModbusRegisterMap_WriteConstants_MatchWireContract()
    {
        Assert.Equal(0x1100, ModbusRegisterMap.ModbusMasterWriteTableBaseAddress);
        Assert.Equal(256, ModbusRegisterMap.ModbusMasterWriteTableTotalLength);
        Assert.Equal(0x1100, ModbusRegisterMap.GetModbusMasterWriteBlockAddress(0));
        Assert.Equal(0x1140, ModbusRegisterMap.GetModbusMasterWriteBlockAddress(1));
        Assert.Equal(0x1180, ModbusRegisterMap.GetModbusMasterWriteBlockAddress(2));
        Assert.Equal(0x11C0, ModbusRegisterMap.GetModbusMasterWriteBlockAddress(3));
    }

    [Fact]
    public void EncodeAndDecodeWriteBlock_ChannelIndexed_Roundtrip_PreservesAllFields()
    {
        var config = new ModbusMasterWriteConfigDto
        {
            BlockIndex = 0,
            Enabled = true,
            SlaveId = 2,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 9,
            IsExclusiveChannel = true,
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
            CyclicIntervalMs = 500,
            TimeoutMs = 300,
            TrigTagIndex = 10,
            DoneTagIndex = 11,
            BusyTagIndex = 12,
            ErrorTagIndex = 13,
            ErrCodeTagIndex = 14,
            ChannelTagIndex = 25,
            CommandTagIndex = 26,
            ParamTagIndex = 27
        };

        var span = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeWriteBlock(config, span);

        var decoded = ModbusMasterCodec.DecodeWriteBlock(span, 0);

        Assert.Equal(0, decoded.BlockIndex);
        Assert.True(decoded.Enabled);
        Assert.Equal(2, decoded.SlaveId);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, decoded.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, decoded.FunctionCode);
        Assert.Equal(0x0100, decoded.BaseAddressCommand);
        Assert.Equal(0x0120, decoded.BaseAddressParam);
        Assert.Equal(9, decoded.ChannelCount);
        Assert.True(decoded.IsExclusiveChannel);
        Assert.Equal(ModbusWriteTriggerMode.OnTriggerRisingEdge, decoded.TriggerMode);
        Assert.Equal((uint)500, decoded.CyclicIntervalMs);
        Assert.Equal((uint)300, decoded.TimeoutMs);
        Assert.Equal((ushort)10, decoded.TrigTagIndex);
        Assert.Equal((ushort)11, decoded.DoneTagIndex);
        Assert.Equal((ushort)12, decoded.BusyTagIndex);
        Assert.Equal((ushort)13, decoded.ErrorTagIndex);
        Assert.Equal((ushort)14, decoded.ErrCodeTagIndex);
        Assert.Equal((ushort)25, decoded.ChannelTagIndex);
        Assert.Equal((ushort)26, decoded.CommandTagIndex);
        Assert.Equal((ushort)27, decoded.ParamTagIndex);
    }

    [Fact]
    public void EncodeAndDecodeWriteBlock_DirectRegisters_Roundtrip_PreservesAllFieldsAndPins()
    {
        var config = new ModbusMasterWriteConfigDto
        {
            BlockIndex = 2,
            Enabled = true,
            SlaveId = 3,
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            StartAddress = 200,
            PointCount = 2,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            CyclicIntervalMs = 1000,
            TimeoutMs = 500,
            TrigTagIndex = 30,
            Pins = new List<ModbusMasterPinBindingDto>
            {
                new ModbusMasterPinBindingDto
                {
                    Label = "SP1",
                    RegOffset = 0,
                    DataType = ModbusDataType.Int16,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 1.0f,
                    TargetTagIndex = 40
                },
                new ModbusMasterPinBindingDto
                {
                    Label = "SP2",
                    RegOffset = 1,
                    DataType = ModbusDataType.UInt16,
                    Endianness = ModbusEndianness.BigEndian_ABCD,
                    Multiplier = 0.5f,
                    TargetTagIndex = 41
                }
            }
        };

        var span = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeWriteBlock(config, span);

        var decoded = ModbusMasterCodec.DecodeWriteBlock(span, 2);

        Assert.Equal(2, decoded.BlockIndex);
        Assert.True(decoded.Enabled);
        Assert.Equal(3, decoded.SlaveId);
        Assert.Equal(ModbusWriteMode.DirectRegisters, decoded.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteMultipleRegisters, decoded.FunctionCode);
        Assert.Equal(200, decoded.StartAddress);
        Assert.Equal(2, decoded.PointCount);
        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, decoded.TriggerMode);
        Assert.Equal((ushort)30, decoded.TrigTagIndex);

        Assert.Equal(2, decoded.Pins.Count);
        Assert.Equal("SP1", decoded.Pins[0].Label);
        Assert.Equal(0, decoded.Pins[0].RegOffset);
        Assert.Equal(ModbusDataType.Int16, decoded.Pins[0].DataType);
        Assert.Equal((ushort)40, decoded.Pins[0].TargetTagIndex);

        Assert.Equal("SP2", decoded.Pins[1].Label);
        Assert.Equal(1, decoded.Pins[1].RegOffset);
        Assert.Equal(0.5f, decoded.Pins[1].Multiplier, 3);
        Assert.Equal((ushort)41, decoded.Pins[1].TargetTagIndex);
    }
}

