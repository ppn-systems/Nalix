# Packet Metadata

`PacketMetadata` is an immutable readonly struct that captures the packet attributes driving dispatch, validation, and transport behavior. It is resolved at compile time by the `PacketHandlerGenerator` source generator and passed into `PacketContext.Attributes` at runtime.

## Source Mapping

- `src/Nalix.Abstractions/Networking/Packets/PacketMetadata.cs`
- `analyzers/Nalix.Analyzers.Generators/PacketHandlerGenerator.cs`

## Why Metadata Exists

Handler behavior (timeout, permission, encryption, rate/concurrency limits) must be resolved once at compile time, then consumed cheaply on hot dispatch paths without reflection or allocation.

## Build Flow

```mermaid
flowchart LR
    A["Handler Method + Attributes"] --> B["PacketHandlerGenerator (compile-time)"]
    B --> C["new PacketMetadata(...)"]
    C --> D["PacketContext.Attributes"]
```

## Struct Shape

`PacketMetadata` is constructed with the following parameters:

| Parameter | Type | Description |
| --- | --- | --- |
| `opCode` | `PacketOpcodeAttribute` | The opcode that identifies the handler. |
| `timeout` | `PacketTimeoutAttribute?` | Optional handler execution timeout. |
| `permission` | `PacketPermissionAttribute?` | Required permission level. |
| `encryption` | `PacketEncryptionAttribute?` | Encryption requirement. |
| `rateLimit` | `PacketRateLimitAttribute?` | Per-connection rate limit. |
| `transport` | `PacketTransportAttribute?` | Preferred transport (TCP/UDP). |
| `customAttributes` | `IReadOnlyDictionary<Type, Attribute>?` | Additional custom attributes. |

## Custom Attribute Access

`PacketMetadata` stores custom attributes in a read-only dictionary. Middleware and handlers can retrieve them via:

```csharp
TAttribute? GetCustomAttribute<TAttribute>() where TAttribute : Attribute
```

This allows application-defined attributes to flow through the dispatch pipeline without modifying the framework.

## Related APIs

- [Packet Attributes](./packet-attributes.md)
- [Packet Context](../runtime/routing/packet-context.md)
- [Packet Dispatch](../runtime/routing/packet-dispatch.md)

