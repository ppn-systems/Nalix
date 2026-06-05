# Nalix.Runtime Refactor Plan

**Date:** 2026-06-05
**Branch:** `refactor/load-tester-and-task-manager`
**Base Commit:** `9c95d5db4`

---

## Baseline

- **Build result:** ✅ Succeeded — 29 warnings (all in Nalix.Network, 0 in Nalix.Runtime)
- **Test result:** ✅ 80 Runtime tests pass; 2 pre-existing Analyzers failures unrelated
- **Existing warnings:** 0 in Nalix.Runtime
- **Existing failures:** 0 in Runtime tests

---

## Current Architecture Summary

Nalix.Runtime is the packet processing and middleware layer of the Nalix networking framework.

**Core flow:**
``
Connection receives packet
  → IPacketDispatch.HandlePacket(lease, connection)
    → Extract opcode from raw span
    → Resolve PacketHandler from ConcurrentDictionary
    → Deserialize (or wrap as MemoryPacket)
    → Execute middleware pipeline (Permission → Concurrency → RateLimit → Timeout)
      → Invoke compiled handler delegate
        → Normalize return type → send response
    → Return PacketContext to pool
``

**Two dispatcher implementations:**
1. InlinePacketDispatcher — queues to ThreadPool, no background workers
2. PacketDispatchChannel — N background worker loops with work-stealing

**Compilation:**
- PacketHandlerCompiler scans controllers for [PacketOpcode] methods
- Builds expression-tree delegates (or AOT-compatible CreateDelegate)
- Caches in FrozenDictionary per controller type

**Middleware:**
- Ordered pipeline with Inbound/Outbound/OutboundAlways stages
- Snapshot-based for lock-free reads during execution
- Local 32-slot pool for PooledPipelineContext

**Throttling:**
- TokenBucketLimiter — sharded per-endpoint, background cleanup
- PolicyRateLimiter — attribute-driven, delegates to TokenBucketLimiter
- ConcurrencyGate — per-opcode SemaphoreSlim with optional FIFO queue

---

## Dependency Graph

``
Nalix.Abstractions (DiagnosticLog, IConnection, IPacket, etc.)
  ↑
Nalix.Framework (InstanceManager, TaskManager, ObjectPoolManager)
  ↑
Nalix.Codec (PacketRegistry, ProtocolFrames, Serialization)
  ↑
Nalix.Runtime
  ↑
Nalix.Network (uses Runtime dispatchers + handlers)
  ↑
Nalix.Hosting (DiagnosticChannel bridges events → ILogger)
``

---

## Responsibility Map

| Layer | Owns | Does NOT Own |
|-------|------|-------------|
| Dispatching | Handler lookup, context creation, middleware invocation | Transport I/O, codec serialization |
| Compilation | Method scanning, delegate building, caching | Packet registration, codec format |
| Middleware | Policy enforcement ordering, timeout, rate limit | Transport framing, connection state |
| Throttling | Token buckets, concurrency semaphores, cleanup | Transport-level backpressure |
| Handlers | Protocol-level request handling | Transport socket lifecycle |
| Sessions | Session CRUD, persistence, scavenger | Connection acceptance |

---

## Issues

### I-001 | CRITICAL | No DiagnosticsEvents in Runtime — all logging uses ILogger

**Severity:** HIGH
**Files:** 14 source files + 2 infrastructure files
**Issue:** Runtime is the only module still using Microsoft.Extensions.Logging. All other modules (Network, Framework, Environment, Codec) have been migrated to DiagnosticLog.
**Why it matters:** Runtime has a hard dependency on Microsoft.Extensions.Logging.Abstractions package. DiagnosticLog is the standard diagnostics payload across the codebase. The Hosting DiagnosticChannel does not subscribe to Runtime events.
**Proposed fix:** Create Runtime.DiagnosticsEvents, migrate all 65 logging sites to DiagnosticLog, remove ILogger dependency.

### I-002 | HIGH | IWithLogging<T> interface depends on ILogger

