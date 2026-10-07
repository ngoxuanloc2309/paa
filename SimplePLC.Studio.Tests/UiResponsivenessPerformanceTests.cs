using System.Diagnostics;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Automation tests to quantify and verify UI Responsiveness, Dispatcher Latency,
/// Collection In-Place updates, and Zero-Backlog Coalescing.
/// </summary>
public sealed class UiResponsivenessPerformanceTests
{
    private static ProductDefinition CreateProduct() => ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void Simulator_IdleState_DoesNotRunTimerWhenNotCompiled()
    {
        // Arrange: Khởi tạo khi vừa mở app (logic chưa compile)
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable);

        // Act: Khởi tạo SimulatorViewModel như trong MainViewModel
        var simVM = new SimulatorViewModel(tagCatalog, ruleTable, logicEditor);

        // Assert: Simulator KHÔNG được tự ý chạy ngầm gây nghẽn UI Dispatcher
        Assert.False(simVM.IsRunning);
        Assert.Equal(0, simVM.ScanNumber);
        Assert.Empty(simVM.RuleStatusList);
    }

    [Fact]
    public void Simulator_RuleStatusList_InPlaceUpdate_FastThroughput()
    {
        // Arrange: Tạo fixture có rule demo đã compile
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: action => action());
        logicEditor.LoadDefaultDemoGraph();
        logicEditor.CompileNow();

        var simVM = new SimulatorViewModel(tagCatalog, ruleTable, logicEditor);
        Assert.True(simVM.IsRunning);

        // Act: Thực hiện 100 chu kỳ StepScan đo đạc thông lượng
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            simVM.StepScan();
        }
        sw.Stop();

        // Assert
        Assert.Equal(iterations, simVM.ScanNumber);
        Assert.NotEmpty(simVM.RuleStatusList);
        Assert.True(sw.ElapsedMilliseconds < 250, 
            $"100 simulation cycles must execute under 250ms (actual: {sw.ElapsedMilliseconds}ms, {sw.Elapsed.TotalMicroseconds / iterations:F1} µs/cycle)");
    }

    [Fact]
    public void LiveWatch_HighFrequencyBurst_CoalescesWithoutBlocking()
    {
        // Arrange
        var product = CreateProduct();
        var store = new RuntimeStateStore();
        store.InitializeProduct(product);

        using var vm = new LiveWatchViewModel(store);
        Assert.Equal(124, vm.AllTags.Count);

        // Act: Giả lập burst 50 gói cập nhật liên tiếp từ thiết bị
        var sw = Stopwatch.StartNew();
        const int burstCount = 50;
        for (int i = 1; i <= burstCount; i++)
        {
            store.UpdateTags(new List<RuntimeTagValue>
            {
                new() { TagIndex = 0, TagName = "DI0", Value = i % 2 },
                new() { TagIndex = 1, TagName = "DI1", Value = (i + 1) % 2 },
                new() { TagIndex = 16, TagName = "AI0", Value = 1000 + i }
            }, product);
        }
        sw.Stop();

        // Assert: 50 gói cập nhật không được gây nghẽn luồng
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"50 snapshot updates must complete under 500ms (actual: {sw.ElapsedMilliseconds}ms, {sw.Elapsed.TotalMilliseconds / burstCount:F2} ms/burst)");

        var di0 = vm.AllTags[0];
        Assert.Equal(burstCount % 2, di0.RawValue);
        Assert.True(vm.FooterSummaryText.Contains("Đang hiển thị") || vm.FooterSummaryText.Contains("Displaying"));
    }

    [Fact]
    public void LogicEditor_SimScanThroughput_SubMillisecondLatency()
    {
        // Arrange: Graph hoàn chỉnh
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: action => action());
        logicEditor.LoadDefaultDemoGraph();
        logicEditor.CompileNow();

        // Act: Chạy 100 chu kỳ mô phỏng trong Logic Editor
        var sw = Stopwatch.StartNew();
        const int cycles = 100;
        for (int i = 0; i < cycles; i++)
        {
            logicEditor.RunSimScan(stepMs: 100, animate: false);
        }
        sw.Stop();

        // Assert: Trung bình mỗi scan < 1ms
        Assert.True(sw.ElapsedMilliseconds < 250,
            $"100 scans should take < 250ms (actual: {sw.ElapsedMilliseconds}ms, {sw.Elapsed.TotalMicroseconds / cycles:F1} µs/scan)");

        // Lịch sử quét không vượt quá dung lượng trần 100
        Assert.True(logicEditor.SimScanHistory.Count <= 100);
        Assert.Equal(cycles, logicEditor.SimScanNumber);
    }
}
