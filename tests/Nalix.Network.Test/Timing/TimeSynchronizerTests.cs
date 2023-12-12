using Nalix.Network.Timing;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Network.Tests.Timing;

public sealed class TimeSynchronizerTests
{
    /// <summary>
    /// Verifies that when enabled PRIOR to starting the loop,
    /// the synchronizer emits at least one tick; also verifies clean cancellation.
    /// </summary>
    [Fact]
    public async Task StartTickLoopAsync_ShouldEmitTicks_WhenEnabled()
    {
        // Arrange
        var sync = new TimeSynchronizer
        {
            IsTimeSyncEnabled = true // IMPORTANT: enable BEFORE starting the loop
        };

        using var cts = new CancellationTokenSource();
        var gotTick = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribe BEFORE starting
        sync.TimeSynchronized += _ => gotTick.TrySetResult(true);

        // Act: start loop on a background task (avoid any sync-context quirks)
        var loopTask = Task.Run(() => sync.StartTickLoopAsync(cts.Token));

        // Give the loop a small head start
        await Task.Yield();

        // Assert: wait for the first tick with a generous timeout (covers the 10s gating if it ever happens)
        try
        {
            _ = await gotTick.Task.WaitAsync(TimeSpan.FromSeconds(12));
        }
        catch (TimeoutException)
        {
            // If it times out, surface more diagnostics
            if (loopTask.IsFaulted)
            {
                // Bubble inner exception to see why loop died (if it did)
                throw new Xunit.Sdk.XunitException(
                    $"TimeSynchronizer loop faulted: {loopTask.Exception?.GetBaseException()}");
            }

            throw new Xunit.Sdk.XunitException("TimeSynchronizer did not emit a tick within 12 seconds.");
        }

        Assert.True(gotTick.Task.IsCompletedSuccessfully, "Tick task did not complete successfully.");

        // Cancel and ensure loop exits
        cts.Cancel();

        var stopped = await Task.WhenAny(loopTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(Object.ReferenceEquals(stopped, loopTask), "TimeSynchronizer loop did not stop after cancellation.");

        // Extra safety
        sync.StopTicking();
    }

    /// <summary>
    /// Negative check: if disabled, no tick should be observed in a short window.
    /// This helps catch accidental auto-start behavior.
    /// </summary>
    [Fact]
    public async Task StartTickLoopAsync_ShouldNotEmit_WhenDisabledInitially()
    {
        var sync = new TimeSynchronizer
        {
            IsTimeSyncEnabled = false
        };

        using var cts = new CancellationTokenSource();
        var gotTick = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.TimeSynchronized += _ => gotTick.TrySetResult(true);

        var loopTask = Task.Run(() => sync.StartTickLoopAsync(cts.Token));

        // Wait a short time; we should not see any tick if disabled
        var winner = await Task.WhenAny(gotTick.Task, Task.Delay(300));
        Assert.False(Object.ReferenceEquals(winner, gotTick.Task), "Tick should not fire while disabled.");

        // Cleanup
        cts.Cancel();
        _ = await Task.WhenAny(loopTask, Task.Delay(2000));
        sync.StopTicking();
    }
}
