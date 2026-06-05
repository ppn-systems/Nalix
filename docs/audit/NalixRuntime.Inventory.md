# Nalix.Runtime — Full Inventory Report

**Date:** 2026-06-05
**Branch:** `refactor/load-tester-and-task-manager`
**Commit:** `9c95d5db4`

---

## 1. Baseline

| Metric | Result |
|--------|--------|
| `dotnet build src/Nalix.sln` | ✅ Succeeded — 29 warnings (all in Nalix.Network, 0 in Nalix.Runtime) |
| `dotnet test tests/Nalix.Tests.sln` | ✅ 80 Runtime tests pass; 2 pre-existing Analyzers failures; SDK.Tests fails to compile (pre-existing) |
| `git status --short` | Clean |

---

## 2. Folder Map

```text
src/Nalix.Runtime/
├── Dispatching/                    # Packet dispatch infrastructure
│   ├── Options/                    # PacketDispatchOptions (partial: 3 files)
│   ├── IDispatchChannel.cs
│   ├── IPacketDispatch.cs
│   ├── IPacketMetadataProvider.cs
│   ├── InlinePacketDispatcher.cs
│   ├── PacketContext.cs
│   ├── PacketDispatchChannel.cs
│   ├── PacketDispatcherBase.cs
│   ├── PacketMetadataBuilder.cs
│   ├── PacketMetadataProviders.cs
│   └── PacketSender.cs
├── Extensions/
│   └── ConnectionExtensions.cs
├── Handlers/                       # Built-in runtime packet handlers
│   ├── HandshakeHandlers.cs
│   ├── SessionHandlers.cs
│   ├── SystemControlHandlers.cs
│   └── SystemTimeSyncHandlers.cs
├── Internal/
│   ├── Compilation/                # Handler scanning, compilation, caching
│   ├── Pooling/                    # CancellableValueTaskSource pool
│   ├── RateLimiting/               # DirectiveGuard
│   ├── Results/                    # Return-type handler adapters (10 files)
│   └── Routing/                    # DispatchChannel (work-stealing) + PaddedSequence
├── Microsoft/                      # Logging abstractions (ILogger-based)
├── Middleware/
│   ├── MiddlewarePipeline.cs
│   ├── MiddlewarePipeline.Types.cs
│   └── Standard/                   # Concurrency, Permission, RateLimit, Timeout
├── Options/                        # Configuration option classes (7 files)
├── Security/
│   └── FileCertificateStore.cs
├── Sessions/                       # Session store, service, factory
├── Throttling/                     # TokenBucket, PolicyRate, ConcurrencyGate (9 files)
├── Timekeeping/
│   └── TimeSynchronizer.cs
└── Nalix.Runtime.csproj
```text

**Total: 56 .cs files**

---

## 3. File List by Feature Area

### Dispatching (12 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| IDispatchChannel.cs | 80 | Interfaces: IDispatchSession, IDispatchChannel |
| IPacketDispatch.cs | 29 | Interface: IPacketDispatch (HandlePacket) |
| IPacketMetadataProvider.cs | 24 | Interface: IPacketMetadataProvider |
| InlinePacketDispatcher.cs | 307 | Stateless inline dispatcher (thread pool queue) |
| PacketContext.cs | 239 | Pooled packet execution context |
| PacketDispatchChannel.cs | 748 | Queued dispatcher with work-stealing workers |
| PacketDispatcherBase.cs | 74 | Abstract base for dispatchers |
| PacketMetadataBuilder.cs | 109 | Builder for PacketMetadata |
| PacketMetadataProviders.cs | 31 | Registry of IPacketMetadataProvider |
| PacketSender.cs | 171 | Async packet send helper |
| Options/PacketDispatchOptions.cs | 86 | Options container (partial part 1) |
| Options/PacketDispatchOptions.PublicMethods.cs | 369 | Fluent builder API |
| Options/PacketDispatchOptions.Execution.cs | 483 | Handler execution + error mapping |

### Handlers (4 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| HandshakeHandlers.cs | 362 | Handshake challenge-response + key exchange |
| SessionHandlers.cs | 233 | Session resume with proof-of-possession |
| SystemControlHandlers.cs | 209 | PING/PONG, DISCONNECT, ERROR, FAIL, NOTICE |
| SystemTimeSyncHandlers.cs | 93 | TimeSync request/response |

