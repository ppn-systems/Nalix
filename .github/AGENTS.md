# Agents Instructions

This repository is the **Nalix** ecosystem — a performance-oriented C# networking and framework library targeting `.NET 10`.

---

## 1. Repository Layout

```plaintext
Nalix/
├── src/
│   ├── Nalix.sln
│   ├── Nalix.Common/
│   ├── Nalix.Framework/
│   ├── Nalix.Logging/
│   ├── Nalix.Network/
│   └── Nalix.SDK/
├── tests/
│   ├── Nalix.Tests.sln
│   └── (mirror of src/ project structure)
├── docs/
└── .github/
    ├── workflows/
    ├── ISSUE_TEMPLATE/
    ├── PULL_REQUEST_TEMPLATE.md
    ├── labels.yml
    ├── copilot-instructions.md
    └── AGENTS.md          ← this file
```

> Do not refer to `Nalix.Shared` — that name is outdated.

---

## 2. Build & Test Commands

Always verify changes compile and tests pass before finishing a task.

```bash
# Restore
dotnet restore src/Nalix.sln
dotnet restore tests/Nalix.Tests.sln

# Build
dotnet build src/Nalix.sln --configuration Release --no-restore
dotnet build tests/Nalix.Tests.sln --configuration Release --no-restore

# Test
dotnet test tests/Nalix.Tests.sln --configuration Release --no-restore
```

For a single project:

```bash
dotnet build src/Nalix.Network/Nalix.Network.csproj --configuration Release
dotnet test tests/Nalix.Network.Tests/Nalix.Network.Tests.csproj --configuration Release
```

---

## 3. Project Responsibilities & Dependency Graph

```plaintext
Nalix.Common      ← no Nalix dependencies (lowest-level)
Nalix.Framework   ← Nalix.Common only
Nalix.Logging     ← Nalix.Common, Nalix.Framework
Nalix.Network     ← Nalix.Common, Nalix.Framework
Nalix.SDK         ← Nalix.Common, Nalix.Framework
```

**Never introduce a dependency that violates this graph. Never create circular references.**

### `Nalix.Common`

Cross-cutting contracts and primitives: abstractions, attributes, diagnostics interfaces, environment helpers, common exceptions, identity contracts, middleware metadata, networking contracts, security enums, serialization contracts.

### `Nalix.Framework`

Foundational runtime utilities: configuration, data frames, packet registry, DI/instance management, `Snowflake` identifiers, LZ4, memory pools, CSPRNG, security engines, hashing, serialization, task orchestration, timing.

### `Nalix.Logging`

Logging subsystem: `NLogix` facade, logging engine, distributor, console/file/batch sinks, formatters, pooling helpers, logging configuration.

### `Nalix.Network`

Networking runtime: TCP/UDP listeners, connection and hub management, protocol lifecycle, middleware pipelines, routing, dispatch, throttling, timing.

### `Nalix.SDK`

Client-facing API layer: transport/session clients, SDK configuration, client extensions, dispatcher abstractions.

---

## 4. Coding Standards

- **Language**: C# 14, `net10.0`
- **Namespaces**: file-scoped only
- **Nullable**: enabled project-wide — never disable
- **Classes**: prefer `sealed` unless inheritance is a genuine requirement
- **Structs**: prefer `readonly struct` and `readonly` members where applicable
- **Fields**: private instance → `_fieldName`, private static → match surrounding file (`s_fieldName` or project-specific)
- **Interfaces**: `IType`
- **Async**: suffix `Async` on all async methods
- **XML docs**: required on all public APIs (build generates XML documentation)
- **Style**: match the existing style in the target file before introducing a new pattern

---

## 5. Performance Rules

Hot-path code must:

- Use `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and pooled buffers
- Avoid allocations, boxing, unnecessary virtual/interface dispatch
- Avoid LINQ — use simple loops when clearer and cheaper
- Use `Try*` APIs when failure is expected
- Prefer non-throwing fast paths for parsing, serialization, encoding, transport

Do not add complexity in the name of optimization unless it fits the local design of the module.

---

## 6. Security Rules

- Validate all external input lengths, ranges, and boundaries before processing
- Never log secrets, keys, tokens, or raw sensitive payloads
- Reuse existing Nalix security primitives — do not invent new cryptographic designs
- Keep nonce/IV/key handling explicit and safe
- Prefer constant-time implementations for authentication and cryptography

---

## 7. Testing Rules

Test stack: **xUnit**, **FluentAssertions**, **Moq** (where needed), **Xunit.SkippableFact** (environment-dependent tests).

- Place tests in the matching project under `tests/`
- Follow naming conventions already used in that area
- Cover: happy path, invalid input, edge cases
- For serialization, cryptography, networking, memory, and timing — include regression-focused tests

---

## 8. CI/CD Context

Workflows in `.github/workflows/`:

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

**Label gate**: PRs with the `documentation` label skip build and CodeQL — they only trigger `docs.yml`. Do not remove or rename this label.

---

## 9. What NOT to Do

- Do not add projects or references outside the dependency graph above
- Do not use `Nalix.Shared` (removed)
- Do not disable nullable, warnings, or XML doc generation in `.csproj` files
- Do not commit `.snk` files — the signing key `Nalix.snk` is injected by CI: `echo "${{ secrets.SIGNING_KEY }}" | base64 -d > ./Nalix.snk`. Without this step the build will fail with `CS7027`.
- Do not suggest `Debug` configuration in CI context — all CI builds use `Release`
- Do not introduce `Thread.Sleep`, `Task.Delay` in production code without explicit justification
- Do not use `dynamic` or suppress nullability warnings without a comment explaining why
