# Phase 1 Final Review

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Verdict

**Accept with notes**

All implemented diagnostics are correct, production code changes are behavior-preserving for non-fatal exceptions, and tests pass. One documentation inconsistency was found and fixed during review. One missing test (`var` form for NALIX075) was added. The NALIX073 namespace-only scoping limitation is documented below and is acceptable for Phase 1.

---

## Summary

* **Diagnostics fixed:** NALIX022 — corrected from byte-offset comparison to reserved header slot check
* **Diagnostics added:** NALIX073 (unguarded catch), NALIX075 (PacketScope not disposed)
* **Diagnostics removed/deprecated:** NALIX007, NALIX019, NALIX031 (dead buffer middleware)
* **Tests added:** 14 (5 NALIX022, 5 NALIX073, 5 NALIX075); total 111
* **Production code changed:** 6 files (catch block fixes)

---

## Findings

### NALIX022

* **Correct:**
  * No longer compares `SerializeOrder` to `PacketHeaderOffset.Region` or the 12-byte header size.
  * `SerializeOrder(0)` is allowed — test `SerializeOrderZeroOnPacketBase_DoesNotReportNalix022` confirms.
  * `SerializeOrder(11)` is allowed — test `SerializeOrderBelow12OnPacketBase_DoesNotReportNalix022` confirms.
  * `SerializeHeader(0)` on non-`PacketBase<TSelf>` types is allowed — test `SerializeHeaderZeroOnNonPacketBase_DoesNotReportNalix022` confirms.
  * Inherited framework header members (`FrameBase._header`) are not reported — the `ContainingType` check filters them; full solution build produces zero NALIX022 warnings.
  * User-defined `SerializeHeader(0)` on a `PacketBase<TSelf>`-derived type reports — test `SerializeHeaderZeroOnPacketBase_ReportsNalix022` confirms.
  * `SerializeHeader(1)` on PacketBase is allowed — test `SerializeHeaderNonZeroOnPacketBase_DoesNotReportNalix022` confirms.
  * Diagnostic title/message/description correctly describes "reserved header slot" semantics, not byte-offset overlap.

* **Issues found during review:**
  * README line 33 still described the old byte-offset behavior ("overlaps the reserved packet header region (first 12 bytes)"). **Fixed** to: "A user-defined member on a `PacketBase`-derived type uses `[SerializeHeader(0)]`, but header slot 0 is reserved by Nalix packet internals."

* **Missing tests:**
  * None remaining. All acceptance criteria have test coverage.

### NALIX073

* **Scope correctness:**
  * Scoped by namespace only: `IsInNalixCoreNamespace` walks the containing namespace hierarchy looking for `"Nalix"`.
  * This means any code in a namespace starting with `Nalix.` will be checked, including hypothetical consumer namespaces like `Nalix.Game.*` or `Nalix.MyApp.*`.
  * This is **not** assembly/project-based filtering.
  * **Documented limitation:** Consumer projects using `Nalix.*` as a namespace prefix will receive NALIX073 warnings. This is an acceptable Phase 1 trade-off because: (a) Nalix Core owns the `Nalix.*` namespace, (b) consumers typically use their own namespace hierarchy, (c) `#pragma warning disable NALIX073` provides an escape hatch.
  * Generated code is correctly excluded via `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` in `Initialize`.

* **Production code behavior:**
  * All 6 changed catch blocks previously caught bare `Exception` or used non-ExceptionClassifier filters.
  * All now include `ExceptionClassifier.IsNonFatal(ex)` as the first condition in their `when` filter.
  * **SessionService.cs:** 4 bare `catch (Exception)` → `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))`. Fatal exceptions (`OutOfMemoryException`, etc.) will now propagate instead of being caught. The cleanup logic (`entry.Return()`, counter increment) will NOT execute for fatal exceptions. This is the intended behavior — if the runtime is in a fatal state, cleanup should not be attempted.
  * **HandshakeHandlers.cs:356:** `when (ex is not InternalErrorException)` → `when (ExceptionClassifier.IsNonFatal(ex) && ex is not InternalErrorException)`. Behavior unchanged for non-fatal exceptions; fatal exceptions now propagate.
  * **TokenBucketLimiter.Cleanup.cs:68:** Same pattern — added `ExceptionClassifier.IsNonFatal(ex)` guard. Added required `using Nalix.Abstractions.Exceptions;` import.
  * **PacketDispatchOptions.Execution.cs:429:** Added `ExceptionClassifier.IsNonFatal(ex)` before `IsConnectionTeardownException(ex)`.
  * **PacketBase.cs:92:** Added `ExceptionClassifier.IsNonFatal(ex)` before existing type checks.
  * **TcpFrameReader.cs:121:** Added `ExceptionClassifier.IsNonFatal(ex)` before `ex is not OperationCanceledException`.
  * All changes are strictly additive guards. No catch block now swallows exceptions that were previously rethrown — all existing `throw;` statements remain intact.

