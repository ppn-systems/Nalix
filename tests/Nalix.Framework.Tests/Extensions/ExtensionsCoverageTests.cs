// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Extensions;
using Xunit;

namespace Nalix.Framework.Tests.Extensions;

public sealed class ExtensionsCoverageTests
{
    [Fact]
    public void EnumExtensionsWhenUsingByteFlagsAddAndRemoveReturnExpectedValues()
    {
        ByteFlags value = ByteFlags.None;
        value = value.AddFlag(ByteFlags.A);
        value = value.AddFlag(ByteFlags.C);

        Assert.Equal(ByteFlags.A | ByteFlags.C, value);

        value = value.RemoveFlag(ByteFlags.A);
        Assert.Equal(ByteFlags.C, value);
    }

    [Fact]
    public void EnumExtensionsWhenUsingUShortFlagsAddAndRemoveReturnExpectedValues()
    {
        UShortFlags value = UShortFlags.None;
        value = value.AddFlag(UShortFlags.B);
        value = value.AddFlag(UShortFlags.C);

        Assert.Equal(UShortFlags.B | UShortFlags.C, value);

        value = value.RemoveFlag(UShortFlags.B);
        Assert.Equal(UShortFlags.C, value);
    }

    [Fact]
    public void EnumExtensionsWhenUsingUIntFlagsAddAndRemoveReturnExpectedValues()
    {
        UIntFlags value = UIntFlags.None;
        value = value.AddFlag(UIntFlags.A);
        value = value.AddFlag(UIntFlags.B);

        Assert.Equal(UIntFlags.A | UIntFlags.B, value);

        value = value.RemoveFlag(UIntFlags.A);
        Assert.Equal(UIntFlags.B, value);
    }

    [Fact]
    public void EnumExtensionsWhenUsingULongFlagsAddAndRemoveReturnExpectedValues()
    {
        ULongFlags value = ULongFlags.None;
        value = value.AddFlag(ULongFlags.A);
        value = value.AddFlag(ULongFlags.C);

        Assert.Equal(ULongFlags.A | ULongFlags.C, value);

        value = value.RemoveFlag(ULongFlags.A);
        Assert.Equal(ULongFlags.C, value);
    }

#if DEBUG
    [Fact]
    public void EnumExtensionsWhenTypeIsNotFlagsThrowsArgumentExceptionInDebugBuilds()
    {
        NotFlags value = NotFlags.A;

        Assert.Throws<ArgumentException>(() => value.AddFlag(NotFlags.B));
        Assert.Throws<ArgumentException>(() => value.RemoveFlag(NotFlags.B));
    }
#endif

    [Fact]
    public void TaskAwaitWhenTaskIsNullThrowsArgumentNullException()
    {
        Task? task = null;

        Assert.Throws<ArgumentNullException>(() => task!.Await());
    }

