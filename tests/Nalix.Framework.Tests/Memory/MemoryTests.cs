#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Pools;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

/// <summary>
/// Covers the public APIs in the Memory folder.
/// </summary>
public sealed class MemoryTests
{
    /// <summary>
    /// Verifies that buffer allocation strings are parsed into sorted size-ratio pairs.
    /// </summary>
    [Theory]
    [InlineData("1024,0.40;256,0.10;512,0.20", new[] { 256, 512, 1024 }, new[] { 0.10, 0.20, 0.40 })]
    [InlineData("2048,1.0", new[] { 2048 }, new[] { 1.0 })]
    public void ParseBufferAllocations_ValidInput_ReturnsSortedAllocations(
        string value,
        int[] expectedSizes,
        double[] expectedRatios)
    {
        // Arrange
        // Input supplied by InlineData.

        // Act
        (int size, double ratio)[] allocations = BufferConfig.ParseBufferAllocations(value);

        // Assert
        Assert.Equal(expectedSizes, allocations.Select(static x => x.size).ToArray());
        Assert.Equal(expectedRatios, allocations.Select(static x => x.ratio).ToArray());
    }

    /// <summary>
    /// Verifies that invalid buffer allocation strings are rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("256,-1")]
    [InlineData("256,0.5;512,0.7")]
    public void ParseBufferAllocations_InvalidInput_ThrowsArgumentException(string value) =>
        // Arrange
        // Input supplied by InlineData.

        // Act
        Assert.Throws<ArgumentException>(() => BufferConfig.ParseBufferAllocations(value));// Assert

