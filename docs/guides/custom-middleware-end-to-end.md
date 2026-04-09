# Custom Middleware End-to-End

This guide shows a practical end-to-end flow for adding your own middleware to Nalix.Network.
The same shape works for built-in packets and custom packet types.

The goal is simple:

- accept a packet
- inspect shared metadata or connection state
- block bad requests early
- let the handler run normally

## Pick the right middleware type

Nalix.Network has two middleware layers:

- **buffer middleware**
  - runs before packet deserialization
  - works on raw `IBufferLease`
  - good for decrypt/decompress/low-level validation
- **packet middleware**
  - runs after deserialization
  - works on `PacketContext<TPacket>`
  - good for permissions, throttling, auditing, and request policies

For most application-level customization, start with **packet middleware**.

## Example scenario

We will build a packet middleware that:

- checks whether a client is authenticated enough
- logs the opcode
- rejects requests that do not meet the minimum permission level

## Step 1. Create the middleware

```csharp
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

[MiddlewareOrder(-20)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class SampleAuditMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket
{
    public async Task InvokeAsync(
        PacketContext<TPacket> context,
        Func<CancellationToken, Task> next)
    {
        ushort opcode = context.Attributes.PacketOpcode.OpCode;

        Console.WriteLine($"packet opcode=0x{opcode:X4} from={context.Connection.NetworkEndpoint}");

        if (context.Attributes.Permission is not null &&
            context.Connection.Level < context.Attributes.Permission.Level)
        {
            await context.Connection.SendAsync(
                ControlType.FAIL,
                ProtocolReason.PERMISSION_DENIED,
                ProtocolAdvice.RETRY_LATER);
            return;
        }

        await next(context.CancellationToken);
    }
}
```

## Why this works

- `[MiddlewareOrder(-20)]` moves the middleware early in the inbound chain
- `[MiddlewareStage(MiddlewareStage.Inbound)]` means it runs before the handler
- `context.Attributes` gives access to the resolved packet metadata
- returning without calling `next(...)` short-circuits the request

## Step 2. Add packet attributes to a handler

```csharp
[PacketController("SampleChatHandlers")]
public sealed class SampleChatHandlers
{
    [PacketOpcode(0x1001)]
    [PacketPermission(PermissionLevel.USER)]
    public ValueTask<Control> Send(PacketContext<Control> request)
    {
        request.Packet.Type = ControlType.PONG;
        return ValueTask.FromResult(request.Packet);
    }
}
```

Now the middleware can read `PacketPermission` from `context.Attributes.Permission`.

## Step 3. Register middleware in dispatch options

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithMiddleware(new SampleAuditMiddleware<IPacket>())
           .WithHandler(() => new SampleChatHandlers());
});

dispatch.Activate();
```

## Step 4. Wire protocol and listener

```csharp
public sealed class SampleProtocol : Protocol
{
    private readonly PacketDispatchChannel _dispatch;

    public SampleProtocol(PacketDispatchChannel dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}

public sealed class SampleTcpListener : TcpListenerBase
{
    public SampleTcpListener(ushort port, IProtocol protocol) : base(port, protocol) { }
}
```

## Full flow

```mermaid
flowchart LR
    A["Socket accepted"] --> B["Protocol.OnAccept"]
    B --> C["connection.TCP.BeginReceive"]
    C --> D["PacketDispatchChannel.HandlePacket"]
    D --> E["Buffer middleware"]
    E --> F["Deserialize packet"]
    F --> G["SampleAuditMiddleware"]
    G --> H["Handler"]
    H --> I["Return handler / response"]
```

The same middleware shape applies to custom packet types as long as the handler generic parameter stays aligned with the dispatch pipeline.
In the listener bridge, `ProcessFrame(...)` still feeds the protocol before `ProcessMessage(...)` runs.

## Common patterns

### Validation middleware

Use middleware to:

- reject invalid state
- stop expensive handlers from running
- send a control response and exit early

### Audit middleware

Log:

- opcode
- endpoint
- username
- elapsed time

### Metadata-driven middleware

Read from:

- `context.Attributes.Permission`
- `context.Attributes.Timeout`
- `context.Attributes.RateLimit`
- custom metadata added by providers

## If you need raw-frame work instead

Use `INetworkBufferMiddleware` when you need to act on raw bytes before deserialization:

```csharp
public sealed class FrameGuard : INetworkBufferMiddleware
{
    public Task<IBufferLease> InvokeAsync(
        IBufferLease buffer,
        IConnection connection,
        CancellationToken ct,
        Func<IBufferLease, CancellationToken, Task<IBufferLease>> next)
    {
        if (buffer.Length < 8)
            return Task.FromResult<IBufferLease>(null);

        return next(buffer, ct);
    }
}
```

## Checklist

- choose buffer vs packet middleware first
- keep middleware small and predictable
- short-circuit early for invalid requests
- read metadata from `PacketContext.Attributes`
- register middleware before activating dispatch

## Related pages

- [Middleware Pipeline](../api/runtime/middleware/pipeline.md)
- [Packet Metadata](../api/runtime/routing/packet-metadata.md)
- [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)
