// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Terminates active client connections according to runtime policies.
/// </summary>
public interface IConnectionTerminator
{
    /// <summary>
    /// Closes all active connections.
    /// </summary>
    /// <param name="reason">The optional close reason.</param>
    /// <returns>The number of close attempts issued.</returns>
    int CloseAllConnections(string? reason = null);

    /// <summary>
    /// Closes all active connections matching the specified endpoint address.
    /// </summary>
    /// <param name="networkEndpoint">The endpoint address to close.</param>
    /// <param name="reason">The optional close reason.</param>
    /// <returns>The number of close attempts issued.</returns>
    int CloseEndpoint(INetworkEndpoint networkEndpoint, string? reason = null);
}
