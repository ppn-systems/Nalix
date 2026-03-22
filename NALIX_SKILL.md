# 🚀 Nalix Framework AI Skills Hub

Welcome to the **Nalix AI Skills** repository. This is a modular knowledge base designed to provide AI coding assistants with deep technical context, architectural constraints, and high-performance coding patterns for the Nalix networking framework.

---

## 🏗️ Architecture & Modules

Nalix is organized into several modular layers. Follow the links below for deep-dive technical guidance:

1.  **[Serialization & Layout](.skill/SKILL_SERIALIZATION_DEEP.md)**: `LiteSerializer`, binary protocols, and `[SerializeOrder]`.
2.  **[Security & Identity](.skill/SKILL_SECURITY_INTERNALS.md)**: Encryption (X25519, ChaCha20), Anti-replay, and Session Resumption.
3.  **[Performance & Memory](.skill/SKILL_PERFORMANCE_TUNING.md)**: Zero-allocation mandates, pooling (`ObjectPool`, `Slab`), and `Span` usage.
4.  **[Runtime & Pipeline](.skill/SKILL_RUNTIME_PIPELINE.md)**: Middleware, Dispatching, and Handler compilation.
5.  **[SDK & Client Usage](.skill/SKILL_SDK_USAGE.md)**: `TransportSession`, Request/Response patterns, and Transport sessions.
6.  **[Debugging & Diagnostics](.skill/SKILL_DEBUGGING.md)**: Logging, metrics, and troubleshooting common issues.
7.  **[Analyzer Development](.skill/SKILL_ANALYZER_DEVELOPMENT.md)**: Writing and maintaining Roslyn analyzers for Nalix.
8.  **[Hosting & Bootstrapping](.skill/SKILL_HOSTING_BOOTSTRAP.md)**: `NetworkApplicationBuilder`, DI, and lifecycle management.
9.  **[Logging Infrastructure](.skill/SKILL_LOGGING_INFRASTRUCTURE.md)**: High-performance async logging and sinks.
10. **[Runtime Compilation](.skill/SKILL_RUNTIME_COMPILATION.md)**: Deep dive into handler JIT/IL generation.
11. **[Connection Hub & Sharding](.skill/SKILL_CONNECTION_HUB_SHARDING.md)**: High-concurrency connection management and broadcasting.
12. **[Session Persistence](.skill/SKILL_SESSION_PERSISTENCE.md)**: ISessionStore, state hydration, and Zero-RTT resumption.
13. **[Middleware Development](.skill/SKILL_MIDDLEWARE_DEVELOPMENT.md)**: Creating custom packet-level middlewares.
14. **[Transport & Sockets](.skill/SKILL_TRANSPORT_INTERNALS.md)**: TCP/UDP framing, socket tuning, and low-level I/O.
15. **[Core Primitives & Identity](.skill/SKILL_CORE_PRIMITIVES.md)**: `UInt56`, `Bytes32`, and high-performance ID systems (`Snowflake`).
16. **[Advanced DI (InstanceManager)](.skill/SKILL_ADVANCED_DI_INJECTION.md)**: Internal DI engine, GenericSlot caching, and Lockdown.
17. **[Traffic Control & Guards](.skill/SKILL_TRAFFIC_CONTROL.md)**: Rate limiting, Throttling, and DDoS protection guards.
18. **[CodeFix Development](.skill/SKILL_CODEFIX_DEVELOPMENT.md)**: Developing Roslyn CodeFixes for automated error correction.
19. **[Benchmarking Standards](.skill/SKILL_BENCHMARKING_STANDARDS.md)**: Using BenchmarkDotNet for zero-allocation validation.
20. **[Protocol Design & Versioning](.skill/SKILL_PROTOCOL_DESIGN.md)**: Designing extensible and secure network protocols.
21. **[Configuration System](.skill/SKILL_CONFIGURATION_SYSTEM.md)**: `ConfigurationManager`, INI binding, and Hot-reloading.
22. **[Packet Registry Internals](.skill/SKILL_PACKET_REGISTRY_INTERNALS.md)**: Auto-discovery, opcode mapping, and static dispatch.
23. **[Error Handling Policy](.skill/SKILL_ERROR_HANDLING_POLICY.md)**: `ProtocolReason`, error propagation, and recovery patterns.
24. **[Protocol Abstraction](.skill/SKILL_PROTOCOL_ABSTRACTION.md)**: Implementing custom `IProtocol` and transport bridging.
25. **[Dispatch Backpressure](.skill/SKILL_DISPATCH_BACKPRESSURE.md)**: Managing queue limits, priorities, and saturation.
26. **[Poolable Objects](.skill/SKILL_POOLABLE_OBJECTS.md)**: `IPoolable` lifecycle and zero-allocation recycling.
27. **[SDK Session Lifecycle](.skill/SKILL_SDK_SESSION_LIFECYCLE.md)**: TransportSession state machine and auto-reconnect.
28. **[SDK Request/Response](.skill/SKILL_SDK_REQUEST_RESPONSE.md)**: `RequestAsync`, retries, and timeouts in client operations.
29. **[SDK Event Subscriptions](.skill/SKILL_SDK_EVENT_SUBSCRIPTIONS.md)**: Granular packet observation and reactive patterns.
30. **[SDK Security & Handshake](.skill/SKILL_SDK_HANDSHAKE_SECURITY.md)**: Client-side X25519, Cipher management, and Zero-RTT.
31. **[SDK Utility Extensions](.skill/SKILL_SDK_UTILITY_EXTENSIONS.md)**: Ping, TimeSync, and control signal helpers.
32. **[SDK Development Guidelines](.skill/SKILL_SDK_DEVELOPMENT_GUIDELINES.md)**: Best practices, threading hygiene, and resilience commandments.

---

## 📜 Global Rules of Engagement

- **Zero-Allocation:** This is non-negotiable for hot paths. Always check for `new` allocations.
- **Span/Memory Over Arrays:** Prefer `ReadOnlySpan<byte>` and `Memory<byte>` for all buffer operations.
- **Analyzer Compliance:** Code MUST pass all `NALIX-XXX` diagnostic rules.
- **Attribute-Driven:** Use `[PacketOpcode]`, `[SerializeOrder]`, and `[PacketController]` to define behavior.
