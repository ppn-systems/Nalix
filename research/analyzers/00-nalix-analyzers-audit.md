# Nalix.Analyzers Audit

> **Audit date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`
> **Commit:** `cc592e05a`

---

## Executive Summary

- **Existing analyzer health:** Good. 47 diagnostics (NALIX001–NALIX058, with gaps at 029, 049, 053) covering routing, serialization, configuration, middleware, hosting, SDK, performance, documentation, and lifecycle categories. The single `NalixUsageAnalyzer` class is well-structured as a partial class split across three files (~95 KB total). It correctly ignores generated code, enables concurrent execution, and registers actions at `CompilationStart` for efficiency.
- **Test coverage status:** 97 tests passing in `Nalix.Analyzers.Tests`. Tests cover nearly every diagnostic and 8 code fix providers. However, some diagnostics have no dedicated test (see gaps below), and negative (no-diagnostic) test cases are sparse.
- **Stale diagnostics:** No stale diagnostics found. The `PacketHandlerCompiler` name referenced in the audit scope does not appear in the current codebase. All symbol references in `SymbolSet.Create()` are current and match the actual Nalix type metadata names. The analyzer correctly falls back when `PacketDispatchOptions` is in either `Nalix.Network.Routing` or `Nalix.Runtime.Dispatching`.
- **Highest-value new analyzers:** (1) **NALIX070** – Preventing `new` packet allocation where `PacketFactory<T>.Acquire()` is required. (2) **NALIX071** – Preventing `System.Security.Cryptography` usage outside approved locations. (3) **NALIX072** – Catching `IPAddress.ToString()` allocations on hot diagnostic/log paths. (4) **NALIX073** – Detecting bare `catch (Exception)` without `ExceptionClassifier` guard.
- **Recommended next step:** Implement the P0 analyzers (NALIX070, NALIX071, NALIX073) to prevent the most critical regression vectors: pooling misuse, crypto boundary violations, and unguarded exception swallowing.

---

## Existing Diagnostics Inventory

| ID | Title | Analyzer Class | Category | Severity | Status | Test Coverage | Notes |
|---|---|---|---|---|---|---|---|
| NALIX001 | Duplicate controller PacketOpcode | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX002 | Handler should declare PacketOpcode | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX003 | Invalid handler signature | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX004 | PacketContext type mismatch | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX005 | Handler packet type mismatch | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX006 | Middleware type mismatch | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX007 | Buffer middleware ignores stage attr | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ⚠️ No dedicated test | `NetworkBufferMiddlewareType` is null in SymbolSet; this diagnostic is dead code — can never fire |
| NALIX008 | Missing PacketHandler attribute | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix (`PacketHandlerCodeFixProvider`) |
| NALIX009 | Missing static Deserialize | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX010 | PacketBase self-type mismatch | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX011 | IPacketDeserializer self-type mismatch | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX012 | Missing Deserialize method | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Negative test only (PacketBase has inherited Deserialize) |
| NALIX013 | Explicit member missing SerializeOrder | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX014 | Duplicate SerializeOrder | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX015 | SerializeIgnore conflicts with Order | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX016 | DynamicSize on fixed member | `NalixUsageAnalyzer` | Serialization | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX017 | Deserialize signature invalid | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX018 | Registered packet not concrete | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX019 | Buffer middleware type invalid | `NalixUsageAnalyzer` | Usage | Warning | **Deprecate** | ⚠️ No test | `NetworkBufferMiddlewareType` is null in SymbolSet — dead code |
| NALIX020 | ResetForPool missing base call | `NalixUsageAnalyzer` | Lifecycle | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX021 | Negative SerializeOrder | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX022 | Member overlaps header region | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ (negative test) | Described in README table but the analysis code (`PacketMemberOverlapsHeaderRegion`) is declared in descriptors but never reported — the header overlap check is listed in `SupportedDiagnostics` but the `AnalyzeSerializationType` method does not actually call `Report` for it. **Potential false negative.** |
| NALIX023 | Unsupported config property type | `NalixUsageAnalyzer` | Configuration | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX024 | Config property not bindable | `NalixUsageAnalyzer` | Configuration | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX025 | Metadata provider clears opcode | `NalixUsageAnalyzer` | Routing | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX026 | Metadata provider overwrites without guard | `NalixUsageAnalyzer` | Routing | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX027 | RequestOptions negative RetryCount | `NalixUsageAnalyzer` | SDK | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX028 | RequestOptions negative TimeoutMs | `NalixUsageAnalyzer` | SDK | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX029 | *(gap – unused)* | — | — | — | — | — | ID not assigned |
| NALIX030 | Packet middleware missing order | `NalixUsageAnalyzer` | Middleware | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX031 | Buffer middleware missing order | `NalixUsageAnalyzer` | Middleware | Info | **Deprecate** | ⚠️ No test | Same issue as NALIX007/019 — `NetworkBufferMiddlewareType` is null |
| NALIX032 | Inbound AlwaysExecute ignored | `NalixUsageAnalyzer` | Middleware | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX033 | Duplicate middleware order in chain | `NalixUsageAnalyzer` | Middleware | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX034 | SerializeHeader conflicts with Order | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX035 | Reserved opcode range | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX036 | Global duplicate opcode | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX037 | Allocation in hot path | `NalixUsageAnalyzer` | Performance | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Limited scope (only `PacketOpcode` handlers and `InvokeAsync`); needs expansion |
| NALIX038 | OpCode doc mismatch | `NalixUsageAnalyzer` | Documentation | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Uses `Regex` — functional but could use `GeneratedRegex` for perf |
| NALIX039 | IBufferLease leak | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Heuristic-based; has known false positive/negative surface |
| NALIX040 | Missing BufferPoolManager | `NalixUsageAnalyzer` | Performance | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX041 | Missing ConnectionHub | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX042 | MapHandlers invalid type | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX043 | Metadata provider invalid type | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX044 | Missing TCP binding | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX045 | UDP without TCP | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX046 | Unusually large SerializeOrder gap | `NalixUsageAnalyzer` | Serialization | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX047 | Dispatch loop count out of range | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX048 | Unsupported handler return type | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX049 | *(gap – unused)* | — | — | — | — | — | ID not assigned |
| NALIX050 | PacketOpcode on non-controller | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX051 | FixedSizeSerializable has dynamic member | `NalixUsageAnalyzer` | Serialization | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX052 | Deserialize span overload missing | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX053 | *(gap – unused)* | — | — | — | — | — | ID not assigned |
| NALIX054 | Duplicate PacketHandler name | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX055 | Redundant PacketContext cast | `NalixUsageAnalyzer` | Usage | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX056 | Middleware registration null | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Has code fix |
| NALIX057 | Infinite timeout with retry | `NalixUsageAnalyzer` | SDK | Info | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |
| NALIX058 | Generic handler method | `NalixUsageAnalyzer` | Usage | Warning | **Keep** | ✅ `NalixUsageAnalyzerTests` | Working correctly |

### Generator Diagnostics (separate ID range)

| ID | Title | Category | Severity | Status | Notes |
|---|---|---|---|---|---|
| NALIX059 | Missing static Create() | Serialization | Error | **Keep** | Source generator validation |
| NALIX060 | Packet class must be partial | Serialization | Error | **Keep** | Source generator validation |
| NALIX063 | No accessible constructor | Injection | Error | **Keep** | Source generator validation |
| NALIX064 | Ambiguous constructors | Injection | Error | **Keep** | Source generator validation |
| NALIX065 | Singleton missing parameterless ctor | Injection | Warning | **Keep** | Source generator validation |

---

## Existing Analyzer Findings

### 1. NALIX007 / NALIX019 / NALIX031 — Buffer Middleware Diagnostics (Dead Code)

**Problem:** `SymbolSet.Create()` passes `null` for `networkBufferMiddlewareType` (line 226: `null, // networkBufferMiddlewareType removed`). The three buffer middleware diagnostics (NALIX007, NALIX019, NALIX031) depend on this symbol to detect `INetworkBufferMiddleware` implementors. Since it is always null, **these diagnostics can never fire**.

