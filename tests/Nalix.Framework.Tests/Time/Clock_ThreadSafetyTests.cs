using Nalix.Framework.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for thread safety of Clock operations.
/// </summary>
[Collection("ClockTests")]
public class Clock_ThreadSafetyTests
{
    [Fact]
    public async Task Concurrent_NowUtc_Calls_Should_Not_Crash()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const Int32 ThreadCount = 10;
        const Int32 IterationsPerThread = 1000;
        var tasks = new List<Task>();

        for (Int32 i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (Int32 j = 0; j < IterationsPerThread; j++)
                {
                    _ = Clock.NowUtc();
                }
            }));
        }

        // Should complete without exception
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Concurrent_Synchronization_And_Read_Should_Be_ThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const Int32 ThreadCount = 4;
        const Int32 IterationsPerThread = 100;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();
        var lockObj = new Object();

        // Readers
        for (Int32 i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (Int32 j = 0; j < IterationsPerThread; j++)
                    {
                        _ = Clock.NowUtc();
                        _ = Clock.UnixMillisecondsNow();
                        _ = Clock.DriftRate();
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        // Writers (synchronized time updates)
        for (Int32 i = 0; i < ThreadCount; i++)
        {
            Int32 offset = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (Int32 j = 0; j < IterationsPerThread / 10; j++)
                    {
                        var time = DateTime.UtcNow.AddMilliseconds(offset * 10);
                        _ = Clock.SynchronizeTime(time, maxAllowedDriftMs: 1000.0);
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // No exceptions should occur
        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task Synchronized_Reads_Should_Return_Consistent_Values()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        // Synchronize with a known time
        var syncTime = DateTime.UtcNow;
        _ = Clock.SynchronizeTime(syncTime, maxAllowedDriftMs: 0.1);

        // Multiple threads reading should get consistent, monotonic values
        const Int32 ThreadCount = 5;
        const Int32 IterationsPerThread = 100;
        var tasks = new List<Task<Boolean>>();

        for (Int32 i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                DateTime prev = Clock.NowUtc();
                for (Int32 j = 0; j < IterationsPerThread; j++)
                {
                    DateTime current = Clock.NowUtc();
                    if (current < prev)
                    {
                        return false; // Time went backwards!
                    }
                    prev = current;
                    Thread.Sleep(1);
                }
                return true;
            }));
        }

        await Task.WhenAll(tasks);

        // All threads should report monotonic time
        Assert.All(tasks, task => Assert.True(task.Result));
    }

    [Fact]
    public async Task DriftRate_Should_Be_ThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const Int32 ThreadCount = 10;
        const Int32 IterationsPerThread = 1000;
        var tasks = new List<Task<Double>>();

        for (Int32 i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                Double sum = 0;
                for (Int32 j = 0; j < IterationsPerThread; j++)
                {
                    sum += Clock.DriftRate();
                }
                return sum / IterationsPerThread;
            }));
        }

        await Task.WhenAll(tasks);

        // All results should be valid numbers (not NaN or Infinity)
        Assert.All(tasks, task =>
        {
            Assert.False(Double.IsNaN(task.Result));
            Assert.False(Double.IsInfinity(task.Result));
        });
    }
}