### Handler Compilation (3 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| PacketHandler.cs | 142 | Handler struct (opcode, invoker, metadata) |
| PacketHandlerCompiler.cs | 1262 | Scanner + expression-tree compiler + AOT invoker |
| PacketHandlerDescriptor.cs | 25 | Descriptor record |

### Return Type Handlers (10 files)
| File | Lines |
|------|-------|
| IReturnHandler.cs | 26 |
| ReturnTypeHandlerFactory.cs | 135 |
| VoidReturnHandler.cs | 18 |
| TaskReturnHandler.cs | 31 |
| TaskVoidReturnHandler.cs | 26 |
| ValueTaskReturnHandler.cs | 31 |
| ValueTaskVoidReturnHandler.cs | 26 |
| PacketReturnHandler.cs | 33 |
| MemoryReturnHandler.cs | 34 |
| ReadOnlyMemoryReturnHandler.cs | 34 |
| ByteArrayReturnHandler.cs | 33 |
| UnsupportedReturnHandler.cs | 19 |

### Middleware (6 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| MiddlewarePipeline.cs | 729 | Pipeline orchestrator (inbound → handler → outbound) |
| MiddlewarePipeline.Types.cs | 79 | Pipeline metrics types |
| ConcurrencyMiddleware.cs | 123 | Per-opcode concurrency enforcement |
| PermissionMiddleware.cs | 103 | Permission level check |
| RateLimitMiddleware.cs | 117 | Token-bucket + policy rate limiting |
| TimeoutMiddleware.cs | 123 | Per-packet timeout via pooled CTS |

### Throttling (9 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| TokenBucketLimiter.cs | 979 | Per-endpoint token bucket (main logic) |
| TokenBucketLimiter.Cleanup.cs | 334 | Background cleanup + eviction |
| TokenBucketLimiter.Report.cs | 366 | IReportable |
| TokenBucketLimiter.Types.cs | 126 | Shard, Endpoint, RateLimitDecision |
| PolicyRateLimiter.cs | 371 | Attribute-driven policy rate limiter |
| ConcurrencyGate.cs | 266 | Per-opcode semaphore gate |
| ConcurrencyGate.Cleanup.cs | 222 | Idle entry cleanup + circuit breaker |
| ConcurrencyGate.Report.cs | 182 | IReportable |
| ConcurrencyGate.Types.cs | 365 | Entry, Lease, circuit breaker |

### Sessions (4 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| SessionService.cs | 247 | Session CRUD + scavenger |
| InMemorySessionStore.cs | 175 | ConcurrentDictionary session store |
| SessionFactory.cs | 69 | Factory for SessionService |
| SessionPersistenceObserver.cs | 66 | Observer for auto-save |

### Timekeeping (1 file)
| File | Lines |
|------|-------|
| TimeSynchronizer.cs | 444 |

### Security (1 file)
| File | Lines |
|------|-------|
| FileCertificateStore.cs | 127 |

### Options (7 files)
| File | Lines |
|------|-------|
| ConcurrencyOptions.cs | 64 |
| DirectiveGuardOptions.cs | 28 |
| DispatchOptions.cs | 118 |
| PacketDrainOptions.cs | 89 |
| PoolingOptions.cs | 76 |
| SessionStoreOptions.cs | 55 |
| TokenBucketOptions.cs | 149 |

### Logging Infrastructure (2 files)
| File | Lines | Responsibility |
|------|-------|----------------|
| IWithLogging.cs | 26 | IWithLogging<T> interface (ILogger-based) |
| ThrottleLogExtensions.cs | 230 | Throttled logging extensions (ILogger + LoggerMessage) |

### Internal Pooling (2 files)
| File | Lines |
|------|-------|
| CancellableValueTaskSource.cs | 156 |
| CancellableValueTaskSource1.cs | 231 |

### Internal Routing (2 files)
| File | Lines |
|------|-------|
| DispatchChannel.cs | 1269 |
| PaddedSequence.cs | 15 |

### Extensions (1 file)
| File | Lines |
|------|-------|
| ConnectionExtensions.cs | 115 |

---

## 4. Public API List

### Interfaces
- IDispatchSession — exclusive connection processing session
- IDispatchChannel — dispatch channel contract
- IPacketDispatch — packet dispatch entry point
- IPacketMetadataProvider — metadata extension point
- IWithLogging<T> — fluent logging attachment (ILogger)
- IReturnHandler<TPacket> — return type normalization

