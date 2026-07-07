# Custom Middleware Guide

This guide shows how to implement custom authorization rules that validate session tokens across the Nalix inbound pipeline.

## Implementation overview

Implementing custom middleware involves creating a class that implements `IPacketMiddleware<TPacket>`. For general-purpose security middleware, you should typically target `IPacket`, the base interface for all packets.

## Step 1. Implement packet middleware for session validation

The following middleware checks for a valid session token in the connection attributes and verifies permissions against the packet's metadata.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Framework.Injection;

[MiddlewareOrder(-60)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class SessionAuthorizationMiddleware : IPacketMiddleware<IPacket>
{
    public async ValueTask InvokeAsync(
        IPacketContext<IPacket> context,
        Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // 1) Permission gate from packet metadata.
        if (context.Attributes.Permission is not null &&
            context.Connection.Level < context.Attributes.Permission.Level)
        {
            context.Connection.Disconnect("Permission denied.");
            return;
        }

        // 2) Session establishment gate: verify that the connection has successfully
        // completed its handshake or resumption.
        if (!context.Connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? established) ||
            established is not true)
        {
            context.Connection.Disconnect("Session not established.");
            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
```

## Step 2. Register middleware in the host dispatch

Middleware is registered fluently during server setup using the `ConfigureDispatchOptions` method.

```csharp
using Nalix.Hosting;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureDispatchOptions(options =>
    {
        _ = options.WithMiddleware(new SessionAuthorizationMiddleware());
    })
    .ListenTcp<MyProtocol>().Bind()
    .Build();
```

## Step 3. Add packet metadata on handlers

The middleware will now automatically enforce permissions based on the attributes applied to your handler methods.

```csharp
[PacketHandler("SecureHandlers")]
public sealed class SecureHandlers
{
    [PacketOpcode(0x1201)]
    [PacketPermission(PermissionLevel.USER)]
    public ValueTask HandleAsync(IPacketContext<MyPacket> context)
    {
        // Business logic execution
        return ValueTask.CompletedTask;
    }
}
```

## Flow summary

## Related pages

- [Middleware Pipeline](../../api/runtime/middleware/pipeline.md)
- [Packet Dispatch](../../api/runtime/routing/packet-dispatch.md)
