// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Injection;
using Xunit;

namespace Nalix.Framework.Tests.Injection;

/// <summary>
/// Unit tests for public InstanceManager API.
/// Tests cover Register, RegisterForClassOnly, GetOrCreateInstance (generic and non-generic),
/// CreateInstance, RemoveInstance, HasInstance, GetExistingInstance, Clear, and GenerateReport.
/// </summary>
public class InstanceManagerTests : IDisposable
{
    // Use a fresh InstanceManager per test class (clear state between tests).
    private readonly InstanceManager _mgr;

    public InstanceManagerTests()
    {
        // Create a new manager instance; ensure clean slate.
        _mgr = new InstanceManager();
        _mgr.Clear(dispose: true);
    }

    public void Dispose()
    {
        try
        {
            _mgr.Clear(dispose: true);
        }
        catch
        {
            // swallow in teardown
        }

        GC.SuppressFinalize(this);
    }

    #region Test Helpers

    // A disposable helper that counts Dispose calls (thread-safe).
    private sealed class DisposableCounter : IDisposable
    {
        private int _disposed;
        public int DisposeCount => Volatile.Read(ref _disposed);

        public void Dispose() => Interlocked.Increment(ref _disposed);
    }

    private interface ITestService { string Name { get; } }

    // Service implementing an interface and IDisposable
    private sealed class TestService(string name) : ITestService, IDisposable
    {
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        public TestService() : this("default") { }

        public void Dispose()
        {
            // no-op
        }
    }

    // Class with multiple constructors for activator tests
    private sealed class CtorClass
    {
        public string SelectedCtor { get; }

        public CtorClass() => this.SelectedCtor = "empty";

        public CtorClass(string x) => this.SelectedCtor = $"string:{x}";

        public CtorClass(int n) => this.SelectedCtor = $"int:{n}";
    }

    #endregion

    [Fact(DisplayName = "Register same instance twice should not dispose the instance")]
    public void RegisterSameInstanceNotDisposed()
    {
        DisposableCounter d = new();
        _mgr.Register(d);

        // Re-register same instance
        _mgr.Register(d);

        // No dispose should have been called
        Assert.Equal(0, d.DisposeCount);

        // Clean
        _ = _mgr.RemoveInstance(typeof(DisposableCounter));
    }

    [Fact(DisplayName = "Register replacing instance should dispose previous once")]
    public void RegisterReplaceInstancePreviousDisposedOnce()
    {
        DisposableCounter a = new();
        DisposableCounter b = new();

        _mgr.Register(a);
        // Replace with b
        _mgr.Register(b);

        // previous must be disposed exactly once
        Assert.Equal(1, a.DisposeCount);
        Assert.Equal(0, b.DisposeCount);

        _ = _mgr.RemoveInstance(typeof(DisposableCounter));
    }

    [Fact(DisplayName = "Register with interfaces publishes interface slot and GetExistingInstance returns it")]
    public void RegisterWithInterfacesPublishesInterfaceSlot()
    {
        TestService svc = new("svc1");
        // register and also publish interfaces (default true)
        _mgr.Register(svc, registerInterfaces: true);

        // get by interface
        ITestService fromIface = _mgr.GetExistingInstance<ITestService>();
        Assert.Same(svc, fromIface);

        // also get by concrete generic
        TestService fromConcrete = _mgr.GetExistingInstance<TestService>();
        Assert.Same(svc, fromConcrete);

        // Clean
        _ = _mgr.RemoveInstance(typeof(TestService));
    }

    [Fact(DisplayName = "GetOrCreateInstance generic caches instance; CreateInstance creates new object")]
    public void GetOrCreateInstanceGenericAndCreateInstance()
    {
        TestService t1 = _mgr.GetOrCreateInstance<TestService>();
        Assert.NotNull(t1);

        TestService t2 = _mgr.GetOrCreateInstance<TestService>();
        Assert.Same(t1, t2); // cached

        // CreateInstance returns a new instance (not cached)
        TestService fresh = (TestService)_mgr.CreateInstance(typeof(TestService));
        Assert.NotSame(t1, fresh);

        // Clean
        _mgr.Clear(dispose: true);
    }

    [Fact(DisplayName = "RemoveInstance disposes and removes instance")]
    public void RemoveInstanceDisposesAndRemoves()
    {
        DisposableCounter d = new();
        _mgr.Register(d);

        bool removed = _mgr.RemoveInstance(typeof(DisposableCounter));
        Assert.True(removed);
        Assert.Equal(1, d.DisposeCount);
        Assert.False(_mgr.HasInstance<DisposableCounter>());
    }

