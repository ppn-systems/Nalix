using Nalix.Framework.Time;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public void Concurrent_NowUtc_Calls_Should_Not_Crash()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 10;
        const int IterationsPerThread = 1000;
        var tasks = new List<Task>();

        for (int i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < IterationsPerThread; j++)
                {
                    _ = Clock.NowUtc();
                }
            }));
        }

        // Should complete without exception
        Task.WaitAll(tasks.ToArray());
    }

    [Fact]
    public void Concurrent_Synchronization_And_Read_Should_Be_ThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 4;
        const int IterationsPerThread = 100;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();
        var lockObj = new object();

        // Readers
        for (int i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < IterationsPerThread; j++)
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
        for (int i = 0; i < ThreadCount; i++)
        {
            int offset = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < IterationsPerThread / 10; j++)
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

        Task.WaitAll(tasks.ToArray());

        // No exceptions should occur
        Assert.Empty(exceptions);
    }

    [Fact]
    public void Synchronized_Reads_Should_Return_Consistent_Values()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        // Synchronize with a known time
        var syncTime = DateTime.UtcNow;
        _ = Clock.SynchronizeTime(syncTime, maxAllowedDriftMs: 0.1);

        // Multiple threads reading should get consistent, monotonic values
        const int ThreadCount = 5;
        const int IterationsPerThread = 100;
        var tasks = new List<Task<bool>>();

        for (int i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                DateTime prev = Clock.NowUtc();
                for (int j = 0; j < IterationsPerThread; j++)
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

        Task.WaitAll(tasks.ToArray());

        // All threads should report monotonic time
        Assert.All(tasks, task => Assert.True(task.Result));
    }

    [Fact]
    public void DriftRate_Should_Be_ThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 10;
        const int IterationsPerThread = 1000;
        var tasks = new List<Task<double>>();

        for (int i = 0; i < ThreadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                double sum = 0;
                for (int j = 0; j < IterationsPerThread; j++)
                {
                    sum += Clock.DriftRate();
                }
                return sum / IterationsPerThread;
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // All results should be valid numbers (not NaN or Infinity)
        Assert.All(tasks, task =>
        {
            Assert.False(double.IsNaN(task.Result));
            Assert.False(double.IsInfinity(task.Result));
        });
    }
}