**Impact:** The diagnostics are registered in `SupportedDiagnostics` and described in the README but are effectively dead code. They create a false sense of protection.

**Recommended change:** Either re-resolve the `INetworkBufferMiddleware` symbol metadata name, or explicitly deprecate these three diagnostics and remove them from `SupportedDiagnostics`.

### 2. NALIX022 — Packet Member Overlaps Header Region (Likely False Negative)

**Problem:** `PacketMemberOverlapsHeaderRegion` is declared in `DiagnosticDescriptors.cs` and listed in `SupportedDiagnostics`, but `AnalyzeSerializationType()` never actually checks `finalOrder.Value < packetHeaderRegionOffset` against `PacketHeaderOffset.Region`. The analyzer checks for negative orders and large gaps, but not header overlap.

**Impact:** Users could assign `SerializeOrder(2)` to a payload field on a `PacketBase`-derived type without any warning, even though the first 12 bytes are the packet header.

**Recommended change:** Add a check in `AnalyzeSerializationType` that compares `finalOrder.Value` against `PacketHeaderRegionOffset` for `PacketBase`-derived types when the order is non-negative and less than the header region size.

### 3. NALIX037 — Allocation in Hot Path (Limited Scope)

**Problem:** The `IsNalixHotPath` method only considers methods with `[PacketOpcode]` and `IPacketMiddleware<T>.InvokeAsync`. It does not cover:
- Middleware `InvokeAsync` implementations beyond the exact name match.
- `IPacketDispatch` implementations.
- `PacketContext<T>` lifecycle methods.
- Dispatch channel processing loops.
- Handler helper methods called from hot paths.

Additionally, the `IsAllowedInHotPath` method permits `string` allocations, which can be problematic on extremely hot paths (e.g., endpoint formatting in `DiagnosticLog`).

**Recommended change:** Expand `IsNalixHotPath` to cover more hot-path scenarios. Consider marking methods with a `[HotPath]` attribute for explicit opt-in.

