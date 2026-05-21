// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;

namespace Nalix.Hosting.Internal;

/// <summary>
/// Terminates active connections without making <see cref="IConnectionHub"/> own policy decisions.
/// </summary>
internal sealed class ConnectionTerminator : IConnectionTerminator
{
    private const string DefaultCloseAllReason = "Force disconnected by server policy.";
    private const string DefaultEndpointReason = "Force disconnected by endpoint policy.";

    private readonly ILogger? _logger;
    private readonly IConnectionHub _hub;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTerminator"/> class.
    /// </summary>
    /// <param name="hub">The active connection registry.</param>
    /// <param name="logger">The optional logger.</param>
    public ConnectionTerminator(IConnectionHub hub, ILogger? logger = null)
    {
        _logger = logger;
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int CloseEndpoint(INetworkEndpoint networkEndpoint, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(networkEndpoint);

        IReadOnlyCollection<IConnection> connections = _hub.ListConnections(networkEndpoint);
        int closedCount = this.Disconnect(connections, reason ?? DefaultEndpointReason, nameof(CloseEndpoint));

        if (closedCount > 0 && _logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                $"[NW.{nameof(ConnectionTerminator)}:{nameof(CloseEndpoint)}] closed={closedCount} ip={networkEndpoint.Address}");
        }

        return closedCount;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int CloseAllConnections(string? reason = null)
    {
        IReadOnlyCollection<IConnection> connections = _hub.ListConnections();
        int closedCount = this.Disconnect(connections, reason ?? DefaultCloseAllReason, nameof(CloseAllConnections));

        if (closedCount > 0 && _logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                $"[NW.{nameof(ConnectionTerminator)}:{nameof(CloseAllConnections)}] closed={closedCount}");
        }

        return closedCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int Disconnect(IReadOnlyCollection<IConnection> connections, string reason, string operationName)
    {
        int closedCount = 0;

        foreach (IConnection connection in connections)
        {
            try
            {
                connection.Disconnect(reason);
                closedCount++;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                connection.ThrottledError(
                    _logger,
                    "connection_terminator.close_error",
                    $"[NW.{nameof(ConnectionTerminator)}:{operationName}] disconnect failed id={connection.ID}",
                    ex);
            }
        }

        return closedCount;
    }
}
