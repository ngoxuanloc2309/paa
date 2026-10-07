using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class RuntimeStateStoreTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset initialTime)
        {
            _utcNow = initialTime;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    private static ProductDefinition CreateStandardProduct() => ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void SkippedPollingLease_DoesNotMarkTagsStale()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time)
        {
            StaleThreshold = TimeSpan.FromMilliseconds(600)
        };
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        // First poll success
        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 1 } }, product);
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);

        // Act: IDeviceOperationCoordinator lease contention (e.g. Deploy rules for 2 seconds)
        store.PollingSuppressed = true;
        time.Advance(TimeSpan.FromSeconds(2)); // Far past 600ms threshold
        store.EvaluateStale();

        // Assert: Quality remains GOOD because skip was intentional policy!
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);
    }

    [Fact]
    public void SinglePollFailure_DoesNotImmediatelySetUnknown()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time)
        {
            StaleThreshold = TimeSpan.FromMilliseconds(600)
        };
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 1 } }, product);
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);

        // Act: Transient failure (e.g. 1 missed cycle of 200ms -> elapsed 300ms < 600ms)
        time.Advance(TimeSpan.FromMilliseconds(300));
        store.EvaluateStale();

        // Assert: Remains GOOD until threshold is exceeded, NEVER immediately UNKNOWN!
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);
    }

    [Fact]
    public void ThreeMissedPolls_TransitionsGoodToStale()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time)
        {
            StaleThreshold = TimeSpan.FromMilliseconds(600)
        };
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 1 } }, product);
        var initialUpdateAt = store.GetTag(0)!.LastSuccessfulUpdateAt;

        // Act: Exceed stale threshold (600ms)
        time.Advance(TimeSpan.FromMilliseconds(650));
        store.EvaluateStale();

        // Assert: Degrades to Stale, RawValue preserved, and LastSuccessfulUpdateAt UNTOUCHED!
        var staleTag = store.GetTag(0)!;
        Assert.Equal(TagQuality.Stale, staleTag.Quality);
        Assert.Equal(1, staleTag.RawValue);
        Assert.Equal(initialUpdateAt, staleTag.LastSuccessfulUpdateAt);
    }

    [Fact]
    public void SuccessfulPoll_AfterStale_TransitionsBackToGood()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time)
        {
            StaleThreshold = TimeSpan.FromMilliseconds(600)
        };
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 1 } }, product);

        time.Advance(TimeSpan.FromMilliseconds(700));
        store.EvaluateStale();
        Assert.Equal(TagQuality.Stale, store.GetTag(0)!.Quality);

        // Act: Fresh poll succeeds
        time.Advance(TimeSpan.FromMilliseconds(100));
        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 0 } }, product);

        // Assert: Restores to Good and updates value and timestamp
        var recovered = store.GetTag(0)!;
        Assert.Equal(TagQuality.Good, recovered.Quality);
        Assert.Equal(0, recovered.RawValue);
        Assert.Equal(time.GetUtcNow(), recovered.LastSuccessfulUpdateAt);
    }

    [Fact]
    public void Disconnect_TransitionsAllTagsToUnknown_ButPreservesLastValue()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time);
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 42 } }, product);
        var tagTimestamp = store.GetTag(0)!.LastSuccessfulUpdateAt;

        // Act: Disconnect session
        store.UpdateConnection(null, ConnectionStatus.Disconnected);

        // Assert: Quality becomes Unknown, but RawValue & Timestamp remain preserved for diagnostics!
        var tag = store.GetTag(0)!;
        Assert.Equal(TagQuality.Unknown, tag.Quality);
        Assert.Equal(42, tag.RawValue);
        Assert.Equal(tagTimestamp, tag.LastSuccessfulUpdateAt);
    }

    [Fact]
    public void Reconnect_FirstSuccessfulPoll_RestoresGood()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var time = new TestTimeProvider(startTime);
        var store = new RuntimeStateStore(time);
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        store.UpdateConnection(null, ConnectionStatus.Disconnected);
        Assert.Equal(TagQuality.Unknown, store.GetTag(0)!.Quality);

        // Act: Reconnected and first poll succeeds
        store.UpdateConnection(null, ConnectionStatus.Connected);
        time.Advance(TimeSpan.FromMilliseconds(50));
        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 100 } }, product);

        // Assert
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);
        Assert.Equal(100, store.GetTag(0)!.RawValue);
    }

    [Fact]
    public void SnapshotUpdated_IsRaisedOutsideStoreLock()
    {
        // Arrange: Verify that event handler can safely re-enter store APIs without deadlocking
        var store = new RuntimeStateStore();
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        bool reentrantReadSucceeded = false;
        store.SnapshotUpdated += snapshot =>
        {
            // Re-enter store methods during event callback
            var tag0 = store.GetTag(0);
            var all = store.GetAllTags();
            var curr = store.CurrentSnapshot;
            if (tag0 != null && all.Count > 0 && curr != null)
            {
                reentrantReadSucceeded = true;
            }
        };

        // Act
        store.UpdateTags(new[] { new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = 1 } }, product);

        // Assert
        Assert.True(reentrantReadSucceeded);
    }

    [Fact]
    public async Task ConcurrentReadersAndMonitorUpdates_DoNotDeadlock()
    {
        var store = new RuntimeStateStore();
        var product = CreateStandardProduct();
        store.InitializeProduct(product);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var writer = Task.Run(() =>
        {
            int counter = 0;
            while (!token.IsCancellationRequested)
            {
                counter++;
                store.UpdateTags(new[]
                {
                    new RuntimeTagValue { TagIndex = 0, TagName = "DI0", Value = counter }
                }, product);
                store.EvaluateStale();
                Thread.Sleep(5);
            }
        });

        var reader = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                var snap = store.CurrentSnapshot;
                var tag = store.GetTag(0);
                var all = store.GetAllTags();
                Assert.NotNull(snap);
                Assert.NotNull(all);
                Thread.Sleep(5);
            }
        });

        await Task.WhenAll(writer, reader);
    }

    [Fact]
    public void ProductDefinition_WithDifferentTagCount_InitializesCorrectly()
    {
        // Arrange: Custom product with 12 tags (e.g. mini gateway profile)
        var customTags = Enumerable.Range(0, 12).Select(i =>
            new TagDefinition((ushort)i, $"CUSTOM_{i}", TagKind.VirtualRegister, TagDataType.Int32, false)).ToList();
        var customProduct = new ProductDefinition("CustomGateway", customTags);

        var store = new RuntimeStateStore();

        // Act
        store.InitializeProduct(customProduct);

        // Assert: Exactly 12 tags initialized, dynamically without assuming 60!
        var tags = store.GetAllTags();
        Assert.Equal(12, tags.Count);
        Assert.Equal("CUSTOM_0", tags[0].Name);
        Assert.Equal("CUSTOM_11", tags[11].Name);
    }
}
