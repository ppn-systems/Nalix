// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Network.Examples.Custom;

/// <summary>
/// Middleware that enforces concurrency limits on incoming packets.
/// </summary>
[MiddlewareOrder(500)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class CustomMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Invokes the concurrency middleware, enforcing concurrency limits on incoming packets.
    /// </summary>
    /// <param name="context">The packet context containing the packet and connection information.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        [NotNull] PacketContext<IPacket> context,
        [NotNull] Func<CancellationToken, Task> next)
    {
        // Try get the attribute instance from the custom attribute dictionary.
        // The dictionary is keyed by Type, so use typeof(PacketCustomAttribute).
        if (!context.Attributes.CustomAttributes.TryGetValue(typeof(PacketCustomAttribute), out var attr)
            || attr is not PacketCustomAttribute)
        {
            // Attribute not present -> continue pipeline.
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // At this point customAttr is non-null and strongly typed.
        // TODO: Implement concurrency enforcement logic using properties on customAttr.

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
