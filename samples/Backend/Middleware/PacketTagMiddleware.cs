// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;
using Backend.Attributes;

namespace Backend.Middleware;

/// <summary>
/// Example middleware that reads a custom packet attribute and documents where to extend the pipeline.
/// </summary>
[MiddlewareOrder(500)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class PacketTagMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Executes the middleware stage.
    /// No async state machine is allocated when the downstream chain completes synchronously.
    /// </summary>
    public ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Attributes.CustomAttributes.TryGetValue(typeof(PacketTagAttribute), out Attribute? attribute) &&
            attribute is PacketTagAttribute tagAttribute)
        {
            // This is where a real application would enforce rate limits, tracing, or tag-based filtering.
            _ = tagAttribute.Tag;
        }

        // Direct return — no async state machine allocated for synchronous completions.
        ValueTask pending = next(context.CancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
#pragma warning disable CA1849 // Completed-success fast path.
            pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
            return default;
        }

        return AwaitNextAsync(pending);

        static async ValueTask AwaitNextAsync(ValueTask operation)
        {
            await operation.ConfigureAwait(false);
        }
    }
}

