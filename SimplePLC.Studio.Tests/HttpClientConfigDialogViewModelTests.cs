using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho ViewModel hộp thoại cấu hình chuyên sâu HTTP Client.
/// Kiểm thử các tính năng: Snapshot Buffer, Visual Field Builder, Caret Insertion,
/// Phân luồng Event vs Cyclic, xác thực cú pháp JSON thời gian thực và thông tin chẩn đoán.
/// </summary>
public class HttpClientConfigDialogViewModelTests
{
    private readonly HttpClientNodeViewModel _node;
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly HttpClientConfigDialogViewModel _dialogVm;

    public HttpClientConfigDialogViewModelTests()
    {
        _node = new HttpClientNodeViewModel();
        _tagCatalog = new TagCatalogViewModel();
        _dialogVm = new HttpClientConfigDialogViewModel(_node, _tagCatalog);
    }

    [Fact]
    public void Constructor_InitializesWithNodeAndRendersPreview()
    {
        // Assert
        Assert.Same(_node, _dialogVm.Node);
        Assert.Same(_tagCatalog, _dialogVm.TagCatalog);
        Assert.Equal(_node.Method, _dialogVm.Method);
        Assert.Equal(_node.Url, _dialogVm.Url);
        Assert.NotEmpty(_dialogVm.PreviewPayloadText);
        Assert.Contains("SPLC-DTU-01", _dialogVm.PreviewPayloadText);
        Assert.DoesNotContain("\"\"SPLC-DTU-01\"\"", _dialogVm.PreviewPayloadText);
    }

    [Fact]
    public void InsertPlaceholderAtCaret_InsertsAtPreciseLocation()
    {
        // Arrange
        _dialogVm.CustomTemplate = "{\"key\": \"\"}";

        // Act: Chèn token {val} vào giữa cặp dấu ngoặc kép "" tại caretIndex = 9
        int newCaret = _dialogVm.InsertPlaceholderAtCaret("{val}", 9);

        // Assert
        Assert.Equal("{\"key\": \"{val}\"}", _dialogVm.CustomTemplate);
        Assert.Equal(14, newCaret);
        Assert.True(_dialogVm.IsJsonValid);
    }

    [Fact]
    public void BufferIsolation_ChangesDoNotAffectNodeUntilApply()
    {
        // Arrange
        var originalUrl = _node.Url;

        // Act
        _dialogVm.Url = "http://modified.server.com";
        _dialogVm.AddField();

        // Assert: Node vẫn giữ nguyên giá trị cũ (chưa bị sửa)
        Assert.Equal(originalUrl, _node.Url);
        Assert.NotEqual(_dialogVm.Fields.Count, _node.Fields.Count);

        // Act 2: Bấm Apply
        _dialogVm.Apply();

        // Assert 2: Node đã được cập nhật
        Assert.Equal("http://modified.server.com", _node.Url);
        Assert.Equal(_dialogVm.Fields.Count, _node.Fields.Count);
    }

    [Fact]
    public void ResetDefaults_RestoresFactorySettingsInBuffer()
    {
        // Arrange
        _dialogVm.Method = "PUT";
        _dialogVm.Url = "https://custom.endpoint.com/api";
        _dialogVm.TimeoutSec = 45;
        _dialogVm.PeriodicIntervalSec = 120;
        _dialogVm.PayloadMode = "Raw";

        // Act
        _dialogVm.ResetDefaults();

        // Assert
        Assert.Equal("POST", _dialogVm.Method);
        Assert.Equal("http://api.myserver.com/telemetry", _dialogVm.Url);
        Assert.Equal(10, _dialogVm.TimeoutSec);
        Assert.Equal(30, _dialogVm.PeriodicIntervalSec);
        Assert.Equal("JSON", _dialogVm.PayloadMode);
    }

