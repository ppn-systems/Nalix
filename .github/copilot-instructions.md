# Copilot Instructions

This repository is the **Nalix** ecosystem. Generated code, docs, and refactors must match the current solution structure and coding style used in `src/` and `tests/`.

---

## 1. Repository Snapshot

- **Language / runtime**: C# 14 on `.NET 10` (`net10.0`)
- **Main solution**: `src/Nalix.sln`
- **Test solution**: `tests/Nalix.Tests.sln`
- **Signing key**: `Nalix.snk` (strong-named assemblies — do not remove)
- **Current projects**:
  - `Nalix.Common`
  - `Nalix.Framework`
  - `Nalix.Logging`
  - `Nalix.Network`
  - `Nalix.SDK`
  - `Nalix.Analyzers`
  - `Nalix.Analyzers.CodeFixes`

> Do not refer to `Nalix.Shared` — that name is outdated.

---

## 2. Project Responsibilities

### `Nalix.Common`
Lowest-level shared dependency. Contains cross-cutting contracts and primitives:
- abstractions and attributes
- diagnostics interfaces and log models
- environment helpers and common exceptions
- identity contracts and primitive types
- middleware metadata, networking contracts
- security enums/contracts, serialization contracts

Keep it **lightweight and broadly reusable**. It must not depend on any other Nalix project.

### `Nalix.Framework`
Builds on `Nalix.Common`. Higher-level foundational utilities:
- configuration and options
- data frames and packet registry support
- dependency injection / instance management
- identifiers (`Snowflake`)
- LZ4 compression
- memory pools and object/buffer helpers
- random / CSPRNG helpers
- security engines and hashing
- serialization implementation
- task orchestration and timing

### `Nalix.Logging`
Builds on `Nalix.Common` and `Nalix.Framework`:
- `NLogix` logging facade
- logging engine and distributor
- console / file / batch sinks
- internal formatters and pooling helpers
- logging configuration objects

### `Nalix.Network`
Builds on `Nalix.Common` and `Nalix.Framework`:
- TCP/UDP listeners
- connection and hub management
- protocol lifecycle / metrics / public methods
- middleware pipelines
- routing and dispatch infrastructure
- throttling and timing systems

### `Nalix.SDK`
Builds on `Nalix.Common` and `Nalix.Framework`:
- transport / session clients
- SDK configuration
- client-facing extensions
- dispatcher abstractions

---

## 3. Dependency Rules

```
Nalix.Common      ← no Nalix dependencies
Nalix.Framework   ← Nalix.Common only
Nalix.Logging     ← Nalix.Common, Nalix.Framework
Nalix.Network     ← Nalix.Common, Nalix.Framework
Nalix.SDK         ← Nalix.Common, Nalix.Framework
```

Do not introduce circular references. Do not add references that violate the above graph.

---

## 4. Coding Style

- Use **file-scoped namespaces**.
- Keep **nullable reference types** enabled (`#nullable enable` is project-wide).
- Prefer explicit, readable control flow over clever abstractions.
- **Naming conventions**:
  - Private instance fields: `_fieldName`
  - Private static fields: match surrounding file (`s_fieldName` or project-specific)
  - Interfaces: `IType`
  - Async methods: suffix `Async`
- Prefer `sealed` for classes unless inheritance is a genuine requirement.
- Prefer `readonly struct` and `readonly` members where applicable.
- Keep **XML documentation** on all public APIs (the build generates XML docs).
- Match the style already present in the target file before introducing a new pattern.

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

Test stack:
- **xUnit** — test framework
- **FluentAssertions** — assertion style
- **Moq** — mocking in framework tests where needed
- **Xunit.SkippableFact** — environment-dependent coverage

When adding tests:
- Place them in the matching project under `tests/`.
- Follow the naming style already used in that area.
- Cover happy path, invalid input, and edge cases.
- For serialization, cryptography, networking, memory, and timing code, include regression-focused tests.

---

## 8. CI/CD Awareness

The repository uses GitHub Actions. Workflows live in `.github/workflows/`:

| File                             | Purpose                                              |
|----------------------------------|------------------------------------------------------|
| `ci-linux.yml`                   | Build & test on Ubuntu via `_build.yml`              |
| `ci-windows.yml`                 | Build & test on Windows via `_build.yml`             |
| `_build.yml`                     | Reusable template: restore → build → test → publish  |
| `_codeql.yml`                    | CodeQL security analysis (C#, scheduled + PR)        |
| `docs.yml`                       | MkDocs build and deploy to GitHub Pages              |
| `release-please.yml`             | Bump `.csproj` versions from release-please manifest |
| `community-welcome.yml`          | Greet first-time contributors                        |
| `community-stale.yml`            | Mark and close stale issues/PRs                      |
| `security-dependency-review.yml` | Scan NuGet deps for CVEs on every PR                 |
| `repo-label-sync.yml`            | Sync labels from `.github/labels.yml`                |

**Label gate**: PRs labelled `documentation` skip build and CodeQL workflows — they only trigger `docs.yml`. Do not remove or rename this label.

**Build configuration**: all CI builds use `--configuration Release`. Do not suggest `Debug` builds in CI context.

---

## 9. Documentation Alignment

When generating README text, XML docs, comments, or architecture notes, use the current package names and responsibilities described above:
- `Common` = contracts / primitives / shared foundations
- `Framework` = foundational runtime utilities
- `Logging` = logging subsystem
- `Network` = networking runtime
- `SDK` = client-side / session-facing API layer