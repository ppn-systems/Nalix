# Phase 2D: NALIX074 Diagnostic Logging Allocation Guard

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Added `EagerStringFormattingInDiagnosticLog` (NALIX074) |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX074 to `SupportedDiagnostics`; registered `OperationKind.ObjectCreation` action; added `AnalyzeDiagnosticLogCreation`, `ContainsEagerStringFormatting`, `IsStringFormattingMethod`, `IsStringType`, `IsInsideIsEnabledGuard`, `ContainsIsEnabledInvocation` methods |
| `analyzers/Nalix.Analyzers/README.md` | Added NALIX074 to active diagnostics table |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 9 NALIX074 tests |

---

## Diagnostic Implementation Summary

**Detection strategy:** Semantic Roslyn operation analysis via `RegisterOperationAction` for `OperationKind.ObjectCreation`.

For each object creation in a Nalix Core assembly:
1. Check if the created type is `Nalix.Abstractions.Diagnostics.DiagnosticLog` (by metadata name and namespace).
2. Check the `Message` argument (index 1) for eager string formatting.
3. Check if the creation is inside an `IsEnabled(...)` guard — if so, skip (the formatting is lazy, not eager).
4. If eager formatting is found and not guarded, report NALIX074.

### Eager string formatting detected

| Pattern | Example | Detected |
|---|---|---|
| Interpolated string | `$"endpoint={endpoint}"` | ✅ `IInterpolatedStringOperation` |
| String concatenation | `"remote=" + endpoint` | ✅ `IBinaryOperation` with `Add` and string result |
| `string.Format(...)` | `string.Format("{0}", val)` | ✅ `IInvocationOperation` on `System.String.Format` |
| `string.Concat(...)` | `string.Concat("a", b)` | ✅ `IInvocationOperation` on `System.String.Concat` |
| `string.Join(...)` | `string.Join(",", items)` | ✅ `IInvocationOperation` on `System.String.Join` |

### Patterns NOT detected (by design)

| Pattern | Reason |
|---|---|
| Constant string | `"connection-closed"` — no allocation |
| `nameof(...)` | Compile-time constant |
| Formatting inside `IsEnabled` guard | Lazy, not eager — formatting only when enabled |
| Formatting outside `DiagnosticLog` | Not diagnostic logging |
| Non-Core assemblies | `IsNalixCoreAssembly` check |

---

## False-Positive Controls

1. **Assembly scoping:** Only Nalix Core assemblies (`IsNalixCoreAssembly`).
2. **Type filtering:** Only `Nalix.Abstractions.Diagnostics.DiagnosticLog` by metadata name and namespace.
3. **IsEnabled guard detection:** `IsInsideIsEnabledGuard` walks the operation parent chain to find `IConditionalOperation` whose condition contains an `IsEnabled(...)` invocation. If found, the creation is considered guarded and not reported.
4. **Constant exclusion:** `IInterpolatedStringOperation` is flagged, but `ILiteralOperation` within it is not — only the overall interpolated string triggers the diagnostic.
5. **`nameof` safety:** `nameof(...)` produces a constant string, not an `IInterpolatedStringOperation`, so it is not flagged.

---

## Production Warnings Found

**Initial scan (without IsEnabled guard check):** 60 warnings across Nalix.Runtime (58) and Nalix.Codec (2).

**After adding IsEnabled guard check:** 0 warnings.

All existing `DiagnosticLog` constructions with eager formatting are already inside `DiagnosticsEvents.Source.IsEnabled(...)` guards. The codebase already follows the correct pattern:
```csharp
if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
{
    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace,
        new DiagnosticLog("tag", $"interpolated message"));
}
```

---

## Production Fixes

None required. All existing code is already properly guarded.

---

## Deferred Logging API Improvements

1. **Lazy logging API:** A `DiagnosticLog` factory that accepts a `Func<string>` or format callback would eliminate the need for `IsEnabled` guards. This would require a larger infrastructure change.
2. **Source-generated log adapters:** Per-event typed adapters could provide zero-allocation logging without manual guards.

---

## Tests Added

| Test | Assembly | Expected | Purpose |
|---|---|---|---|
| `InterpolationInDiagnosticLogInCoreAssembly_ReportsNalix074` | `Nalix.Network` | Reports | Interpolated string in DiagnosticLog reports |
| `StringFormatInDiagnosticLogInCoreAssembly_ReportsNalix074` | `Nalix.Network` | Reports | `string.Format` in DiagnosticLog reports |
| `StringConcatInDiagnosticLogInCoreAssembly_ReportsNalix074` | `Nalix.Network` | Reports | String concatenation in DiagnosticLog reports |
| `ConstantStringInDiagnosticLogInCoreAssembly_DoesNotReportNalix074` | `Nalix.Network` | No report | Constant string is fine |
| `InterpolationOutsideDiagnosticLog_DoesNotReportNalix074` | `Nalix.Network` | No report | Non-DiagnosticLog interpolation not flagged |
| `DiagnosticLogInNonCoreAssembly_DoesNotReportNalix074` | `MyApp` | No report | Consumer assembly not checked |
| `DiagnosticLogInTestAssembly_DoesNotReportNalix074` | `Nalix.Tests` | No report | Test assembly not checked |
| `NameofInDiagnosticLog_DoesNotReportNalix074` | `Nalix.Network` | No report | `nameof(...)` is constant |
| `InterpolationInsideIsEnabledGuard_DoesNotReportNalix074` | `Nalix.Network` | No report | Guarded formatting is lazy, not eager |

Total: 147 tests, all passing.

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 147 passed, 0 failed |
| `dotnet build src/Nalix.sln` (incremental) | ✅ 0 NALIX074 warnings |
| `dotnet build src/Nalix.sln` (full) | ✅ 0 NALIX074 warnings |

---

## Remaining Limitations

1. **IsEnabled guard detection is pattern-based:** The `IsInsideIsEnabledGuard` check looks for `IConditionalOperation` with an `IsEnabled` invocation in the condition. Complex guard patterns (e.g., storing the result in a variable) would not be detected.

2. **Only `DiagnosticLog` is targeted:** Other diagnostic payload types are not checked. If new diagnostic types are added, they would need separate detection.

3. **Interpolation in non-message arguments:** Only the `Message` argument (index 1) is checked. The `Tag` argument (index 0) is typically a constant and not checked.

4. **No data-flow analysis:** If a `DiagnosticLog` is stored in a variable and later passed to `DiagnosticsEvents.Write`, the analyzer cannot trace this flow. Only direct `new DiagnosticLog(...)` constructions are checked.