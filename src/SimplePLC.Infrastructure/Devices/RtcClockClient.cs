using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc và ghi đồng bộ RTC Clock từ/xuống MCU qua Modbus FC03/FC16 tại địa chỉ 0x0810.
/// </summary>
public sealed class RtcClockClient : IRtcClockClient
{
    private readonly IModbusClient _client;

    public RtcClockClient(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<RtcClockDto> ReadRtcClockAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.RtcClockAddress,
            ModbusRegisterMap.RtcClockLength,
            cancellationToken
        );

        return RegisterCodec.DecodeRtcClock(rawRegisters);
    }

    public async Task WriteRtcClockAsync(RtcClockDto rtc, byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rtc);

        ushort[] buffer = new ushort[ModbusRegisterMap.RtcClockLength];
        RegisterCodec.EncodeRtcClock(rtc, buffer);

        await _client.WriteMultipleRegistersAsync(
            slaveId,
            ModbusRegisterMap.RtcClockAddress,
            buffer,
            cancellationToken
        );
    }

    public async Task<RtcClockDto> SyncToNowAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        // Đọc trước trạng thái RTC từ MCU để bảo toàn các cờ phần cứng HW_PRESENT và BATTERY_LOW
        bool hasHwRtc = false;
        bool isBatteryLow = false;
        try
        {
            var current = await ReadRtcClockAsync(slaveId, cancellationToken);
            hasHwRtc = current.HasHardwareRtc;
            isBatteryLow = current.IsBatteryLow;
        }
        catch
        {
            // Bỏ qua nếu thiết bị không hỗ trợ đọc trước
        }

        var now = DateTimeOffset.Now;
        var nowDto = new RtcClockDto
        {
            EpochUtcSeconds = (uint)now.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = (short)now.Offset.TotalMinutes,
            IsSynced = true,
            HasHardwareRtc = hasHwRtc,
            IsBatteryLow = isBatteryLow
        };

        await WriteRtcClockAsync(nowDto, slaveId, cancellationToken);
        return nowDto;
    }

    public async Task<RtcClockDto> SyncSmartAsync(byte slaveId = 1, uint maxDriftSeconds = 2, CancellationToken cancellationToken = default)
    {
        // 1. Đọc trạng thái RTC hiện tại từ MCU (Read-Before-Write)
        var current = await ReadRtcClockAsync(slaveId, cancellationToken);

        var now = DateTimeOffset.Now;
        long pcEpoch = now.ToUnixTimeSeconds();
        short pcTzOffset = (short)now.Offset.TotalMinutes;
        long driftSeconds = Math.Abs(pcEpoch - current.EpochUtcSeconds);

        // 2. Kiểm tra điều kiện cần đồng bộ:
        // - Chưa từng đồng bộ (IsSynced == false)
        // - Thời gian bị reset về epoch 0 (trước năm 2024: < 1,700,000,000)
        // - Lệch múi giờ
        // - Lệch giây lớn hơn ngưỡng cho phép
        bool needsSync = !current.IsSynced ||
                         current.EpochUtcSeconds < 1700000000 ||
                         current.TimezoneOffsetMinutes != pcTzOffset ||
                         driftSeconds > maxDriftSeconds;

        if (!needsSync)
        {
            return current;
        }

        // 3. Thực hiện ghi đồng bộ có bảo toàn cờ phần cứng từ MCU
        var updatedDto = new RtcClockDto
        {
            EpochUtcSeconds = (uint)pcEpoch,
            TimezoneOffsetMinutes = pcTzOffset,
            IsSynced = true,
            HasHardwareRtc = current.HasHardwareRtc,
            IsBatteryLow = current.IsBatteryLow
        };

        await WriteRtcClockAsync(updatedDto, slaveId, cancellationToken);
        return updatedDto;
    }
}
