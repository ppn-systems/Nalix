# NALIX Diagnostic Codes

This page lists the diagnostic descriptors emitted by `Nalix.Analyzers`.
The source of truth is `src/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs`.

## Usage and Dispatch Codes

| ID | Title | Severity | Category | Description |
|---|---|---:|---|---|
| `NALIX001` | Packet controller contains duplicate PacketOpcode | Warning | Usage | Controller dispatch relies on unique opcodes per controller. |
| `NALIX002` | Packet controller handler should declare PacketOpcode | Warning | Usage | Controller methods matching Nalix handler patterns should declare `[PacketOpcode(...)]`. |
| `NALIX003` | Packet controller handler has unsupported signature | Warning | Usage | Handler signatures must match the forms supported by `PacketHandlerCompiler`. |
| `NALIX004` | PacketContext<T> does not match dispatch packet type | Warning | Usage | `PacketContext<T>` should use the dispatcher packet type. |
| `NALIX005` | Registered controller handler packet type does not match dispatcher type | Warning | Usage | Registered controllers should contain handlers compatible with `PacketDispatchOptions<TPacket>`. |
| `NALIX008` | Registered handler controller is missing PacketController | Warning | Usage | Types registered through `WithHandler` should declare `[PacketController]`. |
| `NALIX009` | Registered packet type is missing static Deserialize | Warning | Usage | Packet registry binding expects `IPacketDeserializer<T>` / static deserialize support. |
| `NALIX010` | PacketBase<TSelf> should use the containing packet type | Warning | Usage | `PacketBase<TSelf>` should use the packet's own type as `TSelf`. |
| `NALIX011` | IPacketDeserializer<T> should use the containing packet type | Warning | Usage | Packet deserializer contracts should target the containing packet type. |
| `NALIX012` | PacketBase packet should expose static Deserialize | Warning | Usage | `PacketBase<TSelf>` packet types should expose accessible `Deserialize(ReadOnlySpan<byte>)`. |
| `NALIX017` | Packet Deserialize signature is invalid | Warning | Usage | `Deserialize` should be `public static new {Packet} Deserialize(ReadOnlySpan<byte>)`. |
| `NALIX018` | Registered packet type must be concrete | Warning | Usage | Packet registry entries should be concrete, non-abstract, non-generic packet types. |
| `NALIX047` | Dispatch loop count is out of supported range | Warning | Usage | `WithDispatchLoopCount` expects `1..64`. |
| `NALIX048` | Packet controller handler return type is unsupported | Warning | Usage | Handler returns must be supported by Nalix return handlers. |
| `NALIX050` | PacketOpcode is declared on a non-controller type | Info | Usage | `[PacketOpcode]` handlers are expected inside `[PacketController]` types. |
| `NALIX052` | Packet Deserialize overload should include ReadOnlySpan<byte> | Warning | Usage | Packet discovery expects a `ReadOnlySpan<byte>` deserialize overload. |
| `NALIX054` | PacketController name is duplicated | Info | Usage | Duplicate controller names reduce routing and diagnostic clarity. |
| `NALIX055` | Redundant cast on PacketContext<T>.Packet | Info | Usage | `PacketContext<T>.Packet` is already strongly typed. |
| `NALIX056` | Middleware registration uses null | Warning | Usage | Middleware registration methods require non-null middleware instances. |
| `NALIX058` | Packet handler method should not be generic | Warning | Usage | Generic packet handler methods can lead to ambiguous or unsupported binding. |

## Middleware, Routing, and Buffer Pipeline Codes

| ID | Title | Severity | Category | Description |
|---|---|---:|---|---|
| `NALIX006` | Registered middleware type does not match dispatcher packet type | Warning | Usage | Packet middleware should be compatible with the dispatcher packet type. |
| `NALIX007` | Network buffer middleware ignores MiddlewareStageAttribute | Info | Usage | Buffer middleware ordering uses `MiddlewareOrderAttribute` only. |
| `NALIX019` | Registered buffer middleware does not implement INetworkBufferMiddleware | Warning | Usage | `WithBufferMiddleware` requires an `INetworkBufferMiddleware` type. |
| `NALIX025` | Packet metadata provider clears Opcode | Warning | Routing | Metadata providers should not clear `builder.Opcode`. |
| `NALIX026` | Packet metadata provider overwrites Opcode without guard | Info | Routing | Metadata providers should usually augment rather than overwrite opcode metadata. |
| `NALIX030` | Packet middleware should declare MiddlewareOrder | Info | Middleware | Explicit middleware order makes packet middleware chains predictable. |
| `NALIX031` | Buffer middleware should declare MiddlewareOrder | Info | Middleware | Buffer middleware ordering is determined by `MiddlewareOrderAttribute`. |
| `NALIX032` | Inbound middleware ignores AlwaysExecute | Info | Middleware | `AlwaysExecute` only affects outbound middleware execution. |
| `NALIX033` | Registered middleware shares MiddlewareOrder with another middleware in the same chain | Info | Middleware | Duplicate order values in one builder chain reduce predictability. |
| `NALIX035` | PacketOpcode is in reserved range | Warning | Usage | Application handlers should avoid reserved opcodes `0x0000..0x00FF`. |
| `NALIX036` | PacketOpcode is already used in a different controller | Warning | Usage | Dispatch requires unique opcodes across registered controllers. |
| `NALIX038` | OpCode in documentation does not match attribute | Info | Documentation | XML docs should stay synchronized with `[PacketOpcode]` values. |

