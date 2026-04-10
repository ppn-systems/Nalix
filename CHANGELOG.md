# 📜 Changelog

All notable changes to this project will be documented in this file.  
This project follows [Semantic Versioning](https://semver.org/) guidelines.

---

# [12.0.7] - 2026-04-10

### Added

- Multi-port support for TCP/UDP listeners in `Nalix.Network.Hosting`.
- Background service lifecycle management via `IActivatableAsync` in `NetworkApplication`.
- Improved documentation with better navigation and fixed cross-links.

### Fixed

- Resolved double-disposal bugs in `BufferLease` management within `FrameReader` and `UdpSession`.
- Corrected documentation warnings and broken links across the API reference.

# [11.8.0] - 2026-03-24

### Added

- ConnectionHub now emits telemetry for shard count, anonymous queue depth, and capacity-limit events so operators can monitor throttling at runtime.
- PacketDispatchChannel diagnostics report dispatch loops, semaphore health, and middleware correlations alongside the documentation so the dispatcher behavior is visible in the docs.
- UDP listener authentication tightened with timestamp replay checks, Poly1305 MAC validation, and safer connection/password handling; throttling utilities (ConnectionLimiter, TokenBucketLimiter, PolicyRateLimiter) were documented as part of this release.

### Documentation

- Regenerated `docs/README.md` and the changelog so they highlight the metadata now emitted from the `<Description>`/`<PackageReleaseNotes>` CDATA blocks embedded in each `.csproj`; this lets the documentation consume the same text that shows up on NuGet.
- Updated module summaries and release notes to call out the latest ConnectionHub/dispatch/UDP changes to keep doc readers in sync with the new metadata.

---

## [11.4.0] - 2026-03-15

### Documentation

- **DOCUMENTATION.md:** Added Nalix.Common and Nalix.SDK to the documentation index.
- **docs/README.md:** Fixed Framework doc links (Configuration, Csprng, Snowflake, TaskManager) to use `Nalix.Framework/` paths; added Protocol, Connection & IConnection, and PacketContext to Detailed Module Docs; fixed Send-from-handler link (removed broken anchor).
- **New docs:** `docs/Nalix.Network/Connections/Connection.md` (Connection & IConnection), `docs/Nalix.Network/Routing/PacketContext.md` (PacketContext), `docs/Nalix.Common/README.md` (Common overview), `docs/Nalix.SDK/README.md` (SDK overview).

---

## [6.8.0] - 2025-10-22

### Added (6.8.0)

- Introduced deterministic build and SourceLink support for CI/CD traceability.
- Added full set of modular packages:  
  `Nalix.Common`, `Nalix.Framework`, `Nalix.Logging`, `Nalix.Network`, `Nalix.Shared`, and `Nalix.SDK`.
- Introduced high-performance AEAD cryptography (ChaCha20-Poly1305, XTEA-Poly1305, Speck-Poly1305).
- Implemented Memory subsystem and LZ4 compression engine.
- Added asynchronous and batched logging sinks (console, file, email, channel).
- Introduced network connection hub with middleware and throttling support.

### Changed

- Refactored core namespaces for better modular separation.
- Improved build pipeline and package metadata for NuGet.
- Updated documentation and repository structure under `/docs`.

### Fixed

- Resolved race conditions in internal logging channel.
- Improved task cancellation handling in TaskManager.
- Corrected minor path detection issues in environment utilities.

---

## [6.7.0] - 2025-09-01

### Added (6.7.0)

- Initial experimental builds for Framework and Network modules.
- Core cryptography engine with AEAD integration.
- Early benchmarking setup with BenchmarkDotNet.

---

## [Unreleased]

### Planned

- Add WebSocket transport support.
- Extend Packet metadata reflection caching.
- Provide full runtime diagnostics dashboard.
