using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Runtime.Options;
using Nalix.Runtime.Throttling;
using Xunit;

namespace Nalix.Runtime.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class ConcurrencyGateTests
{
    [Fact]
    public void TryEnter_WhenAttributeIsNull_ThrowsArgumentNullException()
    {
        ConcurrencyGate gate = new();

        _ = Assert.Throws<ArgumentNullException>(() => gate.TryEnter(0x1000, null!, out _));
    }

    [Fact]
    public void TryEnter_WhenMaxIsInvalid_ThrowsArgumentException()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute invalid = new(max: 0, queue: false, queueMax: 0);

        _ = Assert.Throws<ArgumentException>(() => gate.TryEnter(0x1001, invalid, out _));
    }

    [Fact]
    public void TryEnter_WhenCapacityIsOne_RejectsUntilLeaseDisposed()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: false, queueMax: 0);

        bool firstOk = gate.TryEnter(0x1010, attr, out ConcurrencyGate.Lease firstLease);
        bool secondOk = gate.TryEnter(0x1010, attr, out _);
        firstLease.Dispose();
        bool thirdOk = gate.TryEnter(0x1010, attr, out ConcurrencyGate.Lease thirdLease);
        thirdLease.Dispose();

        Assert.True(firstOk);
        Assert.False(secondOk);
        Assert.True(thirdOk);
    }

    [Fact]
    public async Task EnterAsync_WhenQueueDisabledAndBusy_ThrowsConcurrencyFailureException()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: false, queueMax: 0);

        Assert.True(gate.TryEnter(0x1020, attr, out ConcurrencyGate.Lease heldLease));

        await Assert.ThrowsAsync<ConcurrencyFailureException>(async () =>
            await gate.EnterAsync(0x1020, attr, CancellationToken.None));

        heldLease.Dispose();
    }

    [Fact]
    public async Task EnterAsync_WhenQueueEnabled_WaitsAndEventuallySucceeds()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: true, queueMax: 2);
        Assert.True(gate.TryEnter(0x1030, attr, out ConcurrencyGate.Lease firstLease));

        Task<ConcurrencyGate.Lease> waiting = gate.EnterAsync(0x1030, attr).AsTask();
        await Task.Delay(50);
        firstLease.Dispose();

        ConcurrencyGate.Lease secondLease = await waiting;
        secondLease.Dispose();

        (long acquired, _, long queued, _, _, _, _) = gate.GetStatistics();
        Assert.True(acquired >= 2);
        Assert.True(queued >= 1);
    }

    [Fact]
    public async Task EnterAsync_WhenQueueIsFull_ThrowsConcurrencyFailureException()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: true, queueMax: 1);
        Assert.True(gate.TryEnter(0x1040, attr, out ConcurrencyGate.Lease firstLease));

        Task<ConcurrencyGate.Lease> waiter = gate.EnterAsync(0x1040, attr).AsTask();
        bool queued = await WaitForAsync(() => gate.GetStatistics().TotalQueued >= 1, timeoutMs: 1500);
        Assert.True(queued);

        await Assert.ThrowsAsync<ConcurrencyFailureException>(async () =>
            await gate.EnterAsync(0x1040, attr, CancellationToken.None));

        firstLease.Dispose();
        ConcurrencyGate.Lease secondLease = await waiter;
        secondLease.Dispose();
    }

    [Fact]
    public void GetStatistics_AfterAcceptedAndRejectedAttempts_ReturnsExpectedCounters()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: false, queueMax: 0);

        Assert.True(gate.TryEnter(0x1050, attr, out ConcurrencyGate.Lease lease));
        Assert.False(gate.TryEnter(0x1050, attr, out _));
        lease.Dispose();

        (long acquired, long rejected, _, _, _, _, int trackedOpcodes) = gate.GetStatistics();

        Assert.True(acquired >= 1);
        Assert.True(rejected >= 1);
        Assert.True(trackedOpcodes >= 1);
    }

    [Fact]
    public void GenerateReportAndReportData_WhenGateIsUsed_ContainExpectedFields()
    {
        ConcurrencyGate gate = new();
        PacketConcurrencyLimitAttribute attr = new(max: 1, queue: false, queueMax: 0);
        _ = gate.TryEnter(0x1060, attr, out ConcurrencyGate.Lease lease);
        lease.Dispose();

        string report = gate.GenerateReport();
        IDictionary<string, object> reportData = gate.GetReportData();

        Assert.Contains("ConcurrencyGate Status", report, StringComparison.Ordinal);
        Assert.Equal(1, reportData["TrackedOpcodes"]);
        Assert.True(reportData.ContainsKey("Opcodes"));
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }
}
