# Network API Reference

`Nalix.Network` contains the server-side transport runtime: listeners, connection lifecycle, protocol base classes, connection registry, and network-focused options.

## Audit Summary

- Network API section had detailed leaf pages but no package overview page.
- This made onboarding jump directly into low-level pages without a transport mental model.

## Missing Content Identified

- A top-level map of transport components and how they interact.
- A progressive reading order for listener -> protocol -> connection -> hub -> options.

## Improvement Rationale

A package overview reduces time-to-first-understanding for new contributors and operators.

## Core Areas

- [Protocol](./protocol.md)
- [TCP Listener](./tcp-listener.md)
- [UDP Listener](./udp-listener.md)
- [Socket Connection](./socket-connection.md)
- [Session Store](./session-store.md)

### Connection Subsystem

- [Connection](./connection/connection.md)
- [Connection Hub](./connection/connection-hub.md)
- [Connection Events](./connection/connection-events.md)
- [Connection Extensions](./connection/connection-extensions.md)
- [Connection Limiter](./connection/connection-limiter.md)

### Network Options

- [Options Overview](./options/options.md)
- [Network Socket Options](./options/network-socket-options.md)
- [Connection Limit Options](./options/connection-limit-options.md)
- [Connection Hub Options](./connection/connection-hub-options.md)
- [Timing Wheel Options](./options/timing-wheel-options.md)
- [Pooling Options](./options/pooling-options.md)
- [Network Callback Options](./options/network-callback-options.md)
- [Compression Options](./options/compression-options.md)
- [Token Bucket Options](./options/token-bucket-options.md)

## Suggested Reading Order

1. Listener lifecycle: TCP/UDP listener pages.
2. Protocol processing model.
3. Connection and connection hub behavior.
4. Session store and timeout internals.
5. Options and tuning pages.

## Related APIs

- [Runtime Overview](../runtime/index.md)
- [SDK Overview](../sdk/index.md)
- [API Overview](../index.md)