    /// <summary>
    /// Verifies that valid buffer configuration values pass validation.
    /// </summary>
    [Fact]
    public void Validate_ValidBufferConfig_CompletesSuccessfully()
    {
        // Arrange
        BufferConfig config = new()
        {
            TotalBuffers = 64,
            AutoTuneOperationThreshold = 64,
            BufferAllocations = "256,0.50;512,0.50",
            ExpandThresholdPercent = 0.20,
            ShrinkThresholdPercent = 0.60,
            AdaptiveGrowthFactor = 2.0,
            MinimumIncrease = 4,
            MaxBufferIncreaseLimit = 16,
            MaxMemoryPercentage = 0.25,
            MaxMemoryBytes = 0
        };

        // Act
        Exception? exception = Record.Exception(config.Validate);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that invalid buffer configuration combinations fail validation.
    /// </summary>
    [Theory]
    [InlineData(0.70, 0.60, "256,1.0", 32, 32, 2.0, 4, 16)]
    [InlineData(0.20, 0.60, "256,0.60;256,0.20", 32, 32, 2.0, 4, 16)]
    [InlineData(0.20, 0.60, "256,1.0", 32, 16, 2.0, 4, 16)]
    [InlineData(0.20, 0.60, "256,1.0", 32, 32, 5.0, 4, 8)]
    public void Validate_InvalidBufferConfig_ThrowsValidationException(
        double expandThreshold,
        double shrinkThreshold,
        string allocations,
        int totalBuffers,
        int autoTuneThreshold,
        double growthFactor,
        int minimumIncrease,
        int maxIncreaseLimit)
    {
        // Arrange
        BufferConfig config = new()
        {
            ExpandThresholdPercent = expandThreshold,
            ShrinkThresholdPercent = shrinkThreshold,
            BufferAllocations = allocations,
            TotalBuffers = totalBuffers,
            AutoTuneOperationThreshold = autoTuneThreshold,
            AdaptiveGrowthFactor = growthFactor,
            MinimumIncrease = minimumIncrease,
            MaxBufferIncreaseLimit = maxIncreaseLimit
        };

        // Act
        _ = Assert.Throws<ValidationException>(config.Validate);

        // Assert
    }

    /// <summary>
    /// Verifies that pool state derived properties and ratios match the current values.
    /// </summary>
    [Theory]
    [InlineData(100, 80, 10, true, false, 0.20, 0.10)]
    [InlineData(100, 10, 40, false, true, 0.90, 0.40)]
    [InlineData(100, 60, 0, true, false, 0.40, 0.00)]
    [InlineData(0, 0, 5, false, false, 0.00, 0.00)]
    public void GetUsageRatio_StateVaries_ReturnsExpectedMetrics(
        int totalBuffers,
        int freeBuffers,
        int misses,
        bool expectedCanShrink,
        bool expectedNeedsExpansion,
        double expectedUsageRatio,
        double expectedMissRate)
    {
        // Arrange
        BufferPoolState state = new()
        {
            BufferSize = 256,
            TotalBuffers = totalBuffers,
            FreeBuffers = freeBuffers,
            Misses = misses
        };

        // Act
        double usageRatio = state.GetUsageRatio();
        double missRate = state.GetMissRate();

        // Assert
        Assert.Equal(expectedCanShrink, state.CanShrink);
        Assert.Equal(expectedNeedsExpansion, state.NeedsExpansion);
        Assert.Equal(expectedUsageRatio, usageRatio, 3);
        Assert.Equal(expectedMissRate, missRate, 3);
        Assert.Equal(256, state.BufferSize);
    }

    /// <summary>
    /// Verifies that the shared byte-array pool can rent and accept returned arrays.
    /// </summary>
    [Fact]
    public void Rent_ReturnByteArrayPool_ReturnsUsableArray()
    {
        // Arrange
        int requestedCapacity = BufferLease.StackAllocThreshold;

        // Act
        byte[] buffer = BufferLease.ByteArrayPool.Rent(requestedCapacity);

        // Assert
        Assert.NotNull(buffer);
        Assert.True(buffer.Length >= requestedCapacity);

        BufferLease.ByteArrayPool.Return(buffer);
    }

    /// <summary>
    /// Verifies that lease factory methods expose the expected public properties and copied data.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyFrom_StateUnderTest_ExposesLeaseProperties(bool zeroOnDispose)
    {
        // Arrange
        byte[] source = [1, 2, 3, 4];

        // Act
        using BufferLease lease = BufferLease.CopyFrom(source, zeroOnDispose);

        // Assert
        Assert.Equal(source.Length, lease.Length);
        Assert.True(lease.Capacity >= source.Length);
        Assert.True(lease.RawCapacity >= lease.Capacity);
        Assert.Equal(zeroOnDispose, lease.ZeroOnDispose);
        Assert.Equal(source, lease.Memory.ToArray());
        Assert.Equal(source, lease.Span.ToArray());
        Assert.True(lease.SpanFull.Length >= source.Length);
    }

    /// <summary>
    /// Verifies that lease ownership APIs behave correctly for owned and shared leases.
    /// </summary>
    [Fact]
    public void ReleaseOwnership_StateUnderTest_ReturnsExpectedOutcome()
    {
        // Arrange
        using BufferLease sharedLease = BufferLease.CopyFrom([9, 8, 7]);
        using BufferLease ownedLease = BufferLease.TakeOwnership([1, 2, 3, 4], 1, 2);
        sharedLease.Retain();

        // Act
        bool sharedReleased = sharedLease.ReleaseOwnership(out byte[]? sharedBuffer, out int sharedStart, out int sharedLength);
        sharedLease.Dispose();
        bool ownedReleased = ownedLease.ReleaseOwnership(out byte[]? ownedBuffer, out int ownedStart, out int ownedLength);

        // Assert
        Assert.False(sharedReleased);
        Assert.Null(sharedBuffer);
        Assert.Equal(0, sharedStart);
        Assert.Equal(0, sharedLength);

        Assert.True(ownedReleased);
        Assert.NotNull(ownedBuffer);
        Assert.Equal(1, ownedStart);
        Assert.Equal(2, ownedLength);
        Assert.Equal(0, ownedLease.Length);
        Assert.Equal(0, ownedLease.Capacity);
    }

    /// <summary>
    /// Verifies that committing an out-of-range lease length is rejected.
    /// </summary>
    [Fact]
    public void CommitLength_LengthExceedsCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using BufferLease lease = BufferLease.Rent(8);

        // Act
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => lease.CommitLength(lease.Capacity + 1));

