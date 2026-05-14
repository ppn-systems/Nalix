# Packet Sender

`PacketSender` is the default runtime sender implementation bound to the pooled `PacketContext<TPacket>` used during dispatch and exposed to application code through `IPacketContext<TPacket>.Sender`.

## Overview

`PacketSender` is the default runtime sender implementation bound to the pooled `PacketContext<TPacket>` used during dispatch and exposed to application code through `IPacketContext<TPacket>.Sender`. It provides a high-performance, asynchronous API for sending packets back to the client while automatically respecting the security and transport policies defined via attributes on your handlers.

## Source Mapping

- `src/Nalix.Abstractions/Networking/Packets/IPacketSender.cs`
- `src/Nalix.Runtime/Dispatching/PacketSender.cs`

## Why This Type Exists

Handlers need a safe send API that respects runtime metadata while keeping serialization/transform logic centralized and reusable.

## Send Behavior

`PacketSender` serializes the packet into a pooled `BufferLease`, then delegates to `FramePipeline.ProcessOutbound()` for compression/encryption transforms, and finally sends via the selected transport.

### Decision inputs

- Compression: `CompressionOptions.Enabled` and `MinSizeToCompress` threshold.
- Encryption default: `context.Attributes.Encryption?.IsEncrypted ?? false`.
- Encryption override: `SendAsync(packet, forceEncrypt, ...)`.
- Transport selection: `attributes.Transport?.TransportType ?? NetworkTransport.TCP` — selects TCP or UDP per handler metadata.

### Flow

1. Serialize packet into `BufferLease.Rent(packetLength)`.
2. `FramePipeline.ProcessOutbound()` applies compression then encryption as needed.
3. `GetTransport()` resolves TCP or UDP from handler attributes.
4. Transport sends the final buffer.

## Core API

- `SendAsync(IPacket packet, CancellationToken ct = default)`
- `SendAsync(IPacket packet, bool forceEncrypt, CancellationToken ct = default)`

## Practical Example

```csharp
await context.Sender.SendAsync(replyPacket, ct);
await context.Sender.SendAsync(replyPacket, forceEncrypt: true, ct);
```

## Best Practices

- Use default `SendAsync(packet)` for normal metadata-driven behavior.
- Use `forceEncrypt: true` only when you intentionally override handler metadata policy.
- Do not use `PacketSender` without runtime initialization (`PacketContext` initializes it).

## Related APIs

- [Packet Context](./packet-context.md)
- [Compression Options](../../options/network/compression-options.md)
- [Packet Dispatch](./packet-dispatch.md)
