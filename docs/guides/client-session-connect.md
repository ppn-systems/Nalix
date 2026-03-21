# Create and Connect a Client Session (Nalix.SDK)

This guide shows the canonical flow for creating and connecting a client session with `Nalix.SDK`.

You will:

- build/load a packet registry
- configure `TransportOptions`
- create `TcpSession` or `UdpSession`
- connect with `ConnectAsync(...)`
- hook lifecycle events

## 1. Create shared packet catalog

Client and server should use the same packet contracts/registry shape.

```csharp
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

IPacketRegistry catalog = new PacketRegistryFactory().CreateCatalog();
```

## 2. Configure transport options

```csharp
using Nalix.SDK.Options;

TransportOptions options = new()
{
    Address = "127.0.0.1",
    Port = 57206,
    ConnectTimeoutMillis = 5000
};
```

## 3. Create and connect a TCP session

```csharp
using Nalix.SDK.Transport;

using TcpSession session = new(options, catalog);

session.OnConnected += (_, _) => Console.WriteLine("TCP connected");
session.OnDisconnected += (_, ex) => Console.WriteLine($"TCP disconnected: {ex.Message}");
session.OnError += (_, ex) => Console.WriteLine($"TCP error: {ex.Message}");

await session.ConnectAsync();
```

After connecting, you can:

- send packets with `SendAsync(IPacket, ...)`
- send raw payload with `SendAsync(ReadOnlyMemory<byte>, ...)`
- receive via `OnMessageReceived` / `OnMessageAsync`
- call handshake/resume extensions if your server requires secure bootstrap

## 4. Create and connect a UDP session

`UdpSession` follows the same base contract but requires a session token before sending user traffic.

```csharp
using Nalix.Framework.Identifiers;
using Nalix.SDK.Transport;

using UdpSession udp = new(options, catalog);

udp.OnConnected += (_, _) => Console.WriteLine("UDP connected");
udp.OnDisconnected += (_, ex) => Console.WriteLine($"UDP disconnected: {ex.Message}");
udp.OnError += (_, ex) => Console.WriteLine($"UDP error: {ex.Message}");

await udp.ConnectAsync();

// Usually assigned by handshake/resume flow.
udp.SessionToken = Snowflake.NewId(Nalix.Common.Identity.SnowflakeType.Session);
```

## 5. Disconnect and dispose

```csharp
await session.DisconnectAsync();
session.Dispose();
```

`using` is recommended so sockets and background loops are always cleaned up.

## Common pitfalls

- Creating session without a valid `IPacketRegistry`.
- Using `Address`/`Port` mismatch with server listener.
- Sending UDP packets before `SessionToken` is set.
- Ignoring `OnError` and `OnDisconnected` in production clients.

## Related pages

- [SDK Overview](../api/sdk/index.md)
- [Transport Session](../api/sdk/transport-session.md)
- [TCP Session](../api/sdk/tcp-session.md)
- [UDP Session](../api/sdk/udp-session.md)
- [Transport Options](../api/sdk/options/transport-options.md)
- [Handshake Extensions](../api/sdk/handshake-extensions.md)
- [Resume Extensions](../api/sdk/resume-extensions.md)