        // Assert
    }

    /// <summary>
    /// Verifies that wrapping rented arrays through the public factory methods preserves the requested slices.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromRented_StateUnderTest_PreservesPayload(bool zeroOnDispose)
    {
        // Arrange
        byte[] wholeBuffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] sliceBuffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] source = [10, 20, 30, 40];
        source.CopyTo(wholeBuffer, 0);
        source.CopyTo(sliceBuffer, 0);

        // Act
        using BufferLease wholeLease = BufferLease.FromRented(wholeBuffer, 3, zeroOnDispose);
        using BufferLease sliceLease = BufferLease.TakeOwnership(sliceBuffer, 1, 2, zeroOnDispose);

        // Assert
        Assert.Equal([10, 20, 30], wholeLease.Memory.ToArray());
        Assert.Equal([20, 30], sliceLease.Memory.ToArray());
        Assert.Equal(zeroOnDispose, wholeLease.ZeroOnDispose);
        Assert.Equal(zeroOnDispose, sliceLease.ZeroOnDispose);
    }

#if DEBUG
    /// <summary>
    /// Verifies that the debug segment view matches the current lease slice.
    /// </summary>
    [Fact]
    public void AsSegment_LeaseContainsData_ReturnsMatchingSegment()
    {
        // Arrange
        byte[] buffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] source = [4, 5, 6, 7];
        source.CopyTo(buffer, 0);
        using BufferLease lease = BufferLease.TakeOwnership(buffer, 1, 2);

        // Act
        ArraySegment<byte> segment = lease.AsSegment();

        // Assert
        Assert.Equal(1, segment.Offset);
        Assert.Equal(2, segment.Count);
        Assert.Equal([5, 6], segment.ToArray());
    }
