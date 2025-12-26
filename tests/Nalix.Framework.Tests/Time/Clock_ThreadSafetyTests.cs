// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for thread safety of Clock operations.
/// </summary>
[Collection("ClockTests")]
public class ClockThreadSafetyTests
{
    [Fact]
    public async Task ConcurrentNowUtcCallsShouldNotCrash()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 10;
        const int IterationsPerThread = 1000;
        List<Task> tasks = [];

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
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentSynchronizationAndReadShouldBeThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 4;
        const int IterationsPerThread = 100;
        List<Task> tasks = [];
        List<Exception> exceptions = [];
        object lockObj = new();

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
                        DateTime time = DateTime.UtcNow.AddMilliseconds(offset * 10);
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
    public async Task SynchronizedReadsShouldReturnConsistentValues()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        // Synchronize with a known time
        DateTime syncTime = DateTime.UtcNow;
        _ = Clock.SynchronizeTime(syncTime, maxAllowedDriftMs: 0.1);

        // Multiple threads reading should get consistent, monotonic values
        const int ThreadCount = 5;
        const int IterationsPerThread = 100;
        List<Task<bool>> tasks = [];

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

        _ = await Task.WhenAll(tasks);

        // All threads should report monotonic time
        Assert.All(tasks, task => Assert.True(task.Result));
    }

    [Fact]
    public async Task DriftRateShouldBeThreadSafe()
    {
        // Reset to clean state
        Clock.ResetSynchronization();

        const int ThreadCount = 10;
        const int IterationsPerThread = 1000;
        List<Task<double>> tasks = [];

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

        _ = await Task.WhenAll(tasks);

        // All results should be valid numbers (not NaN or Infinity)
        Assert.All(tasks, task =>
        {
            Assert.False(double.IsNaN(task.Result));
            Assert.False(double.IsInfinity(task.Result));
        });
    }
}