**Severity:** HIGH
**Files:** Microsoft/IWithLogging.cs, TokenBucketLimiter.cs, PolicyRateLimiter.cs, ConcurrencyGate.cs, PacketDispatchOptions.cs
**Issue:** IWithLogging<T> accepts ILogger. Four public types implement it.
**Why it matters:** Removing ILogger requires either removing IWithLogging<T> (breaking change) or changing its signature.
**Proposed fix:** Remove IWithLogging<T> interface. The DiagnosticLog pattern does not require per-instance logger injection — events go through the shared DiagnosticListener.

### I-003 | HIGH | ThrottleLogExtensions uses ILogger + LoggerMessage source generator

**Severity:** HIGH
**Files:** Microsoft/ThrottleLogExtensions.cs (230 lines)
**Issue:** Entire file is ILogger-based throttled logging infrastructure.
**Why it matters:** Must be removed or replaced when ILogger is removed.
**Proposed fix:** Replace with DiagnosticLog-based throttled logging. Use ThrottleKey + DiagnosticListener.IsEnabled guard.

### I-004 | MEDIUM | Service-locator overuse (30 InstanceManager calls)

**Severity:** MEDIUM
**Files:** 14 files
**Issue:** Many types resolve dependencies via InstanceManager.Instance at construction time. Some are legitimate (ObjectPoolManager, TaskManager), but others should be explicit constructor parameters.
**Why it matters:** Hidden dependencies make testing harder and lifecycle unclear.
**Proposed fix:** In Phase 2, convert ILogger calls to DiagnosticLog (eliminating 8 InstanceManager calls for ILogger). For remaining calls, evaluate case-by-case in later batches.

### I-005 | MEDIUM | PacketHandlerCompiler is 1262 lines

**Severity:** MEDIUM
**Files:** Internal/Compilation/PacketHandlerCompiler.cs
**Issue:** Single file handles scanning, signature validation, expression-tree building, AOT fallback, bridge invokers, return type wrapping, and metadata lookup.
**Why it matters:** Hard to navigate. All methods are tightly coupled, so splitting requires care.
**Proposed fix:** Split into partial files: scanning, expression-tree builder, AOT builder, bridge invokers, helpers. Only if inventory proves needed.

### I-006 | MEDIUM | DispatchChannel is 1269 lines

**Severity:** MEDIUM
**Files:** Internal/Routing/DispatchChannel.cs
**Issue:** Per-connection mailbox + work-stealing queue + session management + reporting in one file.
**Why it matters:** Hard to navigate.
**Proposed fix:** Evaluate partial split. All concepts are cohesive (queue + session = one abstraction).

### I-007 | LOW | Namespace mismatch for Options