### Dispatchers
- InlinePacketDispatcher — stateless thread-pool dispatcher
- PacketDispatchChannel — queued multi-worker dispatcher
- PacketDispatcherBase<TPacket> — abstract base

### Options and Builders
- PacketDispatchOptions<TPacket> — handler/middleware/dispatch configuration (fluent API)
  - WithHandler, WithMiddleware, WithLogging, WithErrorHandling, WithDispatchLoopCount
- PacketDrainOptions — drain loop configuration

### Context and Sender
- PacketContext<TPacket> — pooled execution context
- PacketSender — async packet send utility

### Middleware (all public)
- RateLimitMiddleware, PermissionMiddleware, ConcurrencyMiddleware, TimeoutMiddleware

### Throttling (all public)
- TokenBucketLimiter, PolicyRateLimiter, ConcurrencyGate

### Handlers (all public, sealed)
- HandshakeHandlers, SessionHandlers, SystemControlHandlers, SystemTimeSyncHandlers

### Sessions (all public)
- ISessionService, SessionService, InMemorySessionStore, SessionFactory

### Security (public)
- FileCertificateStore

### Options (all public)
- ConcurrencyOptions, DirectiveGuardOptions, DispatchOptions, PacketDrainOptions, PoolingOptions, SessionStoreOptions, TokenBucketOptions

---

## 5. Classes Larger Than 300 Lines

| Class | Lines | Notes |
|-------|-------|-------|
| DispatchChannel<TPacket> | 1269 | Work-stealing mailbox — cohesive |
| PacketHandlerCompiler<T,T> | 1262 | Compilation + scanning — tightly coupled |
| TokenBucketLimiter (total) | 1805 | Split across 4 partial files — acceptable |
| PacketDispatchChannel | 748 | Multi-worker dispatch |
| MiddlewarePipeline<TPacket> | 729 | Pipeline execution + snapshot + pooling |
| PacketDispatchOptions<TPacket> | 938 | Split across 3 partials — cohesive |
| TimeSynchronizer | 444 | Tick loop + lifecycle |
| PolicyRateLimiter | 371 | Policy + enforcement |
| ConcurrencyGate.Types | 365 | Entry + Lease types |

---

## 6. Diagnostics / Logging Sites

### Current State: All ILogger-Based — NO DiagnosticLog

**Total logging sites: ~65 across 14 files**

| File | Count | Levels |
|------|-------|--------|
| TimeSynchronizer.cs | 8 | Debug, Info, Warning, Error |
| TokenBucketLimiter.cs | 6 | Trace, Debug, Warning |
| TokenBucketLimiter.Cleanup.cs | 4 | Debug, Warning, Error |
| PolicyRateLimiter.cs | 3 | Info, Warning |
| ConcurrencyGate.cs | 1 | Error |
| ConcurrencyGate.Types.cs | 5 | Error, Warning |
| ConcurrencyGate.Cleanup.cs | 4 | Debug, Info, Error |
| RateLimitMiddleware.cs | 1 | Warning |
| PermissionMiddleware.cs | 1 | Trace |
| PacketHandlerCompiler.cs | 6 | Trace, Debug, Warning, Error |
| PacketDispatchOptions.PublicMethods.cs | 6 | Debug, Info |
| PacketDispatchOptions.Execution.cs | 5 | Debug, Warning, Error |
| PacketDispatchChannel.cs | 4 | Warning, Error |
| PacketSender.cs | 1 | Debug |
| SystemControlHandlers.cs | 3 | Debug, Warning, Error |

### Supporting Infrastructure
- ThrottleLogExtensions.cs — LoggerMessage source generator (6 generated methods)
- IWithLogging.cs — ILogger-based interface

### DiagnosticsEvents: **DOES NOT EXIST** in Runtime

All other modules (Network, Framework, Environment, Codec) already use DiagnosticLog.

---

## 7. Service-Locator Usage (InstanceManager.Instance)

**30 calls across 14 files**

