# Nalix Core Audit Report

## 1. Executive Summary

The Nalix Core codebase is a mature, high-performance .NET 10 networking framework targeting latency-sensitive real-time workloads. The overall architecture is well-structured with clean layer boundaries (Abstractions -> Environment -> Framework -> Codec -> Network -> Runtime -> Hosting -> SDK).

**Overall Health:** Good - the codebase demonstrates strong engineering discipline: consistent use of `Interlocked` for lock-free atomics, `ArrayPool`/custom slab pooling for hot-path buffer management, `stackalloc` for small frames, and structured logging throughout. All 823 tests pass and the build produces 0 warnings.

**Highest-Risk Areas:**
- **TimingWheel idle-timeout logic inversion** - the `ExcludeFromIdleTimeout` check is inverted, causing connections that should be timed out to silently escape monitoring and connections that should NOT be timed out to be disconnected.
- **Async send path missing serialization** - `SendAsync` on `SocketConnection` does not acquire the `_sendLock`, allowing concurrent async sends to interleave wire frames.
- **Blocking sync-over-async WebSocket send** - `WsFrameSender.Send` uses `SemaphoreSlim.Wait()` + `.GetAwaiter().GetResult()` on an inherently async `ClientWebSocket.SendAsync`, risking thread-pool starvation and deadlocks.

| Severity | Count |
|----------|-------|
| CRITICAL | 1     |
| WARNING  | 10    |
| NITPICK  | 3     |

**Production-Readiness:** The codebase is **nearly production-ready**. The CRITICAL TimingWheel bug should be fixed before any production deployment. The WARNING-level async send serialization issue should be addressed soon to prevent subtle wire-frame corruption under concurrent load.

---

## 2. Scan Metadata

| Field | Value |
|-------|-------|
| Date | 2026-06-04 |
| Commit | `50e3d8d7d635f41aea09fd4b84d6be873ac14002` |
| .NET SDK | 10.0.300 |
| OS | Windows 10.0.26200 (win-x64) |
| Solution | `src/Nalix.sln` (9 projects) |
| Scanned .cs files | 493 (excluding .g.cs) |
| Build result | 0 warnings, 0 errors |
| Test result | 823 passed, 0 failed |

---

## 3. Component Coverage

| Component | Files Scanned | Status | Notes |
|-----------|--------------|--------|-------|
| Nalix.Abstractions | 110 | Audited | Interfaces, attributes, packet contracts, networking primitives |
| Nalix.Environment | 31 | Audited | Config binding, BufferLease, LEB128, DataReader/Writer, CSPRNG, Clock |
| Nalix.Framework | 47 | Audited | InstanceManager, TaskManager, BufferPoolManager, ObjectPoolManager, Snowflake |
| Nalix.Codec | 89 | Audited | LiteSerializer, LZ4, ChaCha20, Salsa20, X25519, Poly1305, AEAD, FramePipeline |
| Nalix.Network | 75 | Audited | SocketConnection, TCP/UDP/WS listeners, ConnectionGuard, TimingWheel, ProxyProtocol |
| Nalix.Runtime | 66 | Audited | PacketDispatcher, MiddlewarePipeline, SessionHandlers, HandshakeHandlers, TokenBucketLimiter |
| Nalix.Hosting | 22 | Audited | Bootstrap, NetworkApplicationBuilder, service registrar |
| Nalix.Logging | 24 | Audited | NLogix, console/file sinks, StringBuilder pool, timestamp cache |
| Nalix.SDK | 29 | Audited | TcpSession, UdpSession, WebSocketSession, frame readers/senders, handshake/resume |

---

## 4. Findings Summary

