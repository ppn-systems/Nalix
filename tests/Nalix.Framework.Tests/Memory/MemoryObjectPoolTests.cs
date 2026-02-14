
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Pools;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

public sealed partial class MemoryTests
{
    [Fact]
    public void Get_ObjectPoolRoundTrip_ResetsReturnedInstance()
    {
        ObjectPool pool = new(defaultMaxItemsPerType: 8);
        List<string> traces = [];
        pool.TraceOccurred += traces.Add;

        TestPoolable instance = pool.Get<TestPoolable>();
        instance.Value = 42;
        pool.Return(instance);
        TestPoolable reused = pool.Get<TestPoolable>();
        Dictionary<string, object> stats = pool.GetStatistics();

        Assert.Equal(0, reused.Value);
        Assert.True(pool.TotalCreatedCount >= 1);
        Assert.True(pool.TotalRentedCount >= 2);
        Assert.True(pool.TotalReturnedCount >= 1);
        Assert.True(pool.TotalAvailableCount >= 0);
        Assert.True(pool.TypeCount >= 1);
        Assert.True(pool.UptimeMs >= 0);
        Assert.Equal(pool.TypeCount, ((IReadOnlyCollection<Dictionary<string, object>>)pool.GetAllTypeInfo()).Count);
        Assert.Equal(pool.TotalCreatedCount, stats["TotalCreatedCount"]);
        Assert.NotNull(ObjectPool.Default);
        Assert.NotNull(traces);
    }

    [Fact]
    public void SetMaxCapacity_ObjectPoolUsesBatchApis_ReturnsExpectedResults()
    {
        ObjectPool pool = new(defaultMaxItemsPerType: 4);

        bool negativeCapacity = pool.SetMaxCapacity<TestPoolable>(-1);
        bool configuredCapacity = pool.SetMaxCapacity<TestPoolable>(2);
        int preallocated = pool.Prealloc<TestPoolable>(3);
        List<TestPoolable> rented = pool.GetMultiple<TestPoolable>(2);
        int returned = pool.ReturnMultiple(rented);
        int trimmed = pool.Trim(0);
        Dictionary<string, object> typeInfo = pool.GetTypeInfo<TestPoolable>();
        int clearedType = pool.ClearType<TestPoolable>();
        int clearedAll = pool.Clear();
        pool.ResetStatistics();

        Assert.False(negativeCapacity);
        Assert.True(configuredCapacity);
        Assert.True(preallocated >= 0);
        Assert.Equal(2, rented.Count);
        Assert.Equal(2, returned);
        Assert.True(trimmed >= 0);
        Assert.Equal(nameof(TestPoolable), typeInfo["TypeName"]);
        Assert.True(clearedType >= 0);
        Assert.True(clearedAll >= 0);
        Assert.Equal(0L, pool.TotalCreatedCount);
        Assert.Equal(0L, pool.TotalRentedCount);
        Assert.Equal(0L, pool.TotalReturnedCount);
    }

    [Fact]
    public void Get_TypedObjectPoolStateUnderTest_UsesTypedOperations()
    {
        ObjectPool pool = new(defaultMaxItemsPerType: 4);
        TypedObjectPool<TestPoolable> typedPool = pool.CreateTypedPool<TestPoolable>();

        int preallocated = typedPool.Prealloc(2);
        TestPoolable item = typedPool.Get();
        item.Value = 77;
        typedPool.Return(item);
        List<TestPoolable> rented = typedPool.GetMultiple(2);
        int returned = typedPool.ReturnMultiple(rented);
        typedPool.SetMaxCapacity(3);
        Dictionary<string, object> info = typedPool.GetInfo();
        int cleared = typedPool.Clear();

        Assert.True(preallocated >= 0);
        Assert.Equal(2, rented.Count);
        Assert.Equal(2, returned);
        Assert.Equal(nameof(TestPoolable), info["TypeName"]);
        Assert.True(cleared >= 0);
    }

