# 📜 Changelog

All notable changes to this project will be documented in this file.  
This project follows [Semantic Versioning](https://semver.org/) guidelines.

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
