# Phase 1.1: NALIX073 Core Assembly Scoping Hardening

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## What Existed Before

NALIX073 was scoped by **namespace only**: `IsInNalixCoreNamespace` walked the containing namespace hierarchy looking for any ancestor named `"Nalix"`. This meant:

- `Nalix.Runtime.Internal.Processor` → reported ✅
- `Nalix.Message.Handlers.Processor` → reported ❌ (false positive)
- `Nalix.Sample.App.Processor` → reported ❌ (false positive)
- `NalixGame.Server.Processor` → NOT reported ✅ (doesn't start with `Nalix.`)

The namespace-only approach could not distinguish between Nalix Core projects and consumer projects that happen to use a `Nalix.*` namespace prefix.

---

## New Scoping Logic

`IsInNalixCoreNamespace` replaced with `IsNalixCoreAssembly(string? assemblyName)`:

```csharp
private static bool IsNalixCoreAssembly(string? assemblyName)
{
    if (string.IsNullOrEmpty(assemblyName))
        return false;

    return assemblyName is
        "Nalix.Abstractions" or
        "Nalix.Codec" or
        "Nalix.Environment" or
        "Nalix.Framework" or
        "Nalix.Network" or
        "Nalix.Runtime" or
        "Nalix.SDK" or
        "Nalix.Observability" or
        "Nalix.Observability.Extensions" or
        "Nalix.Hosting";
}
```

The assembly name is obtained from `context.Operation.SemanticModel?.Compilation?.Assembly.Name` at analysis time, which is the assembly being compiled — not a referenced assembly. This ensures:

- Consumer projects referencing Nalix packages are NOT checked
- Only the Nalix Core source projects themselves are checked
- Test, benchmark, sample, and consumer assemblies are excluded by exact-match

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Replaced `IsInNalixCoreNamespace` with `IsNalixCoreAssembly`; changed `AnalyzeCatchClause` to use assembly name |
| `analyzers/Nalix.Analyzers/README.md` | Updated NALIX073 description to mention assembly-based scoping |
| `tests/Nalix.Analyzers.Tests/Verifier.cs` | Added `VerifyAnalyzerInAssemblyAsync` overload and `CreateProject(sources, assemblyName)` overload |
| `tests/Nalix.Analyzers.Tests/AnalyzerTestHarness.cs` | Added `AssertDiagnosticIdsInAssemblyAsync` |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Updated all NALIX073 tests to use assembly names; added consumer namespace and test assembly tests |

---

## Tests Added/Updated

| Test | Assembly Name | Expected | Purpose |
|---|---|---|---|
| `BareCatchExceptionInCoreAssembly_ReportsNalix073` | `Nalix.Runtime` | Reports | Core assembly reports |
| `GuardedCatchExceptionInCoreAssembly_DoesNotReportNalix073` | `Nalix.Runtime` | No report | ExceptionClassifier guard suppresses |
| `SpecificExceptionCatchInCoreAssembly_DoesNotReportNalix073` | `Nalix.Runtime` | No report | Non-Exception type ignored |
| `BareCatchExceptionInNonCoreAssembly_DoesNotReportNalix073` | `MyApp` | No report | Consumer assembly not checked |
| `BareCatchExceptionInConsumerNalixNamespace_DoesNotReportNalix073` | `Nalix.Message` | No report | Consumer with Nalix namespace not checked |
| `BareCatchExceptionInTestAssembly_DoesNotReportNalix073` | `Nalix.Tests` | No report | Test assembly not checked |
| `CatchExceptionWithUnrelatedFilter_ReportsNalix073` | `Nalix.Runtime` | Reports | Non-ExceptionClassifier filter still reports |

Total: 113 tests, all passing.

---

## Test Infrastructure Changes

The `Verifier.CreateProject` method now accepts an optional `assemblyName` parameter:

```csharp
private static Project CreateProject(string[] sources, string? assemblyName = null)
{
    string effectiveAssemblyName = assemblyName ?? "TestProject";
    // ...
    .AddProject(projectId, "TestProject", effectiveAssemblyName, LanguageNames.CSharp)
}
```

A new `VerifyAnalyzerInAssemblyAsync` method (and corresponding `AssertDiagnosticIdsInAssemblyAsync` harness method) was added to avoid overload ambiguity with the params-based `VerifyAnalyzerAsync`.

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 113 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ 0 NALIX073 warnings, 0 errors |

---

## Remaining False-Positive Risks

1. **Nalix extensions or plugins** that use a Core assembly name (e.g., a third-party package named `Nalix.Codec`) would be checked. This is extremely unlikely since those names are owned by the Nalix project.

2. **Future Nalix Core assemblies** not in the allowlist will need to be added manually. The allowlist is explicit and conservative — new assemblies must be opted in.

3. **`catch (Exception ex) when (someOtherSafeFilter)`** patterns that don't use `ExceptionClassifier.IsNonFatal` will still be flagged in Core assemblies. This is intentional — the project standard is to use `ExceptionClassifier`.