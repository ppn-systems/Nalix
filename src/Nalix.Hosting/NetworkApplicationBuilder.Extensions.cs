// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Security;
using Nalix.Framework.Injection;
using Nalix.Hosting.Internal;
using Nalix.Runtime.Handlers;
using Nalix.Runtime.Security;

namespace Nalix.Hosting;

/// <summary>
/// Provides <c>Use…</c> extension methods that opt-in to macro-level hosting
/// features (security, session management, system control) following the
/// ASP.NET Core convention: <c>Use</c> = enable a feature pipeline stage.
/// </summary>
public static class NetworkApplicationBuilderExtensions
{
    /// <summary>
    /// Enables the X25519 handshake and key exchange protocol, and initializes
    /// the server identity certificate.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="certificatePath">
    /// Optional explicit path to the server certificate file.
    /// When <see langword="null"/>, falls back to the path configured via
    /// <see cref="INetworkApplicationBuilder.ConfigureCertificate"/> or the
    /// default certificate location.
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

        _ = builder.AddHandler<HandshakeHandlers>();
        _ = builder.AddHandler<KeyExchangeHandlers>();

        // Resolve certificate path: explicit parameter wins,
        // then ConfigureCertificate() state, then default.
        string? resolvedPath = certificatePath;

        if (resolvedPath is null && builder is NetworkApplicationBuilder concrete)
        {
            resolvedPath = concrete._state.IdentityCertificatePath;
        }

        if (resolvedPath is not null)
        {
            HandshakeHandlers.SetCertificatePath(resolvedPath);
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

        _ = builder.AddHandler<SessionHandlers>();

        ServiceRegistrar.RegisterSessions();

        return builder;
    }

    /// <summary>
    /// Enables system-level control packet handling (PING, PONG,
    /// DISCONNECT, CIPHER_UPDATE, TIME_SYNC, etc.).
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The current builder instance.</returns>
    public static INetworkApplicationBuilder UseSystemControl(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.AddHandler<SystemControlHandlers>();

        return builder;
    }
}