### 4. NALIX038 — Uses Regex for Doc Parsing

**Problem:** `AnalyzeOpCodeDocumentation` uses `Regex.Match(xml, @"0x([0-9A-Fa-f]{1,4})")`. While the analyzer runs at compile time (not a hot path), this could be replaced with `GeneratedRegex` or simple manual parsing for consistency with the project's zero-allocation philosophy.

**Impact:** Minor; no user-visible impact.

### 5. NALIX039 — IBufferLease Leak Detection (Heuristic)

**Problem:** The `AnalyzeBufferLeaseLeak` method uses text-based heuristics (`code.Contains(...)`, `Regex.IsMatch(...)`) to determine if a lease is disposed. This has both false positives and false negatives:
- **False positive:** A method that stores the lease in a field or returns it via a more complex expression.
- **False negative:** A method that disposes the lease through a local helper method call that isn't matched by the regex.
- **False positive on constructors:** `code.Contains("new ") && code.Contains(name)` matches any method that creates any object and uses the lease variable name.

**Recommended change:** Consider upgrading to a more robust data-flow analysis using Roslyn's `IOperation` tree to track disposal paths.

### 6. SymbolSet Resolution — PacketDispatchOptions Dual-Namespace Fallback

**Status:** Working correctly. Line 160–161 tries `Nalix.Network.Routing.PacketDispatchOptions`1` first, then falls back to `Nalix.Runtime.Dispatching.PacketDispatchOptions`1`. This is consistent with the actual codebase where the type exists in both namespaces.

### 7. Analyzer Architecture Quality

**Positive findings:**
- `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` — correctly ignores generated code.
- `context.EnableConcurrentExecution()` — safe for parallel analysis.
- `RegisterCompilationStartAction` — efficient for per-compilation symbol resolution.
- `ConcurrentDictionary` for global opcode tracking — thread-safe.
- `SymbolSet.Create()` returns null when required symbols are missing — graceful degradation.
- `NoWarn` suppresses `RS2008` (missing `SupportedDiagnostics` entries) and `CS1591` (XML doc warnings).

**Negative findings:**
- `AnalyzeMethodDeclaration` (NALIX020, NALIX055) uses `RegisterSyntaxNodeAction` for `MethodDeclaration` instead of `RegisterSymbolAction`. This means it runs on syntax, which is correct for the `base.ResetForPool()` check but forces `GetDeclaredSymbol` on every method declaration.
- The `Regex` in `AnalyzeBufferLeaseLeak` and `AnalyzeOpCodeDocumentation` is compiled per-invocation (not cached).

---

## Codebase Risk Scan

### Packet Handlers

| Risk | Location | Description |
|---|---|---|
| `new MemoryPacket(...)` in dispatch | `PacketDispatchChannel.cs:604,608`, `InlinePacketDispatcher.cs:129,133` | `MemoryPacket` is constructed directly inside the dispatch loop. This is framework-internal and intentional (wrapping a pooled buffer lease), but there is no analyzer to prevent user code from doing the same with custom packet types. |
| Handler methods using `async ValueTask` | `SystemTimeSyncHandlers.cs`, `SystemControlHandlers.cs`, `HandshakeHandlers.cs`, etc. | Handlers are correctly `async ValueTask` and use `ConfigureAwait(false)`. No issues found. |
| `IPacketContext<T>` capture beyond handler scope | None found in current codebase | The framework correctly extracts packet data before passing to handler helpers. No context-leaking closures found. |

### Pooling and Lifetime

| Risk | Location | Description |
|---|---|---|
| `PacketScope<T>` not disposed | None found | All `PacketScope<T>` usages use `using` declarations. Correct pattern. |
| `BufferLease` not disposed in SDK sessions | `WebSocketSession.cs:228`, `TcpSession.cs:310` | `BufferLease` is rented but the `using` pattern is not always explicit. Needs manual review. |
| `ObjectPoolManager` usage in middleware | `TimeoutMiddleware.cs:27` | Correctly uses `s_pool.Get<T>()` / `s_pool.Return<T>()` pattern. |

### Hot-Path Allocations

| Risk | Location | Description |
|---|---|---|
| `new IPAddress(bytes).ToString()` | `SocketEndpoint.cs:137-139, 146` | The `Address` property getter allocates a `byte[]` (4 bytes for IPv4), creates an `IPAddress`, then calls `ToString()`. This is called from 12+ diagnostic log interpolation sites in `SocketConnection.cs` and `SocketConnection.Receive.VarInt.cs`. A `TryFormatAddress` method exists but is not used in the log paths. |
| `$"... {NetworkEndpoint.Address} ..."` in logs | `SocketConnection.cs:458,543,550,579,823`, `SocketConnection.Receive.VarInt.cs:127,135,148` | String interpolation calls `Address` getter, causing allocations on every log statement even when the log level is disabled. |
| `$"... {connection?.NetworkEndpoint} ..."` in logs | `TcpListener.ProcessChannel.cs:137,150,167,277`, `TcpListener.Handle.cs:59,66,116,495,802` | Implicit `ToString()` on `NetworkEndpoint` for logging. |
| `new byte[4]` in `SocketEndpoint.Address` | `SocketEndpoint.cs:137` | Allocates on every IPv4 address formatting call. Should use `stackalloc` or the existing `TryFormatAddress`. |
| `new DiagnosticLog(...)` in log paths | Multiple files | `DiagnosticLog` constructor allocations on every log call. If `DiagnosticsEvents.Write` checks log level lazily, these are wasted. |

