# Nalix.Analyzers

`Nalix.Analyzers` provides Roslyn diagnostics that help catch invalid packet, serialization, middleware, configuration, and SDK usage at compile time.

Use this package when you want feedback before runtime instead of discovering a mistake in a handler, packet model, or startup path later.

## Source mapping

- `src/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs`
- `src/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs`

## Main pieces

- `NalixUsageAnalyzer`
- `DiagnosticDescriptors`

## What it checks

The current analyzer surface covers:

- packet controller usage and handler signatures
- packet registry and packet deserializer contracts
- `PacketBase<TSelf>` shape and `ResetForPool()` usage
- serialization layout issues such as `SerializeOrder`, `SerializeIgnore`, and `SerializeDynamicSize`
- middleware registration and middleware signature issues
- configuration binding issues
- SDK request option issues

## Diagnostic groups

### Packet controllers and handlers

- `NALIX001` duplicate `PacketOpcode` in a controller
- `NALIX002` missing `PacketOpcode`
- `NALIX003` invalid handler signature
- `NALIX004` `PacketContext<T>` packet type mismatch
- `NALIX005` handler packet type mismatch
- `NALIX008` missing `PacketController`

### Packet registry and packet base

- `NALIX009` packet missing `Deserialize`
- `NALIX010` wrong `PacketBase<TSelf>` self type
- `NALIX011` wrong `IPacketDeserializer<T>` self type
- `NALIX012` missing static `Deserialize(ReadOnlySpan<byte>)`
- `NALIX017` invalid `Deserialize` signature
- `NALIX018` packet type is not concrete

### Serialization layout

- `NALIX013` explicit serialization member missing `SerializeOrder`
- `NALIX014` duplicate `SerializeOrder`
- `NALIX015` `SerializeIgnore` conflicts with `SerializeOrder`
- `NALIX016` `SerializeDynamicSize` on a fixed-size member
- `NALIX021` negative `SerializeOrder`
- `NALIX022` serialized member overlaps the packet header region

### Middleware and routing

- `NALIX006` middleware type mismatch
- `NALIX007` buffer middleware ignores `MiddlewareStage`
- `NALIX025` metadata provider clears opcode
- `NALIX026` metadata provider overwrites opcode without guard
- `NALIX030` packet middleware missing `MiddlewareOrder`
- `NALIX031` buffer middleware missing `MiddlewareOrder`
- `NALIX032` inbound middleware ignores `AlwaysExecute`
- `NALIX033` duplicate middleware order in the same chain

### Configuration and SDK

- `NALIX023` unsupported configuration property type
- `NALIX024` configuration property is not bindable
- `NALIX027` negative `RequestOptions.RetryCount`
- `NALIX028` negative `RequestOptions.TimeoutMs`
- `NALIX029` encrypted `RequestAsync` requires `TcpSession`

## Practical notes

- The analyzer is opinionated on purpose. It prefers catching common misuse early over being silent.
- Some diagnostics are warnings, while others are informational nudges to make code more explicit.
- The diagnostics are designed to match the current source conventions in `Nalix.Common`, `Nalix.Framework`, `Nalix.Network`, and `Nalix.SDK`.

## Common pitfalls

- assuming a packet type is valid just because it compiles
- forgetting `PacketOpcode` on controller methods
- mixing explicit serialization layout with missing or duplicate order values
- registering middleware with the wrong packet type
- using `RequestAsync(..., Encrypt = true)` on a client type that is not `TcpSession`

## Related APIs

- [Serialization Attributes](../common/serialization-attributes.md)
- [Serialization Basics](../framework/serialization/serialization-basics.md)
- [Packet Registry](../framework/packets/packet-registry.md)
- [Middleware](../../concepts/middleware.md)
