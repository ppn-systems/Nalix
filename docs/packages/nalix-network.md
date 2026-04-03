# Nalix.Network

`Nalix.Network` is the runtime package for listeners, connections, protocol flow, packet dispatch, and connection guarding.

If you are building the server side of a Nalix-based system, this is the package you will use most.

!!! note "This package is the server runtime core"
    Most production server concerns in Nalix live here: listeners, dispatch, connection state, throttling, and transport-facing diagnostics.

## Runtime map

```mermaid
flowchart LR
    A["TcpListenerBase / UdpListenerBase"] --> B["Protocol"]
    B --> C["PacketDispatchChannel"]
    C --> D["Packet middleware"]
    D --> E["Handlers"]
    E --> F["Connection / ConnectionHub"]
```

## What it gives you

### Listener runtime

Core entry points:

- `TcpListenerBase`
- `UdpListenerBase`
- `Protocol`

These types let you:

- accept TCP or UDP traffic
- keep connection/session state
- bridge traffic into dispatch

### Dispatch and handlers

Core entry points:

- `PacketDispatchChannel`
- `PacketContext<TPacket>`
- packet attributes
- handler controllers with `[PacketController]` and `[PacketOpcode]`

This is where most application logic plugs in.

### Connection management

Core entry points:

- `Connection`
- `ConnectionHub`
- `ConnectionHubOptions`
- `ConnectionGuard`

Use these when you need:

- live connection tracking
- user/session lookup
- forced disconnects
- per-endpoint admission control

### Middleware and safeguards

Core entry points:

- packet middleware
- buffer middleware
- `ConnectionGuard`

The packet-level middleware, throttling, and time-sync helpers live in `Nalix.Network.Pipeline`.

Use these to keep the server stable under real traffic.

### Runtime tuning

Core option types:

- `NetworkSocketOptions`
- `DispatchOptions`
- `ConnectionLimitOptions`
- `ConnectionHubOptions`
- `TimingWheelOptions`
- `PoolingOptions`
- `NetworkCallbackOptions`

## Suggested reading order

If you are new to the server runtime, read in this order:

1. [TCP Listener](../api/network/runtime/tcp-listener.md)
2. [Protocol](../api/network/runtime/protocol.md)
3. [Packet Dispatch](../api/routing/packet-dispatch.md)
4. [Packet Context](../api/routing/packet-context.md)
5. [Connection](../api/network/connection/connection.md)
6. [Connection Hub](../api/network/connection/connection-hub.md)
7. [Network Options](../api/network/options/options.md)

Then continue with the guides:

- [End-to-End Sample](../guides/end-to-end.md)
- [Custom Middleware](../guides/custom-middleware-end-to-end.md)
- [Custom Metadata Provider](../guides/custom-metadata-provider.md)
- [TCP Request/Response](../guides/tcp-request-response.md)
- [UDP Auth Flow](../guides/udp-auth-flow.md)

## Minimal server shape

### Quick example

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithHandler(() => new SamplePingHandlers());
});

[PacketController("SamplePingHandlers")]
public sealed class SamplePingHandlers
{
    [PacketOpcode(0x1001)]
    public ValueTask<Control> Handle(Control request, IConnection connection)
        => ValueTask.FromResult(new Control { Type = ControlType.PONG });
}

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

## Where metadata fits

Packet metadata is a real part of the package design, not just decoration.

It drives:

- permissions
- timeout behavior
- rate limiting
- concurrency limits
- custom conventions through metadata providers

## Related pages

- [Architecture](../concepts/architecture.md)
- [Real-time Engine](../concepts/real-time.md)

## Key API pages

- [TCP Listener](../api/network/runtime/tcp-listener.md)
- [Protocol](../api/network/runtime/protocol.md)
- [Packet Dispatch](../api/routing/packet-dispatch.md)
- [Connection Hub](../api/network/connection/connection-hub.md)
- [Network Options](../api/network/options/options.md)