### Native AOT

| Risk | Location | Description |
|---|---|---|
| No `Assembly.GetTypes()` / reflection scanning | `NetworkApplicationBuilder.cs:461` (comment: "AOT-safe: assembly scanning removed") | Already fixed. The comment explicitly confirms removal. |
| `MethodInfo` used in metadata providers | `IPacketMetadataProvider.Populate(MethodInfo, PacketMetadataBuilder)` | The interface requires `MethodInfo`, which is reflection-heavy. Used in `PacketMetadataBuilder`. This is a design constraint that users must be aware of. |
| Source generators for AOT | `PacketHandlerGenerator.cs`, `SerializeFormatterGenerator.cs`, etc. | Generators correctly emit compile-time code, avoiding runtime reflection for handler dispatch and serialization. |

### Security

| Risk | Location | Description |
|---|---|---|
| `System.Security.Cryptography.RandomNumberGenerator.Fill` | `OsCsprng.cs:304` | Used only as a browser-platform fallback (`SupportedOSPlatform("browser")`). This is acceptable. |
| `System.Security.Cryptography` in `ProofOfWork.cs` | `ProofOfWork.cs:7` | `using System.Security.Cryptography;` is imported but only `CryptographicOperations.FixedTimeEquals` is used (line 75). The actual hashing uses `Nalix.Codec.Security.Hashing.Keccak256` and `HmacKeccak256`. The import is for a single utility method. This should use the Nalix internal abstraction if one exists, or at minimum be documented as an exception. |

### Networking Guard / Endpoint Handling

| Risk | Location | Description |
|---|---|---|
| IP address `ToString()` in hot paths | `SocketEndpoint.Address` getter | Allocations on every call. See "Hot-Path Allocations" above. |
| `catch (Exception)` without `ExceptionClassifier` | `SessionService.cs:94,108,140,161` | Four catch blocks catch ALL exceptions without filtering through `ExceptionClassifier.IsNonFatal()`. While these re-throw, they also perform cleanup operations that could mask fatal exceptions. Every other catch block in the codebase uses `ExceptionClassifier`. |

### Diagnostics / Logging

| Risk | Location | Description |
|---|---|---|
| String interpolation in log calls | See "Hot-Path Allocations" section | Eager evaluation of interpolated strings causes allocations even when the log is suppressed. |
| `connection.NetworkEndpoint?.ToString()` in logs | Multiple TcpListener/WebSocketListener files | Unnecessary allocation for debug/trace-level logging. |

### API Design

| Risk | Location | Description |
|---|---|---|
| `Task` return types in framework hot paths | `IThreadDispatcher.cs` (SDK) | Minor; most framework paths correctly use `ValueTask`. |
| `.Result` on ValueTask | `SocketConnection.Send.cs:240,585`, `SocketTcpTransport.cs:141`, `PacketDispatchOptions.Execution.cs:99,304` | Accessing `.Result` on a completed `ValueTask` is safe but fragile. If the ValueTask is ever not completed synchronously, this will throw. These appear to be after `IsCompleted` checks, which is the correct pattern. |

---

## Proposed New Diagnostics

| Proposed ID | Rule Name | Category | Severity | Priority | Difficulty | Code Fix? | Reason |
|---|---|---|---|---|---|---|---|
| **NALIX070** | Use PacketFactory for packet allocation | Pooling | Error | P0 | Medium | Yes | Prevents `new TPacket()` in handler/middleware code where `PacketFactory<T>.Acquire()` + `PacketScope<T>` is required |
| **NALIX071** | Avoid System.Security.Cryptography | Security | Warning | P0 | Low | No | Prevents using BCL crypto in projects that should use Nalix internal abstractions |
| **NALIX072** | Avoid IPAddress.ToString() allocation in logs | Performance | Info | P1 | Low | Yes | Prevents `new IPAddress(...).ToString()` in network hot paths where `TryFormatAddress` should be used |
| **NALIX073** | Unguarded catch(Exception) | Correctness | Warning | P0 | Low | No | Catches bare `catch (Exception)` without `ExceptionClassifier.IsNonFatal()` guard |
| **NALIX074** | Avoid string interpolation in diagnostic logs | Performance | Info | P1 | Medium | No | Catches `$"...` in `DiagnosticsEvents.Write()` calls which eagerly allocate |
| **NALIX075** | PacketScope must be disposed | Pooling | Error | P0 | Medium | Yes | Prevents `PacketScope<T>` variables that are not wrapped in `using` |
| **NALIX076** | Do not capture PacketContext beyond handler | Correctness | Error | P1 | High | No | Detects lambdas/closures capturing `IPacketContext<T>` or `PacketContext<T>` beyond handler scope |
| **NALIX077** | Avoid async on synchronous hot-path returns | Performance | Warning | P2 | Medium | No | Detects `async ValueTask` methods that could return synchronously (e.g., completed paths) |
| **NALIX078** | Reflection in AOT-sensitive code | AOT | Warning | P1 | Medium | No | Detects `Activator.CreateInstance`, `MakeGenericType`, `Expression.Compile` in framework/library code |
| **NALIX079** | Prefer ValueTask over Task in framework APIs | API Usage | Info | P2 | Medium | No | Detects public API methods returning `Task` where `ValueTask` would be more appropriate |
| **NALIX080** | PacketHeaderRegion overlap check | Serialization | Warning | P1 | Low | No | Actually implement the declared-but-unimplemented NALIX022 header overlap check |

