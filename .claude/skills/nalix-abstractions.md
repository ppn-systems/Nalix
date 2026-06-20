# Nalix.Abstractions

## Triggers
- Adding a new cross-cutting contract, interface, or attribute
- Working with `IConnection` state or authentication checks
- Defining serialization attributes on packet/frame types
- Adding new permission levels, cipher types, or middleware contracts

---

## Rules

### Dependency Constraint
This project has **zero internal Nalix dependencies**. Adding a reference to any other Nalix project here creates a circular dependency that breaks the entire build graph. Only `Microsoft.Extensions.Logging.Abstractions` is permitted.

### `IConnection` Auth Guard Pattern
`connection.Secret.IsZero` is the canonical pre-auth check used throughout the runtime:
- `true` = connection has not yet completed key exchange/handshake
- Used in `SystemControlHandlers` to reject cipher update requests before authentication
- Use this pattern in any handler that requires an established session

### Serialization Attributes
- `[SerializeOrder(n)]` must be present on **every** serializable field — starting from 0, no gaps, no duplicate values
- The source generator (`Nalix.Analyzers.Generators`) emits fields in `[SerializeOrder]` order into the wire format — gaps produce empty wire slots
- `[SerializeIgnore]` excludes a field entirely from the wire format
- `[SerializeHeader(n)]` marks a field/property as part of the header section (ordered before payload)
- `[Packet]` marks a class as a Nalix packet for registration
- `[GenerateFormatter]` on a class triggers `SerializeFormatterGenerator` to emit `IFormatter<T>`

### Attributes Are Metadata Only
No attribute defined here may contain behavior (method bodies beyond property accessors). Attributes store metadata only — behavior belongs in Framework or Runtime.

### `Bytes32` Value Type
Fixed 32-byte value type. Stored inline (no heap allocation) but:
- Boxing `Bytes32` allocates — do not use as `object`, `dynamic`, or in non-generic collections on hot paths
- `.IsZero` property checks all 32 bytes — used as the canonical "not set" sentinel

### Middleware Contracts
- `IPacketMiddleware` implementations must be decorated with `[MiddlewareOrder(n)]` — order is mandatory, no default
- `MiddlewareStage` controls which pipeline slot the middleware occupies (`PreProcess`, `Process`, `PostProcess`)

---

## Checklists

### Add a new interface/contract
1. Define in the appropriate subdirectory (`Networking/`, `Security/`, `Serialization/`, etc.)
2. Add XML documentation on all public members — required for all public APIs here
3. Zero implementation code — contracts only
4. Run build to confirm no accidental project reference was introduced

### Add a new attribute
1. Inherit `Attribute`, decorate with appropriate `[AttributeUsage]`
2. Properties only — no methods with logic
3. Add to `KnownNames.cs` in `Nalix.Analyzers.Generators` if generators need to detect this attribute

---

## Gotchas

- **`[SerializeOrder]` gaps silently corrupt wire format**: The generator does not validate gaps. A sequence `(0, 2)` skips slot 1 — the wire format has an empty slot that existing clients expect to contain data.

- **`IConnection` partial files have cross-cutting blast radius**: `IConnection` is split across `IConnection.cs`, `IConnection.Transmission.cs`, `IConnection.ErrorTracked.cs`, and `IConnection.Hub.cs`. Changes to any partial file affect all implementations — run impact analysis before modifying.

- **Attribute construction must be zero-cost**: Attributes here are instantiated by the Roslyn analyzer on every keystroke in the IDE. Heavy constructors or static initializers degrade IDE responsiveness for every developer on the project.

- **`PermissionLevel` enum gaps affect authorization**: The permission check in the dispatch system uses integer comparison. Adding a new level between two existing levels can accidentally grant access to handlers expecting a higher level.
