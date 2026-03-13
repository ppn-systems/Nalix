# Custom Middleware End-to-End

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Middleware](../concepts/middleware.md)

This guide shows how to implement custom authorization rules that validate session tokens across the Nalix inbound pipeline.

The example combines:

- a **buffer middleware** pre-check (cheap fail-fast guard before deserialization)
- a **packet middleware** authorization check (permission + session token validation against `ISessionStore`)

## Pick the right layer

- **`INetworkBufferMiddleware`** runs on raw `IBufferLease` before packet parsing.
- **`IPacketMiddleware<TPacket>`** runs after deserialization with metadata (`PacketPermission`, timeout, rate-limit, opcode).

For complex authorization, use both:

- buffer middleware to reject obviously invalid traffic early
- packet middleware to apply metadata-aware authorization logic

## Step 1. Add a raw buffer pre-check

```csharp
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;

[MiddlewareOrder(-200)]
public sealed class SessionEnvelopeGuard : INetworkBufferMiddleware
{
    public ValueTask<IBufferLease?> InvokeAsync(
        IBufferLease buffer,
        IConnection connection,
        CancellationToken ct)
    {
        // Cheap guard: drop malformed/empty frames before deserialization work.
        if (buffer.Length <= 0)
        {
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        return ValueTask.FromResult<IBufferLease?>(buffer);
    }
}
```

## Step 2. Add packet middleware for permission + session token validation

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
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
        if (!TryGetSessionToken(context.Connection, out UInt56 sessionToken))
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

        // Optional strict check for SessionResume packet payload.
        if (context.Packet is SessionResume resume &&
            resume.Stage == SessionResumeStage.REQUEST &&
            !resume.SessionToken.IsEmpty &&
            resume.SessionToken.ToUInt56() != sessionToken)
        {
            entry.Return();
            context.Connection.Disconnect("Session token mismatch.");
            return;
        }

        entry.Return();
        await next(context.CancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetSessionToken(IConnection connection, out UInt56 token)
    {
        if (connection.Attributes.TryGetValue(ConnectionAttributes.SessionToken, out object? raw) &&
            raw is UInt56 sessionToken)
        {
            token = sessionToken;
            return true;
        }

        token = default;
        return false;
    }
}
```

## Step 3. Register middleware in the host dispatch

```csharp
using Nalix.Network.Hosting;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureDispatch(options =>
    {
        _ = options.WithBufferMiddleware(new SessionEnvelopeGuard());
        _ = options.WithMiddleware(new SessionAuthorizationMiddleware());
    })
    .AddTcp<MyProtocol>()
    .Build();
```

## Step 4. Add packet metadata on handlers

```csharp
[PacketController("SecureHandlers")]
public sealed class SecureHandlers
{
    [PacketOpcode(0x1201)]
    [PacketPermission(PermissionLevel.USER)]
    public ValueTask HandleAsync(IPacketContext<MyPacket> context)
    {
        // Business logic
        return ValueTask.CompletedTask;
    }
}
```

## Flow summary

```mermaid
flowchart LR
    A["Socket frame"] --> B["NetworkBufferMiddlewarePipeline"]
    B --> C["Deserialize packet"]
    C --> D["SessionAuthorizationMiddleware"]
    D --> E["Handler"]
```

## Notes

- `SessionResume` proof validation (`HMAC-Keccak256`) is implemented in `SessionHandlers`; keep this middleware focused on policy and token-state checks.
- `INetworkBufferMiddleware` has no `next` delegate in its contract; the pipeline owns lease progression.
- Return `null` from buffer middleware to drop a frame early.

## Related pages

- [Middleware Pipeline](../api/runtime/middleware/pipeline.md)
- [Network Buffer Pipeline](../api/runtime/middleware/network-buffer-pipeline.md)
- [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)