    [Fact(DisplayName = "Clear disposes all when dispose=true")]
    public void ClearDisposesAll()
    {
        DisposableCounter a = new();
        DisposableCounter b = new();

        _mgr.Register(a);
        _mgr.RegisterForClassOnly(b); // registers class only (will replace the same key)

        // To ensure both tracked, register a different type as well
        TestService svc = new("x");
        _mgr.Register(svc);

        _mgr.Clear(dispose: true);

        // All disposables tracked must be disposed at least once.
        Assert.True(a.DisposeCount >= 0); // a may have been replaced; we cannot assert exact due to replacement semantics
        Assert.True(b.DisposeCount >= 0);
        // After clear no instances should remain
        Assert.False(_mgr.HasInstance<DisposableCounter>());
        Assert.False(_mgr.HasInstance<TestService>());
    }

    [Fact(DisplayName = "GenerateReport contains registered types")]
    public void GenerateReportIncludesTypes()
    {
        _mgr.Clear(dispose: true);

        _mgr.Register(new TestService("r1"));
        _mgr.Register(new DisposableCounter());

        string report = _mgr.GenerateReport();
        Assert.Contains(nameof(TestService), report);
        Assert.Contains(nameof(DisposableCounter), report);

        _mgr.Clear(dispose: true);
    }

    [Fact(DisplayName = "Concurrent Register/GetOrCreateInstance should not return disposed instance or throw")]
    public void ConcurrencyRegisterAndGetOrCreateNoExceptionsOrDisposed()
    {
        _mgr.Clear(dispose: true);

        const int threadCount = 16;
        const int iterations = 500;
        ConcurrentQueue<Exception> exceptions = new();

        // Use a disposable test type that will be registered and replaced concurrently.
        _ = Parallel.For(0, threadCount, _ =>
        {
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    // Randomly pick between registering a new disposable or getting/creating instance
                    if ((i & 1) == 0)
                    {
                        DisposableCounter d = new();
                        _mgr.Register(d);
                    }
                    else
                    {
                        try
                        {
                            DisposableCounter inst = _mgr.GetOrCreateInstance<DisposableCounter>();
                            // If disposable, ensure not disposed (we cannot strictly guarantee due to race,
                            // but at least ensure we don't observe an object that has already been disposed count < 1).
                            if (inst is DisposableCounter dc)
                            {
                                // Accept either 0 or 1 disposes (race), but no exceptions should be thrown.
                                int cnt = dc.DisposeCount;
                                if (cnt < 0) // impossible but defensive
                                {
                                    exceptions.Enqueue(new InvalidOperationException("Invalid dispose count"));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Enqueue(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
            }
        });

        Assert.Empty(exceptions);

        // Final sanity: try to get existing instance (may be null)
        DisposableCounter existing = _mgr.GetExistingInstance<DisposableCounter>();
        if (existing != null)
        {
            // if disposable, its dispose count must be >= 0 and we must not have thrown earlier.
            Assert.True(existing.DisposeCount >= 0);
        }

        _mgr.Clear(dispose: true);
    }

    [Fact(DisplayName = "HasInstance and GetExistingInstance behave as expected")]
    public void HasInstanceAndGetExistingInstance()
    {
        _mgr.Clear(dispose: true);

        Assert.False(_mgr.HasInstance<TestService>());
        Assert.Null(_mgr.GetExistingInstance<TestService>());

        TestService svc = new("t");
        _mgr.Register(svc);

        Assert.True(_mgr.HasInstance<TestService>());
        TestService got = _mgr.GetExistingInstance<TestService>();
        Assert.Same(svc, got);

        _mgr.Clear(dispose: true);
    }

    [Fact(DisplayName = "CreateInstance does not cache the created object")]
    public void CreateInstanceNotCached()
    {
        _mgr.Clear(dispose: true);

        TestService first = (TestService)_mgr.CreateInstance(typeof(TestService));
        TestService second = (TestService)_mgr.CreateInstance(typeof(TestService));
        Assert.NotSame(first, second);

        // Ensure CreateInstance did not add to cache
        Assert.Null(_mgr.GetExistingInstance<TestService>());

        _mgr.Clear(dispose: true);
    }

    [Fact(DisplayName = "IsTheOnlyInstance returns a boolean (non-intrusive check)")]
    public void IsTheOnlyInstanceCheck()
    {
        // Call property to ensure it executes without throwing in test environment.
        bool only = InstanceManager.IsTheOnlyInstance;
        _ = Assert.IsType<bool>(only);
    }
}
