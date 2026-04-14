# API Reference

Nalix API documentation is organized by package boundary and runtime responsibility so you can move from contracts (`Nalix.Common`) to execution (`Nalix.Runtime`, `Nalix.Network`) and then to client transport (`Nalix.SDK`) without mixing concerns.

## Why This Structure Exists

Nalix is split into focused packages with explicit layering:

- `Nalix.Common` defines shared contracts and attributes.
- `Nalix.Framework` provides reusable framework primitives (packet model, configuration, memory, security, tasking).
- `Nalix.Runtime` turns packets into handler execution.
- `Nalix.Network` owns listeners, connections, protocols, and session stores.
- `Nalix.Network.Hosting` adds host/builder composition on top of runtime + network.
- `Nalix.Network.Pipeline` exposes reusable middleware and throttling components.
- `Nalix.SDK` provides client-side transport sessions and extension APIs.

This keeps server runtime internals, transport lifecycle, and client APIs independently evolvable.

## Package Responsibility Matrix

| Package | Primary responsibility | Use when | Avoid when |
| --- | --- | --- | --- |
| `Nalix.Common` | Core interfaces/enums/attributes | You need shared contracts across packages | You need concrete runtime behavior |
| `Nalix.Framework` | Core framework implementations and infrastructure | You need packet registry, serialization, memory/security utilities | You only need transport/session lifecycle |
| `Nalix.Runtime` | Packet dispatch and middleware execution | You are building handler execution pipelines | You only need socket listener primitives |
| `Nalix.Network` | Connection + listener + protocol runtime | You are implementing server transport/runtime loops | You only need pure client transport |
| `Nalix.Network.Hosting` | Host-style startup composition | You want builder-driven server bootstrapping | You prefer manual wiring |
| `Nalix.Network.Pipeline` | Reusable inbound middleware and limiters | You need throttling/middleware components standalone | You need complete server hosting surface |
| `Nalix.SDK` | Client transport sessions and request/response helpers | You build Nalix clients | You implement server listeners |

## Progressive API Path

1. Start with [Common contracts](./common/packet-contracts.md) and connection/session abstractions.
2. Learn packet runtime flow in [Runtime API](./runtime/index.md).
3. Move to transport lifecycle in [Network API](./network/protocol.md).
4. For clients, continue with [SDK API](./sdk/index.md).
5. For production startup composition, use [Hosting API](./hosting/network-application.md).

## Suggested Architecture Diagrams

- Dispatch sequence: `IBufferLease -> NetworkBufferMiddlewarePipeline -> IPacketRegistry -> PacketContext<TPacket> -> handler`.
- Server layering: Listener (`Nalix.Network`) above runtime dispatch (`Nalix.Runtime`) and shared contracts (`Nalix.Common`).
- Client-server handshake lifecycle using `Nalix.SDK.Transport.Extensions` + runtime handlers.

## Related Pages

- [Package Overview](../packages/index.md)
- [Architecture Concepts](../concepts/architecture.md)
- [Packet Lifecycle](../concepts/packet-lifecycle.md)
