# Nalix.Runtime.Handlers

`Nalix.Runtime.Handlers` contains built-in packet controller classes for core protocol flows.

## Audit Summary

- Existing page focused on generic handler discovery/signature patterns but did not document concrete built-in handler responsibilities.
- Needed alignment with actual public handler classes in `Nalix.Runtime`.

## Missing Content Identified

- Built-in controller coverage (`HandshakeHandlers`, `SessionHandlers`, `SystemControlHandlers`).
- Responsibility boundaries and when not to depend on built-ins directly.

## Improvement Rationale

This clarifies default runtime behavior and extension points for production deployments.

## Source Mapping

- `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`
- `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs`

## Built-in Controllers

### `HandshakeHandlers`

Handles X25519 handshake flow using `Handshake` packets.

- Accepts `CLIENT_HELLO` and `CLIENT_FINISH` stages.
- Validates transcript/proofs.
- Establishes connection secret/algorithm on success.
- Stores session entry when session store is available.

### `SessionHandlers`

Handles session resume with `SessionResume` packets.

- Validates resume request/stage/token.
- Loads session from `IConnectionHub.SessionStore`.
- Restores secret/algorithm/permission/attributes to connection.
- Responds with resume acknowledgement.

### `SystemControlHandlers`

Handles control packet operations (`Control`).

- Handles disconnect requests.
- Responds to ping with pong.
- Handles cipher update control and acknowledgement.
- Handles time sync request/response path.

## Handler Attributes in Built-ins

Built-in handlers currently use packet attributes such as:

- `PacketController`
- `PacketOpcode`
- `PacketEncryption`
- `PacketPermission`
- `ReservedOpcodePermitted`

## Custom Handlers

While built-in controllers handle protocol signals, your application logic should live in custom controllers.

- [**Implementing Packet Handlers**](../../../guides/implementing-packet-handlers.md) — Step-by-step guide to building and registering your own handlers.

## Best Practices

- Keep built-in controllers enabled for standard handshake/session/control protocol behaviors.
- Add custom controllers for domain packet logic; do not overload system opcode responsibilities.
- Use metadata and middleware for policy changes before replacing built-in core handlers.

## Related APIs

- [Packet Attributes](../routing/packet-attributes.md)
- [Packet Metadata](../routing/packet-metadata.md)
- [Handler Result Types](../routing/handler-results.md)
