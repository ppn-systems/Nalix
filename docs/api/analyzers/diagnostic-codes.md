# NALIX Diagnostic Codes

This page provides a detailed list of diagnostic codes emitted by `Nalix.Analyzers`. These rules help ensure that your application uses the framework correctly and efficiently.

## Usage Codes (NALIX001 - NALIX012, NALIX017 - NALIX019, NALIX047 - NALIX048, NALIX050, NALIX052, NALIX054 - NALIX056, NALIX058)

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
| `NALIX047` | Dispatch loop count is out of supported range | Warning | Usage | `WithDispatchLoopCount` expects a value in the range `1..64`. |
| `NALIX048` | Packet controller handler return type is unsupported | Warning | Usage | Handler return type is not supported by Nalix return handlers. |
| `NALIX050` | PacketOpcode is declared on a non-controller type | Info | Usage | `[PacketOpcode]` is expected on methods inside `[PacketController]` types. |
| `NALIX052` | Packet Deserialize overload should include ReadOnlySpan<byte> | Warning | Usage | Packet types should expose `Deserialize(ReadOnlySpan<byte>)` for stable registry/discovery behavior. |
| `NALIX054` | PacketController name is duplicated | Info | Usage | Duplicate `PacketController` names can reduce routing and diagnostics clarity. |
| `NALIX055` | Redundant cast on PacketContext<T>.Packet | Info | Usage | In `PacketContext<T>` handlers, casting `context.Packet` to `T` is unnecessary. |
| `NALIX056` | Middleware registration uses null | Warning | Usage | `WithMiddleware`/`WithBufferMiddleware` should not receive null middleware instances. |
| `NALIX058` | Packet handler method should not be generic | Warning | Usage | Generic `[PacketOpcode]` handlers are not recommended for predictable dispatch binding. |

## Serialization Codes (NALIX013 - NALIX016, NALIX021 - NALIX022, NALIX034, NALIX046, NALIX051)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX013` | Explicit serialization member should declare SerializeOrder | Warning | Serialization | Explicit serialization layouts should assign a stable SerializeOrder to every serialized member. |
| `NALIX014` | SerializeOrder value is duplicated | Warning | Serialization | SerializeOrder values should be unique within a serializable type. |
| `NALIX015` | SerializeIgnore conflicts with SerializeOrder | Warning | Serialization | A member cannot be both ignored and explicitly ordered for serialization. |
| `NALIX016` | SerializeDynamicSize is unnecessary on fixed-size member | Info | Serialization | SerializeDynamicSize should be used only for variable-size serialization members such as strings, arrays, or variable-size collections. |
| `NALIX021` | `SerializeOrder` should not be negative | Warning | Serialization | Nalix explicit serialization layout should not use negative `SerializeOrder` values. |
| `NALIX022` | Packet member `SerializeOrder` overlaps packet header region | Warning | Serialization | Packet payload members on `PacketBase`-derived types should start at or after `PacketHeaderOffset.Region`. |
| `NALIX034` | `SerializeHeader` conflicts with `SerializeOrder` | Warning | Serialization | Nalix serialization members should not declare both `SerializeHeader` and `SerializeOrder`. |
| `NALIX046` | `SerializeOrder` gap is unusually large | Info | Serialization | `SerializeOrder` is ordering metadata, not a byte offset. Large jumps are allowed but may indicate accidental numbering. |
| `NALIX051` | `IFixedSizeSerializable` type contains dynamic serialization member | Warning | Serialization | Fixed-size serializable types should avoid variable-size members such as `string`, arrays, or nested packets. |

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
| `NALIX033` | Registered middleware shares `MiddlewareOrder` with another | Info | Middleware | Nalix middleware registration chains are easier to reason about when `MiddlewareOrder` values are unique within the same builder chain. |
| `NALIX035` | `PacketOpcode` is in reserved range | Warning | Routing | OpCodes between `0x0000` and `0x00FF` are reserved for internal Nalix system packets. |
| `NALIX036` | `PacketOpcode` is already used in a different controller | Warning | Routing | Nalix dispatch requires unique opcodes across all registered controllers. |
| `NALIX038` | OpCode in documentation does not match attribute | Info | Documentation | XML documentation summaries should be synchronized with `PacketOpcode` values. |

## Lifecycle and Configuration Codes (NALIX020, NALIX023 - NALIX024)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX020` | Packet ResetForPool should call base.ResetForPool | Warning | Lifecycle | Nalix packets derived from PacketBase<TSelf> should call base.ResetForPool() to restore header fields and default metadata. |
| `NALIX023` | Configuration property type is not supported | Warning | Configuration | Nalix ConfigurationLoader binds only supported scalar, Guid, TimeSpan, DateTime, and enum property types. |
| `NALIX024` | Configuration property is not bindable | Info | Configuration | Nalix `ConfigurationLoader` binds only public instance properties with public setters. |
| `NALIX037` | Potential allocation in hot path | Info | Performance | Network hot paths should avoid `new` allocations to minimize latency. |
| `NALIX039` | Potential `IBufferLease` leak | Warning | Usage | `IBufferLease` represents a pooled resource that must be returned to the pool exactly once. |

## SDK Codes (NALIX027 - NALIX029, NALIX053, NALIX057)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX027` | RequestOptions RetryCount should not be negative | Warning | SDK | Nalix RequestOptions retry count must be zero or greater. |
| `NALIX028` | RequestOptions TimeoutMs should not be negative | Warning | SDK | Nalix RequestOptions `TimeoutMs` must be zero or greater. Use `0` to wait indefinitely. |
| `NALIX029` | Encrypted RequestAsync requires TcpSessionBase | Warning | SDK | Nalix encrypted RequestAsync overload requires the client to be a TcpSessionBase. |
| `NALIX053` | Encrypted RequestAsync requires TcpSession (options variable path) | Warning | SDK | Same encrypted-request requirement, including options created through local variables. |
| `NALIX057` | RequestOptions uses infinite timeout with retries | Info | SDK | `TimeoutMs=0` with `RetryCount>0` is often an ineffective retry configuration. |

## Hosting Codes (NALIX040 - NALIX045)

| ID | Title | Severity | Category | Description |
|---|---|---|---|---|
| `NALIX040` | `NetworkApplicationBuilder` should configure `BufferPoolManager` | Info | Performance | Nalix network hosting can reduce allocation pressure by registering an explicit `BufferPoolManager` before building. |
| `NALIX041` | `NetworkApplicationBuilder` should configure `ConnectionHub` | Info | Usage | Nalix network hosting can use an explicitly configured `ConnectionHub` instead of the default fallback. |
| `NALIX042` | NetworkApplicationBuilder handler type is not constructible | Warning | Usage | Nalix handler registration creates instances at runtime, so the handler type should be a concrete class. |
| `NALIX043` | NetworkApplicationBuilder metadata provider type is not constructible | Warning | Usage | Nalix metadata provider registration creates instances at runtime, so the provider type should be a concrete class. |
| `NALIX044` | NetworkApplicationBuilder should configure a TCP binding | Info | Usage | Nalix network hosting usually needs at least one TCP binding to serve clients. |
| `NALIX045` | NetworkApplicationBuilder should configure TCP before UDP | Info | Usage | Nalix network hosting in this package expects UDP bindings to be paired with TCP bindings. |

## Source Mapping

- `src/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs`
