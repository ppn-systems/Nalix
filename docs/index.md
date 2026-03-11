# Nalix

<p align="center">
  <img src="assets/nalix.png" alt="Nalix Logo" width="200"/>
</p>

<p align="center">
  <a href="https://www.nuget.org/packages?q=Nalix"><img src="https://img.shields.io/nuget/v/Nalix.Network.svg?style=flat-square&label=NuGet" alt="NuGet version"/></a>
  <a href="https://github.com/ppn-systems/nalix/stargazers"><img src="https://img.shields.io/github/stars/ppn-systems/nalix.svg?style=flat-square&color=yellow" alt="GitHub stars"/></a>
  <a href="https://github.com/ppn-systems/nalix/blob/master/LICENSE"><img src="https://img.shields.io/github/license/ppn-systems/nalix.svg?style=flat-square&color=blue" alt="License"/></a>
</p>

Nalix is a real-time TCP and UDP networking framework for .NET 10. It provides a unified packet model shared between client and server, zero-allocation hot paths, shard-aware dispatch pipelines, and middleware-driven packet processing with production-grade transport infrastructure.

---

[Get Started :material-arrow-right:](./quickstart.md){ .md-button .md-button--primary }
[View Packages](./packages/index.md){ .md-button }

---

![Nalix Data Flow](./assets/overview-flow.svg){ .nlx-hero-img }

## Performance Highlights

<div class="nlx-highlight-grid">
  <div class="nlx-highlight-card">
    <h3>Zero-alloc hot path</h3>
    <p>Pooled buffers, pooled contexts, and frozen lookups reduce GC pressure for sustained low-latency traffic.</p>
  </div>
  <div class="nlx-highlight-card">
    <h3>Shard-aware dispatch</h3>
    <p>Worker distribution prevents head-of-line blocking and scales packet handling across logical cores.</p>
  </div>
  <div class="nlx-highlight-card">
    <h3>Built-in safety controls</h3>
    <p>Timeouts, permission checks, concurrency gates, and token-bucket policies are middleware-ready from day one.</p>
  </div>
</div>

---

## Why Nalix

| Capability | How Nalix delivers it |
|---|---|
| **Unified packet model** | Define packet types once in a shared assembly. Both `Nalix.Network` (server) and `Nalix.SDK` (client) consume the same contracts, attributes, and serialization metadata. |
| **Zero-allocation data paths** | Pooled buffers (`BufferLease`), pooled packet contexts, frozen registry lookups (`FrozenDictionary`), and function-pointer–based deserialization eliminate GC pressure on hot paths. |
| **Shard-aware dispatch** | `PacketDispatchChannel` distributes work across multiple worker loops to prevent head-of-line blocking. Workers scale to logical CPU core count. |
| **Middleware pipeline** | Two-layer middleware system: buffer middleware for raw frame processing (decryption, decompression, validation) and packet middleware for application policy (permissions, rate limiting, timeouts). |
| **Production transport** | Built-in connection guarding, token-bucket rate limiting, policy-based throttling, concurrency gates, and timing-wheel–based idle timeout management. |

## Architecture Overview

Nalix organizes networking into four clean layers.

```mermaid
graph TD
    subgraph Client ["Nalix.SDK"]
        Session["TransportSession / TcpSession"]
    end

    subgraph Server ["Nalix.Network.Hosting"]
        Builder["NetworkApplicationBuilder"]
        Host["NetworkApplication"]
    end

    subgraph Runtime ["Nalix.Runtime"]
        Dispatch["PacketDispatchChannel"]
        Middleware["Middleware Pipeline"]
    end

    subgraph Base ["Nalix.Common / Framework"]
        Registry["PacketRegistry"]
        Pool["BufferPoolManager"]
        Crypto["PacketCipher / PacketCompression"]
    end

    Session -- "TCP / UDP" --> Host
    Host --> Dispatch
    Dispatch --> Registry
    Dispatch --> Middleware
    Middleware --> Handlers["Handler Logic"]
```

## Start Here

Choose the path that matches your role.

=== "Server Developer"

    1. [Introduction](./introduction.md) — Design philosophy and mental model
    2. [Installation](./installation.md) — Package selection and prerequisites
    3. [Quickstart](./quickstart.md) — Build a Ping/Pong server end-to-end
    4. [Architecture](./concepts/architecture.md) — Layered component overview
    5. [Server Blueprint](./guides/server-blueprint.md) — Production-oriented project structure

=== "Client Developer"

    1. [Introduction](./introduction.md) — Design philosophy and mental model
    2. [Installation](./installation.md) — Package selection and prerequisites
    3. [Quickstart](./quickstart.md) — Connect a client to your first server
    4. [Nalix.SDK](./packages/nalix-sdk.md) — Client transport sessions and request helpers
    5. [TCP Session](./api/sdk/tcp-session.md) — Detailed session API

=== "Middleware / Extension Author"

    1. [Choose the Right Building Block](./concepts/choose-the-right-building-block.md) — Decision guide
    2. [Middleware](./concepts/middleware.md) — Buffer vs. packet middleware
    3. [Custom Middleware Guide](./guides/custom-middleware-end-to-end.md) — End-to-end walkthrough
    4. [Custom Metadata Provider](./guides/custom-metadata-provider.md) — Convention-based metadata

=== "Contributor"

    1. [Architecture](./concepts/architecture.md) — System design and component boundaries
    2. [Packet System](./concepts/packet-system.md) — Serialization and wire format
    3. [Packet Lifecycle](./concepts/packet-lifecycle.md) — Request path from socket to handler
    4. [Error Reporting](./concepts/error-reporting.md) — Runtime and protocol signaling
    5. [Glossary](./concepts/glossary.md) — Term definitions

## Core Packages

| Package | Purpose |
|---|---|
| [**Nalix.Network**](./packages/nalix-network.md) | TCP/UDP listeners, connections, protocol logic, and transport infrastructure |
| [**Nalix.Runtime**](./packages/nalix-runtime.md) | Packet dispatch, middleware execution, handler compilation, and session resume |
| [**Nalix.SDK**](./packages/nalix-sdk.md) | Client-side transport sessions, request/response helpers, and handshake flows |
| [**Nalix.Framework**](./packages/nalix-framework.md) | Configuration, service registry, serialization, packet registry, pooling, compression, and identifiers |
| [**Nalix.Network.Hosting**](./packages/nalix-network-hosting.md) | Fluent builder and application lifecycle for server bootstrap |
| [**Nalix.Common**](./packages/nalix-common.md) | Shared contracts, packet attributes, middleware primitives, and connection abstractions |

For the full package map, see [Packages Overview](./packages/index.md).

---

*Nalix is built by [PPN Corporation](https://github.com/ppn-systems). Licensed under Apache 2.0.*
