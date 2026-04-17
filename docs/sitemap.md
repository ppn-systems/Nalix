# Full Sitemap

This page provides a comprehensive index of all documentation available for the Nalix framework. Use this to quickly locate specific topics, API references, or guides.

## 🚀 Getting Started

- [**Overview**](./index.md) — The landing page.
- [**Quick Start**](./quickstart.md) — Build your first Ping/Pong server in minutes.
- [**Introduction**](./introduction.md) — Design philosophy and mental model.
- [**Installation**](./installation.md) — Package selection and prerequisites.

---

## 🧱 Core Concepts

- [**Concepts Overview**](./concepts/index.md) — Landing page for core ideas.
- [**Choose the Right Building Block**](./concepts/choose-the-right-building-block.md) — Middleware vs. Protocol vs. Handler.
- [**Architecture**](./concepts/architecture.md) — The 4-layer system overview.
- [**Performance Optimizations**](./concepts/performance-optimizations.md) — Zero-allocation data paths.
- [**Packet System**](./concepts/packet-system.md) — Serialization and wire format.
- [**Packet Lifecycle**](./concepts/packet-lifecycle.md) — The request journey.
- [**Middleware**](./concepts/middleware.md) — Intercepting and modifying traffic.
- [**Configuration and Runtime**](./concepts/configuration-and-runtime.md) — Managing options and services.
- [**Real-time Engine**](./concepts/real-time.md) — Tick-based execution.
- [**Security Model**](./concepts/security-model.md) — Auth, Handshake, and Encryption.
- [**Error Reporting**](./concepts/error-reporting.md) — Runtime signaling.
- [**Glossary**](./concepts/glossary.md) — Terminology definitions.

### 🎓 Advanced Reliability & Internals
- [**Reliability Overview**](./concepts/advanced/reliability-model.md) — The production confidence layer.
- [**Guarantees & Invariants**](./concepts/advanced/guarantees-and-invariants.md) — Ordering and Concurrency contracts.
- [**Failure Model**](./concepts/advanced/failure-model.md) — Resilience and observable behavior.
- [**Design Tradeoffs**](./concepts/advanced/design-tradeoffs.md) — Performance vs. Convenience.

### Advanced Concepts
- [**Buffer & Memory Management**](./api/framework/memory/buffer-and-pooling.md) — Pooled allocation strategies.
- [**Shard-Aware Dispatch**](./guides/shard-aware-dispatch.md) — Scaling and worker affinity.
- [**Zero-Allocation Hot Path**](./guides/zero-allocation-hot-path.md) — Extreme performance engineering.
- [**Design Trade-offs**](./concepts/advanced/design-tradeoffs.md) — Performance vs latency.

---

## 📖 Practical Guides

- [**Guides Overview**](./guides/index.md) — Navigating the available guides.

### Build a Server
- [**Starter Template**](./guides/starter-template.md) — Basic boilerplate.
- [**Implement Packet Handlers**](./guides/implementing-packet-handlers.md) — Defining and routing message logic.
- [**End-to-End Sample**](./guides/end-to-end.md) — Feature walkthrough.
- [**Production End-to-End**](./guides/production-end-to-end.md) — Real-world implementation.
- [**Project Setup**](./guides/project-setup.md) — Framework bootstrap.
- [**Server Blueprint**](./guides/server-blueprint.md) — Enterprise structure.

### Extend Behavior
- [**Middleware Guide**](./guides/middleware.md) — Adding standard policies.
- [**Custom Middleware**](./guides/custom-middleware-end-to-end.md) — Building your own logic.
- [**Custom Metadata Provider**](./guides/custom-metadata-provider.md) — Attribute-driven logic.

### Networking Patterns
- [**Client Session Connect**](./guides/client-session-connect.md) — SDK client session bootstrap.
- [**TCP Request/Response**](./guides/tcp-request-response.md) — Ordered communication.
- [**UDP Server**](./guides/udp-server.md) — Low-latency datagrams.
- [**UDP Auth Flow**](./guides/udp-auth-flow.md) — Secure session bootstrap.

### Operations & Debugging
- [**Production Checklist**](./guides/production-checklist.md) — Pre-deployment audit.
- [**Troubleshooting**](./guides/troubleshooting.md) — Diagnostic strategies.

---

## 📦 Packages Reference

- [**Packages Map**](./packages/index.md) — Visual dependency graph and overview.
- [**Nalix.Common**](./packages/nalix-common.md) — Shared contracts.
- [**Nalix.Framework**](./packages/nalix-framework.md) — Core infrastructure.
- [**Nalix.Logging**](./packages/nalix-logging.md) — Structured logging.
- [**Nalix.Network**](./packages/nalix-network.md) — Transport & Listeners.
- [**Nalix.Runtime**](./packages/nalix-runtime.md) — Dispatch & Middleware.
- [**Nalix.Network.Hosting**](./packages/nalix-network-hosting.md) — Host Composition.
- [**Nalix.Network.Pipeline**](./packages/nalix-network-pipeline.md) — Throttling components.
- [**Nalix.SDK**](./packages/nalix-sdk.md) — Client sessions.