---

## Detailed Proposed Analyzer Specs

### NALIX070 — Use PacketFactory for Packet Allocation

- **Diagnostic ID:** NALIX070
- **Title:** Use PacketFactory<T>.Acquire() instead of new for packet types
- **Category:** Pooling
- **Severity:** Error
- **Priority:** P0
- **Problem:** User code creates packet instances with `new TPacket()` instead of using the pooled `PacketFactory<T>.Acquire()` with `PacketScope<T>`. This bypasses the object pool, causes GC pressure, and breaks the packet lifecycle contract.
- **Bad code:**
  ```csharp
  [PacketOpcode(0x1200)]
  public void Handle(DemoPacket packet, IConnection connection)
  {
      var response = new ControlPacket(); // BAD: bypasses pool
      response.OpCode = 0x0001;
      connection.SendAsync(response);
  }
  ```
- **Good code:**
  ```csharp
  [PacketOpcode(0x1200)]
  public void Handle(DemoPacket packet, IConnection connection)
  {
      using PacketScope<ControlPacket> lease = PacketFactory<ControlPacket>.Acquire();
      ControlPacket response = lease;
      response.OpCode = 0x0001;
      connection.SendAsync(response);
  }
  ```
- **Detection strategy:** Register an `OperationKind.ObjectCreation` action. If the created type inherits from `PacketBase<TSelf>` or implements `IPacket` with `IPacketStaticOpcode`, and the enclosing method is a handler (`[PacketOpcode]`) or middleware (`InvokeAsync`), report the diagnostic.
- **Code fix strategy:** Replace `new TPacket()` with `PacketFactory<TPacket>.Acquire()` and wrap the variable in a `using PacketScope<TPacket>` declaration.
- **False positives:** Constructor calls inside `PacketFactory<T>.Create()` itself. Packet instantiation in tests. Packet instantiation in non-hot-path factory methods.
- **False negatives:** Packets created via reflection or factory methods that are not directly visible as `new`.
- **Test cases:** `new` in handler method → error. `new` in middleware → error. `new` outside handler → no diagnostic. `PacketFactory<T>.Acquire()` → no diagnostic.
- **Justification from current codebase:** All internal handlers (`HandshakeHandlers.cs`, `SystemControlHandlers.cs`, `SessionHandlers.cs`, etc.) use `PacketFactory<T>.Acquire()` with `using PacketScope<T>`. The pattern is well-established but not enforced by the analyzer.

### NALIX071 — Avoid System.Security.Cryptography

- **Diagnostic ID:** NALIX071
- **Title:** Use Nalix internal crypto abstractions instead of System.Security.Cryptography
- **Category:** Security
- **Severity:** Warning
- **Priority:** P0
- **Problem:** Nalix has its own crypto stack (`Nalix.Codec.Security`, `Nalix.Framework.Cryptography`). Using `System.Security.Cryptography` directly risks bypassing Nalix's crypto audit surface, may not be AOT-compatible, and creates inconsistent cryptographic behavior.
- **Bad code:**
  ```csharp
  using System.Security.Cryptography;

  public static byte[] ComputeHash(byte[] data)
  {
      using var sha = SHA256.Create();
      return sha.ComputeHash(data);
  }
  ```
- **Good code:**
  ```csharp
  using Nalix.Codec.Security.Hashing;

  public static void ComputeHash(ReadOnlySpan<byte> data, Span<byte> output)
  {
      Keccak256.HashData(data, output);
  }
  ```
- **Detection strategy:** Register a `SymbolAction` on `UsingDirectiveSyntax` or track `ITypeSymbol` references to types under the `System.Security.Cryptography` namespace. Flag any usage outside of approved files/projects (e.g., `OsCsprng.cs` which is explicitly a fallback).
- **Code fix strategy:** Not automated — too context-dependent. Just report the diagnostic.
- **False positives:** The `ProofOfWork.cs` import that uses only `CryptographicOperations.FixedTimeEquals`. The `OsCsprng.cs` browser fallback. These could be suppressed with `#pragma warning disable NALIX071` or an allowlist.
- **False negatives:** Indirect crypto usage through helper methods.
- **Test cases:** `using System.Security.Cryptography; SHA256.HashData(...)` → warning. `CryptographicOperations.FixedTimeEquals(...)` → warning (suppressible). Internal crypto usage → no diagnostic.
- **Justification from current codebase:** `ProofOfWork.cs:7` imports `System.Security.Cryptography` (only uses `FixedTimeEquals`). `OsCsprng.cs:304` uses `RandomNumberGenerator.Fill` as a browser fallback. An allowlist mechanism is needed.

