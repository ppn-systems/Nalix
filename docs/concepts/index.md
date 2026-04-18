# Concepts Overview

Nalix architecture is built on a layered system that separates transport, dispatch, and application logic. Understanding these core concepts is key to building high-performance, maintainable networking systems.

## 🧱 The Fundamentals

- :fontawesome-solid-sitemap: [**Architecture**](./fundamentals/architecture.md) — The 4-layer system: Common, Framework, Runtime, and Network.
- :fontawesome-solid-shapes: [**Selecting Building Blocks**](./runtime/building-blocks.md) — A guide to selecting between Middleware, Protocols, and Handlers.
- :fontawesome-solid-book-atlas: [**Glossary**](./glossary.md) — Terminology and definitions used throughout the documentation.

## 🔄 Packet Execution Path

- :fontawesome-solid-envelope: [**Packet System**](./fundamentals/packet-system.md) — Serialization, wire format, and binary layout.
- :fontawesome-solid-repeat: [**Packet Lifecycle**](./fundamentals/packet-lifecycle.md) — The step-by-step journey of a packet from socket to handler.
- :fontawesome-solid-filter: [**Middleware**](./runtime/middleware-pipeline.md) — Intercepting and modifying traffic before it reaches your logic.
- :fontawesome-solid-shield-halved: [**Security Architecture**](./security/security-architecture.md) — Authentication, Handshakes, and Encryption.

## ⚙️ Engine Internals

- :fontawesome-solid-bolt: [**Real-time Engine**](./runtime/real-time-engine.md) — How the tick-based execution and timing wheels work.
- :fontawesome-solid-sliders: [**Configuration System**](./runtime/configuration.md) — Managing options, services, and the `InstanceManager`.
- :fontawesome-solid-gauge-high: [**Performance Optimizations**](./internals/performance-optimizations.md) — Zero-allocation data paths and shard-aware dispatch.
- :fontawesome-solid-triangle-exclamation: [**Errors and Diagnostics**](./fundamentals/errors-and-diagnostics.md) — Runtime signaling and protocol violations.

## 🎓 Advanced Internals

!!! warning "Senior Developers Only"
    These topics cover deep technical contracts and failure models. Skip if you are just getting started.

- :fontawesome-solid-microchip: [**Reliability Model**](./internals/reliability.md) — The production confidence layer.
- :fontawesome-solid-lock: [**System Guarantees**](./internals/guarantees-and-invariants.md) — Ordering and Concurrency contracts.
- :fontawesome-solid-biohazard: [**Failure Model**](./internals/failure-handling.md) — Resilience and observable behavior.
- :fontawesome-solid-balance-scale: [**Design Tradeoffs**](./internals/design-tradeoffs.md) — Why we chose performance over convenience.
