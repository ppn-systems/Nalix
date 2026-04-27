# Custom Middleware Guide

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 10 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Middleware](../../concepts/runtime/middleware-pipeline.md)

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
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;

[MiddlewareOrder(-60)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class SessionAuthorizationMiddleware : IPacketMiddleware<IPacket>
{
    private readonly IConnectionHub? _hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>();

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

        // 2) Session token gate from connection attributes + session store.
        if (!TryGetSessionToken(context.Connection, out ulong sessionToken))
        {
            context.Connection.Disconnect("Missing session token.");
            return;
        }

        if (_hub is null)
        {
            context.Connection.Disconnect("Session service unavailable.");
            return;
        }

        SessionEntry? entry = await _hub.SessionStore
            .RetrieveAsync(sessionToken, context.CancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            context.Connection.Disconnect("Session expired or revoked.");
            return;
        }

        entry.Return();
        await next(context.CancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetSessionToken(IConnection connection, out ulong token)
    {
        if (connection.Attributes.TryGetValue(ConnectionAttributes.SessionToken, out object? raw) &&
            raw is ulong sessionToken)
        {
            token = sessionToken;
            return true;
        }

        token = default;
        return false;
    }
}
```

## Step 2. Register middleware in the host dispatch

Middleware is registered fluently during server setup using the `ConfigureDispatch` method.

```csharp
using Nalix.Hosting;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureDispatch(options =>
    {
        _ = options.WithMiddleware(new SessionAuthorizationMiddleware());
    })
    .AddTcp<MyProtocol>()
    .Build();
```

## Step 3. Add packet metadata on handlers

The middleware will now automatically enforce permissions based on the attributes applied to your handler methods.

```csharp
[PacketController("SecureHandlers")]
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

```mermaid
flowchart LR
    A["Socket buffer"] --> B["Deserialization"]
    B --> C["SessionAuthorizationMiddleware"]
    C --> D["Handler"]
```

## Related pages

- [Middleware Pipeline](../../api/runtime/middleware/pipeline.md)
- [Packet Dispatch](../../api/runtime/routing/packet-dispatch.md)

