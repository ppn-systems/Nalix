// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides a mechanism to guard and manage connection rates, capacities, and blacklists.
/// </summary>
public interface IConnectionGuard : IDisposable, IAsyncDisposable, IReportable
{
    /// <summary>
    /// Safely decrements the connection counter for the specified endpoint without requiring an active connection.
    /// Used for rollback when connection initialization fails.
    /// </summary>
    /// <param name="endPoint">The IP endpoint to release the connection slot for.</param>
    void Release(IPEndPoint endPoint);

    /// <summary>
    /// Manually bans an IP address for a specified duration.
    /// This bypasses progressive limits and applies the ban immediately.
    /// </summary>
    /// <param name="address">The IP address to ban.</param>
    /// <param name="duration">The duration of the ban.</param>
    void BanEndpoint(IPAddress address, TimeSpan duration);

    /// <summary>
    /// Attempts to acquire a connection slot for the given endpoint.
    /// </summary>
    /// <param name="endPoint">The IP endpoint requesting connection.</param>
    /// <returns>True if connection is allowed; false if limit exceeded.</returns>
    bool TryAccept(IPEndPoint endPoint);

    /// <summary>
    /// Handles connection closure event and decrements the connection counter.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="args">Connection event arguments.</param>
    void OnConnectionClosed(object? sender, IConnectionEventArgs args);

    /// <summary>
    /// Checks if the provided endpoint belongs to a known trusted proxy.
    /// </summary>
    /// <param name="endPoint">The endpoint to check.</param>
    /// <returns>True if it is a trusted proxy, false otherwise.</returns>
    bool IsTrustedProxy(IPEndPoint? endPoint);
}