    [Fact]
    public void FieldsBuilder_AddAndRemoveFields_UpdatesJsonPreview()
    {
        // Arrange
        _dialogVm.PayloadMode = "JSON";
        _dialogVm.Fields.Clear();

        // Act: Thêm 2 trường
        _dialogVm.Fields.Add(new HttpPayloadFieldViewModel("temp", new TagModel { Name = "AI0" }, "", _dialogVm.UpdatePreview));
        _dialogVm.Fields.Add(new HttpPayloadFieldViewModel("speed", new TagModel { Name = "VREG1" }, "", _dialogVm.UpdatePreview));
        _dialogVm.UpdatePreview();

        // Assert
        Assert.Contains("temp", _dialogVm.PreviewPayloadText);
        Assert.Contains("speed", _dialogVm.PreviewPayloadText);
        Assert.True(_dialogVm.IsJsonValid);

        // Act 2: Xóa 1 trường
        _dialogVm.RemoveField(_dialogVm.Fields.First());

        // Assert 2
        Assert.DoesNotContain("temp", _dialogVm.PreviewPayloadText);
        Assert.Contains("speed", _dialogVm.PreviewPayloadText);
    }

    [Fact]
    public void TriggerMode_EventAndCyclic_TogglesPeriodicState()
    {
        // Act: Chọn Event
        _dialogVm.TriggerMode = "Event";
        Assert.True(_dialogVm.IsEventMode);
        Assert.False(_dialogVm.IsPeriodicMode);

        // Act: Chọn Cyclic
        _dialogVm.TriggerMode = "Cyclic";
        Assert.False(_dialogVm.IsEventMode);
        Assert.True(_dialogVm.IsPeriodicMode);
    }

    [Fact]
    public void CustomTemplate_InvalidJson_ReportsSyntaxError()
    {
        // Arrange
        _dialogVm.PayloadMode = "Custom";

        // Act: Nhập JSON sai cú pháp (thiếu ngoặc đóng hoặc thừa chuỗi ngoài)
        _dialogVm.CustomTemplate = "{\"device\": \"test\"} trailing_garbage";

        // Assert
        Assert.False(_dialogVm.IsJsonValid);
        Assert.True(_dialogVm.HasJsonError);
        Assert.NotEmpty(_dialogVm.JsonValidationError);
    }

    [Fact]
    public void PayloadModeRaw_WithDataTag_UpdatesPreview()
    {
        // Act
        _dialogVm.PayloadMode = "Raw";
        _dialogVm.DataTag = new TagModel { Name = "SENSOR_AI0" };

        // Assert: PreviewPayloadText chỉ hiển thị thuần túy giá trị thực tế (Visual Truth)
        Assert.Equal("25.5", _dialogVm.PreviewPayloadText);
        Assert.Equal("4 bytes", _dialogVm.PayloadByteSizeText);
        Assert.Contains("SENSOR_AI0", _dialogVm.SummarySourceDataText);
        Assert.False(_dialogVm.IsJsonValid);
        Assert.False(_dialogVm.IsJsonMode);
        Assert.True(_dialogVm.IsRawMode);
        Assert.Equal("text/plain", _dialogVm.SummaryContentType);
    }

    [Fact]
    public void DiagnosticSummary_Properties_ReflectBufferChanges()
    {
        // Arrange
        _dialogVm.Method = "PUT";
        _dialogVm.Url = "https://telemetry.io/v1";
        _dialogVm.TriggerMode = "Cyclic";
        _dialogVm.PeriodicIntervalSec = 15;

        // Assert
        Assert.Equal("PUT https://telemetry.io/v1", _dialogVm.SummaryEndpointText);
        Assert.Contains("15s", _dialogVm.SummaryTriggerText);
        Assert.NotEmpty(_dialogVm.PayloadByteSizeText);
    }

    [Fact]
    public void CleanDefaultTag_AvoidsTagNone()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        catalog.AllTags.Clear();
        catalog.AllTags.Add(new TagModel { Name = "NONE", Kind = TagKind.None });
        catalog.AllTags.Add(new TagModel { Name = "VREG_FLOW", Kind = TagKind.VirtualRegister });

        // Act: Khởi tạo với catalog chứa tag NONE ở đầu
        var vm = new HttpClientConfigDialogViewModel(new HttpClientNodeViewModel(), catalog);

