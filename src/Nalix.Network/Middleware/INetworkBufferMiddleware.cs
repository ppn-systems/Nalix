// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;

namespace Nalix.Network.Middleware;

/// <summary>
/// Represents a middleware component in the network buffer processing pipeline.
/// </summary>
/// <remarks>
/// <para>
/// A middleware can inspect, transform, replace, or short-circuit the processing
/// of a <see cref="IBufferLease"/> within the context of a <see cref="IConnection"/>.
/// </para>
/// <para>
/// Implementations must clearly define ownership of the <see cref="IBufferLease"/>:
/// if a new buffer is returned, the original buffer should typically be disposed
/// to avoid memory leaks.
/// </para>
/// </remarks>
public interface INetworkBufferMiddleware
{
    /// <summary>
    /// Processes the specified buffer within the current connection context.
    /// </summary>
    /// <param name="buffer">
    /// The current buffer lease. Implementations may return this instance, a replacement instance, or
    /// <see langword="null"/> to drop the packet.
    /// </param>
    /// <param name="connection">
    /// The connection associated with the current buffer.
    /// </param>
    /// <param name="ct">
    /// A token used to observe cancellation requests.
    /// </param>
    /// <returns>
    /// A value task that resolves to the processed <see cref="IBufferLease"/>, or
    /// <see langword="null"/> if processing is intentionally short-circuited.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returning <see langword="null"/> indicates that the buffer has been fully handled,
    /// dropped, or deemed invalid.
    /// </para>
    /// </remarks>
    ValueTask<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, CancellationToken ct);
}
