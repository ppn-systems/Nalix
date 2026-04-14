# Contributing to Nalix

Thank you for considering contributing to Nalix! It's people like you that make Nalix a great tool for real-time communication and data sharing.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Environment](#development-environment)
- [How to Contribute](#how-to-contribute)
- [Commit Convention](#commit-convention)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [CI Quality Gates](#ci-quality-gates)
- [Release Automation](#release-automation)
- [Issue Reporting](#issue-reporting)
- [Architecture](#architecture)
- [Community](#community)

---

## Code of Conduct

This project and everyone participating in it is governed by the [Nalix Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to [ppn.system@gmail.com](mailto:ppn.system@gmail.com).

---

## Development Environment

### Prerequisites

| Tool | Version |
| :--- | :--- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ |
| IDE | [Visual Studio 2026](https://visualstudio.microsoft.com/vs/) / [VS Code](https://code.visualstudio.com/) / [Rider](https://www.jetbrains.com/rider/) |
| [Git](https://git-scm.com/) | Latest |

### Getting Started

1. **Fork** the repository on GitHub.

2. **Clone** your fork locally:

   ```bash
   git clone https://github.com/<your-username>/Nalix.git
   cd Nalix
   ```

3. **Add upstream** remote:

   ```bash
   git remote add upstream https://github.com/ppn-systems/Nalix.git
   ```

4. **Create a branch** for your work:

   ```bash
   git checkout -b feature/your-feature-name
   ```

5. **Build** the solution to verify everything compiles:

   ```bash
   dotnet build
   ```

---

## How to Contribute

### Workflow

1. Sync with upstream: `git pull upstream master`
2. Create a feature branch.
3. Make your changes.
4. Write or update tests as needed.
5. Run the test suite: `dotnet test`
6. Commit with a [conventional message](#commit-convention).
7. Push to your fork and open a Pull Request.

### Types of Contributions

| Type | Description |
| :--- | :--- |
| 🚀 Features | New functionality or capabilities |
| 🐛 Bug Fixes | Corrections to existing behavior |
| 📝 Documentation | Improvements to docs, comments, or examples |
| 🧪 Tests | New or improved test coverage |
| 🎨 Code Quality | Refactoring, formatting, or cleanup |

---

## Commit Convention

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification for consistent commit history and automated versioning.

### Format

```text
<type>(optional-scope): <short description>

[optional body]

[optional footer]
```

### Commit Types

| Type | Purpose |
| :--- | :--- |
| `feat` | A new feature |
| `fix` | A bug fix |
| `refactor` | Code change (no feature/fix) |
| `perf` | Performance improvement |
| `docs` | Documentation only |
| `test` | Adding or updating tests |
| `build` | Build system or dependencies |
| `ci` | CI/CD pipeline changes |
| `chore` | Maintenance (no production code) |
| `style` | Formatting or non-functional cleanup |
| `revert` | Revert a previous commit |

### Best Practices

- Keep the subject line short, imperative, and lowercase.
- Do not end the subject with a period.
- Use an optional scope to identify the affected package or area.
- Prefer one logical change per commit.

### Version Bump Rules

When release automation is enabled:

| Commit Type | Release |
| :--- | :--- |
| `fix` | Patch (`x.x.+1`) |
| `feat` | Minor (`x.+1.0`) |
| `!` or `BREAKING CHANGE:` footer | Major (`+1.0.0`) |

### Examples

```text
feat(network): add UDP replay guard for authenticated sessions
fix(logging): handle null formatter options in file target
docs(readme): update package overview for Nalix.SDK
test(framework): add coverage for Snowflake overflow handling
build(ci): align release-please changelog types
```

---

## Coding Standards

### C# Code Style

We follow the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with additional guidelines:

- Use **4 spaces** for indentation (no tabs).
- Use `var` when the type is obvious; explicit type declarations otherwise.
- Use expression-bodied members when appropriate.
- Prefer pattern matching (`is`) over type checking and casting.
- Keep methods short and focused on a single responsibility.
- Write self-documenting code; avoid excessive comments.
- Full formatting rules are codified in [`.editorconfig`](.editorconfig).

### SOLID Principles

| Principle | Guideline |
| :--- | :--- |
| **S**ingle Responsibility | Each class should have only one reason to change. |
| **O**pen/Closed | Open for extension, closed for modification. |
| **L**iskov Substitution | Derived classes must be substitutable for their base classes. |
| **I**nterface Segregation | Prefer many client-specific interfaces over one general-purpose interface. |
| **D**ependency Inversion | Depend on abstractions, not concretions. |

---

## Pull Request Process

1. Ensure your code adheres to the [coding standards](#coding-standards).
2. Update documentation if your changes affect public APIs or behavior.
3. Include relevant tests for your changes.
4. Verify the PR passes all [CI quality gates](#ci-quality-gates).
5. A maintainer will review your PR and may request changes.
6. Once approved, your PR will be merged into the `master` branch.

---

## CI Quality Gates

Pull requests and pushes to `master` must pass the shared `_build.yml` workflow:

| Check | Description |
| :--- | :--- |
| `dotnet format --verify-no-changes` | Code formatting compliance |
| Release build | Full solution compilation |
| Test execution | TRX results with XPlat Code Coverage |
| Coverage artifacts | Cobertura format generation |
| Trim smoke test | `linux-x64` self-contained publish with `PublishTrimmed=true` |

Additionally, the **benchmark workflow** runs BenchmarkDotNet on pushes to `master`, exports JSON/CSV artifacts, and compares results with the previous run to highlight regressions.

---

## Release Automation

Releases follow Conventional Commits and are driven from the `master` branch:

- `fix` commits → next **patch** release.
- `feat` commits → next **minor** release.
- `BREAKING CHANGE:` footers or `!` markers → next **major** release.

> **Tip:** Prefer one commit (or squash-merge result) that clearly communicates the highest-severity change so the version bump is unambiguous.

---

## Issue Reporting

When reporting issues, please use the provided templates and include:

1. A clear, descriptive title.
2. Steps to reproduce the issue.
3. Expected behavior.
4. Actual behavior.
5. Environment details (OS, .NET version, etc.).
6. Any relevant logs or screenshots.

---

## Architecture

Nalix follows [Domain-Driven Design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice) principles:

| Layer | Responsibility |
| :--- | :--- |
| **Domain** | Business logic, entities, value objects, and domain events. |
| **Application** | Orchestration of domain objects to perform tasks. |
| **Infrastructure** | Technical capabilities (persistence, messaging, etc.). |
| **Presentation** | User interface and API endpoints. |

### Key DDD Concepts

- **Bounded Contexts** — clear boundaries between system parts.
- **Entities** — objects with a distinct identity over time.
- **Value Objects** — objects defined by their attributes, without identity.
- **Aggregates** — clusters of domain objects treated as a single unit.
- **Domain Events** — significant occurrences that domain experts care about.
- **Repositories** — methods for obtaining domain objects.

---

## Community

Join the conversation:

- [GitHub Discussions](https://github.com/ppn-systems/Nalix/discussions)
- [GitHub Issues](https://github.com/ppn-systems/Nalix/issues)

---

<p align="center">
  ❤️ Thank you for contributing to Nalix!
</p>