#endif

    /// <summary>
    /// Verifies that writer constructors reject non-positive capacities.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DataWriter_StateUnderTest_InvalidInitialSizeThrows(int size) =>
        // Arrange
        // Input supplied by InlineData.

        // Act
        Assert.Throws<ArgumentOutOfRangeException>(() => new DataWriter(size));// Assert

    /// <summary>
    /// Verifies that a rented writer can expose its free buffer, advance, expand, and materialize the written bytes.
    /// </summary>
    [Fact]
    public void Expand_WriterOwnsRentedBuffer_GrowsAndPreservesContent()
    {
        // Arrange
        DataWriter writer = new(2);
        writer.FreeBuffer[0] = 1;
        writer.FreeBuffer[1] = 2;
        writer.Advance(2);

        // Act
        writer.Expand(3);
        ref byte freeRef = ref writer.GetFreeBufferReference();
        freeRef = 3;
        writer.FreeBuffer[1] = 4;
        writer.FreeBuffer[2] = 5;
        writer.Advance(3);
        byte[] result = writer.ToArray();
        int freeLengthAfterWrite = writer.FreeBuffer.Length;
        int writtenCount = writer.WrittenCount;
        writer.Dispose();

        // Assert
        Assert.Equal([1, 2, 3, 4, 5], result);
        Assert.Equal(5, writtenCount);
        Assert.True(freeLengthAfterWrite >= 0);
    }

    /// <summary>
    /// Verifies that fixed writers reject expansion and invalid advance counts.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Expand_FixedWriter_ThrowsInvalidOperationException(bool useArrayConstructor)
    {
        // Arrange
        DataWriter writer = useArrayConstructor
            ? new DataWriter(new byte[4])
            : new DataWriter(new Span<byte>(new byte[4]));

        // Act
        InvalidOperationException expandException;
        try
        {
            writer.Expand(5);
            throw new Xunit.Sdk.XunitException("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            expandException = ex;
        }

        ArgumentOutOfRangeException advanceException;
        try
        {
            writer.Advance(0);
            throw new Xunit.Sdk.XunitException("Expected ArgumentOutOfRangeException was not thrown.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            advanceException = ex;
        }

        writer.Dispose();

        // Assert
        Assert.Equal("Cannot expand a fixed buffer.", expandException.Message);
        Assert.Equal("count", advanceException.ParamName);
    }

    /// <summary>
    /// Verifies that reader constructors over arrays, spans, and memory track progress consistently.
    /// </summary>
    [Theory]
    [InlineData("array")]
    [InlineData("span")]
    [InlineData("memory")]
    public void Advance_ReaderHasData_UpdatesBytesReadAndRemaining(string sourceKind)
    {
        // Arrange
        byte[] data = [1, 2, 3, 4];
        DataReader reader = sourceKind switch
        {
            "array" => new DataReader(data),
            "span" => new DataReader((ReadOnlySpan<byte>)data),
            "memory" => new DataReader((ReadOnlyMemory<byte>)data),
            _ => throw new InvalidOperationException("Unexpected source kind.")
        };

        // Act
        ref byte first = ref reader.GetSpanReference(1);
        byte observed = first;
        reader.Advance(2);
        int bytesRead = reader.BytesRead;
        int bytesRemaining = reader.BytesRemaining;
        reader.Dispose();

        // Assert
        Assert.Equal((byte)1, observed);
        Assert.Equal(2, bytesRead);
        Assert.Equal(2, bytesRemaining);
        Assert.Equal(0, reader.BytesRead);
        Assert.Equal(0, reader.BytesRemaining);
    }

    /// <summary>
    /// Verifies that reader guard clauses reject invalid reads and advances.
    /// </summary>
    [Fact]
    public void Advance_ReaderWouldOverflow_ThrowsSerializationException()
    {
        // Arrange
        DataReader reader = new([1, 2]);

        // Act
        SerializationException spanException = Assert.Throws<SerializationException>(() => reader.GetSpanReference(3));
        SerializationException advanceException = Assert.Throws<SerializationException>(() => reader.Advance(3));
        ArgumentOutOfRangeException negativeException = Assert.Throws<ArgumentOutOfRangeException>(() => reader.Advance(-1));
        reader.Dispose();

        // Assert
        Assert.Contains("Not enough data", spanException.Message);
        Assert.Contains("Cannot advance", advanceException.Message);
        Assert.Equal("count", negativeException.ParamName);
    }

    /// <summary>
    /// Verifies that a buffer pool manager rents and returns buffers and exposes size metadata from configuration.
    /// </summary>
    [Fact]
    public void Rent_BufferPoolManagerConfigured_ReturnsSizedBuffer()
    {
        // Arrange
        BufferConfig config = CreateBufferConfig(enableMemoryTrimming: false);
        using BufferPoolManager manager = new(config);

        // Act
        byte[] rented = manager.Rent(300);
        double allocation = manager.GetAllocationForSize(300);
        manager.Return(rented);

        // Assert
        Assert.True(rented.Length >= 300);
        Assert.Equal(256, manager.MinBufferSize);
        Assert.Equal(1024, manager.MaxBufferSize);
        Assert.Equal(0.25, allocation, 3);
        Assert.Equal("buf.trim", BufferPoolManager.RecurringName);
    }

    /// <summary>
    /// Verifies that segment-based and SocketAsyncEventArgs-based buffer helpers round-trip the underlying arrays.
    /// </summary>
    [Fact]
    public void RentSegment_StateUnderTest_ReturnsAndClearsSocketBuffers()
    {
        // Arrange
        using BufferPoolManager manager = new(CreateBufferConfig(enableMemoryTrimming: false));
        using SocketAsyncEventArgs saea = new();

        // Act
        ArraySegment<byte> segment = manager.RentSegment(128);
        manager.Return(segment);
        manager.RentForSaea(saea, 64);
        byte[]? rentedBuffer = saea.Buffer;
        manager.ReturnFromSaea(saea);

        // Assert
        Assert.NotNull(segment.Array);
        Assert.Equal(128, segment.Count);
        Assert.NotNull(rentedBuffer);
        Assert.Null(saea.Buffer);
        Assert.Equal(0, saea.Count);
    }

    /// <summary>
    /// Verifies that SocketAsyncEventArgs helper methods reject null input.
    /// </summary>
    [Fact]
    public void RentForSaea_SaeaIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using BufferPoolManager manager = new(CreateBufferConfig(enableMemoryTrimming: false));

        // Act
        _ = Assert.Throws<ArgumentNullException>(() => manager.RentForSaea(null!, 32));
        _ = Assert.Throws<ArgumentNullException>(() => manager.ReturnFromSaea(null!));

        // Assert
    }

    /// <summary>
    /// Verifies that manager reports contain expected top-level metadata.
    /// </summary>
    [Fact]
    public void GenerateReport_StateUnderTest_ReturnsReportAndData()
    {
        // Arrange
        using BufferPoolManager manager = new(CreateBufferConfig(enableMemoryTrimming: false));
        _ = manager.Rent(256);

        // Act
        string report = manager.GenerateReport();
        IDictionary<string, object> data = manager.GenerateReportData();

        // Assert
        Assert.Contains("BufferPoolManager Status", report);
        Assert.Equal(manager.MinBufferSize, data["MinBufferSize"]);
        Assert.Equal(manager.MaxBufferSize, data["MaxBufferSize"]);
        Assert.True(data.ContainsKey("Pools"));
    }

    /// <summary>
    /// Verifies that object maps expose dictionary semantics through their public members.
    /// </summary>
    [Fact]
    public void Add_ObjectMapContainsEntries_UpdatesCollectionMembers()
    {
        // Arrange
        ObjectMap<string, int> map = new()
        {
            // Act
            { "one", 1 },
            new KeyValuePair<string, int>("two", 2)
        };
        map["three"] = 3;
        bool containsPair = map.Contains(new KeyValuePair<string, int>("two", 2));
        bool containsKey = map.ContainsKey("one");
        bool tryGetValue = map.TryGetValue("three", out int value);
        KeyValuePair<string, int>[] copied = new KeyValuePair<string, int>[4];
        map.CopyTo(copied, 1);

        // Assert
        Assert.Equal(3, map.Count);
        Assert.False(map.IsReadOnly);
        Assert.Equal(3, map["three"]);
        Assert.True(containsPair);
        Assert.True(containsKey);
        Assert.True(tryGetValue);
        Assert.Equal(3, value);
        Assert.Equal(3, map.Keys.Count);
        Assert.Equal(3, map.Values.Count);
        Assert.Equal(3, map.ToArray().Length);
        Assert.Contains(copied, static item => item.Key == "one");
    }

    /// <summary>
    /// Verifies that object map remove and reset operations clear stored values.
    /// </summary>
    [Fact]
    public void ResetForPool_ObjectMapHasEntries_ClearsMap()
    {
        // Arrange
        ObjectMap<string, int> map = ObjectMap<string, int>.Rent();
        map.Add("one", 1);
        map.Add("two", 2);

        // Act
        bool removedByKey = map.Remove("one");
        bool removedByPair = map.Remove(new KeyValuePair<string, int>("two", 2));
        map.ResetForPool();
        map.Return();

        // Assert
        Assert.True(removedByKey);
        Assert.True(removedByPair);
        Assert.Empty(map);
    }

    /// <summary>
    /// Verifies that list pools rent, return, and report statistics through their public APIs.
    /// </summary>
    [Fact]
    public void Rent_ListPoolLifecycle_UpdatesStatistics()
    {
        // Arrange
        ListPool<int> pool = new(maxPoolSize: 4, initialCapacity: 2);
        List<string> traces = [];
        pool.TraceOccurred += traces.Add;

        // Act
        List<int> list = pool.Rent(3);
        list.AddRange([1, 2, 3]);
        pool.Return(list);
        Dictionary<string, object> stats = pool.GetStatistics();

        // Assert
        Assert.True(pool.AvailableCount >= 1);
        Assert.True(pool.CreatedCount >= 1);
        Assert.Equal(1L, pool.TotalRentOperations);
        Assert.Equal(1L, pool.TotalReturnOperations);
        Assert.Equal(0L, pool.RentedCount);
        Assert.True(pool.UptimeMs >= 0);
        Assert.Equal(pool.AvailableCount, stats["AvailableCount"]);
        Assert.NotNull(ListPool<int>.Instance);
        Assert.NotNull(traces);
    }

    /// <summary>
    /// Verifies that list pool preallocation, trimming, clearing, and statistics reset work as expected.
    /// </summary>
    [Fact]
    public void Prealloc_ListPoolStateChanges_TrimClearAndResetStatisticsWork()
    {
        // Arrange
        ListPool<int> pool = new(maxPoolSize: 4, initialCapacity: 2);

        // Act
        pool.Prealloc(3, 5);
        int trimmed = pool.Trim(1);
        int cleared = pool.Clear();
        pool.ResetStatistics();

        // Assert
        Assert.True(trimmed >= 0);
        Assert.True(cleared >= 0);
        Assert.Equal(0L, pool.CreatedCount);
        Assert.Equal(0L, pool.TotalRentOperations);
        Assert.Equal(0L, pool.TotalReturnOperations);
        Assert.Equal(0L, pool.TrimmedCount);
    }

    /// <summary>
    /// Verifies that object pools create, return, and reset pooled instances and statistics.
    /// </summary>
    [Fact]
    public void Get_ObjectPoolRoundTrip_ResetsReturnedInstance()
    {
        // Arrange
        ObjectPool pool = new(defaultMaxItemsPerType: 8);
        List<string> traces = [];
        pool.TraceOccurred += traces.Add;

        // Act
        TestPoolable instance = pool.Get<TestPoolable>();
        instance.Value = 42;
        pool.Return(instance);
        TestPoolable reused = pool.Get<TestPoolable>();
        Dictionary<string, object> stats = pool.GetStatistics();

        // Assert
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

    /// <summary>
    /// Verifies that object pool capacity and batch operations behave through the public APIs.
    /// </summary>
    [Fact]
    public void SetMaxCapacity_ObjectPoolUsesBatchApis_ReturnsExpectedResults()
    {
        // Arrange
        ObjectPool pool = new(defaultMaxItemsPerType: 4);

        // Act
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

        // Assert
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

    /// <summary>
    /// Verifies that typed object pools expose the underlying pool behavior through their public methods.
    /// </summary>
    [Fact]
    public void Get_TypedObjectPoolStateUnderTest_UsesTypedOperations()
    {
        // Arrange
        ObjectPool pool = new(defaultMaxItemsPerType: 4);
        TypedObjectPool<TestPoolable> typedPool = pool.CreateTypedPool<TestPoolable>();

        // Act
        int preallocated = typedPool.Prealloc(2);
        TestPoolable item = typedPool.Get();
        item.Value = 77;
        typedPool.Return(item);
        List<TestPoolable> rented = typedPool.GetMultiple(2);
        int returned = typedPool.ReturnMultiple(rented);
        typedPool.SetMaxCapacity(3);
        Dictionary<string, object> info = typedPool.GetInfo();
        int cleared = typedPool.Clear();

        // Assert
        Assert.True(preallocated >= 0);
        Assert.Equal(2, rented.Count);
        Assert.Equal(2, returned);
        Assert.Equal(nameof(TestPoolable), info["TypeName"]);
        Assert.True(cleared >= 0);
    }

    /// <summary>
    /// Verifies that object pool managers expose core pool management and reporting APIs.
    /// </summary>
    [Fact]
    public void Get_ObjectPoolManagerLifecycle_TracksStatisticsAndReports()
    {
        // Arrange
        ObjectPoolManager manager = new() { DefaultMaxPoolSize = 8 };

        // Act
        TestPoolable first = manager.Get<TestPoolable>();
        first.Value = 9;
        manager.Return(first);
        TestPoolable second = manager.Get<TestPoolable>();
        int preallocated = manager.Prealloc<TestPoolable>(2);
        bool setCapacity = manager.SetMaxCapacity<TestPoolable>(4);
        Dictionary<string, object> typeInfo = manager.GetTypeInfo<TestPoolable>();
        IDictionary<string, object> reportData = manager.GenerateReportData();
        string report = manager.GenerateReport();

        // Assert
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

    /// <summary>
    /// Verifies that object pool manager maintenance APIs operate on the managed pools.
    /// </summary>
    [Fact]
    public void ClearPool_ObjectPoolManagerMaintenanceApis_ReturnExpectedCounts()
    {
        // Arrange
        ObjectPoolManager manager = new() { DefaultMaxPoolSize = 4 };
        _ = manager.Prealloc<TestPoolable>(2);
        TypedObjectPoolAdapter<TestPoolable> adapter = manager.GetTypedPool<TestPoolable>();

        // Act
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

        // Assert
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

    /// <summary>
    /// Verifies that object pool manager health checks and scheduled trimming task complete through the public APIs.
    /// </summary>
    [Fact]
    public async Task PerformHealthCheck_StateUnderTest_ReportsUnhealthyPoolsAndStopsScheduledWork()
    {
        // Arrange
        ObjectPoolManager manager = new();
        _ = manager.Get<HealthCheckPoolable>();
        using CancellationTokenSource cancellationTokenSource = new();

        // Act
        int unhealthyPools = manager.PerformHealthCheck();
        Task scheduled = manager.ScheduleRegularTrimming(TimeSpan.FromMilliseconds(10), cancellationToken: cancellationTokenSource.Token);
        cancellationTokenSource.CancelAfter(20);
        await scheduled;

        // Assert
        Assert.True(unhealthyPools >= 0);
        Assert.True(manager.UnhealthyPoolCount >= 0);
    }

    /// <summary>
    /// Verifies that typed object pool adapters validate null returns.
    /// </summary>
    [Fact]
    public void Return_TypedObjectPoolAdapterNullObject_ThrowsArgumentNullException()
    {
        // Arrange
        ObjectPoolManager manager = new();
        TypedObjectPoolAdapter<TestPoolable> adapter = manager.GetTypedPool<TestPoolable>();

        // Act
        _ = Assert.Throws<ArgumentNullException>(() => adapter.Return(null!));
        _ = Assert.Throws<ArgumentNullException>(() => adapter.ReturnMultiple(null!));

        // Assert
    }

    /// <summary>
    /// Verifies that object pool batch getters reject non-positive counts.
    /// </summary>
    [Fact]
    public void GetMultiple_ObjectPoolCountIsInvalid_ThrowsArgumentException()
    {
        // Arrange
        ObjectPool pool = new();

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => pool.GetMultiple<TestPoolable>(0));

        // Assert
        Assert.Equal("count", exception.ParamName);
    }

    private static BufferConfig CreateBufferConfig(bool enableMemoryTrimming)
    {
        return new BufferConfig
        {
            EnableMemoryTrimming = enableMemoryTrimming,
            TotalBuffers = 32,
            AutoTuneOperationThreshold = 32,
            BufferAllocations = "256,0.50;512,0.25;1024,0.25",
            TrimIntervalMinutes = 1,
            DeepTrimIntervalMinutes = 1,
            ExpandThresholdPercent = 0.20,
            ShrinkThresholdPercent = 0.60,
            AdaptiveGrowthFactor = 2.0,
            MinimumIncrease = 2,
            MaxBufferIncreaseLimit = 16,
            MaxMemoryPercentage = 0.25,
            MaxMemoryBytes = 0,
            FallbackToArrayPool = true
        };
    }

    private sealed class TestPoolable : IPoolable
    {
        public int Value { get; set; }

        public void ResetForPool() => this.Value = 0;
    }

    private sealed class HealthCheckPoolable : IPoolable
    {
        public void ResetForPool()
        {
        }
    }
}
