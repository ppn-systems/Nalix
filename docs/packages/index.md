# Packages Overview

Use these packages together or separately depending on whether you are building the server, the client, or shared contracts.

!!! info "Version note"
    Latest verified public package version on 2026-03-27:

    - `Nalix.Network`: `12.0.0`
    - `Nalix.SDK`: `12.0.0`

!!! tip "Safe default package choice"
    If you are building a server, start with `Nalix.Network`, `Nalix.Common`, and `Nalix.Framework`.
    If you are building a client, start with `Nalix.SDK`, `Nalix.Common`, and `Nalix.Framework`.

| Package | Use it for | Key types |
| --- | --- | --- |
| Nalix.SDK | Client transport sessions, request helpers, and control/directive flows | `TransportSession`, `TcpSession`, `TransportOptions`, `RequestOptions` |
| Nalix.Network | Listeners, connections, dispatch pipeline, and connection guarding | `TcpListenerBase`, `UdpListenerBase`, `Protocol`, `ConnectionHub`, `PacketDispatchChannel`, `ConnectionGuard` |
| Nalix.Network.Pipeline | Packet middleware, throttling, and time synchronization helpers | `ConcurrencyGate`, `PolicyRateLimiter`, `TokenBucketLimiter`, `TimeSynchronizer`, `TokenBucketOptions` |
| Nalix.Common | Shared contracts, packet attributes, middleware contracts | `IPacket`, `IConnection`, `PacketControllerAttribute`, `PacketOpcodeAttribute` |
| Nalix.Logging | Structured logging and targets | `NLogix`, `NLogixOptions`, `INLogixTarget` |
| Nalix.Framework | Configuration, service registry, scheduling, IDs, timing helpers, built-in frames, registry, serializer helpers | `ConfigurationManager`, `InstanceManager`, `TaskManager`, `Snowflake`, `Clock`, `PacketRegistryFactory`, `PacketRegistry`, `Handshake`, `Control`, `Directive`, `Text256/512/1024`, `FrameBase`, `PacketBase<TSelf>`, `FrameTransformer`, `FragmentAssembler`, `FragmentOptions`, `DataReaderExtensions`, `DataWriterExtensions`, `HeaderExtensions` |
| Nalix.Analyzers | Compile-time diagnostics and code fixes for packet, serialization, middleware, configuration, and SDK usage | `NalixUsageAnalyzer`, `DiagnosticDescriptors`, code fix providers |

## Minimal wiring map

- Client-only: `Nalix.SDK` + `Nalix.Common` and optionally `Nalix.Framework` if you want `ConfigurationManager` / `InstanceManager`.
- Server-only: `Nalix.Network` + `Nalix.Common` + `Nalix.Framework`.
- Full stack: all packages, with one shared packet catalog shape on both sides.

## Quick example

```mermaid
flowchart TD
    SDK --> Common["Nalix.Common"]
    SDK --> Framework["Nalix.Framework"]

    Network --> Common
    Network --> Framework

    Logging["Nalix.Logging"] --> Common
```
