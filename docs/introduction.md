# Introduction

Nalix is a modular networking framework for .NET 10 that provides the transport, dispatch, and middleware infrastructure needed to build real-time TCP and UDP systems. It separates the network stack into focused packages so that server code, client code, and shared contracts each depend only on the layers they need.

This page explains the design philosophy behind Nalix, the mental model for how the pieces connect, and where to go next based on what you are building.

## Design Philosophy

Nalix is built around five core principles:

1. **Unified packet model.** Packet types are plain C# classes annotated with serialization attributes. Both the server (`Nalix.Network`) and the client (`Nalix.SDK`) consume the same packet definitions from a shared assembly. This eliminates the version drift and duplication that occurs when client and server maintain separate message definitions.

2. **Allocation-free hot paths.** The request path — from socket receive through deserialization, middleware, and handler invocation — is designed to avoid heap allocations. Buffers are pooled (`BufferLease`), packet contexts are pooled, and the packet registry uses `FrozenDictionary` with function-pointer–based deserialization to eliminate delegate overhead.

3. **Separation of transport and application logic.** The `IProtocol` interface bridges raw network I/O to the application dispatch layer. Transport concerns (socket management, connection lifecycle, admission control) are handled by `Nalix.Network`. Application concerns (deserialization, routing, middleware, handler execution) are handled by `Nalix.Runtime`. This separation means you can replace or customize one layer without affecting the other.

4. **Declarative handler metadata.** Permissions, timeouts, rate limits, and concurrency limits are expressed as attributes on handler methods. The runtime resolves these attributes once during startup and caches them as `PacketMetadata`. Middleware reads the cached metadata at request time — no reflection on the hot path.

5. **Performance-aware service resolution.** Nalix uses `InstanceManager` (a service-locator pattern optimized for allocation-free resolution) instead of standard dependency injection. This ensures that shared infrastructure such as loggers, packet registries, and application services can be resolved during hot-path execution without container overhead.

## What Stays Shared

Across the stack, Nalix keeps these pieces aligned between client and server:

- **Packet types and opcodes** — defined once in a shared contracts assembly
- **Serialization attributes** — `[SerializePackable]`, `[SerializeOrder]`, `[SerializeDynamicSize]`
- **Middleware contracts** — `IPacketMiddleware<TPacket>`, `IPacketContext<TPacket>`
- **Configuration patterns** — `ConfigurationManager` for typed options from INI files
- **Logging** — `ILogger` registration through `InstanceManager`

## Mental Model

### Server side

A Nalix server follows this startup sequence:

1. Load and validate configuration (`NetworkSocketOptions`, `DispatchOptions`, etc.)
2. Register shared services (`ILogger`, `IPacketRegistry`) into `InstanceManager`
3. Build `PacketDispatchChannel` with handlers, middleware, and error handling
4. Create a `Protocol` implementation that bridges transport to dispatch
5. Create and activate a `TcpListenerBase` or `UdpListenerBase`

With `Nalix.Network.Hosting`, those same steps are wrapped behind `NetworkApplication.CreateBuilder()` and `RunAsync()`.

### Client side

A Nalix client follows this flow:

1. Create or load `TransportOptions`
2. Create a `TcpSession` (or `UdpSession`) with a packet registry
3. Connect to the server
4. Optionally perform a cryptographic handshake or session resume
5. Use `RequestAsync<TResponse>` or direct send helpers

### Shared contract assembly

For any project beyond a prototype, packet definitions should live in a shared assembly:

```text
MyApp.Contracts/        ← Shared packet definitions
MyApp.Server/           ← References Contracts + Nalix.Network.Hosting
MyApp.Client/           ← References Contracts + Nalix.SDK
```

## Quick Reference

| Goal | Start with |
|---|---|
| Build a TCP or UDP server | [Quickstart](./quickstart.md) |
| Build a server with a fluent host builder | [Nalix.Network.Hosting](./packages/nalix-network-hosting.md) |
| Build a TCP client | [Nalix.SDK](./packages/nalix-sdk.md) |
| Understand the package layout | [Packages Overview](./packages/index.md) |
| Understand packet metadata and dispatch | [Packet Lifecycle](./concepts/packet-lifecycle.md) |
| Add custom middleware | [Custom Middleware Guide](./guides/custom-middleware-end-to-end.md) |

## What Nalix Is Not

- **Not an HTTP framework.** Nalix operates at the TCP/UDP level with its own binary packet protocol. It does not provide HTTP routing, REST endpoints, or WebSocket support.
- **Not a game engine.** Nalix provides the networking layer. Game logic, rendering, physics, and ECS are outside its scope.
- **Not a message queue.** Nalix is designed for real-time bidirectional communication, not store-and-forward messaging.

## Recommended Reading

1. [Installation](./installation.md) — Package selection and prerequisites
2. [Quickstart](./quickstart.md) — End-to-end Ping/Pong example
3. [Architecture](./concepts/architecture.md) — Layered component overview
4. [Packages Overview](./packages/index.md) — What each package provides
