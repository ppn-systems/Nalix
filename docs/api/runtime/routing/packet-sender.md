# Packet Sender

`PacketSender` is the default runtime sender implementation used by `PacketContext<TPacket>`.

## Audit Summary

- Existing page correctly described transform ordering but mixed some behavior details with assumptions.
- Needed explicit mapping to actual branching logic in `SendAsync` implementation.

## Missing Content Identified

- Clear explanation of default encryption decision (`context.Attributes.Encryption?.IsEncrypted`).
- Explicit runtime boundaries: sender is initialized by runtime and used through `IPacketSender<TPacket>`.

## Improvement Rationale

This prevents confusion between per-handler metadata policy and per-call override behavior.

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
