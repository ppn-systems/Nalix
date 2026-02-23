# NALIX Diagnostic Codes

This page provides a detailed list of diagnostic codes emitted by `Nalix.Analyzers`. These rules help ensure that your application uses the framework correctly and efficiently.

## Usage Codes (NALIX001 - NALIX012, NALIX017 - NALIX019)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX001` | Packet controller contains duplicate PacketOpcode | Warning | Usage | Nalix controller dispatch relies on unique opcodes per controller. |
| `NALIX002` | Packet controller handler should declare PacketOpcode | Warning | Usage | Nalix packet controller methods are only discovered when PacketOpcode is present. |
| `NALIX003` | Packet controller handler has unsupported signature | Warning | Usage | Nalix packet handlers must follow one of the signatures supported by PacketHandlerCompiler. |
| `NALIX004` | PacketContext<T> does not match dispatch packet type | Warning | Usage | Nalix dispatch expects PacketContext<T> to use the same TPacket as PacketDispatchOptions<TPacket>. |
| `NALIX005` | Registered controller handler packet type mismatch | Warning | Usage | Nalix dispatch registration should use controllers whose handler packet types are compatible with PacketDispatchOptions<TPacket>. |
| `NALIX008` | Registered handler controller is missing PacketController | Warning | Usage | Nalix requires PacketControllerAttribute on controller types registered through PacketDispatchOptions.WithHandler. |
| `NALIX009` | Registered packet type is missing static Deserialize | Warning | Usage | Nalix packet registry only binds packet types that expose the required static Deserialize(ReadOnlySpan<byte>) member. |
| `NALIX010` | PacketBase<TSelf> should use the containing packet type | Warning | Usage | Nalix packet types should use themselves as the TSelf argument when inheriting from PacketBase<TSelf>. |
| `NALIX011` | IPacketDeserializer<T> should use the containing packet type | Warning | Usage | Nalix packet deserializer contracts should target the containing packet type for consistent registry binding. |
| `NALIX012` | PacketBase packet should expose static Deserialize | Warning | Usage | Nalix packet types built on PacketBase<TSelf> should expose a static Deserialize(ReadOnlySpan<byte>) helper for registry scanning and discoverability. |
| `NALIX017` | Packet Deserialize signature is invalid | Warning | Usage | Nalix packet types should expose a public static Deserialize(ReadOnlySpan<byte>) helper with the packet type as return value. |
| `NALIX018` | Registered packet type must be concrete | Warning | Usage | Nalix packet registry should register only concrete, non-abstract, non-generic packet types. |
| `NALIX019` | Registered buffer middleware does not implement INetworkBufferMiddleware | Warning | Usage | Nalix buffer middleware registration should pass a type implementing INetworkBufferMiddleware. |

## Serialization Codes (NALIX013 - NALIX016, NALIX021 - NALIX022)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX013` | Explicit serialization member should declare SerializeOrder | Warning | Serialization | Explicit serialization layouts should assign a stable SerializeOrder to every serialized member. |
| `NALIX014` | SerializeOrder value is duplicated | Warning | Serialization | SerializeOrder values should be unique within a serializable type. |
| `NALIX015` | SerializeIgnore conflicts with SerializeOrder | Warning | Serialization | A member cannot be both ignored and explicitly ordered for serialization. |
| `NALIX016` | SerializeDynamicSize is unnecessary on fixed-size member | Info | Serialization | SerializeDynamicSize should be used only for variable-size serialization members such as strings, arrays, or variable-size collections. |
| `NALIX021` | SerializeOrder should not be negative | Warning | Serialization | Nalix explicit serialization layout should not use negative SerializeOrder values. |
| `NALIX022` | Packet member SerializeOrder overlaps packet header region | Warning | Serialization | Packet payload members on PacketBase-derived types should start at or after PacketHeaderOffset.Region. |

## Middleware and Routing Codes (NALIX006 - NALIX007, NALIX025 - NALIX026, NALIX030 - NALIX033)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX006` | Registered middleware type does not match dispatcher packet type | Warning | Usage | Nalix packet middleware should be registered against a compatible PacketDispatchOptions<TPacket>. |
| `NALIX007` | Network buffer middleware ignores MiddlewareStageAttribute | Info | Usage | Nalix network buffer middleware ordering uses MiddlewareOrderAttribute only. |
| `NALIX025` | Packet metadata provider clears Opcode | Warning | Routing | Nalix packet metadata providers should not clear builder.Opcode because packet metadata requires a non-null opcode. |
| `NALIX026` | Packet metadata provider overwrites Opcode without guard | Info | Routing | Nalix packet metadata providers usually should augment metadata instead of unconditionally replacing an existing PacketOpcodeAttribute. |
| `NALIX030` | Packet middleware should declare MiddlewareOrder | Info | Middleware | Nalix packet middleware ordering is more predictable when each middleware declares MiddlewareOrderAttribute explicitly. |
| `NALIX031` | Buffer middleware should declare MiddlewareOrder | Info | Middleware | Nalix network buffer middleware ordering is determined solely by MiddlewareOrderAttribute. |
| `NALIX032` | Inbound middleware ignores AlwaysExecute | Info | Middleware | Nalix AlwaysExecute only changes outbound middleware behavior. It has no effect on inbound-only middleware. |
| `NALIX033` | Registered middleware shares MiddlewareOrder with another | Info | Middleware | Nalix middleware registration chains are easier to reason about when MiddlewareOrder values are unique within the same builder chain. |

## Lifecycle and Configuration Codes (NALIX020, NALIX023 - NALIX024)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX020` | Packet ResetForPool should call base.ResetForPool | Warning | Lifecycle | Nalix packets derived from PacketBase<TSelf> should call base.ResetForPool() to restore header fields and default metadata. |
| `NALIX023` | Configuration property type is not supported | Warning | Configuration | Nalix ConfigurationLoader binds only supported scalar, Guid, TimeSpan, DateTime, and enum property types. |
| `NALIX024` | Configuration property is not bindable | Info | Configuration | Nalix ConfigurationLoader binds only public instance properties with public setters. |

## SDK Codes (NALIX027 - NALIX029)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX027` | RequestOptions RetryCount should not be negative | Warning | SDK | Nalix RequestOptions retry count must be zero or greater. |
| `NALIX028` | RequestOptions TimeoutMs should not be negative | Warning | SDK | Nalix RequestOptions timeout must be zero or greater. Use 0 to wait indefinitely. |
| `NALIX029` | Encrypted RequestAsync requires TcpSessionBase | Warning | SDK | Nalix encrypted RequestAsync overload requires the client to be a TcpSessionBase. |

## Source Mapping

- `src/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs`
