# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Changed

- Merged `Nalix.Network.Pipeline` into `Nalix.Runtime` in documentation to reflect codebase consolidation.
- Updated all API references and guides to use consolidated `Nalix.Runtime` namespaces and source paths.

### Planned

- WebSocket transport support.
- Packet metadata reflection caching.
- Runtime diagnostics dashboard.

---

## [12.0.7] — 2026-04-10

### Added

- Multi-port support for TCP/UDP listeners in `Nalix.Network.Hosting`.
- Background service lifecycle management via `IActivatableAsync` in `NetworkApplication`.
- Improved documentation with better navigation and fixed cross-links.

### Fixed

- Resolved double-disposal bugs in `BufferLease` management within `FrameReader` and `UdpSession`.
- Corrected documentation warnings and broken links across the API reference.

---

## [11.8.0] — 2026-03-24

### Added

- `ConnectionHub` now emits telemetry for shard count, anonymous queue depth, and capacity-limit events for runtime monitoring.
- `PacketDispatchChannel` diagnostics report dispatch loops, semaphore health, and middleware correlations.
- UDP listener authentication tightened with timestamp replay checks, Poly1305 MAC validation, and safer connection/password handling.
- Throttling utilities (`ConnectionLimiter`, `TokenBucketLimiter`, `PolicyRateLimiter`) documented as part of this release.

### Documentation

- Regenerated `docs/README.md` and the changelog to highlight metadata from `<Description>` / `<PackageReleaseNotes>` CDATA blocks in `.csproj` files.
- Updated module summaries and release notes to reflect the latest ConnectionHub / dispatch / UDP changes.

---

## [11.4.0] — 2026-03-15

### Documentation

- **DOCUMENTATION.md:** Added `Nalix.Common` and `Nalix.SDK` to the documentation index.
- **docs/README.md:** Fixed Framework doc links (`Configuration`, `Csprng`, `Snowflake`, `TaskManager`) to use `Nalix.Framework/` paths; added Protocol, Connection & IConnection, and PacketContext to Detailed Module Docs.
- **New docs:** `docs/Nalix.Network/Connections/Connection.md`, `docs/Nalix.Network/Routing/PacketContext.md`, `docs/Nalix.Common/README.md`, `docs/Nalix.SDK/README.md`.

---

## [6.8.0] — 2025-10-22

### Added

- Deterministic build and SourceLink support for CI/CD traceability.
- Full set of modular packages: `Nalix.Common`, `Nalix.Framework`, `Nalix.Logging`, `Nalix.Network`, `Nalix.Shared`, and `Nalix.SDK`.
- High-performance AEAD cryptography (ChaCha20-Poly1305, XTEA-Poly1305, Speck-Poly1305).
- Memory subsystem and LZ4 compression engine.
- Asynchronous and batched logging sinks (console, file, email, channel).
- Network connection hub with middleware and throttling support.

### Changed

- Refactored core namespaces for better modular separation.
- Improved build pipeline and package metadata for NuGet.
- Updated documentation and repository structure under `/docs`.

### Fixed

- Resolved race conditions in internal logging channel.
- Improved task cancellation handling in `TaskManager`.
- Corrected minor path detection issues in environment utilities.

---

## [6.7.0] — 2025-09-01

### Added

- Initial experimental builds for Framework and Network modules.
- Core cryptography engine with AEAD integration.
- Early benchmarking setup with BenchmarkDotNet.

