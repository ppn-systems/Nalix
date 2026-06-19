# Nalix.Analyzers Suite — Final Closeout

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`
> **Status:** Phase complete

---

## Summary

The Nalix.Analyzers suite has been fully implemented across Phases 1–2C, covering correctness, performance, security, pooling, AOT, and context lifetime enforcement for the Nalix Core codebase.

---

## Final Diagnostics Inventory

### Active Diagnostics (47 total)

| ID | Title | Severity | Category | Scope |
|---|---|---|---|---|
| NALIX001 | Duplicate PacketOpcode | Warning | Usage | All |
| NALIX002 | Missing Handler Opcode | Warning | Usage | All |
| NALIX003 | Invalid Handler Signature | Warning | Usage | All |
| NALIX004 | PacketContext type mismatch | Warning | Usage | All |
| NALIX005 | Handler packet type mismatch | Warning | Usage | All |
| NALIX006 | Middleware type mismatch | Warning | Usage | All |
| NALIX008 | Missing PacketHandler attribute | Warning | Usage | All |
| NALIX009 | Missing static Deserialize | Warning | Usage | All |
| NALIX010 | Generic Self-Type Mismatch | Warning | Usage | All |
| NALIX011 | IPacketDeserializer self-type | Warning | Usage | All |
| NALIX012 | PacketBase missing Deserialize | Warning | Usage | All |
| NALIX013 | Missing SerializeOrder | Warning | Serialization | All |
| NALIX014 | Duplicate SerializeOrder | Warning | Serialization | All |
| NALIX015 | SerializeIgnore conflicts | Warning | Serialization | All |
| NALIX016 | Unnecessary SerializeDynamicSize | Info | Serialization | All |
| NALIX017 | Invalid Deserialize signature | Warning | Usage | All |
| NALIX018 | Non-concrete packet type | Warning | Usage | All |
| NALIX020 | ResetForPool missing base call | Warning | Lifecycle | All |
| NALIX021 | Negative SerializeOrder | Warning | Serialization | All |
| NALIX022 | Reserved Header Slot | Warning | Serialization | All |
| NALIX023 | Unsupported config type | Warning | Configuration | All |
| NALIX024 | Non-bindable config property | Info | Configuration | All |
| NALIX025 | Metadata provider clears Opcode | Warning | Routing | All |
| NALIX026 | Metadata provider overwrites Opcode | Info | Routing | All |
| NALIX027 | Negative RetryCount | Warning | SDK | All |
| NALIX028 | Negative TimeoutMs | Warning | SDK | All |
| NALIX030 | Missing MiddlewareOrder | Info | Middleware | All |
| NALIX032 | Inbound AlwaysExecute ignored | Info | Middleware | All |
| NALIX033 | Duplicate MiddlewareOrder | Info | Middleware | All |
| NALIX034 | SerializeHeader conflicts | Warning | Serialization | All |
| NALIX035 | Reserved Opcode Range | Warning | Usage | All |
| NALIX036 | Global duplicate Opcode | Warning | Usage | All |
| NALIX037 | Hot path allocation | Info | Performance | All |
| NALIX038 | OpCode doc mismatch | Info | Documentation | All |
| NALIX039 | IBufferLease leak | Warning | Lifecycle | All |
| NALIX040 | Missing BufferPoolManager | Info | Performance | All |
| NALIX041 | Missing ConnectionHub | Info | Usage | All |
| NALIX042 | Invalid handler type | Warning | Usage | All |
| NALIX043 | Invalid metadata provider type | Warning | Usage | All |
| NALIX044 | Missing TCP binding | Info | Usage | All |
| NALIX045 | UDP without TCP | Info | Usage | All |
| NALIX046 | Large SerializeOrder gap | Info | Serialization | All |
| NALIX047 | Dispatch loop count out of range | Warning | Usage | All |
| NALIX048 | Unsupported return type | Warning | Usage | All |
| NALIX050 | PacketOpcode on non-controller | Info | Usage | All |
| NALIX051 | FixedSize with dynamic member | Warning | Serialization | All |
| NALIX052 | Missing Span<byte> Deserialize | Warning | Usage | All |
| NALIX054 | Duplicate PacketHandler name | Info | Usage | All |
| NALIX055 | Redundant PacketContext cast | Info | Usage | All |
| NALIX056 | Null middleware registration | Warning | Usage | All |
| NALIX057 | Infinite timeout with retries | Info | SDK | All |
| NALIX058 | Generic handler method | Warning | Usage | All |
| NALIX071 | Disallowed crypto usage | Warning | Security | Core-only |
| NALIX072 | Allocating endpoint formatting | Info | Performance | Core-only |
| NALIX073 | Unguarded catch(Exception) | Warning | Correctness | Core-only |
| NALIX074 | Eager string formatting in logging | Info | Performance | Core-only |
| NALIX075 | PacketScope not disposed | Error | Pooling | All |
| NALIX076 | Packet context escapes handler | Warning | Correctness | Core-only |
| NALIX078 | Unbounded reflection in AOT | Warning | AOT | Core-only |

### Removed Diagnostics

| ID | Reason |
|---|---|
| NALIX007 | `INetworkBufferMiddleware` intentionally dropped from Nalix |
| NALIX019 | `INetworkBufferMiddleware` intentionally dropped from Nalix |
| NALIX031 | `INetworkBufferMiddleware` intentionally dropped from Nalix |

---

## Diagnostics Fixed

| ID | What was fixed |
|---|---|
| NALIX022 | Changed from "SerializeOrder overlaps header region" to "SerializeHeader(0) reserved on PacketBase types" — the original check incorrectly treated SerializeOrder as a byte offset |

---

## Final Analyzer Test Count

**154 tests** across all test files in `tests/Nalix.Analyzers.Tests/`.

| Test File | Tests |
|---|---|
| NalixUsageAnalyzerTests.cs | 115 |
| CustomControllerAnalyzerTests.cs | 5 |
| ConfigurationAnalyzerTests.cs | 7 |
| MetadataProviderAnalyzerTests.cs | 3 |
| MiddlewareAnalyzerTests.cs | 6 |
| PacketAnalyzerTests.cs | 6 |
| RoutingCodeFixTests.cs | 3 |
| RequestOptionsAnalyzerTests.cs | 2 |
| RequestOptionsCodeFixTests.cs | 2 |
| SerializationAnalyzerTests.cs | 2 |
| AdvancedPacketAnalyzerTests.cs | 1 |
| AdvancedSerializationAnalyzerTests.cs | 2 |
| **Total** | **154** |

---

## Final Build Result

```
dotnet build src/Nalix.sln → Build succeeded. 1 Warning(s), 0 Error(s)
```

- 1 pre-existing warning: `IDE0058` in `Connection.cs:308` (unrelated to analyzers)
- **0 unexpected analyzer warnings** across the full solution
- All NALIX076 suppressions in `MiddlewarePipeline.cs` are narrow and justified (bounded pipeline lifetime, cleared in `ResetForPool`)

---

## NALIX076 Documentation Examples

Added to `docs/api/analyzers/diagnostic-codes.md` with full allowed/disallowed code samples:

**Allowed:**
- Extracting `context.Packet` into a local
- Passing `context` to a private helper awaited directly
- Using `context.Sender.SendAsync()` for responses

**Disallowed:**
- Assigning `context` to a field/property
- Offloading via `Task.Run(() => context.Packet)`
- Event subscription capturing context

**Known limitation (documented):**
- Pre-extracting `MyPacket packet = context.Packet` then `Task.Run(() => Process(packet))` is not detected — intentional trade-off in the first release (no data-flow/alias analysis)

---

## Known Limitations

1. **NALIX076 does not perform data-flow/alias analysis.** Pre-extracting a context member into a local before offloading bypasses detection. This is documented and accepted as a conservative first-release trade-off.

2. **`Activator.CreateInstance` policy is deferred.** NALIX078 intentionally does not flag `Activator.CreateInstance`. This was a deliberate design decision in Phase 2A.

3. **NALIX072 (allocating endpoint formatting)** requires manual `#pragma` suppressions for known-safe uses. Two suppressions exist in production (`PolicyRateLimiter.cs`, `TokenBucketLimiter.cs`).

