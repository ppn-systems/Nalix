# UdpSession

`UdpSession` is a high-performance, zero-allocation UDP client transport in `Nalix.SDK`. It extends `TransportSession` to provide datagram-oriented communication synchronized with the server's protocol layer.

## Source mapping

- `src/Nalix.SDK/Transport/UdpSession.cs`
- `src/Nalix.Framework/DataFrames/FrameTransformer.cs`

## Key Features

- **High Performance**: Built with `BufferLease` and `stackalloc` to minimize GC pressure and memory allocations.
- **Session Identification**: Uses a 7-byte `SessionToken` (Snowflake) prepended to every outbound datagram for O(1) connection mapping on the server.
- **Integrated Transformation**: Supports optional LZ4 compression and AEAD encryption (ChaCha20-Poly1305) via the internal `FrameTransformer` pipeline.
- **MTU Aware**: Enforces a configurable `MaxUdpDatagramSize` (default 1400 bytes) to prevent fragmentation at the network layer.

## Basic Usage

```csharp
TransportOptions options = ConfigurationManager.Instance.Get<TransportOptions>();
UdpSession client = new(options, catalog);

// Essential: Set the token received during TCP login/handshake
client.SessionToken = mySnowflakeToken;

client.OnMessageReceived += (_, lease) =>
{
    using (lease)
    {
        // Handle decrypted/decompressed payload.
    }
};

await client.ConnectAsync(options.Address, options.Port);
await client.SendAsync(myPacket);
```

## Properties

| Property | Description |
| --- | --- |
| `SessionToken` | The 7-byte identifier used to authenticate datagrams. |
| `Options` | Access to transport configuration (MTU, Encryption, etc.). |

## Related APIs

- [SDK Overview](./index.md)
- [Transport Session](./transport-session.md)
- [TCP Session](./tcp-session.md)
- [Transport Options](./options/transport-options.md)
