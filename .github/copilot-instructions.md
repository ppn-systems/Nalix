# Copilot Instructions

This document defines how Copilot must generate C# code, documentation, structure, and architecture inside the **Nalix** ecosystem.  
All generated code must follow the rules below.

---

## 1. Coding Style & Conventions

- Always use modern C# features (`Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `readonly struct`, pattern matching, file-scoped namespaces).
- Prefer `==` for reference type equality comparisons.
- Write XML documentation comments following Microsoft standards.
- Naming rules:
  - Private fields: `_fieldName`
  - Interfaces: `IType`
  - Async methods: suffix `Async`
- Avoid unnecessary memory allocations.
- Avoid mutable global/static state except pure utility types.
- Prefer explicit types unless `var` improves clarity.
  
---

## 2. Architecture & Layering (Nalix Ecosystem)

Follow **SOLID** and **Domain-Driven Design** principles.

### Nalix.Shared  
Contains only:
- Low-level primitives  
- Memory utilities  
- Hashing, AEAD, cryptography  
- Compression (LZ4)  
- Serialization (LiteSerializer and formatters)  

**Must NOT depend on Nalix.Framework or Nalix.Network.**

### Nalix.Framework  
Contains:
- Logging (console, file, batch, structured)  
- Dependency Injection  
- High-level random utilities  
- Configuration  
- Threading and task scheduling  

May depend on Shared.

### Nalix.Network  
Contains:
- TCP/UDP transports  
- Protocol design  
- Dispatching, routing  
- Connection and handshake logic  

May depend on Shared and Framework.

### Architecture Rules
- No circular dependencies.
- Separate Domain, Application, and Infrastructure logic.
- Prefer composition over inheritance.
- Use `sealed` classes unless extensibility is explicitly required.

---

## 3. Performance Guidelines

Code must follow high-performance practices:

- Prefer `Span<T>` and `ReadOnlySpan<T>` instead of `byte[]` for temporary buffers.
- Use `stackalloc` for small, fixed-size temporary memory.
- Avoid LINQ in performance-critical code; use explicit loops.
- Use:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
```

for frequently-used small methods.

- Prefer readonly struct when appropriate.
- Use GC.AllocateUninitializedArray<T> where safe.
- Avoid boxing and interface dispatch in hot paths.
- Avoid throwing exceptions in hot paths; use Try-based APIs.

---

## 4. Security Requirements

Generated code must follow strict security standards:
- Sensitive buffers (keys, nonces, passwords, secrets) must be cleared immediately after use:
  ```csharp
  MemorySecurity.ZeroMemory(buffer);
  ```
- Do not write sensitive data to logs under any circumstance.

- Always validate external inputs:
    - Check for null references
    - Check lengths and boundaries
    - Validate ranges for numeric values    
    - Validate array and buffer sizes before reading or writing

- For randomness, always use:
    - OS CSPRNG
    - Or SecureRandom inside Nalix
    - Never design or implement custom cryptographic primitives.

- Use only approved Nalix cryptographic components:
    - ChaCha20
    - Poly1305
    - ChaCha20-Poly1305 (AEAD)
    - PBKDF2
    - EnvelopeCipher

- Avoid insecure patterns:
    - Predictable seeds
    - Static nonces
    - Reusable IVs
    - Weak hashing algorithms
    - Ensure exception messages never reveal sensitive internal state.

---

## 5. Code Generation Requirements

- When Copilot generates new C# files, it must:
  - Include complete XML documentation for all public types, methods, and properties.
  - Apply debugger-focused attributes when appropriate:
[DebuggerNonUserCode]
[DebuggerStepThrough]

- Use the namespace structure:
  ```text
  Nalix.<Layer>.<Module>
  ```
  where `<Layer>` is one of `Shared`, `Framework`, or `Network`, and `<Module>` reflects the specific functionality.
  - Prefer sealed classes unless extensibility is required.
  - Prefer readonly fields whenever possible.
  - Prioritize immutable or partially immutable object design.
  - Avoid unnecessary abstraction layers or over-engineering.
  - Do not place expensive logic inside constructors.

- Use Try-style APIs instead of throwing exceptions in performance-critical paths:
  - TryParse
  - TryFormat
  - TryEncrypt
  - TryDecode
  - Favor pure functions with predictable behavior.
  - Maintain consistent formatting, naming, and coding conventions throughout the Nalix ecosystem.

---

## 6. Unit Testing Standards

- Generated tests must:

  - Use xUnit as the testing framework.

- Follow the naming pattern:
  - MethodName_Scenario_ExpectedResult
  - Test edge cases and invalid inputs.

- For cryptography:
  - Validate official test vectors
  - Validate tamper detection
  - Validate behavior with incorrect keys and nonces

- For serialization:
  - Test null, empty, nested, dynamic, and random objects
  - Ensure round-trip serialization correctness

- For compression:
  - Test random data across multiple sizes
  - Validate decompression length and integrity
  - Avoid large allocations or excessive randomness inside tests unless required.

---

## 7. Docker-Friendly Code

- Generated server-side or service code must:
  - Write all logs to STDOUT, not to local files.
  - Avoid hardcoded file paths; use environment variables or configuration injection.
  - Support graceful shutdown on SIGTERM (container stop).
  - Avoid platform-specific APIs unless guarded with runtime checks.
  - Ensure no feature requires interactive UI or desktop APIs.
  - Prefer asynchronous operations for I/O-heavy tasks.

---

## 8. General Behavior Rules for Generated Code

- Prefer immutable data structures or readonly members.
- Avoid unnecessary allocations and boxing.
- Favor deterministic behavior and explicit control flow.

- Concurrency-related code must be thread-safe:
  - Prefer lock-free constructs (ConcurrentQueue, ConcurrentDictionary)
  - Use Interlocked for counters and fast atomic operations

- Provide descriptive, minimal, and safe exception messages.
- Avoid any form of hidden side effects.
- Follow defensive programming guidelines throughout.