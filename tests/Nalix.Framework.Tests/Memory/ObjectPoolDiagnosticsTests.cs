// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

public sealed class ObjectPoolDiagnosticsTests
{
    [Fact]
    public void Get_PeakOutstanding_AlwaysTracked()
    {
        ObjectPoolManager manager = new();

        // Rent 3 items
        TestPoolable item1 = manager.Get<TestPoolable>();
        TestPoolable item2 = manager.Get<TestPoolable>();
        _ = manager.Get<TestPoolable>();

        Assert.Equal(3L, (long)manager.GetTypeInfo<TestPoolable>()["PeakOutstanding"]);

        // Rent 1 more to hit peak of 4
        _ = manager.Get<TestPoolable>();
        Assert.Equal(4L, (long)manager.GetTypeInfo<TestPoolable>()["PeakOutstanding"]);

        // Return 2, peak should still be 4
        manager.Return(item1);
        manager.Return(item2);
        Assert.Equal(4L, (long)manager.GetTypeInfo<TestPoolable>()["PeakOutstanding"]);
        Assert.Equal(2L, (long)manager.GetTypeInfo<TestPoolable>()["Outstanding"]);
    }

    [Fact]
    public void GenerateReport_WithDiagnostics_IncludesLifetimeMetrics()
    {
        // Enable diagnostics
        ObjectPoolOptions config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        config.EnableDiagnostics = true;

        try
        {
            ObjectPoolManager manager = new();

            TestPoolable item = manager.Get<TestPoolable>();
            Thread.Sleep(10); // Simulate some work
            manager.Return(item);

            string report = manager.GenerateReport();

            Assert.Contains("Lifetime (ms)", report);
            Assert.Contains("Avg=", report);
            Assert.Contains("p95=", report);
            Assert.Contains("Max=", report);
        }
        finally
        {
            config.EnableDiagnostics = false;
        }
    }

    [Fact]
    public void GenerateReport_SuspiciousObjects_Detected()
    {
        ObjectPoolOptions config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        config.EnableDiagnostics = true;
        config.SuspiciousThresholdSeconds = 0; // Trigger immediately for test

        try
        {
            ObjectPoolManager manager = new();

            TestPoolable item = manager.Get<TestPoolable>();

            string report = manager.GenerateReport();

            Assert.Contains("Suspicious Objects", report);
            Assert.Contains(nameof(TestPoolable), report);
        }
        finally
        {
            config.EnableDiagnostics = false;
            config.SuspiciousThresholdSeconds = 30;
        }
    }


#if DEBUG
    [Fact]
    public void Finalizer_LeakDetection_IncrementsCount()
    {
        ObjectPoolOptions config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        config.EnableDiagnostics = true;
        config.EnableLeakDetection = true;

        try
        {
            ObjectPoolManager manager = new();

            // Rent and drop reference (leak)
            this.CreateLeak(manager);

            // Force GC
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            string report = manager.GenerateReport();
            Assert.Contains("GC Leak Detected", report);
            // Note: TotalLeaked is static and might be > 0 from other tests, 
            // but in a fresh run it should be at least 1.
            Assert.True(Framework.Memory.Internal.PoolTypes.PoolSentinel.TotalLeaked > 0);
        }
        finally
        {
            config.EnableDiagnostics = false;
            config.EnableLeakDetection = false;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CreateLeak(ObjectPoolManager manager) => _ = manager.Get<TestPoolable>();
#endif
}













