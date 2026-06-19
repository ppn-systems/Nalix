# NALIX076 Final Review

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Verdict: Accept with notes

NALIX076 is safe to ship. The suppressions are justified, the false-positive surface is narrow, and the existing test coverage is adequate. Two minor documentation improvements and one noted false-negative gap are documented below.

---

## Suppression Review

### Suppression 1: `MiddlewarePipeline.cs:479`

```csharp
public void Initialize(
    MiddlewareEntry[] middlewares,
    PacketContext<TPacket> context, ...)
{
    _middlewares = middlewares;
#pragma warning disable NALIX076 // MiddlewarePipeline intentionally holds context during execution; cleared in ResetForPool.
    _context = context;
#pragma warning restore NALIX076
    _final = final;
    ...
}
```

**Verdict: Safe.** `PooledPipelineContext` is itself pooled (via `_localPool` or `ObjectPoolManager`). The `_context` field is:
- Set during `Initialize` / `InitializeFull` (bounded to pipeline execution lifetime)
- Read during `RunAsync` → middleware steps → `ExecuteTerminalHandler`
- Nulled in `ResetForPool()` (`_context = null` at line 516)
- Returned via `RETURN_RUNNER_SYNC` which calls `ResetForPool`

The context is not stored in a static field, queue, cache, event, or background task. The lifetime is bounded to the pipeline execution scope.

### Suppression 2: `MiddlewarePipeline.cs:500`

```csharp
public void InitializeFull(
    MiddlewarePipeline<TPacket> owner,
    PipelineSnapshot snapshot,
    PacketContext<TPacket> context, ...)
{
    _owner = owner;
    _snapshot = snapshot;
#pragma warning disable NALIX076 // MiddlewarePipeline intentionally holds context during execution; cleared in ResetForPool.
    _context = context;
#pragma warning restore NALIX076
    _dispatch = dispatch;
    ...
}
```

**Verdict: Safe.** Same lifecycle as Suppression 1. `InitializeFull` is the "full pipeline" variant that also stores `_dispatch` and `_handler`. All fields are cleared in `ResetForPool()`.

### Suppression Quality

- ✅ Narrow (single-line pragma around each assignment)
- ✅ Clear comment explaining why it's safe
- ✅ References `ResetForPool` cleanup
- ✅ No context escapes the `PooledPipelineContext` class boundary

---

## False Positive Review

### Confirmed NOT reported:

| Pattern | Why safe | Status |
|---|---|---|
| `await ProcessAsync(context)` | Argument type is `PacketContext<T>`, but target method is not `Task.Run`/`StartNew`/`QueueUserWorkItem` and not a collection `Add` | ✅ Not reported |
| `DemoPacket packet = context.Packet` | Local variable assignment, not field/property | ✅ Not reported |
| `context.Sender.SendAsync(...)` | Invocation on context member, not an offload or collection method | ✅ Not reported |
| `context.Connection` access | Property read, not assignment to field | ✅ Not reported |
| `using var response = PacketFactory<T>.Acquire()` | `PacketScope<T>` is not `IPacketContext<T>` or `IPacket` | ✅ Not reported |
| `_context = null` in `ResetForPool` | `ConstantValue is { HasValue: true, Value: null }` check skips null assignments | ✅ Not reported |
| Non-Core assembly (`MyApp.Handlers`) | `IsNalixCoreAssembly` check | ✅ Not reported |

### Potential false positive risk (low):

`IsCollectionAddMethod` is name-based (`"Add"`, `"Enqueue"`, `"TryWrite"`, `"Push"`, `"Insert"`). This could theoretically flag `someRandomType.Add(packet)` where `Add` is not a collection operation. However, the check also requires `firstArg.Type` to be `IPacketContext<T>` or `IPacket`, making accidental matches extremely unlikely in practice.

---

## True Positive Review

### Confirmed reported:

| Pattern | Test | Status |
|---|---|---|
| `_saved = context` (field assignment) | `ContextAssignedToField_ReportsNalix076` | ✅ Reported |
| `_savedPacket = context.Packet` (field assignment) | `ContextPacketAssignedToField_ReportsNalix076` | ✅ Reported |
| `Task.Run(() => ProcessLater(context))` | `TaskRunWithContext_ReportsNalix076` | ✅ Reported |
| `Task.Run(async () => { var p = context.Packet; })` | `TaskRunWithLambdaCapturingContext_ReportsNalix076` | ✅ Reported |
| Non-Core assembly escape | `ContextEscapeInNonCoreAssembly_DoesNotReportNalix076` | ✅ Not reported (correct) |

### Missing true positive tests (not blockers):

| Pattern | Risk | Notes |
|---|---|---|
| `ThreadPool.QueueUserWorkItem(_ => Process(context))` | Low | Lambda body walk would catch `context` reference. Not tested but logic is same as `Task.Run` path. |
| `someEvent += () => context.Packet` | Low | `AnalyzeEventAssignment` handles this. Not tested. |
| `someQueue.Enqueue(context)` | Low | `AnalyzeContextEscapeInvocation` handles this. Not tested. |

