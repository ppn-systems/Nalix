using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;

using System;
using System.Collections.Generic;
using Nalix.Abstractions.Exceptions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

[Trait("Category", "Memory")]
public sealed partial class MemoryTests
{
    [Theory]
    [InlineData(100, 80, 90, 10, true, false, 0.20, 0.10)]
    [InlineData(100, 10, 60, 40, false, true, 0.90, 0.40)]
    [InlineData(100, 60, 100, 0, true, false, 0.40, 0.00)]
    [InlineData(0, 0, 0, 0, false, false, 0.00, 0.00)]
    public void GetUsageRatio_StateVaries_ReturnsExpectedMetrics(
        int totalBuffers,
        int freeBuffers,
        int hits,
        int misses,
        bool expectedCanShrink,
        bool expectedNeedsExpansion,
        double expectedUsageRatio,
        double expectedMissRate)
    {
        BufferPoolState state = new()
        {
            BufferSize = 256,
            Hits = hits,
            TotalBuffers = totalBuffers,
            FreeBuffers = freeBuffers,
            Misses = misses
        };

        double usageRatio = state.GetUsageRatio();
        double missRate = state.GetMissRate();

        Assert.Equal(expectedCanShrink, state.CanShrink);
        Assert.Equal(expectedNeedsExpansion, state.NeedsExpansion);
        Assert.Equal(expectedUsageRatio, usageRatio, 3);
        Assert.Equal(expectedMissRate, missRate, 3);
        Assert.Equal(256, state.BufferSize);
    }

    [Fact]
    public void Rent_ReturnByteArrayPool_ReturnsUsableArray()
    {
        int requestedCapacity = BufferLease.StackAllocThreshold;
        byte[] buffer = BufferLease.ByteArrayPool.Rent(requestedCapacity);

        Assert.NotNull(buffer);
        Assert.True(buffer.Length >= requestedCapacity);

        BufferLease.ByteArrayPool.Return(buffer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DataWriter_StateUnderTest_InvalidInitialSizeThrows(int size)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DataWriter(size));

    [Fact]
    public void Expand_WriterOwnsRentedBuffer_GrowsAndPreservesContent()
    {
        DataWriter writer = new(2);
        writer.FreeBuffer[0] = 1;
        writer.FreeBuffer[1] = 2;
        writer.Advance(2);

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

        Assert.Equal([1, 2, 3, 4, 5], result);
        Assert.Equal(5, writtenCount);
        Assert.True(freeLengthAfterWrite >= 0);
    }

    [Theory]
    [InlineData("array")]
    [InlineData("span")]
    [InlineData("memory")]
    public void Advance_ReaderHasData_UpdatesBytesReadAndRemaining(string sourceKind)
    {
        byte[] data = [1, 2, 3, 4];
        DataReader reader = sourceKind switch
        {
            "array" => new DataReader(data),
            "span" => new DataReader((ReadOnlySpan<byte>)data),
            "memory" => new DataReader((ReadOnlyMemory<byte>)data),
            _ => throw new InvalidOperationException("Unexpected source kind.")
        };

        ref byte first = ref reader.GetSpanReference(1);
        byte observed = first;
        reader.Advance(2);
        int bytesRead = reader.BytesRead;
        int bytesRemaining = reader.BytesRemaining;
        reader.Dispose();

        Assert.Equal((byte)1, observed);
        Assert.Equal(2, bytesRead);
        Assert.Equal(2, bytesRemaining);
        Assert.Equal(0, reader.BytesRead);
        Assert.Equal(0, reader.BytesRemaining);
    }

    [Fact]
    public void Advance_ReaderWouldOverflow_ThrowsSerializationException()
    {
        SerializationFailureException spanException = Assert.ThrowsAny<SerializationFailureException>(() =>
        {
            DataReader reader = new([1, 2]);
            reader.GetSpanReference(3);
        });

        SerializationFailureException advanceException = Assert.ThrowsAny<SerializationFailureException>(() =>
        {
            DataReader reader = new([1, 2]);
            reader.Advance(3);
        });

        ArgumentOutOfRangeException negativeException = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            DataReader reader = new([1, 2]);
            reader.Advance(-1);
        });

        Assert.Contains("end of the stream", spanException.Message);
        Assert.Contains("end of the stream", advanceException.Message);
        Assert.Equal("count", negativeException.ParamName);
    }

    [Fact]
    public void Rent_BufferPoolManagerConfigured_ReturnsSizedBuffer()
    {
        BufferOptions config = MemoryTestSupport.CreateBufferOptions(enableMemoryTrimming: false);
        using BufferPoolManager manager = new(config);

        byte[] rented = manager.Rent(300);
        double allocation = manager.GetAllocationForSize(300);
        manager.Return(rented);

        Assert.True(rented.Length >= 300);
        Assert.Equal(256, manager.MinBufferSize);
        Assert.Equal(1024, manager.MaxBufferSize);
        Assert.Equal(0.25, allocation, 3);
        Assert.Equal("buf.trim", BufferPoolManager.RecurringName);
    }


    [Fact]
    public void GenerateReport_StateUnderTest_ReturnsReportAndData()
    {
        using BufferPoolManager manager = new(MemoryTestSupport.CreateBufferOptions(enableMemoryTrimming: false));
        _ = manager.Rent(256);

        string report = manager.GenerateReport();
        IDictionary<string, object> data = manager.GetReportData();

        Assert.Contains("BufferPoolManager Status", report);
        Assert.Equal(manager.MinBufferSize, data["MinBufferSize"]);
        Assert.Equal(manager.MaxBufferSize, data["MaxBufferSize"]);
        Assert.True(data.ContainsKey("Pools"));
    }
}















