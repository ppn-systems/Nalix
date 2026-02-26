# Packet Sender

`PacketSender<TPacket>` is the default runtime implementation of `IPacketSender<TPacket>`.

It is the explicit outbound path used by `PacketContext<TPacket>.Sender` when a handler wants to send one or more replies under the current connection and metadata rules.

## Source mapping

- `src/Nalix.Runtime/Dispatching/PacketSender.cs`

## Main type

- `PacketSender<TPacket>`

## What it does

`PacketSender<TPacket>` reads the current `PacketContext<TPacket>` and decides how outbound bytes should be emitted.

It handles:

- packet serialization
- packet leasing through the pooled context sender pipeline
- compression decisions through `CompressionOptions`
- encryption decisions through the current connection secret and cipher suite
- flag updates for compressed and encrypted frames
- sending through `context.Connection.TCP`

## Initialization model

This type is pooled and must be initialized before use.

`PacketContext<TPacket>.Initialize(...)` rents and configures a sender for the current request, and reset/return logic clears that state after the handler completes.
The surrounding packet flow also uses pooled packet and lease helpers from `Nalix.Framework.DataFrames.Pooling`.

## Send behavior

There are two public paths:

- `SendAsync(packet, ct)` uses `context.Attributes.Encryption?.IsEncrypted`
- `SendAsync(packet, forceEncrypt, ct)` overrides that decision

Both paths now use exception-based failure reporting from the transport layer.

## Runtime cases

The sender splits into four main branches:

| Case | Behavior |
|---|---|
| plain | serialize and send directly |
| compress only | compress, set `PacketFlags.COMPRESSED`, send |
| encrypt only | encrypt, set `PacketFlags.ENCRYPTED`, send |
| compress + encrypt | compress first, then encrypt, then send |

Compression activates only when `CompressionOptions.Enabled` is true and the serialized payload meets `MinSizeToCompress`.

Encryption uses the connection's current:

- `Secret`
- `Algorithm`

If compression or encryption cannot complete, the sender now throws instead of returning a failure flag.

## Basic usage

```csharp
public async ValueTask Handle(IPacketContext<Handshake> context, CancellationToken ct)
{
    Handshake reply = new(
        0x1002,
        HandshakeStage.SERVER_HELLO,
        serverPublicKey,
        serverNonce,
        serverProof);
    await context.Sender.SendAsync(reply, ct);
}
```

Forced encryption:

```csharp
await context.Sender.SendAsync(
    new Handshake(0x1003, HandshakeStage.SERVER_FINISH, [], [], []),
    forceEncrypt: true,
    ct);
```

## Ownership and pooling

- `PacketSender<TPacket>` implements `IPoolable`
- `Initialize(...)` stores the active context
- `ResetForPool()` clears the context reference
- using the sender before initialization throws
- the sender itself participates in the same pooling model as `PacketLease<TPacket>` and `PacketPool<TPacket>`

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Dispatch](./packet-dispatch.md)
- [Handler Return Types](./handler-results.md)
- [Compression Options](../../network/options/compression-options.md)
- [Connection](../../network/connection/connection.md)