| ID | Severity | Component | File | Title | Status |
|----|----------|-----------|------|-------|--------|
| CRIT-001 | CRITICAL | Nalix.Network | TimingWheel.cs | TimingWheel idle-timeout check is inverted | Confirmed |
| WARN-001 | WARNING | Nalix.Network | SocketConnection.Send.cs | Async send path missing _sendLock serialization | Confirmed |
| WARN-002 | WARNING | Nalix.SDK | WsFrameSender.cs | Synchronous Send blocks thread on async WebSocket | Confirmed |
| WARN-003 | WARNING | Nalix.Network | SocketConnection.cs | $"" in ThrottledWarn call violates AGENTS.md | Confirmed |
| WARN-004 | WARNING | Nalix.Runtime | PacketDispatchChannel.cs | $"" in ThrottledWarn/Error calls violates AGENTS.md | Confirmed |
| WARN-005 | WARNING | Nalix.Runtime | InlinePacketDispatcher.cs | $"" in ThrottledWarn call violates AGENTS.md | Confirmed |
| WARN-006 | WARNING | Nalix.Network | Connection.Guard.Cleanup.cs | Fire-and-forget Task.Run without exception observation | Confirmed |
| WARN-007 | WARNING | Nalix.Network | TimingWheel.cs | Activate/Deactivate race on _cts assignment | Confirmed |
| WARN-008 | WARNING | Nalix.Network | SocketConnection.Receive.VarInt.cs | ThrottledError wrapped in IsEnabled(Trace) guard - never fires in Release | Confirmed |
| WARN-009 | WARNING | Nalix.SDK | WsFrameReader.cs | MemoryStream + potential ToArray() for large WebSocket frames | Confirmed |
| WARN-010 | WARNING | Nalix.Codec | PacketScope.cs | PacketScope.Dispose() does not null-guard after return | Needs Verification |
| NIT-001 | NITPICK | Nalix.Framework | InstanceManager.cs | HashSet allocated per Register call even when nothing to dispose | Confirmed |
| NIT-002 | NITPICK | Nalix.Network | Connection.cs | _rateLimitCache lazily allocated as ConcurrentDictionary per connection | Confirmed |
| NIT-003 | NITPICK | Nalix.Framework | TaskManager.cs | List allocated in GetWorkers/GetRecurring every call | Confirmed |

---

## 5. Confirmed Findings

---

### CRIT-001: TimingWheel idle-timeout check is inverted

**Severity:** CRITICAL
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Internal/Time/TimingWheel.cs`
**Member:** `TimingWheel.RUN_LOOP`

**Evidence:**

`csharp
// TimingWheel.cs, lines 650-659
// Idle-time check
if (!connection.ExcludeFromIdleTimeout)       // BUG: condition is inverted
{
    connection.IsRegisteredInWheel = false;
    connection.TimeoutVersion++;
    connection.TimeoutTask = null;
    _poolManager.Return(task);
    task = next;
    continue;                                  // exits WITHOUT checking idle time
}

// Idle-time check (only reached when ExcludeFromIdleTimeout == TRUE)
long idleMs = Clock.UnixMillisecondsNow() - connection.LastPingTime;
`

**Problem:**

The `ExcludeFromIdleTimeout` property semantics (per `IConnection.cs` docs):
- `true` = connection is excluded from idle timeout management (should NOT be disconnected for idleness)
- `false` = connection is NOT excluded (SHOULD be disconnected when idle)

The code does the **opposite**:
- When `ExcludeFromIdleTimeout == false` (should be checked): the connection is silently removed from the wheel - it will never be timed out.
- When `ExcludeFromIdleTimeout == true` (should be skipped): the code checks idle time and disconnects the connection.

The default value is `true` (`Connection.cs` line 142), so the default behavior happens to check idle time correctly (by accident). But any connection that explicitly sets `ExcludeFromIdleTimeout = false` to opt INTO idle-timeout enforcement will instead silently escape monitoring.

**Impact:**
- Connections that opt into idle-timeout (`ExcludeFromIdleTimeout = false`) are silently removed from the wheel and leak indefinitely if the application does not explicitly manage their lifetime.
- Connections that are excluded (`true`, the default) are correctly checked only by coincidence.

**Recommendation:**

Swap the condition so that `ExcludeFromIdleTimeout == true` causes the connection to be re-scheduled without idle checking, and `false` falls through to the idle-time check.

**Suggested diff:**

`diff
- if (!connection.ExcludeFromIdleTimeout)
+ if (connection.ExcludeFromIdleTimeout)
  {
-     connection.IsRegisteredInWheel = false;
-     connection.TimeoutVersion++;
-     connection.TimeoutTask = null;
-     _poolManager.Return(task);
+     // Re-schedule - this connection is excluded from idle timeout
+     long ticks = Math.Max(1, _idleTimeoutMs / (long)_tickMs);
+     task.Version = connection.TimeoutVersion;
+     task.Rounds = (int)((ticks - 1) / _wheelSize);
+
+     int nextBucket = _useMask
+         ? (int)((tickToProcess + ticks) & _mask)
+         : (int)((tickToProcess + ticks) % _wheelSize);
+
+     task.Next = null;
+     task.Prev = null;
+     _wheel[nextBucket].Enqueue(task);
      task = next;
      continue;
  }
