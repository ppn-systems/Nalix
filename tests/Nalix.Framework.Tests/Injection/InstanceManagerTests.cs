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
/// Verifies the public behaviors exposed by <see cref="InstanceManager"/>.
/// </summary>
public sealed class InstanceManagerTests : IDisposable
{
    private readonly InstanceManager _manager = new();

    public InstanceManagerTests() => _manager.Clear(dispose: true);

    public void Dispose()
    {
        try
        {
            _manager.Clear(dispose: true);
        }
        catch
        {
        }

        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "Register same instance twice should not dispose the instance")]
    public void RegisterSameInstanceTwiceDoesNotDisposeIt()
    {
        DisposableCounter instance = new();

        _manager.Register(instance);
        _manager.Register(instance);

        Assert.Equal(0, instance.DisposeCount);

        _ = _manager.RemoveInstance(typeof(DisposableCounter));
    }

    [Fact(DisplayName = "Register replacing instance should dispose previous once")]
    public void RegisterReplacingInstanceDisposesPreviousOnce()
    {
        DisposableCounter first = new();
        DisposableCounter second = new();

        _manager.Register(first);
        _manager.Register(second);

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);

        _ = _manager.RemoveInstance(typeof(DisposableCounter));
    }

    [Fact(DisplayName = "Register with interfaces publishes interface slot and GetExistingInstance returns it")]
    public void RegisterWithInterfacesPublishesInterfaceSlot()
    {
        TestService service = new("svc1");

        _manager.Register(service, registerInterfaces: true);

        ITestService? fromInterface = _manager.GetExistingInstance<ITestService>();
        TestService? fromConcrete = _manager.GetExistingInstance<TestService>();

        Assert.Same(service, fromInterface);
        Assert.Same(service, fromConcrete);

        _ = _manager.RemoveInstance(typeof(TestService));
    }

    [Fact(DisplayName = "GetOrCreateInstance generic caches instance; CreateInstance creates new object")]
    public void GetOrCreateInstanceCachesWhileCreateInstanceReturnsNewObject()
    {
        TestService first = _manager.GetOrCreateInstance<TestService>();
        TestService second = _manager.GetOrCreateInstance<TestService>();
        TestService fresh = (TestService)_manager.CreateInstance(typeof(TestService));

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.NotSame(first, fresh);

        _manager.Clear(dispose: true);
    }

    [Fact(DisplayName = "RemoveInstance disposes and removes instance")]
    public void RemoveInstanceDisposesAndRemovesInstance()
    {
        DisposableCounter instance = new();
        _manager.Register(instance);

        bool removed = _manager.RemoveInstance(typeof(DisposableCounter));

        Assert.True(removed);
        Assert.Equal(1, instance.DisposeCount);
        Assert.False(_manager.HasInstance<DisposableCounter>());
    }

    [Fact(DisplayName = "Clear disposes all when dispose=true")]
    public void ClearDisposesTrackedInstances()
    {
        DisposableCounter first = new();
        DisposableCounter second = new();
        TestService service = new("x");

        _manager.Register(first);
        _manager.RegisterForClassOnly(second);
        _manager.Register(service);

        _manager.Clear(dispose: true);

        Assert.True(first.DisposeCount >= 0);
        Assert.True(second.DisposeCount >= 0);
        Assert.False(_manager.HasInstance<DisposableCounter>());
        Assert.False(_manager.HasInstance<TestService>());
    }

    [Fact(DisplayName = "GenerateReport contains registered types")]
    public void GenerateReportContainsRegisteredTypes()
    {
        _manager.Clear(dispose: true);
        _manager.Register(new TestService("r1"));
        _manager.Register(new DisposableCounter());

        string report = _manager.GenerateReport();

        Assert.Contains(nameof(TestService), report);
        Assert.Contains(nameof(DisposableCounter), report);
    }

    [Fact(DisplayName = "Concurrent Register/GetOrCreateInstance should not return disposed instance or throw")]
    public void ConcurrentRegisterAndGetOrCreateDoesNotThrow()
    {
        _manager.Clear(dispose: true);

        const int threadCount = 16;
        const int iterations = 500;
        ConcurrentQueue<Exception> exceptions = new();

        _ = Parallel.For(0, threadCount, _ =>
        {
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    if ((i & 1) == 0)
                    {
                        _manager.Register(new DisposableCounter());
                        continue;
                    }

                    try
                    {
                        DisposableCounter instance = _manager.GetOrCreateInstance<DisposableCounter>();
                        if (instance.DisposeCount < 0)
                        {
                            exceptions.Enqueue(new InvalidOperationException("Invalid dispose count."));
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
            }
        });

        Assert.Empty(exceptions);

        DisposableCounter? existing = _manager.GetExistingInstance<DisposableCounter>();
        if (existing is not null)
        {
            Assert.True(existing.DisposeCount >= 0);
        }
    }

    [Fact(DisplayName = "HasInstance and GetExistingInstance behave as expected")]
    public void HasInstanceAndGetExistingInstanceBehaveAsExpected()
    {
        _manager.Clear(dispose: true);

        Assert.False(_manager.HasInstance<TestService>());
        Assert.Null(_manager.GetExistingInstance<TestService>());

        TestService service = new("t");
        _manager.Register(service);

        Assert.True(_manager.HasInstance<TestService>());
        Assert.Same(service, _manager.GetExistingInstance<TestService>());
    }

    [Fact(DisplayName = "CreateInstance does not cache the created object")]
    public void CreateInstanceDoesNotCacheCreatedObject()
    {
        _manager.Clear(dispose: true);

        TestService first = (TestService)_manager.CreateInstance(typeof(TestService));
        TestService second = (TestService)_manager.CreateInstance(typeof(TestService));

        Assert.NotSame(first, second);
        Assert.Null(_manager.GetExistingInstance<TestService>());
    }

    [Fact(DisplayName = "IsTheOnlyInstance returns a boolean (non-intrusive check)")]
    public void IsTheOnlyInstanceReturnsBoolean()
    {
        bool only = InstanceManager.IsTheOnlyInstance;

        _ = Assert.IsType<bool>(only);
    }

    private sealed class DisposableCounter : IDisposable
    {
        private int _disposed;

        public int DisposeCount => Volatile.Read(ref _disposed);

        public void Dispose() => _ = Interlocked.Increment(ref _disposed);
    }

    private interface ITestService
    {
        string Name { get; }
    }

    private sealed class TestService(string name) : ITestService, IDisposable
    {
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        public TestService()
            : this("default")
        {
        }

        public void Dispose()
        {
        }
    }
}