### NALIX072 — Avoid IPAddress.ToString() in Network Hot Paths

- **Diagnostic ID:** NALIX072
- **Title:** Use TryFormatAddress instead of IPAddress.ToString() for zero-allocation formatting
- **Category:** Performance
- **Severity:** Info
- **Priority:** P1
- **Problem:** `new IPAddress(bytes).ToString()` allocates an `IPAddress` object and a string on each call. `SocketEndpoint` already provides `TryFormatAddress(Span<char>, out int)` for zero-allocation formatting, but the allocating `Address` property is still used in 12+ diagnostic log interpolation sites.
- **Bad code:**
  ```csharp
  string remoteEndpoint = connection?.NetworkEndpoint?.ToString() ?? "<null>";
  DiagnosticsEvents.Write(..., new DiagnosticLog("...", $"endpoint={connection.NetworkEndpoint.Address}"));
  ```
- **Good code:**
  ```csharp
  Span<char> buf = stackalloc char[45];
  if (connection?.NetworkEndpoint?.TryFormatAddress(buf, out int written) == true)
  {
      // use buf[..written]
  }
  ```
- **Detection strategy:** Register `OperationKind.ObjectCreation` for `IPAddress` creation followed by `.ToString()`, or `OperationKind.Invocation` of `IPAddress.ToString()` or `INetworkEndpoint.Address` getter in methods annotated with `[MethodImpl]` or inside `DiagnosticsEvents.Write` calls.
- **Code fix strategy:** Suggest replacing with `TryFormatAddress` where available, or defer formatting to the logging infrastructure.
- **False positives:** Address formatting in non-hot-path code (admin interfaces, error reporting).
- **False negatives:** Address formatting through intermediate variables.
- **Test cases:** `new IPAddress(bytes).ToString()` → info. `TryFormatAddress(...)` → no diagnostic.
- **Justification from current codebase:** `SocketEndpoint.cs:137-146` allocates. 12+ log interpolation sites in `SocketConnection.cs` and `TcpListener.cs` call the allocating getter.

### NALIX073 — Unguarded catch(Exception)

- **Diagnostic ID:** NALIX073
- **Title:** catch(Exception) should filter through ExceptionClassifier.IsNonFatal()
- **Category:** Correctness
- **Severity:** Warning
- **Priority:** P0
- **Problem:** Catching bare `catch (Exception)` without a `when (ExceptionClassifier.IsNonFatal(ex))` filter can swallow fatal exceptions (`OutOfMemoryException`, `StackOverflowException`, `ThreadAbortException`, `AccessViolationException`) during cleanup or error handling. The entire Nalix codebase uses `ExceptionClassifier` for this purpose.
- **Bad code:**
  ```csharp
  catch (Exception)
  {
      entry.Return();
      throw;
  }
  ```
- **Good code:**
  ```csharp
  catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
  {
      entry.Return();
  }
  ```
- **Detection strategy:** Register `OperationKind.CatchClause` analysis. If the catch clause catches `System.Exception` (or a base type) without a `when` filter containing `ExceptionClassifier.IsNonFatal`, report the diagnostic. Exclude `catch (Exception ex) when (...)` patterns that already have guards.
- **Code fix strategy:** Add `when (ExceptionClassifier.IsNonFatal(ex))` filter. If the catch block re-throws, suggest removing the catch entirely or adding the filter.
- **False positives:** Code that intentionally catches all exceptions for logging and re-throws. Code in test projects.
- **False negatives:** Catch filters that check exception type but don't use `ExceptionClassifier`.
- **Test cases:** `catch (Exception) { throw; }` → warning. `catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))` → no diagnostic. `catch (IOException)` → no diagnostic (specific type).
- **Justification from current codebase:** `SessionService.cs:94,108,140,161` has four bare `catch (Exception)` blocks. Every other catch in the Runtime and Network projects uses `ExceptionClassifier.IsNonFatal(ex)`. This represents a clear regression pattern that should be prevented.

### NALIX075 — PacketScope Must Be Disposed

- **Diagnostic ID:** NALIX075
- **Title:** PacketScope<T> must be wrapped in a using declaration or statement
- **Category:** Pooling
- **Severity:** Error
- **Priority:** P0
- **Problem:** `PacketScope<T>` wraps a pooled packet and must be disposed to return it to the pool. If a `PacketScope<T>` variable is not declared with `using`, the packet will leak.
- **Bad code:**
  ```csharp
  PacketScope<Control> lease = PacketFactory<Control>.Acquire();
  // forgot 'using' — packet leaks
  ```
