# Concepts Overview

Nalix architecture is built on a layered system that separates transport, dispatch, and application logic. Understanding these core concepts is key to building high-performance, maintainable networking systems.

## 🧱 The Fundamentals

- :fontawesome-solid-sitemap: [**Architecture**](./architecture.md) — The 4-layer system: Common, Framework, Runtime, and Network.
- :fontawesome-solid-shapes: [**Choose the Right Building Block**](./choose-the-right-building-block.md) — A guide to selecting between Middleware, Protocols, and Handlers.
- :fontawesome-solid-book-atlas: [**Glossary**](./glossary.md) — Terminology and definitions used throughout the documentation.

## 🔄 Packet Execution Path

- :fontawesome-solid-envelope: [**Packet System**](./packet-system.md) — Serialization, wire format, and binary layout.
- :fontawesome-solid-repeat: [**Packet Lifecycle**](./packet-lifecycle.md) — The step-by-step journey of a packet from socket to handler.
- :fontawesome-solid-filter: [**Middleware**](./middleware.md) — Intercepting and modifying traffic before it reaches your logic.
- :fontawesome-solid-shield-halved: [**Security Model**](./security-model.md) — Authentication, Handshakes, and Encryption.

## ⚙️ Engine Internals

- :fontawesome-solid-bolt: [**Real-time Engine**](./real-time.md) — How the tick-based execution and timing wheels work.
- :fontawesome-solid-sliders: [**Configuration and Runtime**](./configuration-and-runtime.md) — Managing options, services, and the `InstanceManager`.
- :fontawesome-solid-gauge-high: [**Performance Optimizations**](./performance-optimizations.md) — Zero-allocation data paths and shard-aware dispatch.
- :fontawesome-solid-triangle-exclamation: [**Error Reporting**](./error-reporting.md) — Runtime signaling and protocol violations.

## 🎓 Advanced Reliability

!!! warning "Senior Developers Only"
    These topics cover deep technical contracts and failure models. Skip if you are just getting started.

- :fontawesome-solid-microchip: [**Reliability & Internals**](./advanced/reliability-model.md) — The production confidence layer.
- :fontawesome-solid-lock: [**Guarantees & Invariants**](./advanced/guarantees-and-invariants.md) — Ordering and Concurrency contracts.
- :fontawesome-solid-biohazard: [**Failure Model**](./advanced/failure-model.md) — Resilience and observable behavior.
- :fontawesome-solid-balance-scale: [**Design Tradeoffs**](./advanced/design-tradeoffs.md) — Why we chose performance over convenience.
