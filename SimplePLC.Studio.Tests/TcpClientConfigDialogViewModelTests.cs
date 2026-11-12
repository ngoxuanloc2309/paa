using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho ViewModel hộp thoại cấu hình chuyên sâu TCP Client Socket.
/// Kiểm thử các tính năng: Snapshot Buffer, DataTag binding, Reset Defaults,
/// và mô phỏng luồng gói tin Socket Preview.
/// </summary>
public class TcpClientConfigDialogViewModelTests
{
    private readonly TcpClientNodeViewModel _node;
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly TcpClientConfigDialogViewModel _dialogVm;

    public TcpClientConfigDialogViewModelTests()
    {
        _node = new TcpClientNodeViewModel();
        _tagCatalog = new TagCatalogViewModel();
        _dialogVm = new TcpClientConfigDialogViewModel(_node, _tagCatalog);
    }

    [Fact]
    public void Constructor_InitializesWithNodeAndCatalog()
    {
        // Assert
        Assert.Same(_node, _dialogVm.Node);
        Assert.Same(_tagCatalog, _dialogVm.TagCatalog);
        Assert.Equal(_node.RemoteHost, _dialogVm.RemoteHost);
        Assert.Equal(_node.RemotePort, _dialogVm.RemotePort);
        Assert.NotEmpty(_dialogVm.PreviewStreamText);
        Assert.Contains("192.168.1.100", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void BufferIsolation_ChangesDoNotAffectNodeUntilApply()
    {
        // Arrange
        var originalHost = _node.RemoteHost;
        var tag = new TagModel { Name = "STREAM_VREG" };

        // Act
        _dialogVm.RemoteHost = "10.0.0.99";
        _dialogVm.RemotePort = 502;
        _dialogVm.DataTag = tag;

        // Assert: Node vẫn giữ nguyên giá trị cũ (chưa bị sửa)
        Assert.Equal(originalHost, _node.RemoteHost);
        Assert.NotEqual(502, _node.RemotePort);
        Assert.Null(_node.DataTag);

        // Act 2: Bấm Apply
        _dialogVm.Apply();

        // Assert 2: Node đã được cập nhật
        Assert.Equal("10.0.0.99", _node.RemoteHost);
        Assert.Equal(502, _node.RemotePort);
        Assert.Same(tag, _node.DataTag);
    }

    [Fact]
    public void ResetDefaults_RestoresFactorySettingsInBuffer()
    {
        // Arrange
        _dialogVm.RemoteHost = "custom.server.com";
        _dialogVm.RemotePort = 9000;
        _dialogVm.ReconnectDelaySec = 20;
        _dialogVm.HeartbeatIntervalSec = 60;
        _dialogVm.HeartbeatPayload = "HEARTBEAT";
        _dialogVm.RegistrationType = "OnConnect";
        _dialogVm.RegistrationPayload = "DEVICE_123";
        _dialogVm.PayloadMode = "Custom";
        _dialogVm.DataTag = new TagModel { Name = "CUSTOM_TAG" };

        // Act
        _dialogVm.ResetDefaults();

        // Assert
        Assert.Equal("192.168.1.100", _dialogVm.RemoteHost);
        Assert.Equal(8888, _dialogVm.RemotePort);
        Assert.Equal(5, _dialogVm.ReconnectDelaySec);
        Assert.Equal(30, _dialogVm.HeartbeatIntervalSec);
        Assert.Equal("PING", _dialogVm.HeartbeatPayload);
        Assert.Equal("None", _dialogVm.RegistrationType);
        Assert.Equal(string.Empty, _dialogVm.RegistrationPayload);
        Assert.Equal("Raw", _dialogVm.PayloadMode);
        Assert.Null(_dialogVm.DataTag);
    }

    [Fact]
    public void SocketPreviewAndDiagnostics_UpdateCorrectly()
    {
        // Act
        _dialogVm.RemoteHost = "172.16.0.10";
        _dialogVm.RemotePort = 9999;
        _dialogVm.RegistrationType = "OnConnect";
        _dialogVm.RegistrationPayload = "AUTH_KEY_123";
        _dialogVm.HeartbeatIntervalSec = 10;
        _dialogVm.HeartbeatPayload = "KEEPALIVE";
        _dialogVm.PayloadMode = "Raw";
        _dialogVm.DataTag = new TagModel { Name = "FLOW_SENSOR" };

        // Assert
        Assert.Equal("tcp://172.16.0.10:9999", _dialogVm.SummarySocketText);
        Assert.Contains("KEEPALIVE (10s)", _dialogVm.SummaryHeartbeatText);
        Assert.Contains("[CONNECT] -> 172.16.0.10:9999", _dialogVm.PreviewStreamText);
        Assert.Contains("[TX AUTH] -> AUTH_KEY_123", _dialogVm.PreviewStreamText);
        Assert.Contains("[TX PING] -> KEEPALIVE", _dialogVm.PreviewStreamText);
        Assert.Contains("FLOW_SENSOR", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void CustomJsonMode_ValidJson_SetsIsJsonValidTrueAndIndents()
    {
        // Arrange
        _dialogVm.PayloadMode = "Custom";
        _dialogVm.CustomTemplate = "{\"device\":\"{device}\",\"tag\":\"{tag}\",\"val\":{val}}";
        _dialogVm.DataTag = new TagModel { Name = "AI0" };

        // Assert
        Assert.True(_dialogVm.IsJsonMode);
        Assert.True(_dialogVm.IsJsonValid);
        Assert.False(_dialogVm.HasJsonError);
        Assert.Empty(_dialogVm.JsonValidationError);
        Assert.Contains("\"device\": \"SPLC-DTU-01\"", _dialogVm.PreviewStreamText);
        Assert.Contains("\"tag\": \"AI0\"", _dialogVm.PreviewStreamText);
        Assert.Contains("\"val\": 25.5", _dialogVm.PreviewStreamText);
        Assert.Contains("bytes", _dialogVm.PayloadByteSizeText);
    }

    [Fact]
    public void CustomJsonMode_InvalidJson_SetsHasJsonErrorTrue()
    {
        // Arrange: Missing closing brace
        _dialogVm.PayloadMode = "Custom";
        _dialogVm.CustomTemplate = "{\"device\":\"{device}\",\"tag\":";

        // Assert
        Assert.True(_dialogVm.IsJsonMode);
        Assert.False(_dialogVm.IsJsonValid);
        Assert.True(_dialogVm.HasJsonError);
        Assert.NotEmpty(_dialogVm.JsonValidationError);
    }

    [Fact]
    public void RawMode_ShowsStructuredStreamPreview()
    {
        // Arrange
        _dialogVm.PayloadMode = "Raw";
        _dialogVm.FramingDelimiter = "CRLF";
        _dialogVm.HeartbeatIntervalSec = 30;

        // Assert
        Assert.True(_dialogVm.IsRawMode);
        Assert.False(_dialogVm.IsJsonMode);
        Assert.Contains("=== TCP SOCKET STREAM PROTOCOL ===", _dialogVm.PreviewStreamText);
        Assert.Contains("• Target Host   :", _dialogVm.PreviewStreamText);
        Assert.Contains("• Framing (EOL) :", _dialogVm.PreviewStreamText);
        Assert.Contains("=== REAL-TIME PACKET STREAM ===", _dialogVm.PreviewStreamText);
        Assert.Contains("bytes", _dialogVm.PayloadByteSizeText);
    }

    [Fact]
    public void RawMode_DefaultAutoLink_UpdatesStreamPreviewWithDataInPin()
    {
        // Act
        _dialogVm.PayloadMode = "Raw";
        _dialogVm.DataTag = null;

        // Assert
        Assert.Null(_dialogVm.SelectedDataTagOption?.Tag);
        Assert.Contains("DataIn", _dialogVm.PreviewStreamText);
        Assert.Contains("Raw Bytes Stream", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void RawMode_ExplicitTag_UpdatesStreamPreviewWithTagName()
    {
        // Act
        _dialogVm.PayloadMode = "Raw";
        var tag = _tagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG10") ?? new TagModel { Name = "VREG10" };
        _dialogVm.DataTag = tag;

        // Assert
        Assert.Equal("VREG10", _dialogVm.SelectedDataTagOption?.Tag?.Name);
        Assert.Contains("VREG10", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void RawMode_ApplyAndResetDefaults_PersistsAndRestores()
    {
        // Act: Gán Tag cụ thể và Apply
        _dialogVm.PayloadMode = "Raw";
        var startTag = new TagModel { Name = "VREG0" };
        _dialogVm.DataTag = startTag;
        _dialogVm.Apply();

        // Assert Node
        Assert.Equal("Raw", _node.PayloadMode);
        Assert.Same(startTag, _node.DataTag);

        // Act 2: Reset Defaults
        _dialogVm.ResetDefaults();
        Assert.Equal("Raw", _dialogVm.PayloadMode);
        Assert.Null(_dialogVm.DataTag);
        Assert.Null(_dialogVm.SelectedDataTagOption?.Tag);
    }

    [Fact]
    public void PrependToEveryPacket_Registration_AppearsInTxDataPreview()
    {
        // Arrange
        _dialogVm.RegistrationType = "PrependToEveryPacket";
        _dialogVm.RegistrationPayload = "DEV_001";
        _dialogVm.PayloadMode = "Raw";

        // Assert
        Assert.Contains("[DEV_001] + Raw Bytes Stream: <", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void FramingDelimiter_CrlfLfAndCustom_ReflectsInStreamPreviewAndDiagnostics()
    {
        // Act: CRLF
        _dialogVm.FramingDelimiter = "CRLF";
        Assert.Contains("\\r\\n", _dialogVm.PreviewStreamText);
        Assert.Contains("CRLF", _dialogVm.SummaryFramingText);

        // Act: LF
        _dialogVm.FramingDelimiter = "LF";
        Assert.Contains("\\n", _dialogVm.PreviewStreamText);
        Assert.Contains("LF", _dialogVm.SummaryFramingText);

        // Act: None
        _dialogVm.FramingDelimiter = "None";
        Assert.DoesNotContain("\\r\\n", _dialogVm.PreviewStreamText);
        Assert.DoesNotContain("\\n", _dialogVm.PreviewStreamText);

        // Act: Custom
        _dialogVm.FramingDelimiter = "Custom";
        _dialogVm.CustomDelimiterText = "###";
        Assert.True(_dialogVm.IsCustomDelimiter);
        Assert.Contains("###", _dialogVm.PreviewStreamText);
        Assert.Contains("Custom (###)", _dialogVm.SummaryFramingText);
    }

    [Fact]
    public void CustomTemplate_WithTokens_RendersPreviewCorrectly()
    {
        // Arrange
        _dialogVm.PayloadMode = "Custom";
        _dialogVm.CustomTemplate = "DEV={device};TAG={tag};VAL={val}";
        _dialogVm.DataTag = new TagModel { Name = "PRESSURE_AI0" };

        // Assert
        Assert.True(_dialogVm.IsCustomMode);
        Assert.False(_dialogVm.IsRawMode);
        Assert.Contains("DEV=SPLC-DTU-01", _dialogVm.PreviewStreamText);
        Assert.Contains("TAG=PRESSURE_AI0", _dialogVm.PreviewStreamText);
        Assert.Contains("VAL=25.5", _dialogVm.PreviewStreamText);
    }

    [Fact]
    public void HeartbeatZero_RendersDisabled()
    {
        // Arrange
        _dialogVm.HeartbeatIntervalSec = 0;

        // Assert
        Assert.False(_dialogVm.IsHeartbeatEnabled);
        Assert.True(_dialogVm.SummaryHeartbeatText.Contains("Disabled", StringComparison.OrdinalIgnoreCase) || _dialogVm.SummaryHeartbeatText.Contains("Đã tắt", StringComparison.OrdinalIgnoreCase));
        Assert.True(_dialogVm.PreviewStreamText.Contains("Disabled", StringComparison.OrdinalIgnoreCase) || _dialogVm.PreviewStreamText.Contains("Đã tắt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsRegistrationEnabled_ReflectsRegistrationTypeState()
    {
        // Default is None -> disabled
        _dialogVm.RegistrationType = "None";
        Assert.False(_dialogVm.IsRegistrationEnabled);

        // OnConnect -> enabled
        _dialogVm.RegistrationType = "OnConnect";
        Assert.True(_dialogVm.IsRegistrationEnabled);

        // PrependToEveryPacket -> enabled
        _dialogVm.RegistrationType = "PrependToEveryPacket";
        Assert.True(_dialogVm.IsRegistrationEnabled);

        // Back to None -> disabled
        _dialogVm.RegistrationType = "None";
        Assert.False(_dialogVm.IsRegistrationEnabled);
    }

    [Fact]
    public void IsHeartbeatEnabled_ReflectsHeartbeatIntervalState()
    {
        // Positive interval -> enabled
        _dialogVm.HeartbeatIntervalSec = 30;
        Assert.True(_dialogVm.IsHeartbeatEnabled);

        // Zero interval -> disabled
        _dialogVm.HeartbeatIntervalSec = 0;
        Assert.False(_dialogVm.IsHeartbeatEnabled);

        // Re-enable -> enabled
        _dialogVm.HeartbeatIntervalSec = 15;
        Assert.True(_dialogVm.IsHeartbeatEnabled);
    }

    [Fact]
    public void SummaryHeartbeatText_ReflectsCustomPayload_WhenEnabled()
    {
        // Arrange
        _dialogVm.HeartbeatIntervalSec = 45;
        _dialogVm.HeartbeatPayload = "KEEPALIVE";

        // Assert
        Assert.Equal("KEEPALIVE (45s)", _dialogVm.SummaryHeartbeatText);
        Assert.Contains("[TX PING] -> KEEPALIVE (Every 45s)", _dialogVm.PreviewStreamText);

        // Empty payload falls back to PING
        _dialogVm.HeartbeatPayload = "";
        Assert.Equal("PING (45s)", _dialogVm.SummaryHeartbeatText);
        Assert.Contains("[TX PING] -> PING (Every 45s)", _dialogVm.PreviewStreamText);
    }
}