- **Good code:**
  ```csharp
  using PacketScope<Control> lease = PacketFactory<Control>.Acquire();
  ```
- **Detection strategy:** Register `OperationKind.VariableDeclaration` or symbol analysis. If the variable type is `PacketScope<T>` and the variable does not have a `using` declaration modifier, report the diagnostic.
- **Code fix strategy:** Add `using` keyword to the variable declaration.
- **False positives:** `PacketScope<T>` stored in a field (unlikely but possible pattern). Implicit conversion to `TPacket` without storing the scope (the implicit conversion would dispose via the consuming method, but this is fragile).
- **False negatives:** Packet scopes stored in collections or passed to methods.
- **Test cases:** `PacketScope<T> x = ...` → error. `using PacketScope<T> x = ...` → no diagnostic.
- **Justification from current codebase:** Every `PacketScope<T>` usage in the codebase uses `using`. The struct's `Dispose()` returns the packet to the pool. Forgetting `using` is the most common bug vector for this pattern.

### NALIX076 — Do Not Capture PacketContext Beyond Handler Scope

- **Diagnostic ID:** NALIX076
- **Title:** PacketContext or pooled packet must not be captured in closures or stored beyond handler scope
- **Category:** Correctness
- **Severity:** Error
- **Priority:** P1
- **Problem:** `IPacketContext<T>` and the packet it wraps are pooled objects. Capturing them in a lambda, closure, or stored field beyond the handler method's scope leads to use-after-return bugs.
- **Bad code:**
  ```csharp
  [PacketOpcode(0x1200)]
  public ValueTask Handle(PacketContext<MyPacket> ctx)
  {
      _ = Task.Run(() => ProcessLater(ctx.Packet)); // BAD: ctx captured beyond scope
      return ValueTask.CompletedTask;
  }
  ```
- **Good code:**
  ```csharp
  [PacketOpcode(0x1200)]
  public ValueTask Handle(PacketContext<MyPacket> ctx)
  {
      MyPacket copy = ctx.Packet.Clone(); // extract data
      _ = Task.Run(() => ProcessLater(copy)); // OK: copy is not pooled
      return ValueTask.CompletedTask;
  }
  ```
- **Detection strategy:** Register `OperationKind.AnonymousFunction` analysis. If the anonymous function captures a parameter of type `IPacketContext<T>`, `PacketContext<T>`, or `PacketBase<T>`, and the anonymous function is passed to `Task.Run`, stored in a field, or returned from the method, report the diagnostic.
- **Code fix strategy:** Not automated — the fix depends on the user's intent.
- **False positives:** Async continuations that complete within the handler scope. Callbacks that are guaranteed to execute before the handler returns.
- **False negatives:** Captures through intermediate variables. Captures through `async` state machines.
- **Test cases:** `Task.Run(() => ctx.Packet)` in handler → error. `await` in handler → no diagnostic.
- **Justification from current codebase:** No current instances found (the framework is well-disciplined), but this is the highest-risk category of bug that cannot currently be detected at compile time.

### NALIX078 — Reflection in AOT-Sensitive Code

- **Diagnostic ID:** NALIX078
- **Title:** Avoid reflection APIs in AOT-sensitive framework code
- **Category:** AOT
- **Severity:** Warning
- **Priority:** P1
- **Problem:** Native AOT compilation cannot guarantee that reflection-heavy APIs (`Activator.CreateInstance`, `MakeGenericType`, `Expression.Compile`, `Assembly.GetTypes`) work correctly. Nalix targets AOT, so these should be avoided in framework code.
- **Bad code:**
  ```csharp
  var instance = Activator.CreateInstance(type); // May fail under AOT
  ```
- **Good code:**
  ```csharp
  // Use source-generated activator or IActivatable<T>.Create()
  ```
- **Detection strategy:** Register `OperationKind.Invocation` for calls to `Activator.CreateInstance`, `Type.MakeGenericType`, `Expression.Compile`, `Assembly.GetTypes`. Flag only within `Nalix.*` namespaces (not user code or tests).
- **Code fix strategy:** Not automated.
- **False positives:** Usage in non-AOT projects. Usage in test code. Usage behind `[DynamicallyAccessedMembers]` annotations.
- **False negatives:** Reflection through third-party libraries.
- **Test cases:** `Activator.CreateInstance(type)` in `Nalix.Runtime` → warning. Same in `Nalix.Tests` → no diagnostic.
- **Justification from current codebase:** The codebase has already removed these patterns (see `NetworkApplicationBuilder.cs:461` comment). This analyzer protects against regression.

### NALIX080 — Implement PacketHeaderRegion Overlap Check

- **Diagnostic ID:** NALIX080
- **Title:** SerializeOrder value overlaps packet header region (fix for NALIX022)
- **Category:** Serialization
- **Severity:** Warning
- **Priority:** P1
- **Problem:** NALIX022 is declared but never actually reported. `PacketBase<TSelf>` reserves the first `PacketHeaderOffset.Region` bytes (12) for the packet header. Any explicit `SerializeOrder` value less than 12 on a `PacketBase`-derived type overlaps with the header.
- **Bad code:**
  ```csharp
  [SerializePackable(SerializeLayout.Explicit)]
  public sealed class MyPacket : PacketBase<MyPacket>
  {
      [SerializeOrder(2)] // Overlaps header!
      public int Field { get; set; }
  }
  ```
