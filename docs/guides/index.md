# Guides Overview

Nalix guides help you move from a fresh project to a production-hardened binary. We recommend starting with the **Foundation** guides which use the high-level Hosting builder.

## 🚀 Foundation (Hosting-Based)

These guides use the `NetworkApplication` hosting builder, which is the recommended path for 99% of applications.

- :fontawesome-solid-folder-plus: [**Project Setup**](./project-setup.md) — Create your first Nalix solution with proper project separation.
- :fontawesome-solid-file-code: [**Server Boilerplate**](./server-boilerplate.md) — A production-ready boilerplate for new server applications.
- :fontawesome-solid-drafting-compass: [**Server Blueprint**](./server-blueprint.md) — Standard architecture for enterprise applications.
- :fontawesome-solid-rocket: [**Production End-to-End**](./production-end-to-end.md) — Implementing real-world features like auth and persistence in a hosted environment.
- :fontawesome-solid-check-double: [**Production Checklist**](./production-checklist.md) — Security, performance, and stability audit.

## 🔌 Extending Behavior

- :fontawesome-solid-filter: [**Middleware Guide**](./middleware.md) — Adding cross-cutting concerns (Auth, Rate Limit).
- :fontawesome-solid-code-branch: [**Custom Middleware**](./custom-middleware-end-to-end.md) — Building your own pipeline components.
- :fontawesome-solid-tags: [**Custom Metadata Provider**](./custom-metadata-provider.md) — Using attributes to drive custom logic.
- :fontawesome-solid-floppy-disk: [**Custom Serialization Provider**](./custom-serialization-provider.md) — Registering custom formatters.

## 🌐 Networking Patterns

- :fontawesome-solid-plug-circle-check: [**Client Session Connect**](./client-session-connect.md) — Creating and connecting TCP/UDP sessions with Nalix.SDK.
- :fontawesome-solid-broadcast-tower: [**UDP Server**](./udp-server.md) — Building low-latency datagram services with UdpListener.

## 🛠️ Advanced & Low-Level (Manual Setup)

Use these guides when you need absolute control over the runtime lifecycle or are building custom transport layers without the Hosting builder.

- :fontawesome-solid-bolt-lightning: [**Minimal End-to-End**](./end-to-end.md) — Minimal server flow without the hosting builder.
- :fontawesome-solid-bridge: [**Direct TCP Flow**](./tcp-request-response.md) — Manual TcpListener and Protocol wiring.
- :fontawesome-solid-user-lock: [**UDP Auth Handover**](./udp-auth-flow.md) — Deep dive into secure session bootstrap for UDP.
- :fontawesome-solid-microchip: [**Low-Level Session APIs**](./low-level-session-apis.md) — Bypassing high-level event handlers for maximum performance.
- :fontawesome-solid-shuttle-space: [**Shard-Aware Dispatch**](./shard-aware-dispatch.md) — Customizing core worker affinity and sharding.

## 📁 Operations & Debugging

- :fontawesome-solid-wrench: [**Troubleshooting**](./troubleshooting.md) — Common issues and diagnostic strategies.
- :fontawesome-solid-gauge-high: [**Zero-Allocation Paths**](./zero-allocation-hot-path.md) — Understanding how Nalix achieves peak performance.
