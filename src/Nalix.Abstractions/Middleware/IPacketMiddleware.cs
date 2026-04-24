// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Abstractions.Middleware;

/// <summary>
/// Interface that standardizes middleware implementations for packet handling.
/// Provides a method for asynchronous invocation that allows chaining via the next delegate.
/// </summary>
/// <typeparam name="TPacket">The type of packet to be handled by this middleware.</typeparam>
public interface IPacketMiddleware<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles the processing of a packet in the middleware pipeline.
    /// </summary>
    /// <param name="context">Encapsulates the packet and its connection metadata.</param>
    /// <param name="next">Delegate to call the next middleware in the sequence.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
    ValueTask InvokeAsync(IPacketContext<TPacket> context, Func<CancellationToken, ValueTask> next);
}
