// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Common.Shared;

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
    /// The buffer being processed. Implementations may read, modify, or replace it.
    /// </param>
    /// <param name="connection">
    /// The connection associated with the current buffer.
    /// </param>
    /// <param name="nextHandler">
    /// The delegate representing the next middleware in the pipeline.
    /// </param>
    /// <param name="ct">
    /// A token used to observe cancellation requests.
    /// </param>
    /// <returns>
    /// A task that resolves to the processed <see cref="IBufferLease"/>, or
    /// <see langword="null"/> if processing is intentionally short-circuited.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If <paramref name="nextHandler"/> is not invoked, the pipeline will stop executing.
    /// </para>
    /// <para>
    /// Returning <see langword="null"/> indicates that the buffer has been fully handled,
    /// dropped, or deemed invalid.
    /// </para>
    /// </remarks>
    Task<IBufferLease?> InvokeAsync(
        IBufferLease buffer,
        IConnection connection,
        Func<IBufferLease, CancellationToken, Task<IBufferLease?>> nextHandler,
        CancellationToken ct
    );
}