* **False-positive risks:**
  * Consumer code in `Nalix.*` namespaces (documented limitation above).
  * `catch (Exception ex) when (someOtherCheck)` patterns that are semantically safe but don't use `ExceptionClassifier` will be flagged. The test `CatchExceptionWithUnrelatedFilter_ReportsNalix073` confirms this is intentional.

* **Missing tests:**
  * No test for `catch (Exception ex) when (false)` — this would pass (no diagnostic) since the filter is not null but doesn't contain ExceptionClassifier. This is correct behavior (the filter prevents any exception from being caught).
  * No test for `catch (Exception)` with no variable and no filter — covered by `BareCatchExceptionInNalixNamespace_ReportsNalix073`.

### NALIX075

* **Correct:**
  * `using PacketScope<T> scope = ...` does not report — test `PacketScopeWithUsingDeclaration_DoesNotReportNalix075` confirms.
  * `using var scope = ...` form: The analyzer checks `localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)`. The `using var` form uses `UsingKeyword` on the `LocalDeclarationStatementSyntax`, so it is correctly accepted.
  * `using (PacketScope<T> scope = ...) { }` does not report — test `PacketScopeWithUsingStatement_DoesNotReportNalix075` confirms. Note: the `using` statement creates a `UsingStatementSyntax`, not a `LocalDeclarationStatementSyntax`, so the analyzer's `RegisterSyntaxNodeAction` for `LocalDeclarationStatement` won't even fire. This is correct — the using statement is inherently safe.
  * `PacketScope<T> scope = ...` without using reports — test `PacketScopeWithoutUsing_ReportsNalix075` confirms.
  * `var scope = ...` without using reports — test `PacketScopeVarWithoutUsing_ReportsNalix075` confirms (added during review).
  * Unrelated `IDisposable` types do not report — test `UnrelatedDisposableWithoutUsing_DoesNotReportNalix075` confirms.
  * Detection uses `OriginalDefinition` comparison for generic types, which correctly handles any `PacketScope<T>` instantiation.

* **False-positive risks:**
  * Fields of type `PacketScope<T>` are not checked — the analyzer only inspects `LocalDeclarationStatementSyntax`. This is correct because fields are a different pattern (typically managed by the class lifecycle).
  * Parameters of type `PacketScope<T>` are not checked — correct, parameters are not declarations.
  * Types from other assemblies named `PacketScope<T>` would match if they have the same fully-qualified metadata name `Nalix.Codec.Pooling.PacketScope`1`. This is extremely unlikely.

* **Missing tests:**
  * `var` form — **added** during this review.
  * Multiple variables in one declaration (`PacketScope<T> a = ..., b = ...;`) — would report once per variable. Not tested but behavior is correct by design (the `foreach` over `declaration.Variables`).

### Removed Buffer Middleware Diagnostics

* **Fully removed from active diagnostics:**
  * NALIX007, NALIX019, NALIX031 removed from `SupportedDiagnostics` array ✅
  * Removed from `DiagnosticDescriptors.cs` (replaced with `// NALIXxxx — removed` comments) ✅
  * Removed from `SymbolSet` (`NetworkBufferMiddlewareType` property and constructor parameter removed) ✅
  * No tests expecting them to fire ✅

* **Documentation status:**
  * README has a "Deprecated / Removed Diagnostics" section listing all three ✅
  * The deprecation comments in `DiagnosticDescriptors.cs` explain why: `NetworkBufferMiddlewareType` was intentionally dropped ✅

---

## Required Follow-up Changes

**Applied during this review:**

| File | Change | Reason |
|---|---|---|
| `analyzers/Nalix.Analyzers/README.md` line 33 | Updated NALIX022 description from "overlaps the reserved packet header region (first 12 bytes)" to "uses `[SerializeHeader(0)]`, but header slot 0 is reserved by Nalix packet internals" | Documentation inconsistency with actual behavior |
| `tests/.../NalixUsageAnalyzerTests.cs` | Added `PacketScopeVarWithoutUsing_ReportsNalix075` test | Missing coverage for `var` form |

No other required changes identified.

---

## Deferred Work

Keep these deferred unless explicitly requested:

* **NALIX070** (packet pool misuse as hard rule): Not implemented per maintainer decision. User code may intentionally allocate with `new`.
* **NALIX071** (System.Security.Cryptography boundary): Deferred — allowlist support too intrusive for Phase 1. Only 2 known usages in codebase.
* **NALIX076** (context/packet capture beyond handler scope): Requires complex lambda data-flow analysis.

---

## Validation Results

| Command | Result |
|---|---|
| `git status` | 13 modified files, `research/` untracked. No accidental changes. |
| `git diff --stat` | 575 insertions, 51 deletions across 13 files |
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 111 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ 0 NALIX022/NALIX073/NALIX075 warnings, 0 errors |
| NALIX022 on full build | 0 warnings — `FrameBase._header` correctly skipped |
| NALIX073 on full build | 0 warnings — all production catch blocks fixed |
| NALIX075 on full build | 0 warnings — no non-disposed PacketScope locals in codebase |