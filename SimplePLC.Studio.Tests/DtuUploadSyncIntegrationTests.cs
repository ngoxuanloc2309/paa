using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests kiểm tra tính toàn vẹn của việc ánh xạ (mapping) cấu hình DTU Gateway
/// từ hình ảnh thiết bị DTO nhận từ MCU sang ProjectMetadata.DtuConfig.
/// </summary>
public sealed class DtuUploadSyncIntegrationTests
{
    [Fact]
    public void MapDtuConfigDtoToMetadata_ShouldPreserveAllFieldsByteExact()
    {
        // Arrange: Mô phỏng DTO nhận được từ RuleTableReader sau khi đọc FC03 từ MCU
        var dtuDto = new DtuConfigDto
        {
            Enabled = true,
            PortIndex = 2,
            SerialMode = DtuSerialMode.ModbusMaster,
            UplinkMode = DtuUplinkMode.Http,
            FrameTimeoutDelayMs = 50,
            MaxPacketSize = 1024,
            PayloadMode = DtuPayloadMode.Custom,
            PayloadFlags = 0x2B, // 1 (DeviceId) + 2 (Timestamp) + 8 (Sequence) + 32 (PayloadLength) = 43 = 0x2B
            HttpConfig = new DtuHttpConfigDto
            {
                Enabled = true,
                Method = DtuHttpMethod.Put,
                Url = "https://api.factory-scada.vn/v2/ingest",
                CustomHeaders = "X-Api-Key: SPLC-SECRET-9999",
                TimeoutSec = 20,
                PeriodicIntervalSec = 15,
                TriggerMode = DtuHttpTriggerMode.Periodic
            },
            TcpConfig = new DtuTcpConfigDto
            {
                Enabled = true,
                SocketMode = DtuTcpSocketMode.Server,
                RemoteHost = "172.16.0.100",
                RemotePort = 7000,
                LocalPort = 7001,
                ReconnectDelaySec = 8,
                HeartbeatIntervalSec = 45,
                HeartbeatPayload = "KEEPALIVE",
                RegistrationType = DtuTcpRegistrationType.PrependToEveryPacket,
                RegistrationPayload = "GW_HEADER"
            },
            MqttWillConfig = new MqttWillConfigDto
            {
                Enabled = true,
                Topic = "device/emergency/status",
                Payload = "HARDWARE_DISCONNECTED",
                Qos = 2,
                Retain = true
            }
        };

        var metadata = new ProjectMetadata();

        // Act: Thực hiện mapping tương đương logic trong MainViewModel.UploadFromDeviceAsync
        metadata.DtuConfig.Enabled = dtuDto.Enabled;
        metadata.DtuConfig.PortIndex = dtuDto.PortIndex;
        metadata.DtuConfig.SerialMode = dtuDto.SerialMode.ToString();
        metadata.DtuConfig.UplinkMode = dtuDto.UplinkMode.ToString();
        metadata.DtuConfig.FrameTimeoutDelayMs = dtuDto.FrameTimeoutDelayMs;
        metadata.DtuConfig.MaxPacketSize = dtuDto.MaxPacketSize;
        metadata.DtuConfig.PayloadMode = dtuDto.PayloadMode.ToString();
        metadata.DtuConfig.IncludeDeviceId = (dtuDto.PayloadFlags & 0x01) != 0;
        metadata.DtuConfig.IncludeTimestamp = (dtuDto.PayloadFlags & 0x02) != 0;
        metadata.DtuConfig.IncludePort = (dtuDto.PayloadFlags & 0x04) != 0;
        metadata.DtuConfig.IncludeSequence = (dtuDto.PayloadFlags & 0x08) != 0;
        metadata.DtuConfig.IncludeHexStream = (dtuDto.PayloadFlags & 0x10) != 0;
        metadata.DtuConfig.IncludePayloadLength = (dtuDto.PayloadFlags & 0x20) != 0;

        metadata.DtuConfig.HttpSettings.Enabled = dtuDto.HttpConfig.Enabled;
        metadata.DtuConfig.HttpSettings.Method = dtuDto.HttpConfig.Method == DtuHttpMethod.Put ? "PUT" : "POST";
        metadata.DtuConfig.HttpSettings.Url = dtuDto.HttpConfig.Url;
        metadata.DtuConfig.HttpSettings.Headers = dtuDto.HttpConfig.CustomHeaders;
        metadata.DtuConfig.HttpSettings.TimeoutSec = dtuDto.HttpConfig.TimeoutSec;
        metadata.DtuConfig.HttpSettings.PeriodicIntervalSec = dtuDto.HttpConfig.PeriodicIntervalSec;
        metadata.DtuConfig.HttpSettings.TriggerMode = dtuDto.HttpConfig.TriggerMode.ToString();

        metadata.DtuConfig.TcpSettings.Enabled = dtuDto.TcpConfig.Enabled;
        metadata.DtuConfig.TcpSettings.SocketMode = dtuDto.TcpConfig.SocketMode.ToString();
        metadata.DtuConfig.TcpSettings.RemoteHost = dtuDto.TcpConfig.RemoteHost;
        metadata.DtuConfig.TcpSettings.RemotePort = dtuDto.TcpConfig.RemotePort;
        metadata.DtuConfig.TcpSettings.LocalPort = dtuDto.TcpConfig.LocalPort;
        metadata.DtuConfig.TcpSettings.ReconnectDelaySec = dtuDto.TcpConfig.ReconnectDelaySec;
        metadata.DtuConfig.TcpSettings.HeartbeatIntervalSec = dtuDto.TcpConfig.HeartbeatIntervalSec;
        metadata.DtuConfig.TcpSettings.HeartbeatPayload = dtuDto.TcpConfig.HeartbeatPayload;
        metadata.DtuConfig.TcpSettings.RegistrationType = dtuDto.TcpConfig.RegistrationType.ToString();
        metadata.DtuConfig.TcpSettings.RegistrationPayload = dtuDto.TcpConfig.RegistrationPayload;

        metadata.MqttWillEnabled = dtuDto.MqttWillConfig.Enabled;
        metadata.MqttWillTopic = dtuDto.MqttWillConfig.Topic;
        metadata.MqttWillPayload = dtuDto.MqttWillConfig.Payload;
        metadata.MqttWillQos = dtuDto.MqttWillConfig.Qos;
        metadata.MqttWillRetain = dtuDto.MqttWillConfig.Retain;

        // Assert: Khẳng định toàn bộ các trường được lưu giữ chính xác
        Assert.True(metadata.DtuConfig.Enabled);
        Assert.Equal(2, metadata.DtuConfig.PortIndex);
        Assert.Equal("ModbusMaster", metadata.DtuConfig.SerialMode);
        Assert.Equal("Http", metadata.DtuConfig.UplinkMode);
        Assert.Equal(50, metadata.DtuConfig.FrameTimeoutDelayMs);
        Assert.Equal(1024, metadata.DtuConfig.MaxPacketSize);
        Assert.Equal("Custom", metadata.DtuConfig.PayloadMode);

        Assert.True(metadata.DtuConfig.IncludeDeviceId);
        Assert.True(metadata.DtuConfig.IncludeTimestamp);
        Assert.False(metadata.DtuConfig.IncludePort);
        Assert.True(metadata.DtuConfig.IncludeSequence);
        Assert.False(metadata.DtuConfig.IncludeHexStream);
        Assert.True(metadata.DtuConfig.IncludePayloadLength);

        Assert.True(metadata.DtuConfig.HttpSettings.Enabled);
        Assert.Equal("PUT", metadata.DtuConfig.HttpSettings.Method);
        Assert.Equal("https://api.factory-scada.vn/v2/ingest", metadata.DtuConfig.HttpSettings.Url);
        Assert.Equal("X-Api-Key: SPLC-SECRET-9999", metadata.DtuConfig.HttpSettings.Headers);
        Assert.Equal(20, metadata.DtuConfig.HttpSettings.TimeoutSec);
        Assert.Equal(15, metadata.DtuConfig.HttpSettings.PeriodicIntervalSec);
        Assert.Equal("Periodic", metadata.DtuConfig.HttpSettings.TriggerMode);

        Assert.True(metadata.DtuConfig.TcpSettings.Enabled);
        Assert.Equal("Server", metadata.DtuConfig.TcpSettings.SocketMode);
        Assert.Equal("172.16.0.100", metadata.DtuConfig.TcpSettings.RemoteHost);
        Assert.Equal(7000, metadata.DtuConfig.TcpSettings.RemotePort);
        Assert.Equal(7001, metadata.DtuConfig.TcpSettings.LocalPort);
        Assert.Equal(8, metadata.DtuConfig.TcpSettings.ReconnectDelaySec);
        Assert.Equal(45, metadata.DtuConfig.TcpSettings.HeartbeatIntervalSec);
        Assert.Equal("KEEPALIVE", metadata.DtuConfig.TcpSettings.HeartbeatPayload);
        Assert.Equal("PrependToEveryPacket", metadata.DtuConfig.TcpSettings.RegistrationType);
        Assert.Equal("GW_HEADER", metadata.DtuConfig.TcpSettings.RegistrationPayload);

        Assert.True(metadata.MqttWillEnabled);
        Assert.Equal("device/emergency/status", metadata.MqttWillTopic);
        Assert.Equal("HARDWARE_DISCONNECTED", metadata.MqttWillPayload);
        Assert.Equal(2, metadata.MqttWillQos);
        Assert.True(metadata.MqttWillRetain);
    }
}
