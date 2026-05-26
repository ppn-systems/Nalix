// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Nalix.Framework;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

[Collection("ObjectPoolDiagnostics")]
public sealed class ObjectPoolDiagnosticsTests
{
    [Fact]
    public void Get_PeakOutstanding_AlwaysTracked()
    {
        ObjectPoolOptions config = new() { EnableDiagnostics = true };
        using ObjectPoolManager manager = new(config);

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
        // Use a private config instance to avoid interference with other tests using the singleton
        ObjectPoolOptions config = new() { EnableDiagnostics = true };

        using ObjectPoolManager manager = new(config);
        
        TestPoolable item = manager.Get<TestPoolable>();
        Thread.Sleep(50); 
        manager.Return(item);

            string report = "";
            bool found = false;
            for (int i = 0; i < 20; i++)
            {
                report = manager.GenerateReport();
                if (report.Contains("Lifetime (ms)"))
                {
                    found = true;
                    break;
                }
                Thread.Sleep(50);
            }

            Assert.True(found, $"Report should contain Lifetime metrics. Full report:\n{report}");
            Assert.Contains("Avg=", report);
            Assert.Contains("p95=", report);
            Assert.Contains("Max=", report);
    }

    [Fact]
    public void GenerateReport_SuspiciousObjects_Detected()
    {
        ObjectPoolOptions config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        config.EnableDiagnostics = true;
        config.SuspiciousThresholdSeconds = 0; // Trigger immediately for test

        try
        {
            using ObjectPoolManager manager = new();

            TestPoolable item = manager.Get<TestPoolable>();

            // Allow ConcurrentBag's eventual consistency to catch up for the iterator
            Thread.Sleep(10); 

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

    [Fact]
    public void PerformHealthCheck_LifetimeMissesWithoutNewTraffic_DoesNotSpamPoolFailure()
    {
        ObjectPoolOptions config = new() { EnableDiagnostics = true, EnableObjectTrimming = false };
        using ObjectPoolManager manager = new(config);
        using DiagnosticCollector collector = new(DiagnosticsEvents.Memory.PoolFailure);

        for (int i = 0; i < 32; i++)
        {
            _ = manager.Get<HealthCheckPoolable>();
        }

        Assert.Equal(1, manager.PerformHealthCheck());
        Assert.Equal(1, collector.Events.Count);

        Assert.Equal(0, manager.PerformHealthCheck());
        Assert.Equal(1, collector.Events.Count);
    }

    [Fact]
    public void PerformHealthCheck_LowSampleMisses_AreWarmingNotUnhealthy()
    {
        ObjectPoolOptions config = new() { EnableDiagnostics = true, EnableObjectTrimming = false };
        using ObjectPoolManager manager = new(config);
        using DiagnosticCollector collector = new(DiagnosticsEvents.Memory.PoolFailure);

        for (int i = 0; i < 6; i++)
        {
            _ = manager.Get<HealthCheckPoolable>();
        }

        Assert.Equal(0, manager.PerformHealthCheck());
        Assert.Empty(collector.Events);
        Assert.Equal("Warming", manager.GetTypeInfo<HealthCheckPoolable>()["Status"]);
    }

    [Fact]
    public void PerformHealthCheck_HealthyWindowAfterFailure_ResetsStatus()
    {
        ObjectPoolOptions config = new() { EnableDiagnostics = true, EnableObjectTrimming = false };
        using ObjectPoolManager manager = new(config);
        List<HealthCheckPoolable> rented = new(32);

        for (int i = 0; i < 32; i++)
        {
            rented.Add(manager.Get<HealthCheckPoolable>());
        }

        Assert.Equal(1, manager.PerformHealthCheck());

        for (int i = 0; i < rented.Count; i++)
        {
            manager.Return(rented[i]);
        }

        for (int i = 0; i < 32; i++)
        {
            HealthCheckPoolable item = manager.Get<HealthCheckPoolable>();
            manager.Return(item);
        }

        Assert.Equal(0, manager.PerformHealthCheck());
        Assert.Equal("OK", manager.GetTypeInfo<HealthCheckPoolable>()["Status"]);
    }

    [Fact]
    public void PerformHealthCheck_PoolFailurePayload_UsesReadableGenericTypeName()
    {
        ObjectPoolOptions config = new() { EnableDiagnostics = true, EnableObjectTrimming = false };
        using ObjectPoolManager manager = new(config);
        using DiagnosticCollector collector = new(DiagnosticsEvents.Memory.PoolFailure);

        for (int i = 0; i < 32; i++)
        {
            _ = manager.Get<GenericPoolable<HealthCheckPoolable>>();
        }

        Assert.Equal(1, manager.PerformHealthCheck());
        object payload = Assert.Single(collector.Events);

        Assert.Equal("GenericPoolable<HealthCheckPoolable>", DiagnosticCollector.GetProperty<string>(payload, "Type"));
        Assert.Equal(32L, DiagnosticCollector.GetProperty<long>(payload, "WindowGets"));
        Assert.Equal(32L, DiagnosticCollector.GetProperty<long>(payload, "WindowMisses"));
        Assert.Equal("CapacityPressure", DiagnosticCollector.GetProperty<string>(payload, "Reason"));
    }


#if DEBUG
    [Fact]
    public void Finalizer_LeakDetection_IncrementsCount()
    {
        ObjectPoolOptions config = new()
        {
            EnableDiagnostics = true,
            EnableLeakDetection = true
        };

        using ObjectPoolManager manager = new(config);

        // Rent and drop reference (leak)
        this.CreateLeak(manager);

        // Force GC
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        string report = manager.GenerateReport();
        Assert.Contains("GC Leak Detected", report);
        Assert.True(Framework.Memory.Internal.PoolTypes.PoolSentinel.TotalLeaked > 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CreateLeak(ObjectPoolManager manager) => _ = manager.Get<TestPoolable>();
#endif

    private sealed class DiagnosticCollector : IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly string _eventName;
        private IDisposable? _listenerSubscription;

        public DiagnosticCollector(string eventName)
        {
            _eventName = eventName;
            _listenerSubscription = DiagnosticsEvents.Source.Subscribe(this, name => name == _eventName);
        }

        public List<object> Events { get; } = [];

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == _eventName && value.Value != null)
            {
                this.Events.Add(value.Value);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
        }

        public static T GetProperty<T>(object payload, string name)
        {
            PropertyInfo property = payload.GetType().GetProperty(name)
                ?? throw new InvalidOperationException($"Missing diagnostic property '{name}'.");
            return Assert.IsType<T>(property.GetValue(payload));
        }
    }
}









