# Copilot Instructions

This repository is the **Nalix** ecosystem. Generated code, docs, and refactors must match the current solution structure and coding style used in `src/` and `tests/`.

## 1. Repository Snapshot

- Language/runtime: C# 14 on `.NET 10` (`net10.0`)
- Main solution: `src/Nalix.sln`
- Test solution: `tests/Nalix.Tests.sln`
- Current packages/projects:
  - `Nalix.Common`
  - `Nalix.Framework`
  - `Nalix.Logging`
  - `Nalix.Network`
  - `Nalix.SDK`

Do not refer to `Nalix.Shared`; that is outdated for this repository state.

## 2. Project Responsibilities

### `Nalix.Common`
Contains cross-cutting contracts and low-level shared types, including:
- abstractions and attributes
- diagnostics interfaces and log models
- environment helpers
- common exceptions
- identity contracts and primitive types
- middleware metadata
- networking contracts
- security enums/contracts
- serialization contracts

`Nalix.Common` is the lowest-level shared dependency. Keep it lightweight and broadly reusable.

### `Nalix.Framework`
Builds on `Nalix.Common` and contains higher-level foundational features, including:
- configuration
- data frames and packet registry support
- dependency injection / instance management
- identifiers such as `Snowflake`
- LZ4
- memory pools and object/buffer helpers
- options
- random / CSPRNG helpers
- security engines and hashing
- serialization implementation
- task orchestration and timing

### `Nalix.Logging`
Builds on `Nalix.Common` and `Nalix.Framework` and contains:
- `NLogix` logging facade
- logging engine and distributor
- console/file/batch sinks
- internal formatters and pooling helpers
- logging configuration objects

### `Nalix.Network`
Builds on `Nalix.Common` and `Nalix.Framework` and contains:
- TCP/UDP listeners
- connection and hub management
- protocol lifecycle/metrics/public methods
- middleware pipelines
- routing and dispatch infrastructure
- throttling and timing systems

### `Nalix.SDK`
Builds on `Nalix.Common` and `Nalix.Framework` and contains:
- transport/session clients
- SDK configuration
- client-facing extensions
- dispatcher abstractions

## 3. Dependency Rules

Respect the current reference direction:
- `Nalix.Common` must not depend on other Nalix projects.
- `Nalix.Framework` may depend only on `Nalix.Common`.
- `Nalix.Logging` may depend on `Nalix.Common` and `Nalix.Framework`.
- `Nalix.Network` may depend on `Nalix.Common` and `Nalix.Framework`.
- `Nalix.SDK` may depend on `Nalix.Common` and `Nalix.Framework`.

Do not introduce circular references.

## 4. Coding Style

- Use file-scoped namespaces.
- Keep nullable reference types enabled.
- Prefer explicit, readable control flow over clever abstractions.
- Follow existing naming patterns:
  - private fields: `_fieldName`
  - static private fields: existing code may use `s_fieldName` or project-specific names; match the surrounding file
  - interfaces: `IType`
  - async methods: suffix `Async`
- Prefer `sealed` for classes unless extension is a real requirement.
- Prefer `readonly struct` and `readonly` members where appropriate.
- Keep XML documentation on public APIs; the project generates XML docs.
- Match the style already present in the target file before introducing a new pattern.

## 5. Performance Expectations

Nalix is performance-oriented. In hot paths:
- prefer `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and pooled buffers where appropriate
- avoid unnecessary allocations
- avoid LINQ when a simple loop is clearer and cheaper
- avoid boxing and unnecessary virtual/interface dispatch
- use `Try*` APIs when failure is expected
- prefer non-throwing fast paths for parsing, serialization, encoding, and transport work
- use inlining or low-level attributes only when justified by surrounding code patterns

Do not add complexity in the name of optimization unless it fits the local design and measurable intent of the module.

## 6. Security Expectations

- Validate all external input lengths, ranges, and boundaries before processing.
- Never log secrets, keys, tokens, or raw sensitive payloads.
- Reuse existing Nalix security primitives instead of inventing new cryptographic designs.
- Keep nonce/IV/key handling explicit and safe.
- Prefer constant-time or hardened existing implementations when dealing with authentication and cryptography.

## 7. Testing Expectations

Current tests use:
- `xUnit`
- `FluentAssertions`
- `Moq` in framework tests where needed
- `Xunit.SkippableFact` where environment-dependent coverage is necessary

When adding tests:
- place them in the matching test project under `tests/`
- follow the naming style already used in that area
- cover happy path, invalid input, and edge cases
- for serialization, cryptography, networking, memory, and timing code, include regression-focused tests

## 8. Build and Documentation Alignment

- Target `net10.0`.
- Keep code compatible with deterministic builds and XML documentation generation.
- Prefer documentation that describes the current architecture:
  - `Common` = contracts/primitives/shared foundations
  - `Framework` = foundational runtime utilities
  - `Logging` = logging subsystem
  - `Network` = networking runtime
  - `SDK` = client-side/session-facing API layer

When generating README text, comments, XML docs, or architecture notes, use the current package names and responsibilities above.