- **Good code:**
  ```csharp
  [SerializePackable(SerializeLayout.Explicit)]
  public sealed class MyPacket : PacketBase<MyPacket>
  {
      [SerializeOrder(12)] // After header region
      public int Field { get; set; }
  }
  ```
- **Detection strategy:** In `AnalyzeSerializationType`, after computing `finalOrder`, check if the type inherits from `PacketBase<TSelf>` and `finalOrder.Value < PacketHeaderRegionOffset`.
- **Code fix strategy:** Suggest incrementing `SerializeOrder` to at least `PacketHeaderOffset.Region`.
- **False positives:** None (the check is deterministic for `PacketBase`-derived types).
- **False negatives:** Nested types that indirectly overlap.
- **Test cases:** `SerializeOrder(5)` on PacketBase child → warning. `SerializeOrder(12)` → no diagnostic.
- **Justification from current codebase:** NALIX022 is listed in `SupportedDiagnostics` but the check is not implemented in the analysis code.

---

## Recommended Implementation Order

### Phase 1: P0 — Prevent Critical Regressions (1–2 weeks)

1. **NALIX073** — Unguarded `catch(Exception)`. Low difficulty, immediate safety impact. The four instances in `SessionService.cs` should be fixed in the same PR.
2. **NALIX075** — `PacketScope<T>` must be `using`. Low-medium difficulty, prevents pool leaks.
3. **NALIX070** — `new` packet allocation detection. Medium difficulty, prevents pool bypass. Code fix available.
4. **NALIX071** — `System.Security.Cryptography` boundary. Low difficulty, with an allowlist for approved files.

### Phase 2: P1 — Protect Performance and AOT Invariants (2–3 weeks)

5. **NALIX080** — Implement the NALIX022 header overlap check (fix existing dead diagnostic).
6. **NALIX072** — `IPAddress.ToString()` allocation detection. Low difficulty.
7. **NALIX076** — Context/packet capture detection. High difficulty, requires lambda data-flow analysis.
8. **NALIX078** — Reflection in AOT code. Medium difficulty.

### Phase 3: P2 — Quality and Performance Polish (3–4 weeks)

9. **NALIX074** — String interpolation in diagnostic logs.
10. **NALIX077** — Async on synchronous returns.
11. **NALIX079** — `ValueTask` over `Task` in public APIs.

### Phase 4: Documentation and CI Enforcement

12. Fix dead diagnostics (NALIX007, NALIX019, NALIX031) — either re-wire the `INetworkBufferMiddleware` symbol or deprecate these IDs.
13. Add negative test cases for all existing diagnostics to catch false positives.
14. Add `TreatWarningsAsErrors` for analyzer diagnostics in the `Nalix.*` projects (currently the analyzer is referenced via `ProjectReference` but warnings are not escalated to errors).
15. Document all analyzer rules in `docs/` and link from `README.md`.
16. Ensure CI workflows (`ci-windows.yml`, `ci-linux.yml`) run analyzer tests as part of the PR gate.

---

## Final Verdict

**Nalix.Analyzers is partially sufficient.**

The existing analyzer suite is well-engineered: 47 diagnostics covering routing, serialization, configuration, middleware, and hosting, with 97 passing tests and 8 code fix providers. The architecture is sound (compilation-start registration, concurrent execution, generated code exclusion, graceful null-symbol handling).

However, the analyzer has significant gaps in the areas that matter most for Nalix's core architecture:

1. **Pooling is not enforced.** There is no diagnostic preventing `new TPacket()` instead of `PacketFactory<T>.Acquire()`, or detecting missing `using` on `PacketScope<T>`. These are the most common and most damaging bugs in a pooled networking framework.

2. **Exception safety is not enforced.** Bare `catch (Exception)` without `ExceptionClassifier` filtering exists in production code (`SessionService.cs`) and there is no diagnostic to prevent regression.

3. **Security boundaries are not enforced.** `System.Security.Cryptography` usage is not gated by the analyzer.

4. **Three diagnostics are dead code.** NALIX007, NALIX019, and NALIX031 can never fire because the `INetworkBufferMiddleware` symbol is null. NALIX022 is declared but its check is not implemented.

5. **Hot-path allocation detection is limited.** NALIX037 only covers `[PacketOpcode]` handlers and `InvokeAsync`, missing the most impactful allocation site: `IPAddress.ToString()` in network diagnostic logging.

**Bottom line:** The analyzer correctly protects against API misuse and serialization mistakes, but it does not yet protect the two pillars that make Nalix viable — **zero-allocation pooling** and **AOT-safe patterns**. Implementing the P0 analyzers (NALIX070, NALIX073, NALIX075) would close the most critical gaps.