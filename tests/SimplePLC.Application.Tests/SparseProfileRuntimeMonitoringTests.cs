using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Application.Tests;

public class SparseProfileRuntimeMonitoringTests
{
    private sealed class MockRuntimeTagReader : IRuntimeTagReader
    {
        public ushort LastRequestedCount { get; private set; }
        public int[] ValuesToReturn { get; set; } = Array.Empty<int>();

        public Task<int[]> ReadRuntimeTagValuesAsync(byte slaveId = 1, ushort count = 124, CancellationToken cancellationToken = default)
        {
            LastRequestedCount = count;
            int[] buffer = new int[count];
            int copyLen = Math.Min(buffer.Length, ValuesToReturn.Length);
            Array.Copy(ValuesToReturn, buffer, copyLen);
            return Task.FromResult(buffer);
        }
    }

    private sealed class MockHealthReader : IDeviceHealthReader
    {
        public Task<DeviceHealthDto> ReadHealthAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceHealthDto
            {
                UptimeSeconds = 120,
                CpuLoadPercent = 15,
                RamUsagePercent = 30,
                ScanTimeMs = 10,
                MaxScanTimeMs = 12
            });
        }
    }

    [Fact]
    public async Task PollingLoop_WithSparseProfile_ReadsSufficientSlots_AndMapsTagsCorrectly()
    {
        // 1. Arrange: Tạo profile rời rạc 4 DI (0..3) + 4 DO (8..11) + 8 VFLAG (20..27)
        var resources = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 0,
            VirtualFlags: 8,
            VirtualRegisters: 0,
            RetentiveRegisters: 0,
            Counters: 0
        );

        bool built = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 10,
            runtimeTagCount: 16,
            resources: resources,
            out var sparseProduct,
            out var errorMsg
        );

        Assert.True(built, errorMsg);
        Assert.NotNull(sparseProduct);
        Assert.Equal(16, sparseProduct.Tags.Count);
        Assert.Equal(15, sparseProduct.Tags.Max(t => t.TagIndex)); // 16 tags: 0..15 theo dynamic layout

        // 2. Chuẩn bị Mock Reader với dữ liệu tại các vị trí động (DI: 0..3, DO: 4..7, VFLAG: 8..15)
        var mockTagReader = new MockRuntimeTagReader();
        var rawData = new int[32]; // Dải slot 0..31
        rawData[0] = 1;     // DI0 tại index 0
        rawData[3] = 0;     // DI3 tại index 3
        rawData[4] = 42;    // DO0 tại index 4
        rawData[7] = 99;    // DO3 tại index 7
        rawData[8] = 777;   // VFLAG0 tại index 8
        rawData[15] = 888;  // VFLAG7 tại index 15
        mockTagReader.ValuesToReturn = rawData;

        var mockHealth = new MockHealthReader();
        var stateStore = new RuntimeStateStore();

        using var monitor = new RuntimeMonitorService(mockTagReader, mockHealth, coordinator: null, stateStore: stateStore)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20),
            HealthCheckDivisor = 1
        };

        var tagsTcs = new TaskCompletionSource<IReadOnlyList<RuntimeTagValue>>();
        monitor.TagsUpdated += tags => tagsTcs.TrySetResult(tags);

        // 3. Act: Khởi động giám sát không truyền tagCount (để hệ thống tự suy luận dải cần đọc)
        await monitor.StartAsync(sparseProduct, slaveId: 1);

        var completedTask = await Task.WhenAny(tagsTcs.Task, Task.Delay(2000));
        Assert.Same(tagsTcs.Task, completedTask);

        var updatedTags = await tagsTcs.Task;

        // 4. Assert
        // Phải yêu cầu đọc ít nhất 16 slots để bao phủ được VFLAG7 (index 15)
        Assert.True(mockTagReader.LastRequestedCount >= 16,
            $"Expected LastRequestedCount >= 16, but was {mockTagReader.LastRequestedCount}");

        // Kết quả phải trả về đúng 16 tag của sản phẩm
        Assert.Equal(16, updatedTags.Count);

        // Kiểm tra ánh xạ đúng giá trị theo từng TagIndex
        var tagDict = updatedTags.ToDictionary(t => t.TagIndex);

        Assert.True(tagDict.ContainsKey(0));
        Assert.Equal(1, tagDict[0].Value);

        Assert.True(tagDict.ContainsKey(4), "DO0 (index 4) must be present in updated tags");
        Assert.Equal(42, tagDict[4].Value);

        Assert.True(tagDict.ContainsKey(7), "DO3 (index 7) must be present in updated tags");
        Assert.Equal(99, tagDict[7].Value);

        Assert.True(tagDict.ContainsKey(8), "VFLAG0 (index 8) must be present in updated tags");
        Assert.Equal(777, tagDict[8].Value);

        Assert.True(tagDict.ContainsKey(15), "VFLAG7 (index 15) must be present in updated tags");
        Assert.Equal(888, tagDict[15].Value);

        // Xác nhận trong RuntimeStateStore: tất cả 16 tag đều chuyển sang GOOD
        var snapshot = stateStore.CurrentSnapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(16, snapshot.Tags.Count);
        Assert.All(snapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        await monitor.StopAsync();
    }
}
