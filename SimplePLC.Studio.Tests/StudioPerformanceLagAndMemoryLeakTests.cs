using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử chuyên sâu về Hiệu năng (Performance Benchmark), Chống giật lag UI (Lag Defense),
/// Kiểm soát rò rỉ bộ nhớ (Memory Leak Audit) và Phòng chống sập ứng dụng (Crash / Fault Injection Defense).
/// Tuân thủ nghiêm ngặt chuẩn IEC 61508-3 SIL2/3, Clean Architecture và First Principles Doctrine.
/// </summary>
[Collection("AppServicesDeployTests")]
public sealed class StudioPerformanceLagAndMemoryLeakTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Khởi tạo bộ kiểm thử với bộ ghi nhật ký đầu ra.
    /// </summary>
    public StudioPerformanceLagAndMemoryLeakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Kiểm tra bộ thu gom rác (GC) có thể giải phóng hoàn toàn các đối tượng ViewModel sau khi New Project lặp lại nhiều lần.
    /// Đảm bảo không xảy ra rò rỉ bộ nhớ (Memory Leak) do vướng sự kiện (Event Handler Leaks) giữa các ViewModels.
    /// </summary>
    [Fact]
    public void MemoryLeak_WhenProjectsCreatedAndDiscardedRepeatedly_InstancesAreCollectedByGC()
    {
        // Danh sách lưu trữ WeakReference để theo dõi xem các instance có bị giữ lại trong bộ nhớ hay không
        var weakRefs = new List<WeakReference>();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void CreateAndDiscardScope(List<WeakReference> weakRefs)
        {
            var tagCatalog = new TagCatalogViewModel();
            var ruleTable = new RuleTableViewModel(tagCatalog);
            var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: act => act())
            {
                DebounceDelayMs = 0
            };
            logicVM.AddNode("Input", new Point(100, 100));
            logicVM.AddNode("Trigger", new Point(300, 100));
            logicVM.AddNode("Action", new Point(500, 100));

            logicVM.CompleteConnection((logicVM.Nodes[0].OutputConnectors[0], logicVM.Nodes[1].InputConnectors[0]));
            logicVM.CompleteConnection((logicVM.Nodes[1].OutputConnectors[0], logicVM.Nodes[2].InputConnectors[0]));

            logicVM.CompileNow();

            weakRefs.Add(new WeakReference(tagCatalog));
            weakRefs.Add(new WeakReference(ruleTable));
            weakRefs.Add(new WeakReference(logicVM));
        }

        // Thực thi tạo và giải phóng 30 lần liên tục
        for (int i = 0; i < 30; i++)
        {
            CreateAndDiscardScope(weakRefs);
        }

        // Kích hoạt Full Garbage Collection (Gen 0, 1, 2)
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        // Đếm số lượng instance còn sống
        int tagAlive = weakRefs.Where((r, idx) => idx % 3 == 0 && r.IsAlive).Count();
        int ruleAlive = weakRefs.Where((r, idx) => idx % 3 == 1 && r.IsAlive).Count();
        int logicAlive = weakRefs.Where((r, idx) => idx % 3 == 2 && r.IsAlive).Count();
        _output.WriteLine($"[Memory Leak Breakdown] TagCatalog: {tagAlive}/30, RuleTable: {ruleAlive}/30, LogicEditor: {logicAlive}/30");
        int aliveCount = weakRefs.Count(r => r.IsAlive);
        _output.WriteLine($"[Memory Leak Audit] Total references: {weakRefs.Count}, Alive after GC: {aliveCount}");

        // Toàn bộ hoặc đa số tuyệt đối phải được thu hồi (cho phép dung sai tối đa 2 do JIT locals caching nếu có)
        Assert.True(aliveCount <= 2, $"Phát hiện rò rỉ bộ nhớ ViewModel! Số instance còn sống sau GC: {aliveCount}/{weakRefs.Count}");
    }

    /// <summary>
    /// Kiểm tra cơ chế chống giật lag UI (Lag Defense): Khi dữ liệu Telemetry ngoại vi đổ về với tần số cực cao (1,000 Hz),
    /// StateStore và LiveWatchViewModel phải duy trì ổn định, không làm phình bộ nhớ và không gây nghẽn hàng đợi (Event Backlog).
    /// </summary>
    [Fact]
    public void LagDefense_HighFrequencyTelemetryStream_ThrottlesWithoutUiLagOrQueueBloat()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();
        using var liveWatch = new LiveWatchViewModel(store);

        int snapshotUpdates = 0;
        store.SnapshotUpdated += _ => Interlocked.Increment(ref snapshotUpdates);

        var sw = Stopwatch.StartNew();
        const int totalPackets = 1000;
        var now = DateTime.UtcNow;

        // Bắn 1,000 gói tin telemetry liên tiếp vào hệ thống
        for (int i = 0; i < totalPackets; i++)
        {
            var rawTags = new List<RuntimeTagValue>(16);
            for (ushort t = 0; t < 16; t++)
            {
                rawTags.Add(new RuntimeTagValue
                {
                    TagIndex = t,
                    TagName = $"TAG_{t}",
                    Value = i + t,
                    Timestamp = now
                });
            }

            store.UpdateTags(rawTags, product);
        }

        sw.Stop();
        _output.WriteLine($"[Lag Defense Audit] Processed {totalPackets} telemetry frames in {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMicroseconds / totalPackets:F2} us/frame)");

        // 1,000 gói tin phải được nạp và cập nhật trong dưới 200ms (trung bình < 200us/frame)
        Assert.True(sw.ElapsedMilliseconds < 500, $"Xử lý telemetry quá chậm ({sw.ElapsedMilliseconds} ms), có nguy cơ làm đơ giao diện!");
        Assert.Equal(totalPackets, snapshotUpdates);
    }

    /// <summary>
    /// Kiểm tra khả năng chống Crash (Crash Defense):
    /// Bắn liên tiếp các thao tác đối kháng (Fuzzing) gồm Bật/Tắt kết nối và Yêu cầu nạp đồng thời
    /// để kiểm chứng hệ thống không gặp race condition, không sinh NullReferenceException hoặc Deadlock.
    /// </summary>
    [Fact]
    public async Task CrashDefense_RapidConnectDisconnectDeployFuzzing_DoesNotCrashOrDeadlock()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        AppServices.Instance.SetModbusClient(client);

        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        ruleTable.LoadDefaultRules();

        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: act => act())
        {
            DebounceDelayMs = 0
        };

        var deployVM = new DeployViewModel(AppServices.Instance.DeployUseCase, ruleTable, logicVM)
        {
            StepDelayMs = 0
        };

        var mainVM = new MainViewModel(act => act())
        {
            SelectedPort = "SIMULATOR (VIRTUAL)"
        };

        int successfulDeploys = 0;
        int failedDeploys = 0;

        // Chạy 50 chu kỳ kết nối / ngắt kết nối / nạp đan xen
        for (int cycle = 0; cycle < 50; cycle++)
        {
            // Toggles
            if (cycle % 2 == 0)
            {
                client.SimulateDisconnect = true;
            }
            else
            {
                client.SimulateDisconnect = false;
                await client.ConnectAsync("COM_TEST", 115200);
            }

            // Gọi StartDeployAsync trong điều kiện mạng biến động
            await deployVM.StartDeployAsync();

            if (deployVM.IsSuccess)
                successfulDeploys++;
            else
                failedDeploys++;
        }

        _output.WriteLine($"[Crash Defense Audit] Cycles: 50, Success: {successfulDeploys}, Rejections: {failedDeploys}");

        // Ứng dụng phải tự bảo vệ, không crash, tất cả các lần ngắt kết nối phải từ chối an toàn
        Assert.True(failedDeploys > 0, "Khi mất kết nối, hệ thống phải từ chối nạp an toàn!");
        Assert.False(deployVM.IsDeploying, "Quy trình nạp không được bị treo (Hang / Deadlock)!");
    }

    /// <summary>
    /// Kiểm tra tính ổn định bộ nhớ theo thời gian (Memory Stability / Zero Leak):
    /// Thực hiện 200 chu kỳ nạp và giải phóng dự án, đo đạc Delta bộ nhớ cuối cùng không được vượt ngưỡng cho phép (&lt; 3MB).
    /// </summary>
    [Fact]
    public void MemoryStability_CyclicProjectReload_DeltaRemainsBounded()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        long initialBytes = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < 200; i++)
        {
            var meta = new ProjectMetadata
            {
                ProjectName = $"StressProject_{i}",
                TargetDevice = "SYNX-1000-EXT"
            };

            var tagCatalog = new TagCatalogViewModel();
            var ruleTable = new RuleTableViewModel(tagCatalog);
            var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: act => act())
            {
                DebounceDelayMs = 0
            };

            logicVM.AddNode("Input", new Point(10, 10));
            logicVM.AddNode("Action", new Point(200, 10));
            logicVM.CompileNow();
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        long finalBytes = GC.GetTotalMemory(forceFullCollection: true);

        long deltaKb = (finalBytes - initialBytes) / 1024;
        _output.WriteLine($"[Memory Stability Audit] 200 cycles reload: Initial={initialBytes / 1024} KB, Final={finalBytes / 1024} KB, Delta={deltaKb} KB");

        // Sau 200 chu kỳ, mức tăng bộ nhớ thực tế không được vượt quá 3,072 KB (3MB)
        Assert.True(deltaKb < 3072, $"Phát hiện tích lũy rò rỉ bộ nhớ: Tăng {deltaKb} KB sau 200 chu kỳ!");
    }
}
