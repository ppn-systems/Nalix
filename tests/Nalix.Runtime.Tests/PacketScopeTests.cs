// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Injection;
using Nalix.Framework.Injection;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Routing;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class PacketScopeTests
{
    private interface ITestScopedService
    {
        Guid Id { get; }
    }

    private sealed class TestScopedService : ITestScopedService, IDisposable, IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsDisposedSync { get; private set; }
        public bool IsDisposedAsync { get; private set; }
        public List<string>? Log { get; set; }

        public void Dispose()
        {
            this.IsDisposedSync = true;
            this.Log?.Add($"Sync:{this.Id}");
        }

        public ValueTask DisposeAsync()
        {
            this.IsDisposedAsync = true;
            this.Log?.Add($"Async:{this.Id}");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SyncOnlyScopedService : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsDisposedSync { get; private set; }
        public List<string>? Log { get; set; }

        public void Dispose()
        {
            this.IsDisposedSync = true;
            this.Log?.Add($"Sync:{this.Id}");
        }
    }

    private sealed class SingletonService
    {
        public string Name { get; set; } = "Singleton";
    }

    [Fact]
    public void Resolve_RegisteredScopedService_ReturnsSameInstanceWithinSameScope()
    {
        ScopedServiceRegistry registry = new();
        registry.RegisterScoped<ITestScopedService>(scope => new TestScopedService());

        using PacketScope scope = new(registry);

        ITestScopedService first = scope.GetRequiredService<ITestScopedService>();
        ITestScopedService second = scope.GetRequiredService<ITestScopedService>();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first.Should().BeSameAs(second);
        first.Id.Should().Be(second.Id);
    }

    [Fact]
    public void Resolve_DifferentScopes_ReturnsDifferentInstances()
    {
        ScopedServiceRegistry registry = new();
        registry.RegisterScoped<ITestScopedService>(scope => new TestScopedService());

        using PacketScope scope1 = new(registry);
        using PacketScope scope2 = new(registry);

        ITestScopedService first = scope1.GetRequiredService<ITestScopedService>();
        ITestScopedService second = scope2.GetRequiredService<ITestScopedService>();

        first.Should().NotBeSameAs(second);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Resolve_FallbackToSingleton_WhenNotRegisteredInScope()
    {
        ScopedServiceRegistry registry = new();
        SingletonService singleton = new() { Name = "GlobalSingleton" };
        InstanceManager.Instance.Register<SingletonService>(singleton);

        try
        {
            using PacketScope scope = new(registry);
            SingletonService resolved = scope.GetRequiredService<SingletonService>();

            resolved.Should().NotBeNull();
            resolved.Should().BeSameAs(singleton);
        }
        finally
        {
            _ = InstanceManager.Instance.RemoveInstance(typeof(SingletonService));
        }
    }

    [Fact]
    public void Resolve_UnregisteredService_ThrowsInvalidOperationException()
    {
        ScopedServiceRegistry registry = new();
        using PacketScope scope = new(registry);

        Action act = () => scope.GetRequiredService<ITestScopedService>();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*No service for type*");
    }

    [Fact]
    public void GetService_UnregisteredService_ReturnsNull()
    {
        ScopedServiceRegistry registry = new();
        using PacketScope scope = new(registry);

        ITestScopedService? resolved = scope.GetService<ITestScopedService>();

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_DisposesDisposablesInLifoOrder()
    {
        ScopedServiceRegistry registry = new();
        List<string> disposalLog = [];

        TestScopedService s1 = new() { Log = disposalLog };
        TestScopedService s2 = new() { Log = disposalLog };

        PacketScope scope = new(registry);
        scope.RegisterForDisposal((IAsyncDisposable)s1);
        scope.RegisterForDisposal((IAsyncDisposable)s2);

        await scope.DisposeAsync();

        s1.IsDisposedAsync.Should().BeTrue();
        s2.IsDisposedAsync.Should().BeTrue();

        // LIFO order: s2 was registered last, so it must be disposed first
        disposalLog.Should().ContainInOrder($"Async:{s2.Id}", $"Async:{s1.Id}");
    }

    [Fact]
    public void Dispose_DisposesDisposablesInLifoOrder_AndPrefersSyncDispose()
    {
        ScopedServiceRegistry registry = new();
        List<string> disposalLog = [];

        TestScopedService s1 = new() { Log = disposalLog };
        TestScopedService s2 = new() { Log = disposalLog };

        PacketScope scope = new(registry);
        scope.RegisterForDisposal((IAsyncDisposable)s1);
        scope.RegisterForDisposal((IAsyncDisposable)s2);

        scope.Dispose();

        s1.IsDisposedSync.Should().BeTrue();
        s2.IsDisposedSync.Should().BeTrue();

        // LIFO order: s2 was registered last, so it must be disposed first
        disposalLog.Should().ContainInOrder($"Sync:{s2.Id}", $"Sync:{s1.Id}");
    }

    [Fact]
    public async Task DisposeAsync_MixedSyncAndAsync_PreservesStrictLifoOrder()
    {
        ScopedServiceRegistry registry = new();
        List<string> disposalLog = [];

        TestScopedService s1Async = new() { Log = disposalLog };
        SyncOnlyScopedService s2Sync = new() { Log = disposalLog };

        PacketScope scope = new(registry);
        // Register async first, sync second
        scope.RegisterForDisposal((IAsyncDisposable)s1Async);
        scope.RegisterForDisposal((IDisposable)s2Sync);

        await scope.DisposeAsync();

        s1Async.IsDisposedAsync.Should().BeTrue();
        s2Sync.IsDisposedSync.Should().BeTrue();

        // Strict LIFO: s2Sync was registered second, so it MUST be disposed first!
        disposalLog[0].Should().Be($"Sync:{s2Sync.Id}");
        disposalLog[1].Should().Be($"Async:{s1Async.Id}");
    }

    [Fact]
    public void ResetForPool_ClearsResolvedServicesAndAllowsReUse()
    {
        ScopedServiceRegistry registry = new();
        registry.RegisterScoped<ITestScopedService>(scope => new TestScopedService());

        PacketScope scope = new(registry);

        ITestScopedService first = scope.GetRequiredService<ITestScopedService>();
        Guid firstId = first.Id;

        // Reset for pool
        scope.ResetForPool();

        // Rent again
        ITestScopedService second = scope.GetRequiredService<ITestScopedService>();
        Guid secondId = second.Id;

        firstId.Should().NotBe(secondId);
        ((TestScopedService)first).IsDisposedSync.Should().BeTrue();
    }
}
