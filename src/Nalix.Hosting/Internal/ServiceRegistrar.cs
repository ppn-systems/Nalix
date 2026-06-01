// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Sessions;

namespace Nalix.Hosting.Internal;

internal static class ServiceRegistrar
{
    public static void RegisterPacketRegistry()
    {
        if (PacketRegistry.IsBuilt)
        {
            return;
        }

        PacketRegistry.Build();
    }

    public static void RegisterMetadataProviders(HostingBuilderContext state)
    {
        for (int i = 0; i < state.MetadataProviders.Count; i++)
        {
            PacketMetadataProviderDescriptor registration = state.MetadataProviders[i];
            PacketMetadataProviders.Register(registration.Factory());
        }
    }

    public static void RegisterHandler<THandler>(PacketDispatchOptions<IPacket> dispatchOptions, Func<object> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(dispatchOptions);
        ArgumentNullException.ThrowIfNull(factory);

        _ = dispatchOptions.WithHandler(() => (THandler)factory());
    }

    public static void RegisterLogger(HostingBuilderContext state) => InstanceManager.Instance.Register<ILogger>(state.Logger);

    public static void RegisterSessions()
    {
        ISessionService? service = InstanceManager.Instance.GetExistingInstance<ISessionService>();

        if (service == null)
        {
            ISessionFactory? factory = InstanceManager.Instance.GetExistingInstance<ISessionFactory>();
            ISessionStore? store = InstanceManager.Instance.GetExistingInstance<ISessionStore>();

#pragma warning disable CA2000 // Dispose objects before losing scope
            service = new SessionService(factory, store);
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                InstanceManager.Instance.Register<ISessionService>(service);
            }
            catch
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                throw;
            }
        }

        if (InstanceManager.Instance.GetExistingInstance<SessionPersistenceObserver>() == null)
        {
            IConnectionHub? hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>();
            if (hub is not null)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                SessionPersistenceObserver observer = new(hub, service);
#pragma warning restore CA2000 // Dispose objects before losing scope
                InstanceManager.Instance.Register<SessionPersistenceObserver>(observer);
            }
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "On successful registration InstanceManager owns the SessionService lifetime; registration failure disposes the local instance.")]
    public static void RegistererConnectionHub(HostingBuilderContext state)
    {
        if (state.HasCustomConnectionHub)
        {
            return;
        }

        ConnectionHub hub = new(logger: state.Logger);
        try
        {
            InstanceManager.Instance.Register<IConnectionHub>(hub);
        }
        catch
        {
            hub.Dispose();
            throw;
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "On successful registration InstanceManager owns the SessionService lifetime; registration failure disposes the local instance.")]
    public static void RegistererBufferPoolManager(HostingBuilderContext state)
    {
        if (state.HasCustomBufferPoolManager)
        {
            return;
        }

        BufferPoolManager manager = new();
        try
        {
            InstanceManager.Instance.Register<BufferPoolManager>(manager);
            BufferLease.ByteArrayPool.Configure(manager);
        }
        catch
        {
            manager.Dispose();
            throw;
        }
    }
}
