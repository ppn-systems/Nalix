# Packages Overview

Nalix is composed of focused packages that can be used together or independently. This page provides a map of all packages, their responsibilities, and guidance on choosing the right combination.

!!! tip "Safe defaults"
    **Server:** Start with `Nalix.Network.Hosting` — it brings in the core networking and runtime.  
    **Client:** Start with `Nalix.SDK` — it includes `Nalix.Framework` and `Nalix.Common` transitively.

## Package Map

| Package | Purpose | Key Types |
|---|---|---|
| **Nalix.Common** | Shared contracts, packet attributes, middleware primitives, and connection abstractions | `IPacket`, `IConnection`, `PacketOpcodeAttribute`, `PacketControllerAttribute`, `IPacketContext<TPacket>` |
| **Nalix.Framework** | Configuration, service registry, serialization, packet registry, pooling, compression, and identifiers | `ConfigurationManager`, `InstanceManager`, `TaskManager`, `Snowflake`, `PacketRegistryFactory`, `PacketCipher`, `LZ4Codec` |
| **Nalix.Runtime** | Packet dispatch, middleware execution, handler compilation, and session resume | `PacketDispatchChannel`, `MiddlewarePipeline`, `PacketContext<TPacket>`, `PacketMetadata` |
| **Nalix.Network** | TCP/UDP listeners, connections, protocol bridge, session store, and connection guarding | `TcpListenerBase`, `UdpListenerBase`, `Protocol`, `ConnectionHub`, `SocketConnection` |
| **Nalix.Network.Hosting** | Fluent server bootstrap, packet discovery, and application lifecycle | `NetworkApplication`, `INetworkApplicationBuilder`, `NetworkApplicationBuilder` |
| **Nalix.Network.Pipeline** | Built-in packet middleware, throttling primitives, and time synchronization | `ConcurrencyGate`, `PolicyRateLimiter`, `TokenBucketLimiter`, `PermissionMiddleware`, `TimeSynchronizer` |
| **Nalix.SDK** | Client transport sessions, request/response correlation, handshake and resume flows | `TransportSession`, `TcpSession`, `UdpSession`, `TransportOptions`, `RequestOptions` |
| **Nalix.Logging** | Structured logging with batched async sinks | `NLogix`, `NLogixOptions`, `INLogixTarget` |
| **Nalix.Analyzers** | Compile-time Roslyn diagnostics and code fixes | `NalixUsageAnalyzer`, `DiagnosticDescriptors` |

## Dependency Graph

```mermaid
flowchart TD
    Hosting["Nalix.Network.Hosting"] --> Network["Nalix.Network"]
    Hosting --> Runtime["Nalix.Runtime"]

    Network --> Framework["Nalix.Framework"]
    Network --> Common["Nalix.Common"]

    Runtime --> Framework
    Runtime --> Common

    Framework --> Common

    SDK["Nalix.SDK"] --> Framework
    SDK --> Common

    Pipeline["Nalix.Network.Pipeline"] --> Network

    Logging["Nalix.Logging"] --> Common
```

## Minimum Package Sets

| Scenario | Packages |
|---|---|
| **Hosted server** (recommended) | `Nalix.Network.Hosting`, `Nalix.Network.Pipeline`, `Nalix.Logging` |
| **Manual server** | `Nalix.Network`, `Nalix.Runtime`, `Nalix.Framework`, `Nalix.Common`, `Nalix.Logging` |
| **Client only** | `Nalix.SDK` |
| **Shared contracts** | `Nalix.Common`, `Nalix.Framework` |

## Package Detail Pages

- [Nalix.Common](./nalix-common.md)
- [Nalix.Framework](./nalix-framework.md)
- [Nalix.Runtime](./nalix-runtime.md)
- [Nalix.Network](./nalix-network.md)
- [Nalix.Network.Hosting](./nalix-network-hosting.md)
- [Nalix.Network.Pipeline](./nalix-network-pipeline.md)
- [Nalix.SDK](./nalix-sdk.md)
- [Nalix.Logging](./nalix-logging.md)
