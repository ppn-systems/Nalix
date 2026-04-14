# Copilot Instructions

This repository is the **Nalix** ecosystem. Generated code, docs, and refactors must match the current solution structure and coding style used in `src/` and `tests/`.

---

## 1. Repository Snapshot

| Property | Value |
| :--- | :--- |
| Language / Runtime | C# 14 on `.NET 10` (`net10.0`) |
| Main Solution | `src/Nalix.sln` |
| Test Solution | `tests/Nalix.Tests.sln` |
| Signing Key | `Nalix.snk` (strong-named assemblies — do not remove) |

**Current projects:**

- `Nalix.Common`
- `Nalix.Framework`
- `Nalix.Logging`
- `Nalix.Runtime`
- `Nalix.Network`
- `Nalix.Network.Pipeline`
- `Nalix.Network.Hosting`
- `Nalix.SDK`
- `Nalix.Analyzers`
- `Nalix.Analyzers.CodeFixes`

> Do not refer to `Nalix.Shared` — that name is outdated and has been removed.

---

## 2. Project Responsibilities

### `Nalix.Common`

Lowest-level shared dependency. Contains cross-cutting contracts and primitives:

- Abstractions and attributes
- Diagnostics interfaces and log models
- Environment helpers and common exceptions
- Identity contracts and primitive types
- Middleware metadata, networking contracts
- Security enums/contracts, serialization contracts

Keep it **lightweight and broadly reusable**. It must not depend on any other Nalix project.

### `Nalix.Framework`

Builds on `Nalix.Common`. Higher-level foundational utilities:

- Configuration and options
- Data frames and packet registry support
- Dependency injection / instance management
- Identifiers (`Snowflake`)
- LZ4 compression
- Memory pools and object/buffer helpers
- Random / CSPRNG helpers
- Security engines and hashing
- Serialization implementation
- Task orchestration and timing

### `Nalix.Logging`

Builds on `Nalix.Common` and `Nalix.Framework`:

- `NLogix` logging facade
- Logging engine and distributor
- Console / file / batch sinks
- Internal formatters and pooling helpers
- Logging configuration objects

### `Nalix.Runtime`

Builds on `Nalix.Common` and `Nalix.Framework`:

- Packet processing and middleware infrastructure
- Middleware pipeline orchestration
- Packet dispatching channels
- Execution management for message-oriented traffic

### `Nalix.Network`

Builds on `Nalix.Common` and `Nalix.Framework`:

- TCP/UDP listeners and connection management
- Protocol lifecycle orchestration
- Routing and packet dispatch infrastructure
- Adaptive throttling and timing systems

### `Nalix.Network.Pipeline`

Builds on `Nalix.Common` and `Nalix.Framework`:

- Reusable middleware for permissions and rate limiting
- Traffic shaping and concurrency gates
- Timekeeping and clock coordination primitives

### `Nalix.Network.Hosting`

Builds on `Nalix.Common`, `Nalix.Framework`, `Nalix.Network`, and `Nalix.Runtime`:

- Microsoft-style host and builder APIs
- Simplified bootstrapping for packet registry and dispatch
- TCP listener lifecycle management

### `Nalix.SDK`

Builds on `Nalix.Common` and `Nalix.Framework`:

- Transport / session clients
- SDK configuration
- Client-facing extensions
- Dispatcher abstractions

---

## 3. Dependency Rules

```text
Level 0 : Nalix.Common, Nalix.Analyzers
Level 1 : Nalix.Framework            → Common
Level 2 : Nalix.Logging              → Common, Framework
           Nalix.Runtime              → Common, Framework
           Nalix.Network              → Common, Framework
           Nalix.Network.Pipeline     → Common, Framework
           Nalix.SDK                  → Common, Framework
Level 3 : Nalix.Network.Hosting      → Common, Framework, Network, Runtime
```

Do not introduce circular references. Do not add references that violate the above graph.

---

## 4. Coding Style