`

**Risk of change:** Low - the logic is clearly inverted. Adding a unit test that registers a connection with `ExcludeFromIdleTimeout = false` and verifies it gets disconnected after idle timeout will confirm the fix.

---

### WARN-001: Async send path missing _sendLock serialization

**Severity:** WARNING
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Internal/Transport/SocketConnection.Send.cs`
**Member:** `SocketConnection.SendAsync`

**Evidence:**

`csharp
// SocketConnection.Send.cs, sync path (line 91-113) - USES lock:
lock (_sendLock)
{
    int sent = 0;
    while (sent < frameS.Length)
    {
        int n = _socket.Send(frameS[sent..]);
        ...
    }
}

// SocketConnection.Send.cs, async path (line 282-303) - NO lock:
int sent = 0;
while (sent < totalLength)
{
    ValueTask<int> vt = _socket.SendAsync(...);
    // no _sendLock anywhere in this path
}
`

The same pattern exists in `SocketConnection.Send.VarInt.cs` (lines 137-158 for async vs 47-62 for sync which uses `lock`).

**Problem:**

The synchronous `Send` and `SEND_VARINT` methods protect the socket write loop with `lock (_sendLock)`. The async counterparts (`SendAsync`, `SEND_VARINT_ASYNC`) do NOT acquire `_sendLock`. If two threads call `SendAsync` concurrently, their frame bytes can interleave on the wire, corrupting the stream.

**Impact:** Under concurrent async sends (e.g., broadcast to the same connection, or application code sending while the protocol handler sends an ACK), wire frames can be corrupted. This manifests as intermittent protocol errors, deserialization failures, or connection drops.

**Recommendation:**

Use `SemaphoreSlim.WaitAsync()` to serialize async sends:

`diff
+ private readonly SemaphoreSlim _asyncSendLock = new(1, 1);

  public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
  {
+     await _asyncSendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
+     try
+     {
          // existing send loop
+     }
+     finally
+     {
+         _asyncSendLock.Release();
+     }
  }
`

**Risk of change:** Medium - adds a contention point under high concurrent send load, but correctness requires serialization.

---

### WARN-002: Synchronous Send blocks thread on async WebSocket

**Severity:** WARNING
**Component:** Nalix.SDK
**File:** `src/Nalix.SDK/Transport/Internal/Ws/WsFrameSender.cs`
**Member:** `WsFrameSender.SEND_RAW`

**Evidence:**

`csharp
// WsFrameSender.cs, lines 92-115
private bool SEND_RAW(ReadOnlyMemory<byte> frame)
{
    ClientWebSocket socket = _getSocket();
    if (socket.State != WebSocketState.Open) { return false; }

    _sendLock.Wait();       // sync wait on SemaphoreSlim
    try
    {
        socket.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None)
               .AsTask()
               .GetAwaiter()
               .GetResult();   // sync-over-async
        return true;
    }
    ...
}
`

**Problem:**

`ClientWebSocket.SendAsync` is inherently async (uses `SslStream` underneath). Calling `.GetAwaiter().GetResult()` blocks the calling thread until the SSL write completes. Combined with `_sendLock.Wait()`, this blocks a thread-pool thread for the duration of the SSL write and risks deadlock in single-threaded contexts. `CancellationToken.None` means the send cannot be cancelled.

**Impact:** Thread-pool starvation under high send throughput on WebSocket connections. Potential deadlock in single-threaded contexts.

**Recommendation:**

Remove the synchronous `Send(ReadOnlySpan<byte>)` overload for WebSocket transport, or make it wrap the async path with a timeout CancellationToken.

**Risk of change:** Medium - removing the sync overload is a public API change.

---

### WARN-003: $"" in ThrottledWarn call violates AGENTS.md logging rules