    [Fact]
    public void Get_ObjectPoolManagerLifecycle_TracksStatisticsAndReports()
    {
        ObjectPoolManager manager = new() { DefaultMaxPoolSize = 8 };

        TestPoolable first = manager.Get<TestPoolable>();
        first.Value = 9;
        manager.Return(first);
        TestPoolable second = manager.Get<TestPoolable>();
        int preallocated = manager.Prealloc<TestPoolable>(2);
        bool setCapacity = manager.SetMaxCapacity<TestPoolable>(4);
        Dictionary<string, object> typeInfo = manager.GetTypeInfo<TestPoolable>();
        IDictionary<string, object> reportData = manager.GenerateReportData();
        string report = manager.GenerateReport();

        Assert.Equal(0, second.Value);
        Assert.True(manager.PoolCount >= 1);
        Assert.True(manager.PeakPoolCount >= 1);
        Assert.True(manager.TotalGetOperations >= 2);
        Assert.True(manager.TotalReturnOperations >= 1);
        Assert.True(manager.TotalCacheHits >= 1);
        Assert.True(manager.TotalCacheMisses >= 0);
        Assert.True(manager.CacheHitRate >= 0);
        Assert.True(manager.Uptime >= TimeSpan.Zero);
        Assert.True(preallocated >= 0);
        Assert.True(setCapacity);
        Assert.Equal(nameof(TestPoolable), typeInfo["TypeName"]);
        Assert.True(reportData.ContainsKey("Pools"));
        Assert.Contains("ObjectPoolManager Status", report);
    }

    [Fact]
    public void ClearPool_ObjectPoolManagerMaintenanceApis_ReturnExpectedCounts()
    {
        ObjectPoolManager manager = new() { DefaultMaxPoolSize = 4 };
        _ = manager.Prealloc<TestPoolable>(2);
        TypedObjectPoolAdapter<TestPoolable> adapter = manager.GetTypedPool<TestPoolable>();

        List<TestPoolable> rented = adapter.GetMultiple(2);
        int returned = adapter.ReturnMultiple(rented);
        int trimmed = adapter.Trim(150);
        int preallocated = adapter.Prealloc(1);
        adapter.SetMaxCapacity(3);
        Dictionary<string, object> info = adapter.GetInfo();
        int clearedAdapter = adapter.Clear();
        int clearedPool = manager.ClearPool<TestPoolable>();
        int clearedAll = manager.ClearAllPools();
        int trimmedAll = manager.TrimAllPools(50);
        manager.ResetStatistics();

        Assert.Equal(2, returned);
        Assert.True(trimmed >= 0);
        Assert.True(preallocated >= 0);
        Assert.Equal(nameof(TestPoolable), info["TypeName"]);
        Assert.True(clearedAdapter >= 0);
        Assert.True(clearedPool >= 0);
        Assert.True(clearedAll >= 0);
        Assert.True(trimmedAll >= 0);
        Assert.Equal(0L, manager.TotalGetOperations);
        Assert.Equal(0L, manager.TotalReturnOperations);
    }

    [Fact]
    public async Task PerformHealthCheck_StateUnderTest_ReportsUnhealthyPoolsAndStopsScheduledWork()
    {
        ObjectPoolManager manager = new();
        _ = manager.Get<HealthCheckPoolable>();
        using CancellationTokenSource cancellationTokenSource = new();

        int unhealthyPools = manager.PerformHealthCheck();
        Task scheduled = manager.ScheduleRegularTrimming(TimeSpan.FromMilliseconds(10), cancellationToken: cancellationTokenSource.Token);
        cancellationTokenSource.CancelAfter(20);
        await scheduled.ConfigureAwait(true);

        Assert.True(unhealthyPools >= 0);
        Assert.True(manager.UnhealthyPoolCount >= 0);
    }

    [Fact]
    public void Return_TypedObjectPoolAdapterNullObject_ThrowsArgumentNullException()
    {
        ObjectPoolManager manager = new();
        TypedObjectPoolAdapter<TestPoolable> adapter = manager.GetTypedPool<TestPoolable>();

        _ = Assert.Throws<ArgumentNullException>(() => adapter.Return(null!));
        _ = Assert.Throws<ArgumentNullException>(() => adapter.ReturnMultiple(null!));
    }

    [Fact]
    public void GetMultiple_ObjectPoolCountIsInvalid_ThrowsArgumentException()
    {
        ObjectPool pool = new();

        static void Act(ObjectPool objectPool) => _ = objectPool.GetMultiple<TestPoolable>(0);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Act(pool));

        Assert.Equal("count", exception.ParamName);
    }
}
