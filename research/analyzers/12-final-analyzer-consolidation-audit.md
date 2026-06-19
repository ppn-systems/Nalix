# Final Nalix.Analyzers Consolidation Audit

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Verdict

**Accept with notes** — small consistency fixes applied during this audit.

---

## Summary

- **Active diagnostics added:** NALIX071, NALIX072, NALIX073, NALIX074, NALIX075, NALIX078
- **Diagnostics fixed:** NALIX022 (corrected to reserved `SerializeHeader(0)` semantics)
- **Diagnostics removed/deprecated:** NALIX007, NALIX019, NALIX031
- **Test count:** 147 (108 in NalixUsageAnalyzerTests + 39 in other test files)
- **Build status:** ✅ Full solution builds with zero unexpected analyzer warnings

---

## Descriptor and SupportedDiagnostics Review

**Findings:**
- All 44 active descriptors in `DiagnosticDescriptors.cs` are listed in `SupportedDiagnostics`. ✅
- Removed descriptors NALIX007, NALIX019, NALIX031 have `// removed` comments and are NOT in `SupportedDiagnostics`. ✅
- No ID collisions. IDs 029, 049, 053 are intentionally unused gaps. ✅
- NALIX022 descriptor correctly says "SerializeHeader(0) is reserved on PacketBase types" — no mention of byte offset or 12 bytes. ✅
- All descriptor titles, messages, categories, severities, and descriptions are accurate. ✅
- NALIX078 description does NOT say "Activator.CreateInstance is forbidden" — it says "unbounded reflection". ✅

**Required changes applied:**
- Removed unused `HasDynamicallyAccessedConstructorsAnnotation` method (leftover from deferred Activator.CreateInstance detection, caused IDE0051 warning).

---

## Documentation Review

**Findings:**

1. **`analyzers/Nalix.Analyzers/README.md`**: ✅ Active diagnostics table matches `SupportedDiagnostics`. NALIX071/072/073/074/078 documented as Core-only. NALIX075 documented clearly. NALIX078 mentions `Activator.CreateInstance` is deferred.

2. **`docs/api/analyzers/diagnostic-codes.md`**: ❌ **Was stale.** Missing all Phase 2 diagnostics (NALIX071–078). NALIX007/019/031 still shown as active. NALIX022 had old description.

3. **README ordering**: ❌ NALIX073 and NALIX075 were listed after NALIX078 instead of in ID order.

**Required changes applied:**
- Updated `docs/api/analyzers/diagnostic-codes.md`:
  - Marked NALIX007, NALIX019, NALIX031 as **Removed** with strikethrough.
  - Updated NALIX022 description to match corrected semantics.
  - Added new "Correctness, Security, Pooling, and AOT Codes" section with NALIX071, NALIX072, NALIX073, NALIX074, NALIX075, NALIX078. Each includes scope (Core-only where applicable) and NALIX078 notes Activator.CreateInstance deferral.
- Fixed README ordering: NALIX073 and NALIX075 now in correct ID order.

---

## Core-only Scope Review

**Findings:**

`IsNalixCoreAssembly` is the single shared helper. ✅

All Core-only diagnostics reuse it:
- NALIX071 (line 1701) ✅
- NALIX072 (line 1803) ✅
- NALIX073 (line 1618) ✅
- NALIX074 (line 2070) ✅
- NALIX078 (line 1907) ✅

Allowlist includes: `Nalix.Abstractions`, `Nalix.Codec`, `Nalix.Environment`, `Nalix.Framework`, `Nalix.Network`, `Nalix.Runtime`, `Nalix.SDK`, `Nalix.Observability`, `Nalix.Observability.Extensions`, `Nalix.Hosting`. ✅

Consumer assemblies (`Nalix.Message`, `Nalix.Tests`, etc.) are excluded by exact match. ✅

**Limitation documented:** Future Core assemblies must be added manually. This is noted in `IsNalixCoreAssembly` comments.

---

## Test Coverage Review

**Test count by diagnostic:**

| Diagnostic | Positive tests | Negative tests | Core scope tests | Non-Core negative | Total |
|---|---|---|---|---|---|
| NALIX022 | 1 | 2 | N/A (all assemblies) | N/A | 3 |
| NALIX071 | 2 | 2 | 2 (Core assemblies) | 2 (MyApp, Nalix.Tests) | 8 |
| NALIX072 | 3 | 2 | 3 (Nalix.Network) | 2 (MyApp, Nalix.Tests) | 7 |
| NALIX073 | 2 | 3 | 2 (Nalix.Runtime) | 2 (MyApp, Nalix.Tests) | 7 |
| NALIX074 | 3 | 5 | 3 (Nalix.Network) | 2 (MyApp, Nalix.Tests) | 9 |
| NALIX075 | 2 | 3 | N/A (all assemblies) | N/A | 5 |
| NALIX078 | 6 | 5 | 6 (Nalix.Framework) | 2 (MyApp, Nalix.Tests) | 11 |

