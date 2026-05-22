# Nalix.Analyzers

## Triggers
- Adding a new diagnostic rule to enforce a Nalix coding pattern
- Investigating why the IDE shows a `NAL0xxx` warning or error
- Changing the Roslyn analyzer infrastructure

---

## Rules

### Diagnostic ID Ranges
| Range | Category |
| :--- | :--- |
| `NAL01xx` | Serialization (`[SerializeOrder]`, `[SerializeIgnore]`, conflicts) |
| `NAL02xx` | Packet system (opcodes, self-type constraints, casts, deserialization) |
| `NAL03xx` | Middleware and handlers (ordering, null middleware, controller patterns) |
| `NAL04xx` | Registry and dispatch (deserializer patterns, loop count) |
| `NAL05xx` | Configuration (`[ConfiguredIgnore]` usage) |
| `NAL06xx` | Pooling (`Reset()` enforcement) |
| `NAL07xx` | Options (request options consistency) |

### Analyzer Performance Constraints
- **Zero allocations in `AnalyzeXxx` methods** — analyzers run on every keystroke in the IDE; allocation degrades typing responsiveness for all developers
- Cache symbol lookups in `SymbolSet` — never re-resolve `INamedTypeSymbol` on every analysis call
- Check `CancellationToken` before `SyntaxTree.GetRoot()` — IDE cancels analysis on every edit
- Match types by fully-qualified name string (from `KnownNames`) — never reference Nalix runtime assemblies

### Partial Class Structure
`NalixUsageAnalyzer` is split into:
- `NalixUsageAnalyzer.cs` — registration, main `Initialize()` entry
- `NalixUsageAnalyzer.InvocationAnalysis.cs` — invocation-specific analysis
- `NalixUsageAnalyzer.SymbolSet.cs` — cached Roslyn symbol lookups

---

## Checklists

### Add a new diagnostic rule
1. Add descriptor in `DiagnosticDescriptors.cs` — use the correct `NAL0xxx` range for the category
2. Register the analysis action in `NalixUsageAnalyzer.Initialize()` (e.g., `context.RegisterSyntaxNodeAction(...)`)
3. Implement analysis logic in `InvocationAnalysis.cs` or a new partial file
4. Add corresponding code fix in `Nalix.Analyzers.CodeFixes` — see nalix-analyzers-codefixes skill
5. Build `Nalix.Analyzers` — all consuming projects pick it up automatically via `Directory.Build.props`

### Suppress a diagnostic (when justified)
```csharp
#pragma warning disable NAL0123 // reason why suppression is justified
... code ...
#pragma warning restore NAL0123
```
Do not suppress at the project level — suppressions should be local and documented.

---

## Gotchas

- **Allocating in analyzer = IDE lag**: A `new List<>()` inside `AnalyzeInvocation` runs thousands of times per second. Use `ImmutableArray`, stackalloc, or pre-allocated pools instead.

- **`SymbolSet` must be initialized once per compilation**: Re-resolving symbols on every analysis call causes repeated semantic model queries. `SymbolSet` caches them at `CompilationStartAction` time.

- **Analyzer applies to all projects automatically**: `Directory.Build.props` attaches the analyzer to every Nalix project. A buggy analyzer that throws `NullReferenceException` will fail builds across the entire solution — test on a single project first.

- **Diagnostic severity affects build**: `DiagnosticSeverity.Error` causes `dotnet build` to fail. `Warning` does not. Be intentional about severity — use `Error` only for patterns that always produce broken code.