---

## Severity Review

**Current severity: Warning** — appropriate for first release.

- The analyzer is conservative (does not track data flow through locals).
- Some patterns are false negatives by design (extracting to local before offloading).
- Warning allows users to suppress if needed without breaking builds.
- No change recommended.

---

## Documentation Review

### README (`analyzers/Nalix.Analyzers/README.md`)

Current:
> `IPacketContext<T>` or its `Packet` is captured by a field, long-lived delegate, or offloaded `Task.Run`. Extract needed data into locals before offloading. Scoped to Nalix Core assemblies only.

**Assessment:** Adequate. Mentions the key patterns and the recommended fix.

### Docs (`docs/api/analyzers/diagnostic-codes.md`)

Current:
> `IPacketContext<T>` and its `Packet` are pooled and must not escape the handler scope. Capturing in a field, long-lived delegate, offloaded task, or collection causes use-after-return bugs. Extract needed data into locals before offloading.

**Assessment:** Adequate. Clear and actionable.

### Suggested improvement (not blocking):

Add a brief example of allowed vs disallowed patterns to the docs page. This would help users understand the boundary between "passing to a helper" (allowed) and "offloading to Task.Run" (disallowed).

---

## False Negative Risks

### 1. Pre-extracted local capture (documented, accepted)

```csharp
var p = context.Packet;       // local, not flagged
_ = Task.Run(() => Process(p)); // lambda captures local, not context — NOT caught
```

This is documented in the implementation report as intentional conservatism. The analyzer does not perform data-flow analysis to track that `p` originated from `context.Packet`. This is an acceptable trade-off for the first release.

### 2. Method group captures (minor gap)

```csharp
ThreadPool.QueueUserWorkItem(ProcessContext); // ProcessContext(IPacketContext<T>)
```

The `ArgumentCapturesContextOrPacket` method checks `argument.Type` (which is `WaitCallback`), not the method group's parameter types in this path. The `IMethodReferenceOperation` check is present but only fires if `argument.Value` is `IMethodReferenceOperation`, which it is for method groups. Let me re-check this...

Actually, looking at the code again at line 2396:
```csharp
if (argument.Value is IMethodReferenceOperation methodRef)
{
    foreach (IParameterSymbol param in methodRef.Method.Parameters)
    {
        if (IsPacketContextType(param.Type, symbols) || IsPacketType(param.Type, symbols))
            return true;
    }
}
```

This DOES handle method groups. If `ProcessContext(IPacketContext<T>)` is passed as a method group, the `methodRef.Method.Parameters` check would find the context parameter. So this is actually handled — not a false negative.

### 3. Static field assignments (handled)

The `AnalyzeFieldPropertyAssignment` checks `assignment.Target is not IFieldReferenceOperation and not IPropertyReferenceOperation`. `IFieldReferenceOperation` covers both instance and static fields. Static field assignments like `SharedContext = context` would be caught. ✅

---

## Tests Reviewed

| Test | Pattern | Category | Status |
|---|---|---|---|
| `ContextAssignedToField_ReportsNalix076` | Field assignment | True positive | ✅ |
| `ContextPacketAssignedToField_ReportsNalix076` | Packet field assignment | True positive | ✅ |
| `TaskRunWithContext_ReportsNalix076` | Task.Run offload | True positive | ✅ |
| `TaskRunWithLambdaCapturingContext_ReportsNalix076` | Lambda capture in Task.Run | True positive | ✅ |
| `PassingContextToHelper_DoesNotReportNalix076` | Helper method call | True negative | ✅ |
| `ExtractPacketToLocal_DoesNotReportNalix076` | Local extraction | True negative | ✅ |
| `ContextEscapeInNonCoreAssembly_DoesNotReportNalix076` | Non-Core assembly | True negative | ✅ |

**Coverage assessment:** Core patterns covered. Missing tests for event subscription, collection enqueue, static field assignment, and method group capture. These are not blockers since the logic paths are exercised by the existing tests.

---

## Validation Results

| Command | Result |
|---|---|
| `git status` | 6 modified files, 1 untracked (report) |
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 154 passed, 0 failed |
| `dotnet build` (full solution) | ✅ 0 NALIX076 warnings, build succeeded |

---

## Remaining Limitations

1. **No data-flow analysis.** Pre-extracting context data into a local before offloading bypasses detection. This is documented and accepted as conservative behavior.

2. **Name-based collection method detection.** `IsCollectionAddMethod` checks method names, not interface implementations. Extremely unlikely to cause false positives given the type check on the argument.

3. **No `ConcurrentQueue`/`Channel` specific detection.** The generic `Enqueue`/`TryWrite` name check covers these, but not all channel writer patterns (e.g., `channel.Writer.TryWrite` is covered by `TryWrite`).

4. **Lambda body walk is shallow.** Only checks direct references to context/packet types inside the lambda. Does not track through intermediate variables within the lambda body (e.g., `var x = context; Task.Run(() => Process(x))` where `x` is typed as `object`).