    [Fact]
    public void TaskAwaitWhenTaskFaultsRethrowsOriginalException()
    {
        InvalidOperationException expected = new("boom");
        Task faulted = Task.FromException(expected);

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() => faulted.Await());
        Assert.Same(expected, actual);
    }

    [Fact]
    public void TaskAwaitWithResultReturnsValue()
    {
        Task<int> task = Task.FromResult(123);

        int value = task.Await();

        Assert.Equal(123, value);
    }

    [Fact]
    public void TaskAwaitWithConfigureAwaitFlagStillCompletes()
    {
        Task task = Task.CompletedTask;

        task.Await(continueOnCapturedContext: false);
    }

    [Fact]
    public void ValueTaskAwaitWithNoResultCompletes()
    {
        ValueTask task = ValueTask.CompletedTask;

        task.Await();
    }

    [Fact]
    public void ValueTaskAwaitWithResultReturnsValue()
    {
        ValueTask<int> task = ValueTask.FromResult(42);

        int value = task.Await();

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task WithTimeoutWhenTaskCompletesBeforeDeadlineReturnsResult()
    {
        Task<int> task = Task.FromResult(9);

        int? result = await task.WithTimeout(100);

        Assert.Equal(9, result);
    }

    [Fact]
    public async Task WithTimeoutWhenValueTypeTaskDoesNotCompleteReturnsDefaultValue()
    {
        Task<int> task = Task.Delay(200).ContinueWith(_ => 7, TaskScheduler.Default);

        int? result = await task.WithTimeout(10);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task WithTimeoutWhenReferenceTypeTaskDoesNotCompleteReturnsNull()
    {
        Task<string> task = Task.Delay(200).ContinueWith(_ => "done", TaskScheduler.Default);

        string? result = await task.WithTimeout(10);

        Assert.Null(result);
    }

    [Fact]
    public async Task WithTimeoutWhenTaskIsNullThrowsArgumentNullException()
    {
        Task<int>? task = null;

        await Assert.ThrowsAsync<ArgumentNullException>(() => task!.WithTimeout(10));
    }

    [Fact]
    public async Task WithTimeoutWhenTimeoutIsLessThanMinusOneThrowsArgumentOutOfRangeException()
    {
        Task<int> task = Task.FromResult(1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => task.WithTimeout(-2));
    }

    [Fact]
    public void LinkCancellationWhenTokenIsNotCancelableReturnsDisposableAndDoesNotCancel()
    {
        TaskCompletionSource<int> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable registration = tcs.LinkCancellation(CancellationToken.None);

        Assert.False(tcs.Task.IsCanceled);
    }

    [Fact]
    public void LinkCancellationWhenTokenCancelsTransitionsTaskToCanceled()
    {
        TaskCompletionSource<int> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cts = new();
        using IDisposable registration = tcs.LinkCancellation(cts.Token);

        cts.Cancel();

        Assert.True(tcs.Task.IsCanceled);
    }

    [Fact]
    public void LinkCancellationWhenTcsIsNullThrowsArgumentNullException()
    {
        TaskCompletionSource<int>? tcs = null;

        Assert.Throws<ArgumentNullException>(() => tcs!.LinkCancellation(CancellationToken.None));
    }

    [Fact]
    public void HeaderExtensionsWhenBufferContainsValuesReadsExpectedFields()
    {
        byte[] buffer = new byte[(int)PacketHeaderOffset.Region];
        const uint magic = 0xA1B2C3D4;
        const ushort opCode = 0x7788;
        const uint sequence = 0x01020304;

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan((int)PacketHeaderOffset.MagicNumber), magic);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan((int)PacketHeaderOffset.OpCode), opCode);
        buffer[(int)PacketHeaderOffset.Flags] = (byte)PacketFlags.ENCRYPTED;
        buffer[(int)PacketHeaderOffset.Priority] = (byte)PacketPriority.HIGH;
        buffer[(int)PacketHeaderOffset.Transport] = (byte)ProtocolType.UDP;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan((int)PacketHeaderOffset.SequenceId), sequence);

        Assert.Equal(magic, buffer.AsSpan().ReadMagicNumberLE());
        Assert.Equal(opCode, buffer.AsSpan().ReadOpCodeLE());
        Assert.Equal(PacketFlags.ENCRYPTED, buffer.AsSpan().ReadFlagsLE());
        Assert.Equal(PacketPriority.HIGH, buffer.AsSpan().ReadPriorityLE());
        Assert.Equal(ProtocolType.UDP, buffer.AsSpan().ReadTransportLE());
        Assert.Equal(sequence, buffer.AsSpan().ReadSequenceIdLE());
    }

    [Fact]
    public void HeaderExtensionsWriteFlagsWritesAtFlagsOffsetOnly()
    {
        byte[] buffer = new byte[(int)PacketHeaderOffset.Region];
        buffer[(int)PacketHeaderOffset.Priority] = 0xAA;
        buffer.AsSpan().WriteFlagsLE(PacketFlags.RELIABLE);

        Assert.Equal(PacketFlags.RELIABLE, buffer.AsSpan().ReadFlagsLE());
        Assert.Equal(0xAA, buffer[(int)PacketHeaderOffset.Priority]);
    }

    [Fact]
    public void HeaderExtensionsWhenBufferTooSmallThrowsArgumentException()
    {
        byte[] small = new byte[2];
        byte[] smallWritable = new byte[2];

        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadMagicNumberLE());
        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadOpCodeLE());
        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadFlagsLE());
        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadPriorityLE());
        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadTransportLE());
        Assert.Throws<ArgumentException>(() => small.AsSpan().ReadSequenceIdLE());
        Assert.Throws<ArgumentException>(() => smallWritable.AsSpan().WriteFlagsLE(PacketFlags.NONE));
    }

    [Fact]
    public void ReportExtensionsWhenReportableIsNullThrowsArgumentNullException()
    {
        IReportable? reportable = null;

        Assert.Throws<ArgumentNullException>(() => reportable!.SaveReportToFile());
    }

    [Fact]
    public void ReportExtensionsSaveReportToFileCreatesFileAndPersistsContent()
    {
        FakeReportable reportable = new("hello-report");

        string path = reportable.SaveReportToFile("Prefix:*Value");

        try
        {
            Assert.True(File.Exists(path));
            Assert.Contains("prefix_", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("hello-report", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Flags]
    private enum ByteFlags : byte
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4
    }

    [Flags]
    private enum UShortFlags : ushort
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4
    }

    [Flags]
    private enum UIntFlags : uint
    {
        None = 0,
        A = 1,
        B = 2
    }

    [Flags]
    private enum ULongFlags : ulong
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4
    }

    private enum NotFlags : byte
    {
        A = 1,
        B = 2
    }

    private sealed class FakeReportable(string text) : IReportable
    {
        private readonly string _text = text;

        public string GenerateReport() => _text;

        public System.Collections.Generic.IDictionary<string, object> GetReportData() =>
            new System.Collections.Generic.Dictionary<string, object>();
    }
}