---

## 🛠️ API Reference

- [**API Overview**](./api/index.md) — Landing page for API docs.

### Analyzers
- [**Analyzers Home**](./api/analyzers/index.md)
- [**Diagnostic Codes**](./api/analyzers/diagnostic-codes.md)
- [**Code Fixes**](./api/analyzers/code-fixes.md)

### Common & Contracts
- [**Control Types**](./api/common/protocols/control-type.md)
- [**Diagnostics**](./api/common/diagnostics-contracts.md)
- [**Packets**](./api/common/packet-contracts.md)
- [**Connections**](./api/common/connection-contracts.md)
- [**Sessions**](./api/common/session-contracts.md)
- [**Concurrency**](./api/common/concurrency-contracts.md)

### Framework Internals
- [**Directories**](./api/framework/environment/directories.md)
- [**Configuration**](./api/framework/runtime/configuration.md)
- [**Instance Manager (DI)**](./api/framework/runtime/instance-manager.md)
- [**Task Manager**](./api/framework/runtime/task-manager.md)
- [**Snowflake**](./api/framework/runtime/snowflake.md)
- [**Clock**](./api/framework/runtime/clock.md)
- [**Timing Scope**](./api/framework/runtime/timing-scope.md)
- [**Singletons**](./api/framework/runtime/singleton-base.md)
- [**Worker Options**](./api/framework/options/worker-options.md)
- [**Recurring Tasks**](./api/framework/options/recurring-options.md)
- [**Frame Model**](./api/framework/packets/frame-model.md)
- [**Serialization Attrs**](./api/common/serialization-attributes.md)
- [**Registry**](./api/framework/packets/packet-registry.md)
- [**Built-in Frames**](./api/framework/packets/built-in-frames.md)
- [**Pooling**](./api/framework/packets/packet-pooling.md)
- [**Fragmentation**](./api/framework/packets/fragmentation.md)
- [**IO Extensions**](./api/framework/packets/reader-writer-and-header-extensions.md)
- [**LZ4**](./api/framework/memory/lz4.md)
- [**Buffers**](./api/framework/memory/buffer-and-pooling.md)
- [**Object Pools**](./api/framework/memory/object-map-and-typed-pools.md)

### Security & Crypto
- [**Cryptography**](./api/security/cryptography.md)
- [**Hashing & MAC**](./api/security/hashing-and-mac.md)
- [**AEAD**](./api/security/aead-and-envelope.md)
- [**Handshake**](./api/security/handshake.md)
- [**Resume**](./api/security/session-resume.md)
- [**Permissions**](./api/security/permission-level.md)

### Runtime Dispatch
- [**Runtime Home**](./api/runtime/index.md)
- [**Routing Home**](./api/runtime/routing/index.md)
- [**Handlers**](./api/runtime/handlers/index.md)
- [**Attributes**](./api/runtime/routing/packet-attributes.md)
- [**Metadata**](./api/runtime/routing/packet-metadata.md)
- [**Context**](./api/runtime/routing/packet-context.md)
- [**Dispatcher**](./api/runtime/routing/packet-dispatch.md)
- [**Return Types**](./api/runtime/routing/handler-results.md)
- [**Pipeline**](./api/runtime/middleware/pipeline.md)
- [**Buffer Pipeline**](./api/runtime/middleware/network-buffer-pipeline.md)
- [**Rate Limiters**](./api/runtime/middleware/token-bucket-limiter.md)

### Network Transport
- [**Network Home**](./api/network/index.md)
- [**Protocols**](./api/network/protocol.md)
- [**TCP Listener**](./api/network/tcp-listener.md)
- [**UDP Listener**](./api/network/udp-listener.md)
- [**Connections**](./api/network/connection/connection.md)
- [**Hubs**](./api/network/connection/connection-hub.md)
- [**Timing Wheel**](./api/network/timing-wheel.md)
- [**Options**](./api/network/options/options.md)

### SDK (Client)
- [**SDK Home**](./api/sdk/index.md)
- [**TCP Sessions**](./api/sdk/tcp-session.md)
- [**UDP Sessions**](./api/sdk/udp-session.md)
- [**Handshake**](./api/sdk/handshake-extensions.md)
- [**Resumption**](./api/sdk/resume-extensions.md)
- [**Subscriptions**](./api/sdk/subscriptions.md)
- [**Transport Options**](./api/sdk/options/transport-options.md)

---

## 📊 Benchmarks

- [**Benchmarks Home**](./benchmarks/index.md)
- [**Infrastructure**](./benchmarks/infrastructure.md)
- [**Memory**](./benchmarks/memory.md)
- [**Data Processing**](./benchmarks/data-processing.md)
- [**Security**](./benchmarks/security.md)
- [**Serialization**](./benchmarks/serialization.md)
- [**Identifiers**](./benchmarks/identifiers.md)
