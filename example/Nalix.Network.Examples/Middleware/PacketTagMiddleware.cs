// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

namespace Nalix.Network.Examples.Middleware;

/// <summary>
/// Example middleware that reads a custom packet attribute and documents where to extend the pipeline.
/// </summary>
[MiddlewareOrder(500)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class PacketTagMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Executes the middleware stage.
    /// </summary>
    public async ValueTask InvokeAsync(PacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        if (!context.Attributes.CustomAttributes.TryGetValue(typeof(PacketTagAttribute), out Attribute? attribute) ||
            attribute is not PacketTagAttribute tagAttribute)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // This is where a real application would enforce rate limits, tracing, or tag-based filtering.
        _ = tagAttribute.Tag;

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
