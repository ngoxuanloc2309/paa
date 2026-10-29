using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Giao tiếp thực tế với phân hệ Wi-Fi ST67 trên MCU qua Modbus RTU FC03/FC06/FC16 tại vùng 0x1300..0x13FF.
/// Tuân thủ chuẩn SPLC-SPEC-WIFI-004-ICD.
/// </summary>
public sealed class WifiClient : IWifiClient
{
    private readonly IModbusClient _client;

    public WifiClient(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<WifiConfigDto> ReadWifiConfigAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.WifiConfigBaseAddress,
            ModbusRegisterMap.WifiConfigRegisters,
            cancellationToken
        ).ConfigureAwait(false);

        return WifiCodec.DecodeConfig(rawRegisters);
    }

    public async Task WriteWifiConfigAsync(byte slaveId, WifiConfigDto config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        ushort[] buffer = new ushort[ModbusRegisterMap.WifiConfigRegisters];
        WifiCodec.EncodeConfig(config, buffer);

        await _client.WriteMultipleRegistersAsync(
            slaveId,
            ModbusRegisterMap.WifiConfigBaseAddress,
            buffer,
            cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task TriggerWifiCommandAsync(byte slaveId, ushort command, CancellationToken cancellationToken = default)
    {
        await _client.WriteSingleRegisterAsync(
            slaveId,
            ModbusRegisterMap.WifiControlAddress,
            command,
            cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task<WifiScanControlDto> ReadWifiScanStatusAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.WifiControlAddress,
            ModbusRegisterMap.WifiControlRegisters,
            cancellationToken
        ).ConfigureAwait(false);

        return WifiCodec.DecodeControl(rawRegisters);
    }

    public async Task<IReadOnlyList<WifiApInfoDto>> ReadWifiScanResultsAsync(byte slaveId = 1, ushort count = 8, CancellationToken cancellationToken = default)
    {
        int actualCount = Math.Clamp((int)count, 0, ModbusRegisterMap.WifiMaxScanAps);
        if (actualCount == 0) return Array.Empty<WifiApInfoDto>();

        ushort regLength = (ushort)(actualCount * ModbusRegisterMap.WifiRegistersPerAp);
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.WifiScanTableBaseAddress,
            regLength,
            cancellationToken
        ).ConfigureAwait(false);

        return WifiCodec.DecodeScanResults(rawRegisters, actualCount);
    }
}
