
using System;
using System.IO;
using System.Text;
using System.Threading;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{
    [Theory]
    [InlineData((ushort)1, (ushort)0, (ushort)1, true)]
    [InlineData((ushort)2, (ushort)1, (ushort)3, false)]
    public void WriteToThenReadFromFragmentHeaderPreservesPublicState(ushort streamId, ushort chunkIndex, ushort totalChunks, bool isLast)
    {
        FragmentHeader header = new(streamId, chunkIndex, totalChunks, isLast);
        byte[] buffer = new byte[FragmentHeader.WireSize];

        header.WriteTo(buffer);
        FragmentHeader roundTripped = FragmentHeader.ReadFrom(buffer);

        Assert.Equal(header, roundTripped);
        Assert.Equal(streamId, roundTripped.StreamId);
        Assert.Equal(chunkIndex, roundTripped.ChunkIndex);
        Assert.Equal(totalChunks, roundTripped.TotalChunks);
        Assert.Equal(isLast, roundTripped.IsLast);
    }

    [Fact]
    public void ReadFromWhenMagicIsInvalidThrowsInvalidDataException()
    {
        byte[] buffer = new byte[FragmentHeader.WireSize];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => FragmentHeader.ReadFrom(buffer));

        Assert.Equal("Invalid fragment magic", exception.Message);
    }

    [Theory]
    [InlineData(1400, 1400, true)]
    [InlineData(0, 1400, false)]
    [InlineData(1000, 1400, false)]
    public void ValidateWhenFragmentOptionsVaryMatchesExpectedValidity(
        int maxPayloadSize,
        int maxChunkSize,
        bool shouldSucceed)
    {
        FragmentOptions options = new()
        {
            MaxPayloadSize = maxPayloadSize,
            MaxChunkSize = maxChunkSize
        };

        Exception? exception = Record.Exception(options.Validate);

        if (shouldSucceed)
        {
            Assert.Null(exception);
        }
        else
        {
            _ = Assert.IsType<InvalidOperationException>(exception);
        }
    }

    [Fact]
    public void NextWhenCalledRepeatedlyNeverReturnsZero()
    {
        ushort[] values = new ushort[128];

        for (int index = 0; index < values.Length; index++)
        {
            values[index] = FragmentStreamId.Next();
        }

        Assert.DoesNotContain((ushort)0, values);
    }

    [Fact]
    public void AddWhenAllChunksArriveInOrderReturnsAssembledBuffer()
    {
        using FragmentAssembler assembler = new();
        FragmentHeader first = new(7, 0, 2, false);
        FragmentHeader second = new(7, 1, 2, true);

        FragmentAssemblyResult? firstAssembled = assembler.Add(first, Encoding.UTF8.GetBytes("hello "), out bool firstEvicted);
        FragmentAssemblyResult? secondAssembled = assembler.Add(second, Encoding.UTF8.GetBytes("world"), out bool secondEvicted);

        Assert.Null(firstAssembled);
        Assert.False(firstEvicted);
        Assert.False(secondEvicted);
        Assert.True(secondAssembled.HasValue);
        using BufferLease assembled = secondAssembled.Value.Lease;
        Assert.Equal("hello world", Encoding.UTF8.GetString(secondAssembled.Value.Span));
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Fact]
    public void AddWhenFirstChunkArrivesOutOfOrderThrowsInvalidDataException()
    {
        using FragmentAssembler assembler = new();
        FragmentHeader second = new(9, 1, 2, true);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            assembler.Add(second, Encoding.UTF8.GetBytes("world"), out _));

        Assert.Contains("started with chunk index 1 instead of 0", exception.Message);
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Fact]
    public void AddWhenChunkSequenceStartsCorrectlyAndCompletesAssemblesPayload()
    {
        using FragmentAssembler assembler = new();
        FragmentHeader first = new(10, 0, 2, false);
        FragmentHeader second = new(10, 1, 2, true);

        FragmentAssemblyResult? firstAttempt = assembler.Add(first, Encoding.UTF8.GetBytes("hello "), out bool firstEvicted);
        FragmentAssemblyResult? completed = assembler.Add(second, Encoding.UTF8.GetBytes("world"), out bool secondEvicted);

        Assert.Null(firstAttempt);
        Assert.False(firstEvicted);
        Assert.False(secondEvicted);
        Assert.True(completed.HasValue);
        using BufferLease assembled = completed.Value.Lease;
        Assert.Equal("hello world", Encoding.UTF8.GetString(completed.Value.Span));
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Theory]
    [InlineData((ushort)0, (ushort)0, (ushort)1)]
    [InlineData((ushort)1, (ushort)1, (ushort)1)]
    [InlineData((ushort)1, (ushort)0, (ushort)0)]
    public void AddWhenHeaderIsInvalidThrowsInvalidDataException(ushort streamId, ushort chunkIndex, ushort totalChunks)
    {
        using FragmentAssembler assembler = new();
        FragmentHeader header = new(streamId, chunkIndex, totalChunks, false);

        _ = Assert.Throws<InvalidDataException>(() => assembler.Add(header, [1, 2, 3], out _));
    }

    [Fact]
    public void AddWhenStreamHasTimedOutEvictsStreamAndReturnsNull()
    {
        using FragmentAssembler assembler = new() { StreamTimeoutMs = 1000 };
        FragmentHeader first = new(15, 0, 2, false);
        FragmentHeader second = new(15, 1, 2, true);
        _ = assembler.Add(first, [1], out _);
        Thread.Sleep(1500);

        FragmentAssemblyResult? assembled = assembler.Add(second, [2], out bool streamEvicted);

        Assert.Null(assembled);
        Assert.True(streamEvicted);
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Fact]
    public void EvictExpiredAndClearWhenStreamsExistRemovesTrackedStreams()
    {
        using FragmentAssembler assembler = new() { StreamTimeoutMs = 1000 };
        _ = assembler.Add(new FragmentHeader(21, 0, 2, false), [1, 2], out _);
        Thread.Sleep(1500);

        int evicted = assembler.EvictExpired();
        _ = assembler.Add(new FragmentHeader(22, 0, 2, false), [3, 4], out _);
        assembler.Clear();

        Assert.Equal(1, evicted);
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Fact]
    public void AddWhenEvictIntervalIsReachedAutomaticallySweepsExpiredStreams()
    {
        using FragmentAssembler assembler = new() { StreamTimeoutMs = 2000 };

        _ = assembler.Add(new FragmentHeader(40, 0, 2, false), [1], out _);
        Thread.Sleep(2500);

        for (ushort streamId = 41; streamId < 41 + FragmentAssembler.EvictInterval - 1; streamId++)
        {
            _ = assembler.Add(new FragmentHeader(streamId, 0, 2, false), [2], out _);
        }

        Assert.Equal(FragmentAssembler.EvictInterval - 1, assembler.OpenStreamCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsFragmentedFrameWhenPayloadVariesReturnsExpectedResult(bool useValidPayload)
    {
        byte[] payload = useValidPayload
            ? CreateFragmentPayload(new FragmentHeader(30, 0, 1, true), [9, 8, 7])
            : [0x01, 0x02, 0x03];

        bool isFragment = FragmentAssembler.IsFragmentedFrame(payload, out FragmentHeader header);

        Assert.Equal(useValidPayload, isFragment);
        if (useValidPayload)
        {
            Assert.Equal((ushort)30, header.StreamId);
            Assert.True(header.IsLast);
        }
        else
        {
            Assert.Equal(default, header);
        }
    }
}