| File | Count | What |
|------|-------|------|
| PacketHandlerCompiler.cs | 6 | ObjectPoolManager, ILogger |
| HandshakeHandlers.cs | 4 | ObjectPoolManager, ISessionService, ICertificateStore |
| RateLimitMiddleware.cs | 3 | ILogger, PolicyRateLimiter, TokenBucketLimiter |
| PacketDispatchOptions.cs | 1 | ObjectPoolManager |
| PacketDispatchOptions.PublicMethods.cs | 1 | CreateInstanceWithInjection |
| PacketDispatchChannel.cs | 1 | TaskManager |
| PacketContext.cs | 1 | ObjectPoolManager |
| PacketSender.cs | 1 | ILogger |
| DispatchChannel.cs | 2 | IConnectionHub |
| CancellableValueTaskSource.cs | 1 | ObjectPoolManager |
| CancellableValueTaskSource1.cs | 1 | ObjectPoolManager |
| TimeoutMiddleware.cs | 1 | ObjectPoolManager |
| MiddlewarePipeline.cs | 1 | ObjectPoolManager |
| PermissionMiddleware.cs | 1 | ILogger |
| ConcurrencyMiddleware.cs | 1 | ConcurrencyGate |
| SystemControlHandlers.cs | 1 | ILogger |
| TokenBucketLimiter.Cleanup.cs | 1 | TaskManager |
| PolicyRateLimiter.cs | 1 | TokenBucketLimiter |
| ConcurrencyGate.cs | 1 | TaskManager |
| SessionService.cs | 1 | TaskManager |
| TimeSynchronizer.cs | 2 | ILogger, TaskManager |

---

## 8. Concurrency-Sensitive Files

| File | Primitives | Risk |
|------|-----------|------|
| DispatchChannel.cs | ConcurrentDictionary, Interlocked, Volatile, SemaphoreSlim | HIGH |
| PacketDispatchChannel.cs | SemaphoreSlim, Interlocked, Volatile, CTS | HIGH |
| TokenBucketLimiter.cs | ConcurrentDictionary per shard, Interlocked, Volatile | HIGH |
| ConcurrencyGate.cs | ConcurrentDictionary, SemaphoreSlim, Interlocked | HIGH |
| ConcurrencyGate.Types.cs | SemaphoreSlim, Interlocked, Lock | HIGH |
| TimeSynchronizer.cs | Interlocked, Volatile, lock, PeriodicTimer, CTS | MEDIUM |
| MiddlewarePipeline.cs | Lock, Volatile, Interlocked | MEDIUM |
| InMemorySessionStore.cs | ConcurrentDictionary | LOW |

---

## 9. Test Coverage

### Runtime Tests (80 passing)
| Test File | Coverage |
|-----------|----------|
| ConcurrencyGateTests.cs | TryEnter, EnterAsync, cleanup, circuit breaker |
| DispatchChannelTests.cs | Push, claim, dequeue |
| PacketHandlerCompilerTests.cs | Compilation, signature validation |
| PolicyRateLimiterTests.cs | Policy evaluation |
| ReturnTypeHandlerTests.cs | Return handler factory |
| RuntimeDispatchAndHandlersTests.cs | Dispatcher + handler integration |
| RuntimeOptionsAndMetadataTests.cs | Options + metadata builder |
| TimeSynchronizerTests.cs | Lifecycle |
| TokenBucketLimiterTests.cs | Evaluation, cleanup |
| FileCertificateStoreTests.cs | Certificate store |

### Missing Test Areas
- TimeoutMiddleware (no tests)
- PermissionMiddleware (no tests)
- RateLimitMiddleware (no tests)
- ConcurrencyMiddleware (no tests)
- DirectiveGuard (no tests)
- Middleware pipeline execution order (no tests)
- SessionHandlers resume flow (no tests)
- HandshakeHandlers (no tests)
- PacketContext pooling lifecycle (no tests)
- ThrottleLogExtensions (no tests)
- CancellableValueTaskSource (no tests)

---

## 10. Risk Assessment

| Risk | Severity | Description |
|------|----------|-------------|
| ILogger removal | HIGH | 65 sites across 14 files + 2 infrastructure files |
| IWithLogging<T> removal | HIGH | Public interface on 4 types |
| Service locator cleanup | MEDIUM | 30 calls, some acceptable, some should be explicit |
| Large file splits | MEDIUM | DispatchChannel (1269), PacketHandlerCompiler (1262) |
| DiagnosticsEvents creation | LOW | New file, no behavior change |
| Options class cleanup | LOW | Already split into partials |

---

## 11. Project Dependencies

```xml
<ProjectReference Include="..\Nalix.Codec\Nalix.Codec.csproj" />
<ProjectReference Include="..\Nalix.Framework\Nalix.Framework.csproj" />
<ProjectReference Include="..\Nalix.Abstractions\Nalix.Abstractions.csproj" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.8" />
```

**Goal:** Remove `Microsoft.Extensions.Logging.Abstractions` after migrating to DiagnosticLog.
