// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Framework.Injection.DI;
using Xunit;

namespace Nalix.Framework.Tests.Injection;

public sealed class SingletonTests : IDisposable
{
    public SingletonTests() => Singleton.Clear();

    public void Dispose() => Singleton.Clear();

    [Fact]
    public void SingletonRegisterInstanceThenResolveReturnsSameInstance()
    {
        Service service = new("alpha");

        Singleton.Register<Service>(service);
        Service? resolved = Singleton.Resolve<Service>();

        Assert.Same(service, resolved);
        Assert.True(Singleton.IsRegistered<Service>());
    }

    [Fact]
    public void SingletonRegisterDuplicateWithoutOverwriteThrowsInvalidOperationException()
    {
        Singleton.Register<Service>(new Service("a"));

        Assert.Throws<InvalidOperationException>(() => Singleton.Register<Service>(new Service("b")));
    }

    [Fact]
    public void SingletonRegisterWithOverwriteReplacesResolvedService()
    {
        Singleton.Register<Service>(new Service("old"));
        Singleton.Register<Service>(new Service("new"), allowOverwrite: true);

        Service? resolved = Singleton.Resolve<Service>();

        Assert.NotNull(resolved);
        Assert.Equal("new", resolved!.Name);
    }

    [Fact]
    public void SingletonRegisterInterfaceWithFactoryThenResolveReturnsFactoryInstance()
    {
        Singleton.Register<IService, Service>(() => new Service("factory"));

        IService? resolved = Singleton.Resolve<IService>();

        Assert.NotNull(resolved);
        Assert.Equal("factory", resolved!.Name);
    }

    [Fact]
    public void SingletonRegisterInterfaceWithoutFactoryThenResolveCreatesImplementation()
    {
        Singleton.Register<IService, DefaultService>();

        IService? resolved = Singleton.Resolve<IService>();

        Assert.NotNull(resolved);
        Assert.Equal("default", resolved!.Name);
    }

    [Fact]
    public void SingletonResolveWhenNotRegisteredAndCreateDisabledReturnsNull()
    {
        Service? resolved = Singleton.Resolve<Service>(createIfNotExists: false);

        Assert.Null(resolved);
    }

    [Fact]
    public void SingletonResolveWhenNotRegisteredAndCreateEnabledThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Singleton.Resolve<Service>(createIfNotExists: true));
    }

    [Fact]
    public void SingletonRemoveUnregistersType()
    {
        Singleton.Register<Service>(new Service("x"));
        Assert.True(Singleton.IsRegistered<Service>());

        Singleton.Remove<Service>();

        Assert.False(Singleton.IsRegistered<Service>());
        Assert.Null(Singleton.Resolve<Service>(createIfNotExists: false));
    }

    [Fact]
    public void SingletonDisposeDisposesCreatedServices()
    {
        DisposableService disposable = new();
        Singleton.Register<DisposableService>(disposable);
        _ = Singleton.Resolve<DisposableService>();

        Singleton.Dispose();

        Assert.True(disposable.IsDisposed);
        Assert.False(Singleton.IsDisposing);
    }

    [Fact]
    public void SingletonBaseTryGetInstanceBeforeCreationReturnsFalseThenTrueAfterEnsureCreated()
    {
        bool before = FreshSingleton.TryGetInstance(out FreshSingleton? instanceBefore);
        FreshSingleton.EnsureCreated();
        bool after = FreshSingleton.TryGetInstance(out FreshSingleton? instanceAfter);

        Assert.False(before);
        Assert.Null(instanceBefore);
        Assert.True(after);
        Assert.NotNull(instanceAfter);
        Assert.Same(FreshSingleton.Instance, instanceAfter);
    }

    [Fact]
    public void SingletonBaseDisposeIsIdempotentAndCallsDisposeManagedOnce()
    {
        DisposableSingleton singleton = DisposableSingleton.Instance;

        singleton.Dispose();
        singleton.Dispose();

        Assert.Equal(1, singleton.DisposeManagedCount);
    }

    [Fact]
    public void SingletonBaseWithoutParameterlessConstructorThrowsTypeInitializationException()
    {
        TypeInitializationException ex = Assert.Throws<TypeInitializationException>(() =>
        {
            _ = NoDefaultCtorSingleton.Instance;
        });

        Assert.NotNull(ex.InnerException);
        Assert.Contains("parameterless constructor", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    private interface IService
    {
        string Name { get; }
    }

    private sealed class Service(string name) : IService
    {
        public string Name { get; } = name;
    }

    private sealed class DefaultService : IService
    {
        public string Name => "default";
    }

    private sealed class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class FreshSingleton : SingletonBase<FreshSingleton>
    {
        private FreshSingleton() { }
    }

    private sealed class DisposableSingleton : SingletonBase<DisposableSingleton>
    {
        private DisposableSingleton() { }
        public int DisposeManagedCount { get; private set; }
        protected override void DisposeManaged() => DisposeManagedCount++;
    }

    private sealed class NoDefaultCtorSingleton : SingletonBase<NoDefaultCtorSingleton>
    {
        private NoDefaultCtorSingleton(int _) { }
    }
}













