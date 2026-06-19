# NALIX076: Packet Context Escape Analyzer

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Diagnostic

- **ID:** NALIX076
- **Title:** Packet context must not escape handler scope
- **Category:** Correctness
- **Severity:** Warning
- **Scope:** Nalix Core assemblies only

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Added `PacketContextEscapesHandlerScope` (NALIX076) |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX076 to `SupportedDiagnostics`; registered `OperationKind.SimpleAssignment`, `OperationKind.Invocation`, `OperationKind.EventAssignment` actions; added `AnalyzeContextEscape`, `AnalyzeFieldPropertyAssignment`, `AnalyzeContextEscapeInvocation`, `AnalyzeEventAssignment`, `IsOffloadMethod`, `IsCollectionAddMethod`, `ArgumentCapturesContextOrPacket`, `LambdaCapturesContextOrPacket`, `IsPacketContextType`, `IsPacketType` methods |
| `analyzers/Nalix.Analyzers/README.md` | Added NALIX076 to active diagnostics table |
| `docs/api/analyzers/diagnostic-codes.md` | Added NALIX076 to Correctness/Security/Pooling/AOT section |
| `src/Nalix.Runtime/Middleware/MiddlewarePipeline.cs` | Added `#pragma warning disable/restore NALIX076` around 2 framework-infrastructure field assignments |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 7 NALIX076 tests |

---

## Detection Strategy

### Pattern 1: Field/Property Assignment

`ISimpleAssignmentOperation` where:
- Target is `IFieldReferenceOperation` or `IPropertyReferenceOperation`
- Value type implements `IPacketContext<T>` or inherits from `PacketBase<TSelf>` / implements `IPacket`
- Value is not a null literal (excludes `ResetForPool` cleanup)

### Pattern 2: Task.Run / Offload Invocation

`IInvocationOperation` where:
- Target method is `Task.Run`, `Task.Factory.StartNew`, or `ThreadPool.QueueUserWorkItem`
- First argument is a context/packet type, OR a delegate/lambda that captures context/packet types

Lambda capture detection walks `IAnonymousFunctionOperation.Descendants()` looking for `IParameterReferenceOperation`, `ILocalReferenceOperation`, `IFieldReferenceOperation`, or `IPropertyReferenceOperation` whose type matches context/packet.

### Pattern 3: Event Assignment

`IEventAssignmentOperation` where:
- Value is a `IDelegateCreationOperation` wrapping an `IAnonymousFunctionOperation`
- The lambda captures context/packet types

### Pattern 4: Collection Add

`IInvocationOperation` where:
- Target method name is `Add`, `Enqueue`, `TryWrite`, `Push`, or `Insert`
- First argument type is context/packet

---

## False-Positive Controls

1. **Null assignment excluded:** `assignment.Value.ConstantValue is { HasValue: true, Value: null }` skips `ResetForPool` cleanup patterns.
2. **Type-based detection:** Uses semantic type checking, not name-based heuristics.
3. **Core-only scope:** `IsNalixCoreAssembly` filter.
4. **No local variable tracking:** Extracting `context.Packet` into a local is not flagged.
5. **No helper method flagging:** Passing context to a private method is not flagged (only `Task.Run`/offload patterns).

---

## Production Suppressions

| File | Lines | Reason |
|---|---|---|
| `MiddlewarePipeline.cs:479` | `_context = context` in `Initialize` | Framework infrastructure intentionally holds context during middleware execution; cleared in `ResetForPool()` |
| `MiddlewarePipeline.cs:500` | `_context = context` in `InitializeFull` | Same pattern as above |

Both suppressions have inline comments explaining the intentional lifecycle management.

---

## Tests Added

| Test | Expected | Purpose |
|---|---|---|
| `ContextAssignedToField_ReportsNalix076` | Reports | Field assignment of PacketContext |
| `ContextPacketAssignedToField_ReportsNalix076` | Reports | Field assignment of context.Packet |
| `TaskRunWithContext_ReportsNalix076` | Reports | Task.Run with context argument |
| `TaskRunWithLambdaCapturingContext_ReportsNalix076` | Reports | Task.Run with lambda capturing context |
| `PassingContextToHelper_DoesNotReportNalix076` | No report | Passing to private async helper |
| `ExtractPacketToLocal_DoesNotReportNalix076` | No report | Extracting Packet into local |
| `ContextEscapeInNonCoreAssembly_DoesNotReportNalix076` | No report | Non-Core assembly not checked |

Total: 154 tests, all passing.

---

## Validation Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 154 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded, 0 NALIX076 warnings |

---

## Known Limitations

1. **No data-flow analysis:** The analyzer does not track whether a local variable holding context is later assigned to a field or passed to `Task.Run`. Only direct patterns are caught.

2. **Event assignment detection is conservative:** Only detects lambda-based event subscriptions, not method group subscriptions that capture context.

3. **Collection method names are heuristic:** `Add`, `Enqueue`, `TryWrite`, `Push`, `Insert` cover common patterns but may miss custom collection methods.

4. **Framework infrastructure false positives:** The `MiddlewarePipeline` stores context by design. Suppressed with pragmas rather than excluding the pattern.

5. **`context.Packet` into local then to field:** If a user writes `var p = context.Packet; _field = p;`, the second assignment is not caught because `p` is a local variable, not a field/property of context type. This is intentional conservatism.