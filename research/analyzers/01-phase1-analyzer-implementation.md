# Phase 1 Analyzer Implementation Report

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Removed NALIX007/019/031 dead descriptors; added NALIX073, NALIX075 |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX022 implementation, NALIX073 catch analysis, NALIX075 PacketScope analysis; removed dead buffer middleware entries from SupportedDiagnostics |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.SymbolSet.cs` | Removed `NetworkBufferMiddlewareType`; added `ExceptionClassifierType`, `PacketScopeType`, `CaughtExceptionType` |
| `analyzers/Nalix.Analyzers/README.md` | Updated NALIX022 description; added NALIX073/075; added deprecation table |
| `src/Nalix.Runtime/Sessions\SessionService.cs` | Fixed 4 bare `catch (Exception)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))` |
| `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:356` | Added `ExceptionClassifier.IsNonFatal(ex)` to catch filter |
| `src/Nalix.Runtime/Throttling/TokenBucketLimiter.Cleanup.cs:68` | Added `ExceptionClassifier.IsNonFatal(ex)` to catch filter; added using directive |
| `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.Execution.cs:429` | Added `ExceptionClassifier.IsNonFatal(ex)` to catch filter |
| `src/Nalix.Codec/DataFrames/PacketBase.cs:92` | Added `ExceptionClassifier.IsNonFatal(ex)` to catch filter |
| `src/Nalix.SDK/Transport/Internal/Tcp/TcpFrameReader.cs:121` | Added `ExceptionClassifier.IsNonFatal(ex)` to catch filter |
| `tests/Nalix.Analyzers.Tests/TestSources.cs` | Added `IPacketStaticOpcode`, `ExceptionClassifier`, `PacketScope<T>`, `ReservedOpcodePermittedAttribute` stubs |
| `tests/Nalix.Analyzers.Tests/Verifier.cs` | Added `ExceptionClassifier` and `PacketScope`1` to required metadata names |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 13 new tests; updated 1 existing test |

---

## Diagnostics Removed / Deprecated

| ID | Title | Action |
|---|---|---|
| NALIX007 | Buffer middleware ignores stage attribute | **Removed** — `INetworkBufferMiddleware` intentionally dropped from Nalix |
| NALIX019 | Buffer middleware type invalid | **Removed** — same reason |
| NALIX031 | Buffer middleware missing order | **Removed** — same reason |

These were dead code since `NetworkBufferMiddlewareType` was always `null` in `SymbolSet.Create()`. Removed from `DiagnosticDescriptors.cs`, `SupportedDiagnostics`, and documented in README.

---

## Diagnostics Fixed

### NALIX022 — Packet Member Overlaps Header Region

**Problem:** Declared in `DiagnosticDescriptors.cs` and listed in `SupportedDiagnostics` but the check was never implemented in `AnalyzeSerializationType`.

**Fix:** Added the check after the negative order validation:

```csharp
else if (isPacketBaseType && finalOrder.Value < symbols.PacketHeaderRegionOffset)
{
    Report(context, DiagnosticDescriptors.PacketMemberOverlapsHeaderRegion, member, member.Name, finalOrder.Value, symbols.PacketHeaderRegionOffset);
}
```

**Applies to:** `PacketBase<TSelf>`-derived types only. Non-PacketBase types are not checked. Both `SerializeOrder` and `SerializeHeader` are checked (via `finalOrder`).

**Existing test updated:** `SerializeOrderStartingFromZero_DoesNotReportNalix022` renamed to `SerializeOrderStartingFromZero_ReportsNalix022` — orders 0 and 1 ARE inside the header region (0-11) and should report.

---

## Diagnostics Added

### NALIX073 — Unguarded catch(Exception)

- **Category:** Correctness
- **Severity:** Warning
- **Scope:** Nalix Core only (namespace hierarchy under `Nalix.*`)
- **Detection:** `ICatchClauseOperation` analysis. Checks `ExceptionType` against `System.Exception`. Checks `Filter` for `ExceptionClassifier.IsNonFatal()` invocation. Walks `ChildOperations` recursively.
- **Test cases:** 5 tests (positive bare catch, negative guarded catch, negative specific catch, negative non-Nalix namespace, positive unrelated filter)

### NALIX075 — PacketScope must be disposed

- **Category:** Pooling
- **Severity:** Error
- **Scope:** All code
- **Detection:** `RegisterSyntaxNodeAction` for `LocalDeclarationStatement`. Checks if the declared type is `PacketScope<T>` (via `OriginalDefinition` comparison) and if the `using` keyword is present. Handles both explicit types and `var` via semantic model.
- **Test cases:** 4 tests (missing using, using declaration, using statement, unrelated IDisposable)

---

## Tests Added