**Severity:** LOW
**Files:** Dispatching/Options/*.cs use namespace `Nalix.Runtime.Routing` instead of `Nalix.Runtime.Dispatching.Options`
**Issue:** Options files live in Dispatching/Options/ folder but declare namespace Routing.
**Why it matters:** Confusing for developers.
**Proposed fix:** Keep as-is (preserving public API). Add XML doc comment noting the namespace.

### I-008 | LOW | Missing test coverage for middleware

**Severity:** LOW
**Files:** No test files for TimeoutMiddleware, PermissionMiddleware, RateLimitMiddleware, ConcurrencyMiddleware
**Issue:** Four public middleware types have zero direct tests.
**Why it matters:** Refactoring middleware diagnostics without tests risks undetected behavior changes.
**Proposed fix:** Add basic middleware tests in Batch 4.

---

## Proposed Batches

### Batch 1 — Diagnostics Migration (ILogger → DiagnosticLog)

**Scope:** Create Runtime.DiagnosticsEvents, migrate all 65 logging sites, remove ILogger dependency.

**Risk:** HIGH (touches 14+ files, all logging paths)

**Files to change:**
- NEW: `DiagnosticsEvents.cs` (top-level namespace Nalix.Runtime)
- MODIFY: `Timekeeping/TimeSynchronizer.cs` (8 sites)
- MODIFY: `Throttling/TokenBucketLimiter.cs` (6 sites)
- MODIFY: `Throttling/TokenBucketLimiter.Cleanup.cs` (4 sites)
- MODIFY: `Throttling/PolicyRateLimiter.cs` (3 sites)
- MODIFY: `Throttling/ConcurrencyGate.cs` (1 site)
- MODIFY: `Throttling/ConcurrencyGate.Types.cs` (5 sites)
- MODIFY: `Throttling/ConcurrencyGate.Cleanup.cs` (4 sites)
- MODIFY: `Middleware/Standard/RateLimitMiddleware.cs` (1 site)
- MODIFY: `Middleware/Standard/PermissionMiddleware.cs` (1 site)
- MODIFY: `Internal/Compilation/PacketHandlerCompiler.cs` (6 sites)
- MODIFY: `Dispatching/Options/PacketDispatchOptions.PublicMethods.cs` (6 sites)
- MODIFY: `Dispatching/Options/PacketDispatchOptions.Execution.cs` (5 sites)
- MODIFY: `Dispatching/PacketDispatchChannel.cs` (4 sites)
- MODIFY: `Dispatching/PacketSender.cs` (1 site)
- MODIFY: `Handlers/SystemControlHandlers.cs` (3 sites)
- MODIFY: `Dispatching/InlinePacketDispatcher.cs` (uses ThrottledWarn/Error)
- DELETE: `Microsoft/ThrottleLogExtensions.cs` (replace with DiagnosticLog-based)
- DELETE: `Microsoft/IWithLogging.cs` (remove interface)
- MODIFY: `Nalix.Runtime.csproj` (remove Microsoft.Extensions.Logging.Abstractions)
- MODIFY: `Hosting/Internal/DiagnosticChannel.cs` (register Runtime listener)

**Migration pattern for each site:**

Before:
``
if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("[RT.ClassName:Method] action key={Value}", value);
}
``

After:
``
if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
{
    DiagnosticsEvents.Source.Write(
        DiagnosticsEvents.Internal.Debug,
        new DiagnosticLog(
            "RT.ClassName:Method",
            $"action key={value}"));
}
``

**IWithLogging<T> removal:**
- Remove interface definition
- Remove `WithLogging(ILogger)` methods from TokenBucketLimiter, PolicyRateLimiter, ConcurrencyGate, PacketDispatchOptions
- These types no longer need per-instance logger injection

**ThrottleLogExtensions replacement:**
- Remove ThrottleLogExtensions.cs entirely
- In InlinePacketDispatcher and PacketDispatchChannel, replace `connection.ThrottledWarn(logger, key, message)` with a simple DiagnosticLog guard using ThrottleKey
- Add a minimal internal helper or inline the guard

**Validation:**
``
dotnet build src/Nalix.sln
dotnet test tests/Nalix.Tests.sln
rg "Microsoft.Extensions.Logging|ILogger|LogTrace|LogDebug|LogInformation|LogWarning|LogError|LogCritical" src/Nalix.Runtime
rg "new \{ Action|new \{ Operation|new \{ Message|new \{ Reason|Payload|Fields|Args" src/Nalix.Runtime
rg "Action = \"[A-Z]|Operation = \"[A-Z]|Message = \"[A-Z]|Reason = \"[A-Z]" src/Nalix.Runtime
rg "CallerMemberName|StackTrace" src/Nalix.Runtime
``

**Public API changes:**
- REMOVED: IWithLogging<T> interface
- REMOVED: WithLogging(ILogger) from TokenBucketLimiter, PolicyRateLimiter, ConcurrencyGate, PacketDispatchOptions
- REMOVED: Microsoft.Extensions.Logging.Abstractions package dependency
- ADDED: Nalix.Runtime.DiagnosticsEvents static class (public)

**Behavior changes:** NONE — diagnostic output format changes from ILogger structured logging to DiagnosticLog, but the Hosting DiagnosticChannel renders them equivalently.

---

### Batch 2 — Handler Compiler Cleanup (if needed)

**Scope:** Evaluate splitting PacketHandlerCompiler into partial files.

**Risk:** MEDIUM

**Files:**
- MODIFY: `Internal/Compilation/PacketHandlerCompiler.cs` → split into:
  - PacketHandlerCompiler.cs (CompileHandlers entry point + scanning)
  - PacketHandlerCompiler.Signature.cs (SignatureKind + RESOLVE_SIGNATURE_KIND)
  - PacketHandlerCompiler.ExpressionTree.cs (BUILD_ARG_EXPRESSIONS, expression-tree path)
  - PacketHandlerCompiler.AotInvokers.cs (BUILD_AOT_INVOKER and all CreateDelegate builders)
  - PacketHandlerCompiler.Bridge.cs (INVOKE_CONTEXT_BRIDGE_ASYNC, bridge invokers)
  - PacketHandlerCompiler.ReturnWrappers.cs (WRAP_RETURN_TYPE, async wrappers)
  - PacketHandlerCompiler.Helpers.cs (GET_PACKET_METADATA, FORMAT_HANDLER_INFO, THROW_*)

**Validation:**
``
dotnet build src/Nalix.sln
dotnet test tests/Nalix.Tests.sln
``

**Public API changes:** NONE (all methods are internal/private)
**Behavior changes:** NONE

---

### Batch 3 — Dispatching Cleanup

**Scope:** Minor cleanup of dispatch files.

**Risk:** LOW

**Possible changes:**
- Evaluate if PacketDispatchOptions.Execution.cs can be reduced
- No splits unless justified
- Add XML docs where missing
- Preserve all dispatch behavior

**Validation:**
``
dotnet build src/Nalix.sln
dotnet test tests/Nalix.Tests.sln
``

---

### Batch 4 — Middleware Tests + Cleanup

**Scope:** Add missing tests for standard middleware.

**Risk:** LOW

**Files:**
- NEW: test files for TimeoutMiddleware, PermissionMiddleware, RateLimitMiddleware, ConcurrencyMiddleware

**Validation:**
``
dotnet build src/Nalix.sln
dotnet test tests/Nalix.Tests.sln
``

---

### Batch 5 — Throttling Cleanup

**Scope:** Minor cleanup of throttling files.

**Risk:** LOW

**Possible changes:**
- Verify cleanup worker behavior is correct
- Add XML docs where missing
- Preserve all rate limiting behavior

---

### Batch 6 — Dead Code and Docs

**Scope:** Remove unused code (with rg proof), update XML docs.

**Risk:** LOW

---

## Do Not Change

- Packet opcodes
- Packet serialization format
- Handler attribute behavior ([PacketOpcode], [PacketPermission], etc.)
- Middleware execution order (Permission → Concurrency → RateLimit → Timeout)
- Throttling semantics (token bucket math, cleanup intervals, circuit breaker thresholds)
- Concurrency semantics (SemaphoreSlim, ConcurrentDictionary patterns)
- Timeout semantics (PooledCancellationTokenSource + CancelAfter)
- Permission semantics (StrictMatch, MinimumLevel)
- Dispatch loop behavior (work-stealing, claim/dequeue, drain limits)
- Public API signatures (except IWithLogging<T> removal)
- PacketContext pooling semantics
- ObjectPoolManager integration

---

## Approval Needed

The plan is ready for review. Please approve before implementation begins.

**Key decisions requiring approval:**

1. **IWithLogging<T> removal** — This is a public interface. Removing it is a breaking change. Alternative: keep interface but change signature to accept no arguments (DiagnosticLog is global). Recommendation: Remove it since DiagnosticLog does not require per-instance injection.

2. **ThrottleLogExtensions removal** — This removes 230 lines of ILogger-based throttled logging. The replacement will be inline DiagnosticLog guards with ThrottleKey. The ThrottledWarn/ThrottledError helpers will be replaced with a simpler pattern.

3. **Batch ordering** — Batch 1 (diagnostics) is the highest-risk batch. Should it be split into smaller sub-batches (e.g., 1a: create DiagnosticsEvents, 1b: migrate throttling, 1c: migrate middleware, 1d: remove ILogger)?