**Missing tests:** None significant. All diagnostics have positive, negative, and scope tests. ✅

**NALIX074 guarded test:** `InterpolationInsideIsEnabledGuard_DoesNotReportNalix074` ✅

**NALIX078 Activator tests:**
- `ActivatorCreateInstanceGeneric_DoesNotReportNalix078` ✅
- `ActivatorCreateInstanceStaticType_DoesNotReportNalix078` ✅
- Deferred comment in test file ✅

**Removed diagnostics:** No active "should report" tests for NALIX007/019/031. ✅

---

## Production Suppression Review

**Suppressions found:**

| File | Suppression | Has comment | Narrow | Risk |
|---|---|---|---|---|
| `TypeMetadata.Cache.cs:61` | NALIX078 | ✅ "Intentional one-time metadata inspection" | ✅ Single line | Low — static constructor, one-time per type |
| `DiagnosticChannel.cs:226` | NALIX078 | ✅ "Intentional diagnostic payload inspection" | ✅ Two lines | Low — diagnostic bridge, not hot path |
| `DiagnosticChannel.cs:251` | NALIX078 | ✅ "Intentional diagnostic property enumeration" | ✅ Single line | Low — same diagnostic bridge |
| `PolicyRateLimiter.cs:90` | NALIX072 | ✅ (fixed this audit) "ScopedEndpoint proxies Address" | ✅ Single property | Low — proxy pattern |
| `PolicyRateLimiter.cs:126` | NALIX072 | ✅ (fixed this audit) "ToString() fallback" | ✅ Single line | Low — ToString, not hot path |
| `TokenBucketLimiter.cs:263` | NALIX072 | ✅ (fixed this audit) "Validation check" | ✅ Single line | Low — one-time per endpoint |
| `TokenBucketLimiter.cs:930` | NALIX072 | ✅ (fixed this audit) "INetworkEndpoint lacks zero-alloc API" | ✅ Single line | Medium — hot path, but no alternative exists |
| `NetworkApplication.cs:19-20` | NALIX040/041 | ✅ Inline comments | ✅ File-level | Low — intentional bootstrap |

**Risky suppressions:** None. All suppressions are narrow, commented, and address intentional patterns.

---

## Fixes Applied in This Audit

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Removed unused `HasDynamicallyAccessedConstructorsAnnotation` method; replaced with deferred note comment |
| `analyzers/Nalix.Analyzers/README.md` | Fixed NALIX073/075 ordering; added Activator.CreateInstance deferral note to NALIX078 |
| `docs/api/analyzers/diagnostic-codes.md` | Marked NALIX007/019/031 as Removed; updated NALIX022; added NALIX071–078 section |
| `src/Nalix.Runtime/Throttling/PolicyRateLimiter.cs` | Added comments to NALIX072 suppressions |
| `src/Nalix.Runtime/Throttling/TokenBucketLimiter.cs` | Added comments to NALIX072 suppressions |

---

## Validation Results

| Command | Result |
|---|---|
| `git status` | Clean — all changes committed or staged |
| `git diff --stat` | 5 files changed, 23 insertions, 33 deletions |
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors, 1 IDE0066 style suggestion |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 147 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded, 0 NALIX warnings |

---

## Recommended Next Phase

**Recommended: Stop here for analyzer work.** The analyzer suite now covers the critical safety/performance/AOT surface area. Further analyzer work has diminishing returns.

If the team wants to continue, the priority order would be:

1. **Analyzer packaging/CI enforcement** — Ensure the analyzer NuGet package is consumed by all Nalix projects and CI gates on analyzer warnings. This is infrastructure work, not new diagnostics.

2. **NALIX076 packet/context capture analyzer** — Detects lambdas/closures capturing `IPacketContext<T>` or pooled packets beyond handler scope. High value but high implementation complexity (requires data-flow analysis).

3. **Documentation polish** — The `docs/api/analyzers/diagnostic-codes.md` could benefit from code examples for each diagnostic.

4. **NALIX070 soft packet allocation info diagnostic** — Low priority. The existing NALIX037 already covers hot-path allocations. A softer "prefer PacketFactory" rule would be informational only.

**Do not implement NALIX076 without a dedicated design phase** — lambda capture detection is significantly harder than the other diagnostics implemented.