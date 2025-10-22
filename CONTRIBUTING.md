# Contributing to Nalix 🚀

First of all, thank you for considering contributing to Nalix! It's people like you that make Nalix such a great tool for real-time communication and data sharing.

## Table of Contents 📑

- [Code of Conduct](https://github.com/phcnguyen/Nalix/blob/master/CODE_OF_CONDUCT.md)
- [Coding Standards](https://github.com/phcnguyen/Nalix/blob/master/.editorconfig)
- [Pull Request Process](https://github.com/phcnguyen/Nalix/pulls)
- [Issue Reporting Guidelines](https://github.com/phcnguyen/Nalix/issues)
- [DDD Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)

## Code of Conduct 📜

This project and everyone participating in it is governed by the [Nalix Code of Conduct](https://github.com/phcnguyen/Nalix/blob/master/CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to [email@domain.com].

## Development Environment Setup 💻

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [Visual Studio Code](https://code.visualstudio.com/) with C# extensions
- [Git](https://git-scm.com/)

### Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally:

   ```bash
   git clone https://github.com/ppn-systems/Nalix.git
   ```

3. Add the original repository as upstream:

   ```bash
   git remote add upstream https://github.com/ppn-systems/Nalix.git
   ```

4. Create a new branch for your feature or bugfix:

   ```bash
   git checkout -b feature/your-feature-name
   ```

5. Open the solution in Visual Studio or VS Code and build the project to ensure everything is working correctly

## How to Contribute 🤝

### Contribution Workflow

1. Make sure you have the latest changes:

    ```bash
        git pull upstream main
    ```

2. Create a new branch for your work
3. Make your changes
4. Write or update tests as needed
5. Run the tests to ensure they pass
6. Commit your changes with a clear commit message
7. Push your branch to your fork
8. Create a Pull Request from your fork to the main repository

### Types of Contributions

- Implementing new features
- Fixing bugs
- Improving documentation
- Improving code quality and test coverage
- Reporting issues

## Coding Standards 📏

### C# Code Style

We follow the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with some additional guidelines:

- Use 4 spaces for indentation, not tabs
- Use `var` when the type is obvious, explicit type declarations otherwise
- Use expression-bodied members when appropriate
- Prefer pattern matching (using `is` operator) over type checking and casting
- Use C# latest features where they enhance readability and maintainability
- Keep methods short and focused on a single responsibility
- Avoid excessive comments - code should be self-documenting

### SOLID Principles

We strive to follow SOLID principles in our codebase:

- **S**ingle Responsibility: Each class should have only one reason to change
- **O**pen/Closed: Classes should be open for extension but closed for modification
- **L**iskov Substitution: Derived classes must be substitutable for their base classes
- **I**nterface Segregation: Many client-specific interfaces are better than one general-purpose interface
- **D**ependency Inversion: Depend on abstractions, not concretions

## Pull Request Process 🔄

1. Ensure your code adheres to the coding standards outlined above
2. Update the README.md or documentation with details of changes if applicable
3. Include relevant tests for your changes
4. The PR should work on our CI pipeline without errors
5. A maintainer will review your PR and may request changes
6. Once approved, your PR will be merged into the main branch

## Issue Reporting Guidelines 🐛

When reporting issues, please use the provided issue templates and include:

1. A clear and descriptive title
2. Steps to reproduce the issue
3. Expected behavior
4. Actual behavior
5. Environment details (OS, .NET version, etc.)
6. Any relevant logs or screenshots

## DDD Architecture 🏗️

Nalix follows Domain-Driven Design principles. When contributing, please respect the existing architecture:

- **Domain Layer**: Contains business logic, entities, value objects, and domain events
- **Application Layer**: Orchestrates the domain objects to perform tasks
- **Infrastructure Layer**: Provides technical capabilities (persistence, messaging, etc.)
- **Presentation Layer**: Handles user interface and API endpoints

### Key DDD Concepts

- **Bounded Contexts**: Clear boundaries between different parts of the system
- **Entities**: Objects with a distinct identity that runs through time
- **Value Objects**: Objects defined by their attributes, with no identity
- **Aggregates**: Clusters of domain objects treated as a single unit
- **Domain Events**: Things that happen which domain experts care about
- **Repositories**: Methods for obtaining domain objects

## Community 👥

Join our community channels to discuss development, ask questions, or just hang out:

- [Discord](https://discord.gg/nalix) (Replace with actual link)
- [Discussions on GitHub](https://github.com/phcnguyen/Nalix/discussions)

---

Thank you for contributing to Nalix! ❤️
