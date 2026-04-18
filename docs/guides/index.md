# Guides Overview

Nalix guides help you move from a fresh project to a production-hardened binary. The documentation is organized by the developer journey — from initial setup to advanced extensibility and deployment.

## 🚀 Getting Started

- :fontawesome-solid-folder-plus: [**Project Setup**](./getting-started/project-setup.md) — Create your first Nalix solution with proper project separation.
- :fontawesome-solid-file-code: [**Server Boilerplate**](./getting-started/server-boilerplate.md) — A production-ready boilerplate for new server applications.
- :fontawesome-solid-drafting-compass: [**Server Blueprint**](./getting-started/server-blueprint.md) — Standard architecture for enterprise applications.

## 🌐 Networking

- :fontawesome-solid-plug-circle-check: [**Client Session Guide**](./networking/connecting-clients.md) — Connecting TCP/UDP sessions with Nalix.SDK.
- :fontawesome-solid-broadcast-tower: [**UDP Server Guide**](./networking/udp-server.md) — Building low-latency datagram services.
- :fontawesome-solid-route: [**TCP Patterns Guide**](./networking/tcp-patterns.md) — Request/Response and manual listener wiring.
- :fontawesome-solid-user-lock: [**UDP Security Guide**](./networking/udp-security.md) — Deep dive into secure session handover for UDP.
- :fontawesome-solid-bolt-lightning: [**Minimal Server Guide**](./networking/minimal-server.md) — Minimal server flow without the hosting builder.
- :fontawesome-solid-microchip: [**Session APIs Guide**](./networking/session-apis.md) — High-performance low-level session interaction.

## 📦 Application Logic

- :fontawesome-solid-envelope: [**Implementing Packet Handlers**](./application/packet-handlers.md) — Writing business logic and managing opcodes.
- :fontawesome-solid-filter: [**Middleware Usage Guide**](./application/middleware-usage.md) — Enforcing policy at the transport and packet levels.

## 🔌 Extensibility

- :fontawesome-solid-code-branch: [**Custom Middleware Guide**](./extensibility/custom-middleware.md) — Building your own pipeline components.
- :fontawesome-solid-tags: [**Custom Metadata Providers**](./extensibility/metadata-providers.md) — Using attributes to drive custom behavior.
- :fontawesome-solid-floppy-disk: [**Custom Serialization Provider**](./extensibility/serialization-providers.md) — Registering custom formatters.

## 🏗️ Deployment & Operations

- :fontawesome-solid-rocket: [**Production Server Example**](./deployment/production-example.md) — Real-world example with auth and persistence.
- :fontawesome-solid-check-double: [**Production Checklist**](./deployment/production-checklist.md) — Security, performance, and stability audit.
- :fontawesome-solid-wrench: [**Troubleshooting Guide**](./deployment/troubleshooting.md) — Common issues and diagnostic strategies.
