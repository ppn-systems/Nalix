# UDP Session

`UdpSession` is a high-performance, datagram-oriented client transport in `Nalix.SDK`. It is designed for low-latency scenarios where packet loss is acceptable but speed is critical. It uses a 7-byte session token mechanism to allow the server to multiplex thousands of concurrent UDP streams.

## Datagram Architecture

```mermaid
graph TD
    A[Nalix IPacket] --> B[Serialization]
    B --> C[Transform Pipe: LZ4 + ChaCha20]
    C --> D[Frame Envelope]
    D --> E[Session Token: 7 Bytes]
    E --> F[Network Payload]
    F -->|Outbound| G[UDP Socket Send]
```

## Source mapping

- `src/Nalix.SDK/Transport/UdpSession.cs`
- `src/Nalix.SDK/Transport/Internal/PacketFrameTransforms.cs`
- `src/Nalix.Framework/DataFrames/Transforms/PacketCipher.cs`
- `src/Nalix.Framework/DataFrames/Transforms/PacketCompression.cs`

## Role and Design

Unlike TCP, `UdpSession` is connectionless at the socket level but "session-aware" at the framework level. Every outbound datagram is prepended with a 7-byte `Snowflake` identifier, which the server uses to map the packet to a trusted session.

- **Zero-Allocation Receive**: Uses pooled `BufferLease` memory and direct `ReceiveAsync` to eliminate per-datagram allocations.
- **MTU Enforcement**: Automatically prevents sending datagrams larger than `MaxUdpDatagramSize` (default: 1400 bytes) to avoid IP fragmentation.
- **AEAD Integrated**: Automatically applies encryption if configured, utilizing the shared `PacketFrameTransforms` pipeline.

## Public API

### Events
| Member | Description |
|---|---|
| `OnConnected` | Raised when the UDP socket is initialized and bound to the remote endpoint. |
| `OnDisconnected` | Raised when the session is closed. |
| `OnMessageReceived` | Surfaces decrypted and decompressed payload for each inbound datagram. |
| `OnError` | Reports socket or transformation faults. |

### Properties
| Member | Description |
|---|---|
| `SessionToken` | The 7-byte identifier used to authenticate outbound datagrams. |
| `IsConnected` | True if the socket is open and bound. |
| `Options` | Access to transport options like `MaxUdpDatagramSize` and `Secret`. |

### Methods
| Member | Description |
|---|---|
| `ConnectAsync(...)` | Initializes the socket and binds to the server address. |
| `DisconnectAsync()` | Shuts down the socket and stops the receive loop. |
| `SendAsync(IPacket)` | Serializes, transforms (encrypts/compresses), and sends the packet. |

## Basic usage

```csharp
var client = new UdpSession(options, catalog);

// Essential: must match the session identifier assigned during TCP login
client.SessionToken = mySessionSnowflake;

client.OnMessageReceived += (s, lease) => 
{
    using (lease)
    {
        // Handle low-latency update
    }
};

await client.ConnectAsync();
await client.SendAsync(new PlayerInputPacket { Velocity = 1.0f });
```

## Related APIs

- [SDK Overview](./index.md)
- [TCP Session](./tcp-session.md)
- [Transport Session](./transport-session.md)
- [Transport Options](./options/transport-options.md)
