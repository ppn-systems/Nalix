// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Framework.Injection;
using Nalix.Runtime.Sessions;

namespace Nalix.Hosting.Internal;

internal static class ServiceRegistrar
{
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
}
