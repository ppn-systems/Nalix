# Full Sitemap

This page provides a comprehensive index of all documentation available for the Nalix framework. Use this to quickly locate specific topics, API references, or guides.

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸Ãƒâ€¦Ã‚Â¡ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ Getting Started

- [**Overview**](./index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â The landing page.
- [**Quick Start**](./quickstart.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Build your first Ping/Pong server in minutes.
- [**Introduction**](./introduction.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Design philosophy and mental model.
- [**Installation**](./installation.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Package selection and prerequisites.

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸Ãƒâ€šÃ‚Â§Ãƒâ€šÃ‚Â± Core Concepts

- [**Concepts Overview**](./concepts/index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Landing page for core ideas.
- [**Selecting Building Blocks**](./concepts/runtime/building-blocks.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Middleware vs. Protocol vs. Handler.
- [**Architecture**](./concepts/fundamentals/architecture.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â The 4-layer system overview.
- [**Performance Optimizations**](./concepts/internals/performance-optimizations.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Zero-allocation data paths.
- [**Packet System**](./concepts/fundamentals/packet-system.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Serialization and wire format.
- [**Packet Lifecycle**](./concepts/fundamentals/packet-lifecycle.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â The request journey.
- [**Middleware Pipeline**](./concepts/runtime/middleware-pipeline.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Intercepting and modifying traffic.
- [**Configuration System**](./concepts/runtime/configuration.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Managing options and services.
- [**Real-time Engine**](./concepts/runtime/real-time-engine.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Tick-based execution.
- [**Security Architecture**](./concepts/security/security-architecture.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Auth, Handshake, and Encryption.
- [**Errors and Diagnostics**](./concepts/fundamentals/errors-and-diagnostics.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Runtime signaling.
- [**Glossary**](./concepts/glossary.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Terminology definitions.

### ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸Ãƒâ€¦Ã‚Â½ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œ Advanced Reliability & Internals

- [**Reliability Model**](./concepts/internals/reliability.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â The production confidence layer.
- [**System Guarantees**](./concepts/internals/guarantees-and-invariants.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Ordering and Concurrency contracts.
- [**Failure Handling**](./concepts/internals/failure-handling.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Resilience and observable behavior.
- [**Design Tradeoffs**](./concepts/internals/design-tradeoffs.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Performance vs. Convenience.

### Advanced Concepts

- [**Buffer Management**](./api/framework/memory/buffer-management.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Pooled allocation strategies.
- [**Object Pooling**](./api/framework/memory/object-pooling.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Object recycling strategies.
- [**Sharding & Concurrency**](./concepts/internals/sharding.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Scaling and worker affinity.
- [**Zero-Allocation Path**](./concepts/internals/zero-allocation.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Extreme performance engineering.
- [**Design Trade-offs**](./concepts/internals/design-tradeoffs.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Performance vs latency.

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“ Practical Guides

- [**Guides Overview**](./guides/index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Navigating the available guides.

### Build a Server

- [**Server Boilerplate**](./guides/getting-started/server-boilerplate.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Production-ready starting point.
- [**Implement Packet Handlers**](./guides/application/packet-handlers.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Defining and routing message logic.
- [**Minimal Server Guide**](./guides/networking/minimal-server.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Feature walkthrough.
- [**Production Server Example**](./guides/deployment/production-example.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Real-world implementation.
- [**Project Setup**](./guides/getting-started/project-setup.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Framework bootstrap.
- [**Server Blueprint**](./guides/getting-started/server-blueprint.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Enterprise structure.

### Extend Behavior

- [**Middleware Usage Guide**](./guides/application/middleware-usage.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Adding standard policies.
- [**Custom Middleware**](./guides/extensibility/custom-middleware.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Building your own logic.
- [**Custom Metadata Provider**](./guides/extensibility/metadata-providers.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Attribute-driven logic.

### Networking Patterns

- [**Client Session Guide**](./guides/networking/connecting-clients.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â SDK client session bootstrap.
- [**TCP Patterns Guide**](./guides/networking/tcp-patterns.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Ordered communication.
- [**UDP Server**](./guides/networking/udp-server.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Low-latency datagrams.
- [**UDP Security Guide**](./guides/networking/udp-security.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Secure session bootstrap.

### Operations & Debugging

- [**Production Checklist**](./guides/deployment/production-checklist.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Pre-deployment audit.
- [**Troubleshooting**](./guides/deployment/troubleshooting.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Diagnostic strategies.

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒâ€šÃ‚Â¦ Packages Reference

- [**Packages Map**](./packages/index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Visual dependency graph and overview.
- [**Nalix.Abstractions**](./packages/nalix-abstractions.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Shared contracts.
- [**Nalix.Framework**](./packages/nalix-framework.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Core infrastructure.
- [**Nalix.Logging**](./packages/nalix-logging.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Structured logging.
- [**Nalix.Network**](./packages/nalix-network.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Transport & Listeners.
- [**Nalix.Runtime**](./packages/nalix-runtime.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Dispatch & Middleware.
- [**Nalix.Hosting**](./packages/nalix-hosting.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Host Composition.
- [**Nalix.SDK**](./packages/nalix-sdk.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Client sessions.

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸ÃƒÂ¢Ã¢â€šÂ¬Ã‚ÂºÃƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¯Ãƒâ€šÃ‚Â¸Ãƒâ€šÃ‚Â API Reference

- [**API Overview**](./api/index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Landing page for API docs.

### Analyzers

- [**Analyzers Home**](./api/analyzers/index.md)
- [**Diagnostic Codes**](./api/analyzers/diagnostic-codes.md)
- [**Code Fixes**](./api/analyzers/code-fixes.md)

### Common & Contracts

- [**Control Types**](./api/abstractions/protocols/control-type.md)
- [**Packets**](./api/abstractions/packet-contracts.md)
- [**Connections**](./api/abstractions/connection-contracts.md)
- [**Sessions**](./api/abstractions/session-contracts.md)
- [**Concurrency**](./api/abstractions/concurrency-contracts.md)

### Framework Internals

- [**Directories**](./api/environment/directories.md)
- [**Configuration**](./api/environment/configuration.md)
- [**Instance Manager (DI)**](./api/framework/instance-manager.md)
- [**Task Manager**](./api/framework/task-manager.md)
- [**Snowflake**](./api/framework/snowflake.md)
- [**Clock**](./api/environment/clock.md)
- [**Timing Scope**](./api/environment/timing-scope.md)
- [**Singletons**](./api/framework/singleton-base.md)
- [**Worker Options**](./api/framework/options/worker-options.md)
- [**Recurring Tasks**](./api/framework/options/recurring-options.md)
- [**Frame Model**](./api/codec/packets/frame-model.md)
- [**Serialization Attrs**](./api/abstractions/serialization-attributes.md)
- [**Registry**](./api/codec/packets/packet-registry.md)
- [**Built-in Frames**](./api/codec/packets/built-in-frames.md)
- [**Pooling**](./api/runtime/pooling/packet-pooling.md)
- [**Fragmentation**](./api/codec/packets/fragmentation.md)
- [**IO Extensions**](./api/codec/serialization/reader-writer-and-header-extensions.md)
- [**LZ4**](./api/codec/lz4.md)
- [**Buffer Management**](./api/framework/memory/buffer-management.md)
- [**Object Pooling**](./api/framework/memory/object-pooling.md)
- [**Object Map**](./api/framework/memory/object-map.md)
- [**Typed Object Pools**](./api/framework/memory/typed-object-pools.md)

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
- [**Attributes**](./api/abstractions/packet-attributes.md)
- [**Metadata**](./api/abstractions/packet-metadata.md)
- [**Context**](./api/runtime/routing/packet-context.md)
- [**Dispatcher**](./api/runtime/routing/packet-dispatch.md)
- [**Return Types**](./api/runtime/routing/handler-results.md)
- [**Pipeline**](./api/runtime/middleware/pipeline.md)
- [**Rate Limiters**](./api/runtime/middleware/token-bucket-limiter.md)

### Network Transport

- [**Network Home**](./api/network/index.md)
- [**Protocols**](./api/network/protocol.md)
- [**TCP Listener**](./api/network/tcp-listener.md)
- [**UDP Listener**](./api/network/udp-listener.md)
- [**Connections**](./api/network/connection/connection.md)
- [**Hubs**](./api/network/connection/connection-hub.md)
- [**Timing Wheel**](./api/network/time/timing-wheel.md)
- [**Options**](./api/options/network/options.md)

### SDK (Client)

- [**SDK Home**](./api/sdk/index.md)
- [**TCP Sessions**](./api/sdk/tcp-session.md)
- [**UDP Sessions**](./api/sdk/udp-session.md)
- [**Handshake**](./api/sdk/handshake-extensions.md)
- [**Resumption**](./api/sdk/resume-extensions.md)
- [**Subscriptions**](./api/sdk/subscriptions.md)
- [**Transport Options**](./api/options/sdk/transport-options.md)

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸ÃƒÂ¢Ã¢â€šÂ¬Ã‚ÂºÃƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¯Ãƒâ€šÃ‚Â¸Ãƒâ€šÃ‚Â Developer Tools

- [**Tools Overview**](./guides/tools/index.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Landing page for internal utilities.
- [**Identity Certificate Tool**](./guides/tools/certificate-tool.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Generating X25519 keys.
- [**Packet Visualizer**](./guides/tools/packet-visualizer.md) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Debugging network frames in real-time.

---

## ÃƒÆ’Ã‚Â°Ãƒâ€¦Ã‚Â¸ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒâ€¦Ã‚Â  Benchmarks

- [**Benchmarks Home**](./benchmarks/index.md)
- [**Infrastructure**](./benchmarks/infrastructure.md)
- [**Memory**](./benchmarks/memory.md)
- [**Data Processing**](./benchmarks/data-processing.md)
- [**Security**](./benchmarks/security.md)
- [**Serialization**](./benchmarks/serialization.md)
- [**Identifiers**](./benchmarks/identifiers.md)
