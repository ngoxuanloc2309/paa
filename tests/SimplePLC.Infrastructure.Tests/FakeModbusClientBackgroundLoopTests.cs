using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class FakeModbusClientBackgroundLoopTests
{
    [Fact]
    public async Task FakeModbusClient_BackgroundScan_ExecutesCommittedRulesInBackground()
    {
        var client = new FakeModbusClient(isConnected: true)
        {
            ScanIntervalMs = 15
        };

        try
        {
            // Stage and commit a rule: DI0 -> DO0 = 1
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_RISE,
                TriggerTag = 0,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 8,
                ActionParam = 1
            };

            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.RuleCountStagedAddress, 1);
            ushort[] stagedBuf = new ushort[ModbusRegisterMap.RegistersPerRule];
            SimplePLC.Protocol.Codec.RegisterCodec.EncodeRuleRecord(rule, stagedBuf);
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, stagedBuf);

            ushort crc = SimplePLC.Protocol.Cryptography.Crc16Modbus.ComputeFromRegisters(stagedBuf);
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.ExpectedCrc16Address, crc);
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

            // Verify active rule count is 1
            var countRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
            Assert.Equal(1, countRegs[0]);

            // Set DI0 = 1 via Control API
            client.Simulator.Control.SetTagValue(0, 1);

            // Wait up to 1000ms for background periodic scan loop to tick and execute committed rule
            ushort do0Addr = ModbusRegisterMap.GetRuntimeTagAddress(8);
            int do0Value = 0;
            for (int retry = 0; retry < 50; retry++)
            {
                var do0Regs = await client.ReadHoldingRegistersAsync(1, do0Addr, 2);
                do0Value = SimplePLC.Protocol.Codec.RegisterCodec.DecodeInt32(do0Regs);
                if (do0Value == 1) break;
                await Task.Delay(20);
            }

            Assert.Equal(1, do0Value);
        }
        finally
        {
            await client.DisposeAsync();
        }
    }
}