        // Assert: Trường mẫu tạo ra phải là VREG_FLOW chứ không phải NONE
        Assert.NotEmpty(vm.Fields);
        Assert.NotEqual("none", vm.Fields[0].Key);
        Assert.Equal("vreg_flow", vm.Fields[0].Key);
    }

    [Fact]
    public void RawMode_DefaultAutoLink_FormatsPreviewWithFbdPin()
    {
        // Arrange
        _dialogVm.PayloadMode = "Raw";
        _dialogVm.DataTag = null;

        // Assert: Giá trị tải trọng thuần túy là 25.5, nguồn dữ liệu hiển thị ở SummarySourceDataText
        Assert.True(_dialogVm.IsRawMode);
        Assert.Null(_dialogVm.SelectedDataTagOption?.Tag);
        Assert.Equal("25.5", _dialogVm.PreviewPayloadText);
        Assert.Equal("4 bytes", _dialogVm.PayloadByteSizeText);
        Assert.Contains("DATA", _dialogVm.SummarySourceDataText);
        Assert.Contains("FBD", _dialogVm.SummarySourceDataText);
    }

    [Fact]
    public void RawMode_ExplicitTag_FormatsPreviewWithTagName()
    {
        // Arrange
        _dialogVm.PayloadMode = "Raw";
        var tag = new TagModel { Name = "SENSOR_AI0" };
        _dialogVm.DataTag = tag;

        // Assert
        Assert.True(_dialogVm.IsRawMode);
        Assert.Same(tag, _dialogVm.SelectedDataTagOption?.Tag);
        Assert.Equal("25.5", _dialogVm.PreviewPayloadText);
        Assert.Equal("4 bytes", _dialogVm.PayloadByteSizeText);
        Assert.Contains("SENSOR_AI0", _dialogVm.SummarySourceDataText);
    }

    [Fact]
    public void CustomJsonMode_ValidJson_SetsIsJsonValidTrueAndIndents()
    {
        // Arrange: Chuỗi JSON viết liền 1 dòng
        _dialogVm.PayloadMode = "Custom";
        _dialogVm.CustomTemplate = "{\"device\":\"{device}\",\"tag\":\"{tag}\",\"val\":{val}}";

        // Assert: ViewModel tự động parse và thụt lề 2 spaces
        Assert.True(_dialogVm.IsJsonMode);
        Assert.True(_dialogVm.IsJsonValid);
        Assert.False(_dialogVm.HasJsonError);
        Assert.Empty(_dialogVm.JsonValidationError);
        Assert.Contains("\"device\": \"SPLC-DTU-01\"", _dialogVm.PreviewPayloadText);
        Assert.Contains("\"tag\": \"VREG0\"", _dialogVm.PreviewPayloadText);
        Assert.Contains("\"val\": 25.5", _dialogVm.PreviewPayloadText);
    }

    [Fact]
    public void PayloadModeChanged_SynchronizesContentTypeHeader()
    {
        // Arrange: Khởi tạo mặc định JSON -> Content-Type: application/json
        _dialogVm.PayloadMode = "JSON";
        Assert.Equal("Content-Type: application/json", _dialogVm.CustomHeaders);
        Assert.Equal("application/json", _dialogVm.SummaryContentType);

        // Act: Đổi sang Raw
        _dialogVm.PayloadMode = "Raw";
        Assert.Equal("Content-Type: text/plain", _dialogVm.CustomHeaders);
        Assert.Equal("text/plain", _dialogVm.SummaryContentType);

        // Act 2: Đổi lại sang Custom JSON
        _dialogVm.PayloadMode = "Custom";
        Assert.Equal("Content-Type: application/json", _dialogVm.CustomHeaders);
        Assert.Equal("application/json", _dialogVm.SummaryContentType);
    }

    [Fact]
    public void RawMode_ApplyAndResetDefaults_PersistsAndRestores()
    {
        // Act: Gán Tag cụ thể và Apply
        _dialogVm.PayloadMode = "Raw";
        var startTag = new TagModel { Name = "VREG10" };
        _dialogVm.DataTag = startTag;
        _dialogVm.Apply();

        // Assert Node
        Assert.Equal("Raw", _node.PayloadMode);
        Assert.Same(startTag, _node.DataTag);

        // Act 2: Reset Defaults
        _dialogVm.ResetDefaults();
        Assert.Equal("JSON", _dialogVm.PayloadMode);
        Assert.Null(_dialogVm.DataTag);
        Assert.Null(_dialogVm.SelectedDataTagOption?.Tag);
    }
}
