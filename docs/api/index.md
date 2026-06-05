# API Reference

Nalix API documentation is organized by package boundary and runtime responsibility so you can move from contracts (`Nalix.Abstractions`) to execution (`Nalix.Runtime`, `Nalix.Network`) and then to client transport (`Nalix.SDK`) without mixing concerns.

## Where to start

If you're looking for the core interfaces and classes that drive Nalix:

- :fontawesome-solid-cube: **Packet contracts & attributes** -> [Common API](./abstractions/index.md)
- :fontawesome-solid-route: **Handlers, routing & metadata** -> [Runtime Routing](./runtime/routing/index.md)
- :fontawesome-solid-filter: **Middleware pipelines** -> [Runtime Middleware](./runtime/middleware/index.md)
- :fontawesome-solid-network-wired: **TCP/UDP transport & protocols** -> [Network API](./network/index.md)
- :fontawesome-solid-mobile-screen: **Client sessions & requests** -> [SDK API](./sdk/index.md)

!!! warning "Important"
    Most users should NOT start by reading the API reference.  
    If you are new, follow the [Recommended Path](../index.md#recommended-path)  
    or explore the [Guides](../guides/index.md) first.

## Source Mapping

- `src/Nalix.Abstractions`
- `src/Nalix.Framework`
- `src/Nalix.Runtime`
- `src/Nalix.Network`
- `src/Nalix.Hosting`
- `src/Nalix.SDK`

## Why This Structure Exists

Nalix is split into focused packages with explicit layering:

- `Nalix.Abstractions` defines shared contracts and attributes.
- `Nalix.Environment` provides foundational memory types, configuration, and environment primitives.
- `Nalix.Codec` handles serialization, built-in frames, and data transforms.
- `Nalix.Framework` provides reusable runtime services, instance management, and tasking.
- `Nalix.Runtime` turns packets into handler execution and provides reusable middleware/throttling components.
- `Nalix.Network` owns listeners, connections, protocols, and session stores.
- `Nalix.Hosting` adds host/builder composition on top of runtime + network.
- `Nalix.SDK` provides client-side transport sessions and extension APIs.

This keeps server runtime internals, transport lifecycle, and client APIs independently evolvable.

## Package Responsibility Matrix

| Package | Primary responsibility | Use when | Avoid when |
| --- | --- | --- | --- |
| `Nalix.Abstractions` | Core interfaces/enums/attributes | You need shared contracts across packages | You need concrete runtime behavior |
| `Nalix.Environment` | Memory, Config, IO, Clock | You need zero-allocation memory primitives | You only need high-level networking |
| `Nalix.Codec` | Serialization, Frames, Transforms | You need to pack/unpack data or register packets | You only need transport lifecycle |
| `Nalix.Framework` | Runtime services and identifiers | You need instance management or task scheduling | You only need pure client transport |
| `Nalix.Runtime` | Packet dispatch and middleware | You are building handler execution pipelines | You only need socket listener primitives |
| `Nalix.Network` | Connection + listener + protocol | You are implementing server transport loops | You only need pure client transport |
| `Nalix.Hosting` | Host-style startup composition | You want builder-driven server bootstrapping | You prefer manual wiring |
| `Nalix.SDK` | Client transport and sessions | You build Nalix clients | You implement server listeners |

## Progressive API Path

1. Start with [Common contracts](./abstractions/packet-contracts.md) and connection/session abstractions.
2. Learn packet runtime flow in [Runtime API](./runtime/index.md).
3. Move to transport lifecycle in [Network API](./network/protocol.md).
4. For clients, continue with [SDK API](./sdk/index.md).
5. For production startup composition, use [Hosting API](./hosting/network-application.md).

## Suggested Architecture Diagrams

- Dispatch sequence: `IBufferLease -> PacketRegistry -> PacketContext<TPacket> / IPacketContext<TPacket> -> handler`.
- Server layering: Listener (`Nalix.Network`) above runtime dispatch (`Nalix.Runtime`) and shared contracts (`Nalix.Abstractions`).
- Client-server handshake lifecycle using `Nalix.SDK.Transport.Extensions` + runtime handlers.

## Related Pages

- [Package Overview](../packages/index.md)
- [Architecture Concepts](../concepts/fundamentals/architecture.md)
- [Packet Lifecycle](../concepts/fundamentals/packet-lifecycle.md)
