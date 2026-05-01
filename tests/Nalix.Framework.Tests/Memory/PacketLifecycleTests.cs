// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.DataFrames;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Runtime.Pooling;

namespace Nalix.Framework.Tests.Memory;

[Collection("Sequential Pooling Tests")]
public sealed class PacketLifecycleTests : IDisposable
{
    private readonly ObjectPoolManager _manager;
    private readonly bool _previousDiagnostics;
    public PacketLifecycleTests()
    {
        // Enable diagnostics globally for the duration of the test
        var config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        _previousDiagnostics = config.EnableDiagnostics;
        config.EnableDiagnostics = true;

        _manager = new ObjectPoolManager();
        PacketRegistry.Configure(_manager);
    }

    public void Dispose()
    {
        // Restore previous manager if any, otherwise clear
        PacketRegistry.Configure(null!);

        // Restore diagnostics setting
        ConfigurationManager.Instance.Get<ObjectPoolOptions>().EnableDiagnostics = _previousDiagnostics;

        _manager.ResetStatistics();
    }

    [Fact]
    public void PacketLease_Struct_Disposal_ReturnsToPool()
    {
        Handshake packet = _manager.Get<Handshake>();
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);

        // Create a lease
        using (var lease = new PacketScope<Handshake>(packet))
        {
            Assert.Same(packet, lease.Value);
        }

        // Disposal of lease should have called packet.Dispose() which returns to manager
        Assert.Equal(0L, (long)_manager.GetTypeInfo<Handshake>()["Outstanding"]);
        Assert.Equal(1L, (long)_manager.GetTypeInfo<Handshake>()["TotalReturns"]);
    }

#if DEBUG
    [Fact]
    public void PacketBase_AtomicDisposal_PreventsDoubleReturn()
    {
        Assert.NotNull(PacketRegistry.Manager);
        Assert.Same(_manager, PacketRegistry.Manager);
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
#endif
}