4. **NALIX078 (unbounded reflection)** requires manual `#pragma` suppressions for known-safe uses. Two suppressions exist in production (`TypeMetadata.Cache.cs`, `DiagnosticChannel.cs`).

5. **NALIX073 (unguarded catch)** scope relies on `ExceptionClassifier.IsNonFatal()`. Any Nalix Core code using bare `catch (Exception)` without this filter will be flagged.

---

## Future Work (Not Implemented)

| Item | Priority | Notes |
|---|---|---|
| CI enforcement | High | Add analyzer warnings-as-errors for Nalix Core projects in CI pipeline |
| NALIX076 data-flow expansion | Medium | Track `var packet = context.Packet; Task.Run(() => Process(packet))` pattern |
| NALIX070 soft info rule | Low | Optional soft info rule for `new Packet()` in non-pool-aware code |
| `Activator.CreateInstance` policy | Low | Deferred from Phase 2A — needs AOT trimming annotation analysis |
| NALIX072 `INetworkEndpoint` API | Medium | Consider adding `TryFormatAddress`-style API to `INetworkEndpoint` to eliminate suppressions |
| Analyzer packaging | High | Publish `Nalix.Analyzers` as NuGet package for external consumers |

---

## Files Changed in Final Closeout

| File | Change |
|---|---|
| `docs/api/analyzers/diagnostic-codes.md` | Added NALIX076 examples section with allowed/disallowed/known-limitation patterns |
| `analyzers/Nalix.Analyzers/README.md` | Added cross-reference link to NALIX076 examples |
| `research/analyzers/15-analyzer-suite-final-closeout.md` | This file (new) |

---

## Validation

| Command | Result |
|---|---|
| `git status` | Clean (only research docs and analyzer docs modified) |
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 1 pre-existing style warning (IDE0066), 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 154 passed, 0 failed, 0 skipped |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded, 1 pre-existing warning (IDE0058), 0 errors |

---

## Acceptance Criteria

- [x] Docs include NALIX076 allowed/disallowed examples
- [x] Analyzer tests still pass (154/154)
- [x] Full solution builds
- [x] No unexpected analyzer warnings
- [x] No new diagnostics implemented
- [x] Stale shell/process note documented (see below)

### Stale Processes Note

Multiple `dotnet.exe` and `MSBuild.exe` processes were detected at review time. These are likely residual from previous build/test runs and the user's IDE. Killing them was not possible due to permission restrictions — the user should manually verify and terminate stale processes if needed (e.g., via Task Manager or `taskkill`).