## Serialization Codes

| ID | Title | Severity | Category | Description |
|---|---|---:|---|---|
| `NALIX013` | Explicit serialization member should declare SerializeOrder | Warning | Serialization | Explicit layouts should assign stable order metadata to every serialized member. |
| `NALIX014` | SerializeOrder value is duplicated | Warning | Serialization | `SerializeOrder` values should be unique within a serializable type. |
| `NALIX015` | SerializeIgnore conflicts with SerializeOrder | Warning | Serialization | A member cannot be both ignored and explicitly ordered. |
| `NALIX016` | SerializeDynamicSize is unnecessary on fixed-size member | Info | Serialization | Dynamic-size metadata should be used only for variable-size members. |
| `NALIX021` | SerializeOrder should not be negative | Warning | Serialization | Explicit serialization order values should be non-negative. |
| `NALIX022` | Packet member SerializeOrder overlaps packet header region | Warning | Serialization | Packet payload members on `PacketBase`-derived types should start at or after `PacketHeaderOffset.Region`. |
| `NALIX034` | SerializeHeader conflicts with SerializeOrder | Warning | Serialization | Do not combine `SerializeHeader` and `SerializeOrder` on one member. |
| `NALIX046` | SerializeOrder gap is unusually large | Info | Serialization | `SerializeOrder` is ordering metadata, not a byte offset. |
| `NALIX051` | IFixedSizeSerializable type contains dynamic serialization member | Warning | Serialization | Fixed-size serializable types should avoid dynamic-size members. |

## Lifecycle, Configuration, SDK, and Hosting Codes

| ID | Title | Severity | Category | Description |
|---|---|---:|---|---|
| `NALIX020` | Packet ResetForPool should call base.ResetForPool | Warning | Lifecycle | `PacketBase<TSelf>` overrides should restore base header/default metadata. |
| `NALIX023` | Configuration property type is not supported | Warning | Configuration | `ConfigurationLoader` binds supported scalar, `Guid`, `TimeSpan`, `DateTime`, and enum properties. |
| `NALIX024` | Configuration property is not bindable | Info | Configuration | Non-bindable config properties should expose a public setter or use `[ConfiguredIgnore]`. |
| `NALIX027` | RequestOptions RetryCount should not be negative | Warning | SDK | `RequestOptions.RetryCount` must be `>= 0`. |
| `NALIX028` | RequestOptions TimeoutMs should not be negative | Warning | SDK | `RequestOptions.TimeoutMs` must be `>= 0`; `0` waits indefinitely. |
| `NALIX029` | Encrypted RequestAsync requires TcpSession | Warning | SDK | `RequestOptions.Encrypt=true` requires the client to be `TcpSession`. |
| `NALIX037` | Potential allocation in hot path | Info | Performance | High-frequency Nalix hot paths should avoid allocations where practical. |
| `NALIX039` | Potential IBufferLease leak | Warning | Usage | `IBufferLease` must be disposed exactly once on all code paths. |
| `NALIX040` | NetworkApplicationBuilder should configure BufferPoolManager | Info | Performance | Explicit buffer pool configuration can reduce allocation pressure. |
| `NALIX041` | NetworkApplicationBuilder should configure ConnectionHub | Info | Usage | Explicit connection hub configuration makes host wiring clear. |
| `NALIX042` | NetworkApplicationBuilder handler type is not constructible | Warning | Usage | Handler types should be concrete constructible classes. |
| `NALIX043` | NetworkApplicationBuilder metadata provider type is not constructible | Warning | Usage | Metadata provider types should be concrete constructible classes. |
| `NALIX044` | NetworkApplicationBuilder should configure a TCP binding | Info | Usage | Hosts usually need at least one TCP binding. |
| `NALIX045` | NetworkApplicationBuilder should configure TCP before UDP | Info | Usage | UDP bindings are expected to be paired with TCP bindings in this host setup. |
| `NALIX053` | Encrypted RequestAsync requires TcpSession (options variable path) | Warning | SDK | The encrypted-request TCP requirement also applies when options are built through variables. |
| `NALIX057` | RequestOptions uses infinite timeout with retries | Info | SDK | `TimeoutMs=0` can make retries ineffective because each attempt may wait indefinitely. |

## Source Mapping

- `src/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs`
- `src/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs`
- `src/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.InvocationAnalysis.cs`
- `src/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.SymbolSet.cs`