**Severity:** WARNING
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Internal/Transport/SocketConnection.cs`
**Member:** `SocketConnection.SAEA_RECEIVE_LOOP_ASYNC` (fragment eviction)

**Evidence:**

`csharp
// SocketConnection.cs, lines 398-400
_owner?.ThrottledWarn(
    _logger, s_keyEvictedFragments,
    $"evicted {evicted} stale fragment stream(s) ep={_owner.NetworkEndpoint.Address}");
`

**Problem:**

AGENTS.md rule 1: "No $"" in any Log call". AGENTS.md also states: "These rules also apply to `ThrottledError` calls." Using $"" means the string is always interpolated even when the log level is disabled or the throttle suppresses the message.

**Recommendation:** Replace with message template: `"[NW.SocketConnection:Receive] evicted {EvictedCount} stale fragment stream(s) ep={EndpointAddress}"` with corresponding arguments.

**Risk of change:** Low.

---

### WARN-004: $"" in ThrottledWarn/Error calls in PacketDispatchChannel

**Severity:** WARNING
**Component:** Nalix.Runtime
**File:** `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`
**Member:** `PacketDispatchChannel.ExecutePacketAsync`

**Evidence:**

`csharp
// PacketDispatchChannel.cs, lines 550-553
connection.ThrottledWarn(
    this.Logging, s_keyExecute,
    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] no-handler opcode={opcode}");

// PacketDispatchChannel.cs, lines 679-682
connection.ThrottledError(
    owner.Logging, s_keyExecute,
    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] handler-error ep={connection.NetworkEndpoint}");
`

**Problem:** Same as WARN-003. Additionally uses `nameof()` in templates, violating Rule 7.

**Recommendation:** Replace with message template placeholders and hardcoded bracket prefixes.

**Risk of change:** Low.

---

### WARN-005: $"" in ThrottledWarn call in InlinePacketDispatcher

**Severity:** WARNING
**Component:** Nalix.Runtime
**File:** `src/Nalix.Runtime/Dispatching/InlinePacketDispatcher.cs`
**Member:** `InlinePacketDispatcher.ExecutePacketAsync`

**Evidence:**

`csharp
// InlinePacketDispatcher.cs, lines 103-106
connection.ThrottledWarn(
    this.Logging, s_keyExecute,
    $"[RT.{nameof(InlinePacketDispatcher)}:{nameof(ExecutePacketAsync)}] no-handler opcode={opcode}");
`

**Problem:** Same as WARN-003 - `$""` in Throttled calls.

**Recommendation:** Replace with message template placeholders.

**Risk of change:** Low.

---

### WARN-006: Fire-and-forget Task.Run without exception observation

**Severity:** WARNING
**Component:** Nalix.Network
**File:** `src/Nalix.Network/RateLimiting/Connection.Guard.Cleanup.cs`
**Member:** `ConnectionGuard.OnFileChanged`

**Evidence:**

`csharp
// Connection.Guard.Cleanup.cs, lines 257-271
if (Interlocked.CompareExchange(ref _reloadPending, 1, 0) == 0)
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(300).ConfigureAwait(false);
        try
        {
            this.CHECK_FILE_CHANGES();
        }
        finally
        {
            _ = Interlocked.Exchange(ref _reloadPending, 0);
        }
    });
}
`

**Problem:** The `Task.Run` result is discarded. If `CHECK_FILE_CHANGES()` throws, the exception is unobserved. Configuration file changes may be silently ignored if the method throws (e.g., file I/O error, deserialization failure).

**Impact:** Configuration file changes (blacklist, trusted proxies) may be silently ignored.

**Recommendation:** Add exception observation with logging inside the task body.

**Risk of change:** Low.

---

### WARN-007: Activate/Deactivate race on _cts assignment

**Severity:** WARNING
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Internal/Time/TimingWheel.cs`
**Member:** `TimingWheel.Activate` / `TimingWheel.Deactivate`

**Evidence:**

`csharp
// Activate (lines 286-311):
if (Interlocked.Increment(ref _activeListeners) > 1) { return; }
CancellationTokenSource linkedCts = ...;
_cts = linkedCts;  // assigned AFTER increment check
_worker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(...);

// Deactivate (lines 324-343):
int count = Interlocked.Decrement(ref _activeListeners);
if (count > 0) { return; }
CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
if (cts is null) { return; }  // if Activate hasn't assigned _cts yet, deactivation is lost
`