| Rule | Guideline |
| :--- | :--- |
| Namespaces | File-scoped only |
| Nullable | Enabled project-wide (`#nullable enable`) — never disable |
| Control flow | Prefer explicit, readable flow over clever abstractions |
| Private instance fields | `_fieldName` |
| Private static fields | Match surrounding file (`s_fieldName` or project-specific) |
| Interfaces | `IType` |
| Async methods | Suffix `Async` |
| Classes | Prefer `sealed` unless inheritance is a genuine requirement |
| Structs | Prefer `readonly struct` and `readonly` members where applicable |
| XML docs | Required on all public APIs (the build generates XML docs) |
| Existing style | Match the style in the target file before introducing a new pattern |

---

## 5. Performance Expectations

Nalix is performance-oriented. In hot paths:

- Prefer `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and pooled buffers.
- Avoid unnecessary allocations, boxing, and virtual/interface dispatch.
- Avoid LINQ when a simple loop is clearer and cheaper.
- Use `Try*` APIs when failure is expected.
- Prefer non-throwing fast paths for parsing, serialization, encoding, and transport.
- Use inlining or low-level attributes only when justified by surrounding code patterns.

Do not add complexity in the name of optimization unless it fits the local design and measurable intent of the module.

---

## 6. Security Expectations

- Validate all external input lengths, ranges, and boundaries before processing.
- Never log secrets, keys, tokens, or raw sensitive payloads.
- Reuse existing Nalix security primitives — do not invent new cryptographic designs.
- Keep nonce / IV / key handling explicit and safe.
- Prefer constant-time or hardened existing implementations for authentication and cryptography.

---

## 7. Testing Expectations

| Tool | Purpose |
| :--- | :--- |
| **xUnit** | Test framework |
| **FluentAssertions** | Assertion style |
| **Moq** | Mocking in framework tests where needed |
| **Xunit.SkippableFact** | Environment-dependent coverage |

When adding tests:

- Place them in the matching project under `tests/`.
- Follow the naming style already used in that area.
- Cover happy path, invalid input, and edge cases.
- For serialization, cryptography, networking, memory, and timing code, include regression-focused tests.

---

## 8. CI/CD Awareness

The repository uses GitHub Actions. Workflows live in `.github/workflows/`:

| Workflow | Purpose |
| :--- | :--- |
| `ci-linux.yml` | Build & test on Ubuntu via `_build.yml` |
| `ci-windows.yml` | Build & test on Windows via `_build.yml` |
| `_build.yml` | Reusable template: restore → build → test → publish |
| `benchmark.yml` | Run BenchmarkDotNet on `master`, upload artifacts, compare against previous run |
| `_codeql.yml` | CodeQL security analysis (C#, scheduled + PR) |
| `docs.yml` | MkDocs build and deploy to GitHub Pages |
| `community-welcome.yml` | Greet first-time contributors |
| `community-stale.yml` | Mark and close stale issues/PRs |
| `security-dependency-review.yml` | Scan NuGet deps for CVEs on every PR |
| `repo-label-sync.yml` | Sync labels from `.github/labels.yml` |
| `nuget.yml` | Automated NuGet packaging and versioning |

Release/versioning rules are documented in `CONTRIBUTING.md` and follow Conventional Commits (`fix` = patch, `feat` = minor, breaking changes = major).

**Label gate:** PRs labelled `documentation` skip build and CodeQL workflows — they only trigger `docs.yml`. Do not remove or rename this label.

**Build configuration:** All CI builds use `--configuration Release`. Do not suggest `Debug` builds in CI context.

---

## 9. Documentation Alignment

When generating README text, XML docs, comments, or architecture notes, use the current package names and responsibilities:

| Package | Role |
| :--- | :--- |
| `Common` | Contracts / primitives / shared foundations |
| `Framework` | Foundational runtime utilities |
| `Logging` | Logging subsystem |
| `Runtime` | Packet processing / middleware core |
| `Network` | Networking transport runtime |
| `Network.Hosting` | High-level host/builder support |
| `Network.Pipeline` | Reusable middleware components |
| `SDK` | Client-side / session-facing API layer |
