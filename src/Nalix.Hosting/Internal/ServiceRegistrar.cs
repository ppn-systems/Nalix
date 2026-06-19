// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.RateLimiting;
using Nalix.Runtime.Routing;
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

    public static void RegisterHandler(
        PacketDispatchOptions<IPacket> dispatchOptions,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        Type handlerType, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(dispatchOptions);
        ArgumentNullException.ThrowIfNull(handlerType);

        _ = dispatchOptions.WithHandler(handlerType, factory);
    }

    public static void RegisterLogger(HostingBuilderContext state) => InstanceManager.Instance.Register<ILogger>(state.Logger);

    public static void RegisterSessions()
    {
        ISessionService? service = InstanceManager.Instance.GetExistingInstance<ISessionService>();

        if (service == null)
        {
            ISessionFactory? factory = InstanceManager.Instance.GetExistingInstance<ISessionFactory>();
            ISessionStore? store = InstanceManager.Instance.GetExistingInstance<ISessionStore>();
            ISessionPersistencePolicy? policy = InstanceManager.Instance.GetExistingInstance<ISessionPersistencePolicy>();

#pragma warning disable CA2000 // Dispose objects before losing scope
            service = new SessionService(factory, store, policy);
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

        ConnectionHub hub = new(state.TaskManager);
        try
        {
            InstanceManager.Instance.Register<IConnectionHub>(hub);
            InstanceManager.Instance.Register<IConnectionBroadcaster>(hub);
        }
        catch
        {
            hub.Dispose();
            throw;
        }
    }

    public static void RegisterTaskManager(HostingBuilderContext state)
    {
        if (state.HasCustomTaskManager)
        {
            return;
        }

        InstanceManager.Instance.Register<ITaskManager>(state.TaskManager);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "On successful registration InstanceManager owns the SessionService lifetime; registration failure disposes the local instance.")]
    public static void RegisterConnectionGuard(HostingBuilderContext state)
    {
        if (state.HasCustomConnectionGuard)
        {
            return;
        }

        ConnectionGuard guard = new();
        try
        {
            InstanceManager.Instance.Register<IConnectionGuard>(guard);
            InstanceManager.Instance.Register<IProofOfWorkPolicy>(guard);
        }
        catch
        {
            guard.Dispose();
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