**Problem:** If Thread A calls `Activate` (increments to 1, starts setting up) and Thread B calls `Deactivate` concurrently (decrements to 0), Thread B may read `_cts` as `null` because Thread A has not assigned it yet. The worker loop will never be stopped.

**Impact:** Low probability in practice (Activate/Deactivate are lifecycle calls with clear temporal ordering), but the race exists and could cause resource leaks in test scenarios or hot-reload configurations.

**Recommendation:** Use a `Lock` to synchronize setup/teardown.

**Risk of change:** Low - the lock is only taken on lifecycle transitions (rare), not on the hot path.

---

### WARN-008: ThrottledError wrapped in IsEnabled(Trace) guard - never fires in Release

**Severity:** WARNING
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Internal/Transport/SocketConnection.Receive.VarInt.cs`
**Member:** `SocketConnection.SAEA_RECEIVE_LOOP_VARINT_ASYNC`

**Evidence:**

`csharp
// SocketConnection.Receive.VarInt.cs, lines 125-134
catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
{
    if (_logger != null && _logger.IsEnabled(LogLevel.Trace))  // Trace guard
    {
        Exception e = (ex as AggregateException)?.Flatten() ?? ex;
        _owner.ThrottledError(
            _logger, s_keyReceiveVarIntFaulted,
            "[NW.SocketConnection:ReceiveVarInt] faulted ep=" + _owner.NetworkEndpoint.Address, e);
    }
}
`

The same pattern exists in `SocketConnection.cs` line 480-489 (the non-VarInt receive loop).

**Problem:** `ThrottledError` calls `ILogger.LogError()` internally. Wrapping it in `IsEnabled(LogLevel.Trace)` means the error is never logged in production (where the minimum level is typically `Information` or `Warning`). Errors in the receive loop are silently swallowed.

**Impact:** Production operators will not see receive-loop errors, making it difficult to diagnose connection issues, malformed packets, or protocol violations.

**Recommendation:** Remove the `Trace` guard or change it to `Warning`:

`diff
- if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
+ if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
`

Or remove the guard entirely since `ThrottledError` already handles the level check internally.

**Risk of change:** Low.

---

### WARN-009: MemoryStream + potential ToArray() for large WebSocket frames

**Severity:** WARNING
**Component:** Nalix.SDK
**File:** `src/Nalix.SDK/Transport/Internal/Ws/WsFrameReader.cs`
**Member:** `WsFrameReader.RECEIVE_LARGE_FRAME_ASYNC`

**Evidence:**

`csharp
// WsFrameReader.cs, lines 107-153
private async Task RECEIVE_LARGE_FRAME_ASYNC(...)
{
    using MemoryStream ms = new();
    // ... accumulate data ...
    if (ms.TryGetBuffer(out ArraySegment<byte> fullBuffer))
    {
        this.PROCESS_FRAME(fullBuffer.AsSpan());
    }
    else
    {
        this.PROCESS_FRAME(ms.ToArray());  // allocation + copy for large frames
    }
}
`

**Problem:** `MemoryStream` is used as a growable buffer. When the internal buffer is reallocated, `TryGetBuffer` returns `false` and `ms.ToArray()` allocates a new array. For large messages (up to `MaxMessageSize`), this is a significant allocation.

**Impact:** Under high-throughput large-WebSocket-frame scenarios, this causes GC pressure.

**Recommendation:** Pre-rent a pooled buffer of `MaxMessageSize` and use it as the accumulation buffer instead of `MemoryStream`.

**Risk of change:** Medium - requires careful bounds checking but eliminates all intermediate allocations.

---

### WARN-010: PacketScope.Dispose() does not null-guard after return

**Severity:** WARNING (Needs Verification)
**Component:** Nalix.Codec
**File:** `src/Nalix.Codec/Pooling/PacketScope.cs`
**Member:** `PacketScope<TPacket>.Dispose`

**Evidence:**

`csharp
// PacketScope.cs, line 52
public void Dispose() => _packet?.Dispose();
`

**Problem:** `PacketScope` is a `readonly struct`. After `Dispose()`, the `_packet` field still holds a reference to the packet object. If the scope is accidentally used after disposal, it will return a disposed packet that may have been returned to the pool and reused by another caller - a use-after-return bug.

**Impact:** Low in practice because `using` patterns are enforced by the compiler. But in manual `try/finally` patterns, a post-dispose access could return a stale/recycled packet.

**Recommendation:** Consider making `_packet` non-readonly with a null-after-dispose pattern for defensive safety.

**Risk of change:** Low.

---

### NIT-001: HashSet allocated per Register call

**Severity:** NITPICK
**Component:** Nalix.Framework
**File:** `src/Nalix.Framework/Injection/InstanceManager.cs`
**Member:** `InstanceManager.Register<T>`

**Evidence:** Line 225: `HashSet<object> prevsToDispose = new(ReferenceEqualityComparer.Instance);` - allocates on every call.

**Recommendation:** Use a thread-static or pooled HashSet, or only allocate when a replacement actually occurs.

**Risk of change:** Low.

---

### NIT-002: _rateLimitCache lazily allocated per connection

**Severity:** NITPICK
**Component:** Nalix.Network
**File:** `src/Nalix.Network/Connections/Connection.cs`
**Member:** `Connection.RateLimitCache`

**Evidence:** Line 173: `public ConcurrentDictionary<ushort, object> RateLimitCache => _rateLimitCache ??= new();` - allocates per connection on first access.

**Recommendation:** Consider using a lightweight dictionary or deferring allocation.

---

### NIT-003: List allocated in GetWorkers/GetRecurring every call

**Severity:** NITPICK
**Component:** Nalix.Framework
**File:** `src/Nalix.Framework/Tasks/TaskManager.cs`
**Member:** `TaskManager.GetWorkers`, `TaskManager.GetRecurring`

**Evidence:** Line 687: `List<IWorkerHandle> list = new(_workers.Count);` - allocates a new List every call.

**Recommendation:** Consider returning a snapshot array or documenting these as diagnostic-only APIs.

---

## 6. Needs Verification

| # | Pattern | Location | Reason |
|---|---------|----------|--------|
| V-001 | WsFrameSender.SEND_RAW sync-over-async | WsFrameSender.cs:103 | Requires load testing to confirm thread-pool starvation under high WS send rate |
| V-002 | SocketConnection buffer elastic resize under sustained large-packet flood | SocketConnection.cs:429-453 | The buffer grows but only shrinks when idle. Need to verify MaxChunkSize prevents LOH allocation |
| V-003 | TimingWheel._connectionLocks fixed 256 locks | TimingWheel.cs:267-271 | With >256 connections, lock striping collisions increase. Need benchmark to verify contention at 10K+ connections |
| V-004 | WsFrameReader.PROCESS_FRAME lease ownership | WsFrameReader.cs:155-195 | If _onMessage(lease) stores the lease reference beyond the callback, the finally block will return a buffer still in use. Need to verify all consumers copy data |
| V-005 | ConnectionGuard ConcurrentDictionary key allocation for IPv6 | Datagram.Guard.cs:63 | IPv6 addresses stored as strings allocated per unique source. Under IPv6 flood, could exhaust memory |

---

## 7. False Positives / Safe Patterns

| # | Pattern | Why it is safe |
|---|---------|---------------|
| FP-001 | Thread.Sleep(10) in listener shutdown loops | Intentional - used in synchronous drain loops where await Task.Delay would require async context. Documented with comments. |
| FP-002 | new byte[_bufferDataLength] in StolenData | Intentional - only fires when socket is detached (unwrap), a rare control path. The array is small. |
| FP-003 | InstanceManager.Instance.GetOrCreateInstance static calls | Intentional - this is the project service locator pattern. Uses ConcurrentDictionary with thread-local L1 caching for performance. |
| FP-004 | Buffer.BlockCopy in receive loop | Safe - copies within the same buffer for compaction, or from old to new buffer during resize. No overlap in resize case. |
| FP-005 | stackalloc in SEND_FRAGMENTED loop | Suppressed with CA2014 justification. Size bounded by StackAllocLimit (constant), stack overflow not possible. |
| FP-006 | lock (_sendLock) using Lock type | Safe - .NET 10 Lock is the modern fast-path lock. Used correctly in the sync send path. |
| FP-007 | Interlocked.Exchange(ref _buffer, null!) in Dispose | Safe - prevents double-return of pooled buffer when Dispose races with receive loop cleanup. |
| FP-008 | catch (OperationCanceledException) { } in WsFrameReader | Safe - cancellation is the expected shutdown path for the receive loop. |
| FP-009 | DateTime.UtcNow in report methods | Safe - used only in diagnostic report/status methods (cold path). Stopwatch.GetTimestamp() correctly used for hot-path timing. |

---

## 8. Hot-Path Allocation Review

### Confirmed Hot-Path Allocations

| Location | Allocation | Frequency | Risk |
|----------|-----------|-----------|------|
| BufferLease.CopyFrom in PROCESS_FRAME_FROM_BUFFER | byte[] rental + BufferLease shell | Per received frame | Low - pooled |
| SocketConnection.Send (heap path) | byte[] rental from ByteArrayPool | Per large send | Low - pooled |
| Connection.AcquireEventArgs | ConnectionEventArgs from ObjectPoolManager | Per received frame | Low - pooled |
| PooledSocketReceiveContext | SAEA context from pool | Per connection (once) | Low - pooled, long-lived |
| FragmentAssembler.Add | BufferLease for assembled payload | Per fragmented frame | Low - pooled |

### Potential LOH Risks

| Location | Condition | Mitigation |
|----------|-----------|------------|
| SocketConnection._buffer resize | If MaxChunkSize > 85,000 bytes | s_maxReceiveBufferSize capped by 5 + FragmentHeader.WireSize + MaxChunkSize. Verify MaxChunkSize < 85,000 in default config. |
| WsFrameReader MemoryStream | If MaxMessageSize > 85,000 | MemoryStream internal buffer will hit LOH. Replace with pooled buffer (WARN-009). |

### Suggested Benchmark Targets

1. SocketConnection.Send (stackalloc path, 64-byte payload) - verify zero allocations
2. SocketConnection.SendAsync (pooled path, 1400-byte payload) - verify single rental
3. PROCESS_FRAME_FROM_BUFFER - verify BufferLease shell reuse
4. BufferLease.Rent/Dispose cycle - verify thread-local cache hit rate
5. TimingWheel.Register/Unregister - verify TimeoutTask pool hit rate

---

## 9. Thread-Safety Review

### Locking Model

| Lock | Scope | Assessment |
|------|-------|------------|
| SocketConnection._sendLock | Sync send only | Missing from async path (WARN-001) |
| TimingWheelBucket (lock per bucket) | Enqueue/Remove/DequeueAll | Safe - minimal scope |
| TimingWheel._connectionLocks (256 striped) | Register/Unregister | Safe - prevents concurrent registration |
| Connection._lock | PerformDestructiveCleanup | Safe - prevents double cleanup |
| TcpListener._proxyLock | Proxy protocol parsing | Safe - per-listener lock |

### Dispose/Start/Stop Idempotency

| Component | Start Idempotent | Stop Idempotent | Dispose Idempotent |
|-----------|-----------------|-----------------|-------------------|
| SocketConnection | Yes (_receiveStarted CAS) | Yes (_cancelSignaled CAS) | Yes (_disposed CAS) |
| TcpListener | Yes (_state CAS) | Yes (_state CAS) | Yes (_isDisposed CAS) |
| TimingWheel | Yes (_activeListeners refcount) | Yes (refcount) | Yes (_disposed CAS) |
| TaskManager | N/A | N/A | Yes (_disposed volatile) |
| Connection | N/A | Yes (_closeSignaled CAS) | Yes (_disposeState CAS) |

---

## 10. Logging Compliance Review

### AGENTS.md Rule Violations

| Rule | Violations | Locations |
|------|-----------|-----------|
| Rule 1: No $"" in Log/Throttled calls | 4 confirmed | WARN-003, WARN-004, WARN-005 |
| Rule 2: Exception first parameter | All compliant | - |
| Rule 3: Extract method calls before logging | All compliant | - |
| Rule 4: Extract ternary before logging | All compliant | - |
| Rule 5: Extract format specifiers | All compliant | - |
| Rule 6: No multi-line concat | All compliant | - |
| Rule 7: No nameof() in log templates | 2 violations | PacketDispatchChannel.cs lines 553, 682 |

### Sensitive Data Logging Risks

| Location | Data | Risk |
|----------|------|------|
| Session handlers | Session tokens, proofs | Not logged - only reason codes logged |
| Handshake handlers | Keys, secrets | MemorySecurity.ZeroMemory used after use |
| Connection attributes | User-defined | Not logged in hot path |

---

## 11. Public API / Abstraction Review

### Interfaces with Too Many Responsibilities

| Interface | Methods | Assessment |
|-----------|---------|------------|
| IConnection | ~30+ members across partial interfaces | Acceptable - split into partials (IConnection.TrafficMetrics, IConnection.ErrorTracked, etc.) |
| ITaskManager | ~15 methods | Acceptable - cohesive task management concern |

### Concrete Dependencies in Abstractions

| Abstraction | Dependency | Assessment |
|-------------|-----------|------------|
| IBufferLease | None - pure contract | Clean |
| IPacket | None - pure contract | Clean |
| IConnection | Bytes32 (value type from Abstractions) | Clean |

---

## 12. Recommended Fix Order

### Must Fix Before Release

1. **CRIT-001** - TimingWheel idle-timeout logic inversion. Correctness bug that silently leaks connections.

### Should Fix Soon

2. **WARN-001** - Async send serialization. Prevents wire-frame corruption under concurrent async sends.
3. **WARN-008** - Receive-loop errors silently swallowed in production due to Trace-level guard.
4. **WARN-003/004/005** - AGENTS.md logging rule violations. Low-effort fixes.
5. **WARN-006** - Fire-and-forget Task.Run exception observation.

### Nice to Improve Later

6. **WARN-002** - WebSocket sync-over-async blocking. Requires API design decision.
7. **WARN-007** - TimingWheel lifecycle race. Low probability in practice.
8. **WARN-009** - WebSocket large-frame MemoryStream allocation. Performance optimization.
9. **WARN-010** - PacketScope defensive null-guard.
10. **NIT-001/002/003** - Minor allocation optimizations.

---

## 13. Suggested Follow-Up Tests

### Unit Tests

1. **TimingWheel idle-timeout fix** - Register a connection with ExcludeFromIdleTimeout = false, advance the wheel past IdleTimeoutMs, verify the connection is disconnected.
2. **TimingWheel excluded connection** - Register a connection with ExcludeFromIdleTimeout = true, advance the wheel, verify the connection is NOT disconnected.
3. **SocketConnection async send serialization** - Send two frames concurrently via SendAsync, verify the receiver gets exactly two complete, non-interleaved frames.

### Concurrency Stress Tests

4. **Concurrent SendAsync on same connection** - 100 tasks sending 1000 frames each, verify frame integrity.
5. **TimingWheel register/unregister under high churn** - Register and unregister 10,000 connections rapidly, verify no pool exhaustion or crashes.
6. **Connection.Dispose during active receive** - Verify clean shutdown without ObjectDisposedException leaks.

### Fuzz Tests

7. **VarInt framing** - Feed random bytes to the VarInt receive loop, verify no crashes or infinite loops.
8. **PROXY protocol parsing** - Feed random bytes to ProxyProtocolParser.TryParse, verify no crashes.
9. **Fragment assembly** - Send random fragment sequences (out-of-order, missing chunks, duplicate chunks), verify the assembler handles all cases gracefully.

### BenchmarkDotNet Cases

10. SocketConnection.Send - stackalloc path (64B), pooled path (1400B), fragmented path (64KB).
11. BufferLease.Rent/Dispose cycle - measure thread-local cache hit rate.
12. TimingWheel.Register/Unregister - measure pool hit rate and lock contention.
13. PacketFactory.Acquire/Dispose - measure packet pool hit rate.

### Socket Disconnect/Reconnect Tests

14. TCP disconnect during handshake - verify clean state teardown.
15. TCP disconnect during fragment assembly - verify fragment streams are evicted.
16. UDP session token mismatch - verify packet is silently dropped.
17. WebSocket close during large-frame receive - verify buffer is returned.

### Malformed Packet Tests

18. Frame with length prefix = 0 - verify rejected.
19. Frame with length prefix > PacketConstants.PacketSizeLimit - verify rejected.
20. VarInt payload size > _maxVarIntPayloadSize - verify rejected.
21. Fragment with TotalChunks = 0 - verify handled gracefully.
22. Session resume with expired token - verify rejected with SESSION_EXPIRED.
23. Session resume with invalid proof - verify rejected with TOKEN_REVOKED.

---

*End of Audit Report*
