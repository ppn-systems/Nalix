// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Observes connection unregistrations and automatically persists session data if applicable.
/// Decouples the session service from the connection hub.
/// </summary>
public sealed class SessionPersistenceObserver : IDisposable
{
    private bool _disposed;

    private readonly IConnectionHub _hub;
    private readonly ISessionService _sessionService;

    /// <summary>
    /// Initializes a new instance of the SessionPersistenceObserver and subscribes to hub events.
    /// </summary>
    public SessionPersistenceObserver(IConnectionHub hub, ISessionService sessionService)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        _hub.ConnectionUnregistered += this.OnConnectionClosed;
    }

    private void OnConnectionClosed(IConnection connection)
    {
        if (connection != null && !connection.IsDisposed)
        {
            _ = PersistBackgroundAsync(_sessionService, connection);
        }
    }

    private static async Task PersistBackgroundAsync(ISessionService service, IConnection connection)
    {
        try
        {
            await service.SaveSessionAsync(connection).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Background persistence failures are ignored in fire-and-forget scenarios
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _hub.ConnectionUnregistered -= this.OnConnectionClosed;
    }
}