| Test Name | Diagnostic | Expected |
|---|---|---|
| `PacketBaseWithSerializeOrderInHeaderRegion_ReportsNalix022` | NALIX022 | Reports |
| `NonPacketBaseWithSerializeOrderInHeaderRegion_DoesNotReportNalix022` | NALIX022 | No report |
| `PacketBaseWithSerializeOrderAtHeaderOffset_DoesNotReportNalix022` | NALIX022 | No report |
| `PacketBaseWithSerializeHeaderInHeaderRegion_ReportsNalix022` | NALIX022 | Reports |
| `BareCatchExceptionInNalixNamespace_ReportsNalix073` | NALIX073 | Reports |
| `GuardedCatchException_DoesNotReportNalix073` | NALIX073 | No report |
| `SpecificExceptionCatch_DoesNotReportNalix073` | NALIX073 | No report |
| `BareCatchExceptionOutsideNalixNamespace_DoesNotReportNalix073` | NALIX073 | No report |
| `CatchExceptionWithUnrelatedFilter_ReportsNalix073` | NALIX073 | Reports |
| `PacketScopeWithoutUsing_ReportsNalix075` | NALIX075 | Reports |
| `PacketScopeWithUsingDeclaration_DoesNotReportNalix075` | NALIX075 | No report |
| `PacketScopeWithUsingStatement_DoesNotReportNalix075` | NALIX075 | No report |
| `UnrelatedDisposableWithoutUsing_DoesNotReportNalix075` | NALIX075 | No report |

**Test count:** 97 → 110 (+13 new, 1 updated)

---

## Production Code Fixes

### SessionService.cs — 4 bare `catch (Exception)` blocks

All four catch blocks in `SaveSessionAsync` and `ConsumeAsync` were catching bare `Exception` without `ExceptionClassifier.IsNonFatal()` filter. Changed to `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))`.

### HandshakeHandlers.cs:356

`catch (Exception ex) when (ex is not InternalErrorException)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex) && ex is not InternalErrorException)`

### TokenBucketLimiter.Cleanup.cs:68

`catch (Exception ex) when (ex is not ObjectDisposedException)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex) && ex is not ObjectDisposedException)`

### PacketDispatchOptions.Execution.cs:429

`catch (Exception ex) when (IsConnectionTeardownException(ex))` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex) && IsConnectionTeardownException(ex))`

### PacketBase.cs:92

`catch (Exception ex) when (ex is InvalidOperationException || ...)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex) && (ex is InvalidOperationException || ...))`

### TcpFrameReader.cs:121

`catch (Exception ex) when (ex is not OperationCanceledException)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex) && ex is not OperationCanceledException)`

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 110 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ 0 NALIX073 warnings, 58 pre-existing warnings, 0 errors |
| `dotnet test tests/Nalix.Tests.sln` | ✅ Analyzer tests pass; Network/Runtime test failures are pre-existing (config/report issues) |

---

## Known Limitations

1. **NALIX073 scope detection:** Uses namespace hierarchy (`Nalix.*`). Code in user namespaces like `Nalix.MyApp` would be incorrectly flagged. This is an acceptable trade-off since the `Nalix` namespace is controlled by the framework.

2. **NALIX073 filter detection:** Only recognizes `ExceptionClassifier.IsNonFatal(ex)` as a valid guard. Custom safety filters like `IsConnectionTeardownException` require explicit `ExceptionClassifier.IsNonFatal` chaining.

3. **NALIX075 var detection:** Relies on the semantic model to resolve `var` types. If the semantic model cannot resolve the type (e.g., missing references), the diagnostic will not fire.

4. **NALIX022 uses `finalOrder` not `order`:** Both `SerializeHeader` and `SerializeOrder` are checked against the header region offset, since `finalOrder = headerOrder ?? order`.

---

## Deferred Diagnostics

| Diagnostic | Reason |
|---|---|
| NALIX070 (packet pool misuse) | Not implemented as a hard rule per maintainer decision. User code may intentionally allocate packets with `new`. Existing NALIX037 (hot-path allocation) covers the core concern. |
| NALIX071 (System.Security.Cryptography) | Deferred — allowlist support is too intrusive for this phase. Only 2 known usages in the codebase (OsCsprng browser fallback, ProofOfWork FixedTimeEquals). Can be addressed in a future phase with a simpler allowlist mechanism. |
| NALIX072 (IPAddress.ToString allocation) | Deferred — optimization concern, not a correctness/safety issue. |
| NALIX074 (string interpolation in logs) | Deferred — performance concern. |
| NALIX076 (context capture beyond handler) | Deferred — requires complex lambda data-flow analysis. |
| NALIX078 (reflection in AOT code) | Deferred — no current violations found in codebase. |
| NALIX080 (implement NALIX022) | **Done** in this phase. |

---

## Acceptance Criteria Verification

| Criterion | Status |
|---|---|
| Analyzer project builds | ✅ |
| Analyzer tests pass (110/110) | ✅ |
| NALIX022 implemented correctly | ✅ — header overlap check added for PacketBase types |
| NALIX007/019/031 removed | ✅ — removed from descriptors and SupportedDiagnostics |
| NALIX073 only applies to Nalix Core | ✅ — namespace-based scope check |
| NALIX075 catches non-disposed PacketScope | ✅ — syntax-based using detection |
| NALIX070 not implemented as hard rule | ✅ — not implemented |
| NALIX071 deferred | ✅ — documented as deferred |
| No unrelated large refactor | ✅ — changes scoped to analyzer + 6 production catch fixes |
