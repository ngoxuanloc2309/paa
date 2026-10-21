using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Exceptions;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class ModbusTcpClientTests
{
    private const int TestPort = 15502;

    [Fact]
    public async Task ReadAndWriteHoldingRegisters_OverModbusTcp_SucceedsWithExactValues()
    {
        // 1. Arrange: Khởi động MCU Modbus TCP Server trên cổng thử nghiệm 15502
        var simulator = new McuReferenceSimulator();
        var tcpServer = new McuModbusTcpServer(simulator, TestPort);
        tcpServer.Start();

        try
        {
            await using var client = new ModbusTcpClient("127.0.0.1", TestPort, timeoutMs: 2000);
            await client.ConnectAsync();
            Assert.True(client.IsConnected);

            // 2. Act & Assert: Đọc DeviceDescriptor tại 0x0000 (10 thanh ghi)
            var descriptorRegs = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: 0x0000, count: 10);
            Assert.Equal(10, descriptorRegs.Length);
            Assert.Equal((ushort)SPLC_DeviceClass.REMOTE_IO, descriptorRegs[0]);
            Assert.Equal(2, descriptorRegs[8]); // Protocol Version 2.0

            // 3. Act & Assert: Ghi FC06 vào RuleCountStagedAddress (0x9002)
            ushort stageCountReg = ModbusRegisterMap.RuleCountStagedAddress;
            await client.WriteSingleRegisterAsync(slaveId: 1, address: stageCountReg, value: 1);

            var readBackFc06 = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: stageCountReg, count: 1);
            Assert.Equal(1, readBackFc06[0]);

            // 4. Act & Assert: Ghi FC16 nhiều thanh ghi vào Staging Rule Table (4 thanh ghi tại 0x9010)
            ushort stagingReg = ModbusRegisterMap.StagingRuleTableBaseAddress;
            ushort[] multiValues = new ushort[] { 0x1111, 0x2222, 0x3333, 0x4444 };
            await client.WriteMultipleRegistersAsync(slaveId: 1, startAddress: stagingReg, values: multiValues);

            var readBackFc16 = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: stagingReg, count: 4);
            Assert.Equal(multiValues, readBackFc16);
        }
        finally
        {
            await tcpServer.StopAsync();
        }
    }

    [Fact]
    public async Task DeviceConnectionFactory_WithTcpEndpoint_EstablishesSessionSuccessfully()
    {
        var simulator = new McuReferenceSimulator();
        var tcpServer = new McuModbusTcpServer(simulator, 15503);
        tcpServer.Start();

        try
        {
            var factory = new DeviceConnectionFactory();
            var endpoint = new TcpEndpoint("127.0.0.1", 15503);

            var result = await factory.ConnectAsync(endpoint, slaveId: 1);
            Assert.True(result.IsSuccess, $"TCP Handshake failed: {result.FailureReason}");

            var session = result.Session!;
            Assert.True(session.IsActive);
            Assert.Equal("Modbus TCP (127.0.0.1:15503)", session.Endpoint.DisplayName);
            Assert.Equal(1, session.SlaveId);
            Assert.NotNull(session.Product);

            await session.DisposeAsync();
        }
        finally
        {
            await tcpServer.StopAsync();
        }
    }

    [Fact]
    public async Task DeployRulesAsync_ThroughGateway_OverModbusTcp_CompletesAtomicCommit()
    {
        var simulator = new McuReferenceSimulator();
        var tcpServer = new McuModbusTcpServer(simulator, 15504);
        tcpServer.Start();

        try
        {
            await using var client = new ModbusTcpClient("127.0.0.1", 15504, timeoutMs: 3000);
            await client.ConnectAsync();

            var gateway = new RuleTableGateway(client);
            var logs = new List<string>();

            var rules = new List<RuleRecordDto>
            {
                new()
                {
                    ThresholdLo = 1,
                    ThresholdHi = 1,
                    ForMs = 0,
                    ActionParam = 1,
                    TriggerTag = 0, // DI0
                    ActionTag = 8,  // DO0
                    GuardTag = ModbusRegisterMap.GuardTagNone,
                    Enabled = true,
                    TriggerType = SPLC_TriggerType.ON_RISE,
                    CompareOp = SPLC_CompareOp.EQ,
                    ActionType = SPLC_ActionType.SET_TAG
                }
            };

            // Nạp cấu hình qua Gateway trên kết nối Modbus TCP
            var deployResult = await gateway.DeployRulesAsync(
                slaveId: 1,
                rules: rules,
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: null,
                directWriteConfigs: null,
                cancellationToken: default,
                progressLogger: logs.Add);

            Assert.True(deployResult.IsSuccess, $"Deploy over TCP failed: {deployResult.ErrorCode} - {deployResult.ErrorMessage}");
            Assert.Equal(SPLC_ErrorCode.NONE, deployResult.ErrorCode);
            Assert.True(deployResult.ActiveVersion >= 1);

            // Xác thực bảng Active Rule trên MCU simulator đã được kích hoạt
            var activeRules = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleTableBaseAddress, 16);
            Assert.Equal(16, activeRules.Length);
        }
        finally
        {
            await tcpServer.StopAsync();
        }
    }

    [Fact]
    public async Task ReadHoldingRegisters_WhenAddressOutOfRange_ThrowsModbusProtocolException()
    {
        var simulator = new McuReferenceSimulator();
        var tcpServer = new McuModbusTcpServer(simulator, 15505);
        tcpServer.Start();

        try
        {
            await using var client = new ModbusTcpClient("127.0.0.1", 15505, timeoutMs: 2000);
            await client.ConnectAsync();

            // Địa chỉ 0xFFFF vượt ngoài giới hạn bộ nhớ giả lập -> Phải nhận Exception response từ MCU
            var ex = await Assert.ThrowsAsync<ModbusProtocolException>(async () =>
            {
                await client.ReadHoldingRegistersAsync(1, 0xFFFF, 5);
            });

            Assert.Equal(ModbusExceptionCode.IllegalDataAddress, ex.ExceptionCode);
        }
        finally
        {
            await tcpServer.StopAsync();
        }
    }
}
