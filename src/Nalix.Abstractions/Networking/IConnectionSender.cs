// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Defines a generalized mechanism for sending messages or data to a connection.
/// Typically implemented as a struct and passed to generic broadcast templates to achieve
/// zero-allocation, devirtualized invocation during high-performance loops.
/// </summary>
/// <typeparam name="TState">The type of the state passed alongside the connection.</typeparam>
public interface IConnectionSender<TState>
{
    /// <summary>
    /// Processes and sends a message to the specified connection using the provided state.
    /// </summary>
    /// <param name="connection">The target connection.</param>
    /// <param name="state">The state containing the message data, configurations, or buffers.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SendAsync(IConnection connection, ref TState state, CancellationToken ct);
}
