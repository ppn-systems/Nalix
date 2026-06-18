// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Security;
using Nalix.Framework.Injection;
using Nalix.Hosting.Internal;
using Nalix.Network.RateLimiting;
using Nalix.Runtime.Handlers;
using Nalix.Runtime.Security;
using Nalix.Runtime.Sessions;

namespace Nalix.Hosting;

/// <summary>
/// Provides <c>Use…</c> extension methods that opt-in to macro-level hosting
/// features (security, session management, system control) following the
/// ASP.NET Core convention: <c>Use</c> = enable a feature pipeline stage.
/// </summary>
public static class NetworkApplicationBuilderExtensions
{
    /// <summary>
    /// Enables system-level control packet handling (DISCONNECT, CIPHER_UPDATE, TIME_SYNC, etc.).
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSystemControl(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = InstanceManager.Instance.GetOrCreateInstance<ConnectionGuard>();

        _ = builder.MapHandlers(typeof(SystemControlHandlers));

        return builder;
    }

    /// <summary>
    /// Enables time synchronization packet handling, allowing clients to synchronize
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseTimeSync(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.MapHandlers(typeof(SystemTimeSyncHandlers));

        return builder;
    }

    /// <summary>
    /// Enables the X25519 handshake and key exchange protocol, and initializes
    /// the server identity certificate.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="certificatePath">
    /// Optional explicit path to the server certificate file.
    /// When <see langword="null"/>, falls back to the default certificate location.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSecureConnections(this INetworkApplicationBuilder builder, string? certificatePath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!InstanceManager.Instance.HasInstance<ICertificateStore>())
        {
            InstanceManager.Instance.Register<ICertificateStore>(
                InstanceManager.Instance.GetOrCreateInstance<FileCertificateStore>()
            );
        }

        _ = InstanceManager.Instance.GetOrCreateInstance<ConnectionGuard>();

        _ = builder.MapHandlers(typeof(HandshakeHandlers));
        _ = builder.MapHandlers(typeof(ProofOfWorkHandlers));
        _ = builder.MapHandlers(typeof(SessionRekeyHandlers));

        if (certificatePath is not null)
        {
            HandshakeHandlers.SetCertificatePath(certificatePath);
        }
        else
        {
            HandshakeHandlers.Initialize();
        }

        return builder;
    }

    /// <summary>
    /// Enables server-side session management. Registers the
    /// <see cref="SessionHandlers"/> controller and, when the session store
    /// is enabled, automatically injects the
    /// <see cref="Nalix.Abstractions.Networking.Sessions.ISessionService"/>
    /// and <see cref="Nalix.Runtime.Sessions.SessionPersistenceObserver"/>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSessions(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.MapHandlers(typeof(SessionHandlers));

        ServiceRegistrar.RegisterSessions();

        return builder;
    }

    /// <summary>
    /// Sets the <see cref="ISessionService"/> instance used by the hosted Nalix runtime.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="sessionService">The session service to register.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSessionService(this INetworkApplicationBuilder builder, SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sessionService);
        InstanceManager.Instance.Register<ISessionService>(sessionService);
        return builder;
    }

    /// <summary>
    /// Sets the <see cref="ISessionStore"/> instance used by the default session service.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="sessionStore">The session store to register.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSessionStore(this INetworkApplicationBuilder builder, ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sessionStore);
        InstanceManager.Instance.Register<ISessionStore>(sessionStore);
        return builder;
    }

    /// <summary>
    /// Sets the <see cref="ISessionFactory"/> instance used by the default session service.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="sessionFactory">The session factory to register.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSessionFactory(this INetworkApplicationBuilder builder, ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        InstanceManager.Instance.Register<ISessionFactory>(sessionFactory);
        return builder;
    }
}
