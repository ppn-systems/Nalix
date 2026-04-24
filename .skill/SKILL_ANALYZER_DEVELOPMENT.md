# 🛠️ Nalix AI Skill — Roslyn Analyzer Development

This skill covers the development and maintenance of the `Nalix.Analyzers` project. It is essential for enforcing Nalix best practices and preventing common coding errors at compile time.

---

## 🏗️ Analyzer Architecture

Nalix uses a single multi-purpose analyzer (`NalixUsageAnalyzer`) divided into several partial classes for maintainability:

1.  **`NalixUsageAnalyzer.cs`**: The main entry point. Handles registration and common utility methods.
2.  **`NalixUsageAnalyzer.SymbolSet.cs`**: Resolves and caches all required Nalix symbols (Attributes, Interfaces) from the compilation.
3.  **`NalixUsageAnalyzer.InvocationAnalysis.cs`**: Contains logic for analyzing method calls, attribute usage, and signatures.

---

## 📜 Key Diagnostic Categories

- **Usage (NALIX00x)**: Validates correct use of attributes like `[PacketController]` and `[PacketOpcode]`.
- **Serialization (NALIX01x)**: Ensures correct serialization layouts and member ordering.
- **Performance (NALIX03x)**: Flags potential allocations in hot paths (Middleware/Handlers).
- **Hosting (NALIX04x)**: Validates the configuration of the `NetworkApplicationBuilder`.

---

## 🛠️ Implementation Patterns

### 1. Symbol Resolution
Always use the `SymbolSet` to resolve types. Never use strings directly in the analysis logic.
```csharp
if (SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, symbols.PacketControllerAttribute))
{
    // Logic here
}
```

### 2. Signature Validation
The analyzer must verify that handler methods follow the supported signatures:
- `(TPacket, IConnection)`
- `(PacketContext<T>)`
- Optional `CancellationToken` as the last parameter.

### 3. Allocation Tracking
The analyzer tracks `ObjectCreationExpression` and `ArrayCreationExpression` inside methods marked as hot paths (e.g., inside a `[PacketController]` method).

---

## 🧪 Testing Analyzers

Nalix uses the standard Roslyn test harness (`VerifyCS`).

- **Location:** `tests/Nalix.Analyzers.Tests`.
- **Method:** `VerifyAnalyzerAsync(source, expectedDiagnostic)`.
- **Code Fixes:** Use `VerifyCodeFixAsync` to ensure the suggested fix correctly modifies the source code.

---

## 🛡️ Best Practices for Analyzer Dev

- **Efficiency:** Analyzers run during every keystroke. Use `OperationAnalysisContext` and `SymbolAnalysisContext` efficiently.
- **Caching:** Cache resolved symbols in the `CompilationStartAnalysisContext`.
- **Suppressors:** If a Nalix pattern triggers a standard .NET warning (like "Unused parameter"), implement a `DiagnosticSuppressor`.

---

## 🛡️ Common Pitfalls

- **False Positives:** Ensure the analyzer only runs on projects that actually reference Nalix assemblies.
- **Recursive Analysis:** Avoid complex recursive symbol resolution that could slow down the IDE.
- **Null Safety:** Always check for null when accessing `symbol.ContainingType` or `symbol.BaseType`.
