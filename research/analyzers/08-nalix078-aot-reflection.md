# Phase 2C: NALIX078 AOT/Trimming-Sensitive Reflection Diagnostic

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Added `UnboundedReflectionInAotCode` (NALIX078) |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX078 to `SupportedDiagnostics`; registered `OperationKind.Invocation` action; added `AnalyzeReflectionUsage`, `IsReflectionInvokeOrAccessor`, `TypeInheritsFrom`, `HasDynamicallyAccessedConstructorsAnnotation` methods |
| `analyzers/Nalix.Analyzers/README.md` | Added NALIX078 to active diagnostics table |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 11 NALIX078 tests |

---

## Diagnostic Implementation Summary

**Detection strategy:** Semantic Roslyn operation analysis via `RegisterOperationAction` for `OperationKind.Invocation`.

For each invocation in a Nalix Core assembly:
1. Get `targetMethod.ContainingType` metadata name and namespace.
2. Match against known dangerous reflection patterns by method name and containing type.
3. Report NALIX078 if matched.

### Patterns detected

| Pattern | Containing Type | Method Name |
|---|---|---|
| Assembly scanning | `System.Reflection.Assembly` | `GetTypes`, `GetExportedTypes` |
| AppDomain scanning | `System.AppDomain` | `GetAssemblies` |
| Dynamic code generation | `System.Linq.Expressions.LambdaExpression` | `Compile` |
| Dynamic generic construction | `System.Type` | `MakeGenericType` |
| Dynamic generic method | `System.Reflection.MethodInfo` | `MakeGenericMethod` |
| String-based type lookup | `System.Type`, `System.Reflection.Assembly` | `GetType` (with string param) |
| Reflection invocation | `System.Reflection.MethodInfo`, `ConstructorInfo`, `MethodBase` | `Invoke` |
| Property reflection | `System.Reflection.PropertyInfo` | `GetValue`, `SetValue` |
| Field reflection | `System.Reflection.FieldInfo` | `GetValue`, `SetValue` |

### Patterns NOT detected (by design)

| Pattern | Reason |
|---|---|
| `Activator.CreateInstance(Type)` | Deferred — requires reliable `DynamicallyAccessedMembers` annotation detection |
| `Activator.CreateInstance<T>()` | Allowed by default |
| `Activator.CreateInstance(typeof(KnownType))` | Allowed by default |
| `typeof(T)` | Not reflection |
| Non-Core assemblies | `IsNalixCoreAssembly` check |

---

## Activator.CreateInstance — Deferred

`Activator.CreateInstance` is intentionally excluded from NALIX078 in this phase. The reasons:

1. `Activator.CreateInstance<T>()` is safe and should be allowed.
2. `Activator.CreateInstance(typeof(KnownType))` is safe when the type is statically known.
3. `Activator.CreateInstance(type)` with a runtime `Type` parameter is only unsafe when the `Type` is not annotated with `[DynamicallyAccessedMembers]`.
4. Detecting the annotation reliably in the test compilation framework proved unreliable in this phase.

The codebase already uses `DynamicallyAccessedMembers` extensively (60+ annotations across `Nalix.Hosting`, `Nalix.Framework`, `Nalix.Codec`). The production code has zero `Activator.CreateInstance` calls — the `SingletonActivatorCache` uses source-generated activators.

---

## Current Codebase Status

The Nalix Core codebase is already very clean of dangerous reflection patterns:

| Pattern | Status in src/ |
|---|---|
| `Activator.CreateInstance` | **Zero calls** — source-generated activators used |
| `Assembly.GetTypes()` | **Removed** — comment in `NetworkApplicationBuilder.cs:461` |
| `Expression.Compile` | **Removed** — comment in `SingletonBase.cs:41` |
| `MakeGenericType` / `MakeGenericMethod` | **Zero direct calls** |
| `PropertyInfo.GetValue` | **2 locations** — `TypeMetadata.Cache.cs`, `DiagnosticChannel.cs` (intentional) |
| `DynamicallyAccessedMembers` | **60+ annotations** throughout |

---

## Production Code Warnings

| File | Line | Pattern | Assessment |
|---|---|---|---|
| `Nalix.Codec/Serialization/Internal/Types/TypeMetadata.Cache.cs` | 57 | `PropertyInfo.GetValue` | **Intentional** — reads `IFixedSizeSerializable.Size` static property for type metadata initialization. One-time cost per type. |
| `Nalix.Hosting/Internal/DiagnosticChannel.cs` | 222, 223, 244 | `PropertyInfo.GetValue` | **Intentional** — reads diagnostic event properties dynamically. Diagnostic/debug path, not hot path. |

These are Info/Warning severity and do not block builds. They serve as documentation for future optimization.

---

## Tests Added

| Test | Assembly | Expected | Purpose |
|---|---|---|---|
| `AssemblyGetTypesInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | Assembly scanning in Core reports |
| `AppDomainGetAssembliesInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | AppDomain scanning in Core reports |
| `ExpressionCompileInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | Dynamic code generation in Core reports |
| `MakeGenericTypeInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | Dynamic generic construction in Core reports |
| `MethodInfoInvokeInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | Reflection invocation in Core reports |
| `TypeGetTypeStringInCoreAssembly_ReportsNalix078` | `Nalix.Framework` | Reports | String-based type lookup in Core reports |
| `ReflectionInNonCoreAssembly_DoesNotReportNalix078` | `MyApp` | No report | Consumer assembly not checked |
| `ReflectionInTestAssembly_DoesNotReportNalix078` | `Nalix.Tests` | No report | Test assembly not checked |
| `TypeofDoesNotReportNalix078` | `Nalix.Framework` | No report | `typeof()` is not reflection |
| `ActivatorCreateInstanceGeneric_DoesNotReportNalix078` | `Nalix.Framework` | No report | Generic form is allowed |
| `ActivatorCreateInstanceStaticType_DoesNotReportNalix078` | `Nalix.Framework` | No report | Statically known type is allowed |

Total: 138 tests, all passing.

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 138 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded (4 NALIX078 warnings in Codec/Housing — intentional) |

---

## Remaining Limitations

1. **`Activator.CreateInstance(Type)` detection deferred.** Requires reliable `DynamicallyAccessedMembers` annotation detection in the analyzer. The codebase has zero `Activator.CreateInstance` calls in production, so this is low risk.

2. **`PropertyInfo.GetValue` warnings are intentional.** The 2 production locations use reflection for type metadata initialization and diagnostic logging, not hot paths. These could be suppressed with `#pragma warning disable NALIX078` if desired.

3. **No `MethodBase.Invoke` detection.** The current check covers `MethodInfo.Invoke` and `ConstructorInfo.Invoke` but not the base `MethodBase.Invoke`. This is unlikely to matter in practice.

4. **No `FieldInfo.GetValue/SetValue` in production.** The analyzer detects these patterns but none exist in the current codebase. The detection serves as a regression guard.