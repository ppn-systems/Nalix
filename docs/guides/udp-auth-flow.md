# UDP Auth Flow Example

This guide explains the actual UDP session shape used by `UdpListenerBase` today, in a client-friendly way.

Use it when you already know you need UDP and want to understand the trust and replay rules before implementing a client.

## What the runtime expects

When `UdpListenerBase` receives a datagram, it expects the first 7 bytes to be the session token:

- session token (`Snowflake`, 7 bytes)
- payload

In source, the listener validates:

- the datagram is at least 7 bytes long
- the packet explicitly identifies itself as UDP
- the token resolves to a connection in `ConnectionHub`
- your overridden `IsAuthenticated(...)` also returns `true`

## High-level flow

```mermaid
flowchart LR
    A["UDP datagram received"] --> B["Read 7-byte session token prefix"]
    B --> C["Find connection in ConnectionHub"]
    C --> D["Check UDP marker in packet header"]
    D --> E["Call IsAuthenticated(...)"]
    E --> F["Inject payload into connection"]
```

## Server shape

### 1. Subclass `UdpListenerBase`

```csharp
public sealed class SampleUdpListener : UdpListenerBase
{
    public SampleUdpListener(IProtocol protocol) : base(protocol) { }

    protected override bool IsAuthenticated(IConnection connection, in UdpReceiveResult result)
    {
        // Add your own checks here:
        // - allowed endpoint
        // - session state
        // - region / shard ownership
        return connection.Secret is not null && connection.Secret.Length > 0;
    }
}
```

### 2. Keep TCP and UDP tied to the same session

The UDP path assumes there is already a known connection in `ConnectionHub`.

That means a common pattern is:

1. authenticate or handshake on TCP first
2. create/populate the connection secret
3. store the session in `ConnectionHub`
4. allow UDP packets to reference that session ID

## Conceptual client packet layout

The listener currently validates these parts:

```text
[session-token][payload]
```

The token is the `Snowflake` session ID assigned to the connection after TCP login.

## Pseudocode for client-side send

```csharp
byte[] payload = BuildGamePayload();
byte[] sessionToken = connectionId.ToBytes();

byte[] datagram = Concat(sessionToken, payload);
await udp.SendAsync(datagram, serverEndPoint);
```

## Transport rules

The runtime does not add timestamp, nonce, or Poly1305 metadata to UDP datagrams.
Instead, it uses the session token plus the connection/auth state already established through TCP.

For clients, that means:

- send the 7-byte session token first
- keep the payload small enough to fit `MaxUdpDatagramSize`
- treat UDP as a fast datagram path, not a second authentication protocol

## What happens on failure

The listener records diagnostics for:

- short packets
- unknown sessions
- unauthenticated packets
- receive errors

Invalid packets are dropped before they reach your protocol logic.

## Recommended rollout

For a simple deployment:

1. establish session over TCP
2. assign/store session secret
3. return session ID to the client
4. let the client send UDP datagrams prefixed with the session token
5. verify in `IsAuthenticated(...)` that the session is ready for UDP

## Related pages

- [UDP Listener](../api/network/udp-listener.md)
- [UDP Session](../api/sdk/udp-session.md)
- [Connection Hub](../api/network/connection/connection-hub.md)
- [Network Options](../api/network/options/options.md)
