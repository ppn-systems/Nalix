// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

public sealed class PacketLifecycleTests : IDisposable
{
    private readonly ObjectPoolManager _manager;
    private readonly ObjectPoolManager? _previousManager;
    private readonly bool _previousDiagnostics;

    public PacketLifecycleTests()
    {
        // Enable diagnostics globally for the duration of the test
        var config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        _previousDiagnostics = config.EnableDiagnostics;
        config.EnableDiagnostics = true;

        _manager = new ObjectPoolManager();
        
        // Mock the global instance so that PacketBase.Dispose uses our local manager
        _previousManager = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>();
        InstanceManager.Instance.Register<ObjectPoolManager>(_manager);
    }

    public void Dispose()
    {
        // Restore previous manager if any, otherwise clear
        if (_previousManager != null)
        {
            InstanceManager.Instance.Register<ObjectPoolManager>(_previousManager);
        }
        else
        {
            InstanceManager.Instance.RemoveInstance(typeof(ObjectPoolManager));
        }

        // Restore diagnostics setting
        ConfigurationManager.Instance.Get<ObjectPoolOptions>().EnableDiagnostics = _previousDiagnostics;
        
        _manager.ResetStatistics();
    }

    [Fact]
    public void Handshake_ReturnViaInterface_CorrectlyTracked()
    {
        // 1. Rent a Handshake packet
        Handshake packet = _manager.Get<Handshake>();
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        
        // 2. Return it as an IPoolable interface
        IPoolable poolable = packet;
        _manager.Return(poolable);
        
        // 3. Verify it was tracked against the concrete type metrics, not the interface
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);
    }

    [Fact]
    public void PacketLease_Struct_Disposal_ReturnsToPool()
    {
        Handshake packet = _manager.Get<Handshake>();
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);

        // Create a lease
        using (var lease = new PacketLease<Handshake>(packet))
        {
            Assert.Same(packet, lease.Value);
        }

        // Disposal of lease should have called packet.Dispose() which returns to manager
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);
    }

    [Fact]
    public void PacketBase_AtomicDisposal_PreventsDoubleReturn()
    {
        Handshake packet = _manager.Get<Handshake>();
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);

        // First disposal
        packet.Dispose();
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);

        // Second disposal should be a no-op
        packet.Dispose();
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]); // Should NOT be 2
    }

    [Fact]
    public void PacketBase_OnRent_SetsFlagCorrectly()
    {
        // 1. Created via 'new' (not rented)
        Handshake manual = new();
        manual.Dispose(); // Should NOT be returned to pool (no-op)
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);

        // 2. Created via Manager (rented)
        Handshake rented = _manager.Get<Handshake>();
        rented.Dispose(); // SHOULD be returned
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);
    }
}
