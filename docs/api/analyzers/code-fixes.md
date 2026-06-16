# Nalix.Analyzers Code Fixes

`Nalix.Analyzers.CodeFixes` provides the Roslyn fixes that complement the diagnostics in `Nalix.Analyzers`.

Use this page when you want a quick sense of what the analyzer can usually fix for you automatically.

## Source mapping

- `analyzers/Nalix.Analyzers.CodeFixes/ConfigurationIgnoreCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/DispatchLoopCountCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/DuplicateSerializeOrderCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/GenericPacketHandlerCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/MiddlewareCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/NullMiddlewareCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/PacketHandlerCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/PacketDeserializeCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/PacketOpcodeCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/PacketRegistryDeserializerCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/PacketSelfTypeCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/RedundantPacketCastCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/ResetForPoolCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/RequestOptionsConsistencyCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/SerializationConflictCodeFixProvider.cs`
- `analyzers/Nalix.Analyzers.CodeFixes/SerializeOrderMissingCodeFixProvider.cs`

## Main pieces

- `ConfigurationIgnoreCodeFixProvider`
- `DispatchLoopCountCodeFixProvider`
- `DuplicateSerializeOrderCodeFixProvider`
- `GenericPacketHandlerCodeFixProvider`
- `MiddlewareCodeFixProvider`
- `NullMiddlewareCodeFixProvider`
- `PacketHandlerCodeFixProvider`
- `PacketDeserializeCodeFixProvider`
- `PacketOpcodeCodeFixProvider`
- `PacketRegistryDeserializerCodeFixProvider`
- `PacketSelfTypeCodeFixProvider`
- `RedundantPacketCastCodeFixProvider`
- `ResetForPoolCodeFixProvider`
- `RequestOptionsConsistencyCodeFixProvider`
- `SerializationConflictCodeFixProvider`
- `SerializeOrderMissingCodeFixProvider`

## What the code fixes do

The available fixes currently cover these common workflows:

- add missing packet or controller attributes
- fix packet self-type inheritance or deserializer shape
- clamp invalid dispatch loop counts into supported range
- add missing `SerializeOrder`
- resolve `SerializeIgnore` versus `SerializeOrder` conflicts
- add `ConfiguredIgnore` for unsupported configuration properties
- add middleware attributes or ordering helpers
- remove redundant `PacketContext<T>.Packet` casts
- resolve null middleware registrations
- add a missing registry deserializer pattern
- add a `base.ResetForPool()` call in packet reset code
- normalize inconsistent request retry/timeout option combinations
- remove `PacketOpcode` attributes from generic handlers

## Common fix groups

### Packet and controller fixes

- `PacketHandlerCodeFixProvider`
- `PacketOpcodeCodeFixProvider`
- `PacketDeserializeCodeFixProvider`
- `PacketRegistryDeserializerCodeFixProvider`
- `PacketSelfTypeCodeFixProvider`

### Serialization fixes

- `SerializeOrderMissingCodeFixProvider`
- `DuplicateSerializeOrderCodeFixProvider`
- `SerializationConflictCodeFixProvider`

### Middleware and configuration fixes

- `MiddlewareCodeFixProvider`
- `NullMiddlewareCodeFixProvider`
- `DispatchLoopCountCodeFixProvider`
- `ConfigurationIgnoreCodeFixProvider`
- `RequestOptionsConsistencyCodeFixProvider`

### Lifecycle fixes

- `ResetForPoolCodeFixProvider`
- `RedundantPacketCastCodeFixProvider`
- `GenericPacketHandlerCodeFixProvider`

## Practical notes

- Code fixes are intentionally narrow. They usually make the smallest safe correction, then let you refine the code afterward.
- Not every diagnostic has a code fix. Some are better left as human decisions, especially when the fix would be ambiguous.
- The code fixes are most helpful when you are already close to the intended Nalix shape.

## Common pitfalls

- assuming every analyzer warning has an automatic fix
- applying a mechanical fix without checking whether the surrounding type design is correct
- using a code fix to patch over a larger architecture mismatch

## Related APIs

- [Analyzers Overview](./index.md)
- [Serialization Attributes](../abstractions/serialization-attributes.md)
- [Packet Registry](../codec/packets/packet-registry.md)
- [Middleware Pipeline](../../concepts/runtime/middleware-pipeline.md)
