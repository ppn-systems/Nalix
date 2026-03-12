# ConcurrencyGate — Per-OpCode Concurrent Execution Limiter (with FIFO Queue, Circuit Breaker, and Diagnostics)

The `ConcurrencyGate` is a robust, thread-safe, per-opcode concurrency limiter designed for .NET backend systems.
It provides fine-grained control over how many simultaneous handler executions are allowed for each command ("OpCode"), along with optional FIFO queuing, auto-cleanup, diagnostics, and an overload protection circuit breaker.

---

## Key Features

- **Per-command (OpCode) concurrency limiting:**  
  Each handler/command is limited independently (not global lock)
- **Optional FIFO queueing:**  
  If the handler is busy, requests can wait in a queue (with max length) or be rejected immediately
- **Automatic resource cleanup:**  
  Idle queues and state are removed after a configurable interval, preventing leaks in long-running servers
- **Circuit breaker:**  
  If the global rejection rate exceeds 95% (with at least 1,000 attempts), the system will *trip* and temporarily reject all requests (protecting against thundering herd)
- **Live diagnostics:**  
  Instant reporting of usage, pressure, queue, and rejection stats per OpCode, top pressure, queue state, and circuit status
- **Safe disposal and reference-counting:**  
  All slots, queues, and resources disposed only after usage; fully robust to thread races and server shutdown edge cases

---

## Usage

### Declarative use (via handler attributes)

```csharp
[PacketConcurrencyLimit(4, queue: true, queueMax: 32)]
public async Task HandleExpensiveTask(MyPacket pkt, IConnection conn) { ... }
```

- Max 4 in parallel; overflow up to 32 in the wait queue, then reject.

### Imperative use

```csharp
var attr = new PacketConcurrencyLimitAttribute(max: 4, queue: true, queueMax: 32);
if (concurrencyGate.TryEnter(opCode, attr, out var lease)) {
    // Do work
    try { ... }
    finally { lease.Dispose(); }
}
```

Or with async wait/queueing:

```csharp
using var lease = await concurrencyGate.EnterAsync(opCode, attr, cancellationToken);
// Do work
```

---

## Circuit Breaker

- If >95% of attempts are rejected in recent samples, circuit breaker *opens* (all requests instantly rejected for 60s).
- Resets (closes) automatically after timeout expires or under lighter load.
- Prevents wasteful resource churn during upstream outages or attack spikes.

---

## Resource Cleanup

- Periodically checks all tracked opcodes for idleness (no active, no queue, all tokens free).
- Removes state for inactive commands (saves RAM, prevents zombie handler growth).
- Idle time and cleanup interval can be tuned as needed; see constants in code.

---

## Diagnostic Reporting

Call `concurrencyGate.GenerateReport()` to get a live status string:

```log
[2026-03-12 13:57:00] ConcurrencyGate Status:
CleanupInterval     : 1.0 min
MinIdleAge          : 10.0 min
TrackedOpcodes      : 6
TotalAcquired       : 5,892
TotalRejected       : 112
TotalQueued         : 38
TotalCleaned        : 1
RejectionRate       : 1.87%
CircuitBreaker      : Closed (trips=0)

Top Opcodes by Load:
---------------------------------------------------------------------------------
Opcode | Capacity | InUse | Avail | Queue | QueueMax | Queuing | LastUsed
---------------------------------------------------------------------------------
0x1002 |        4 |    4  |    0  |    3  |       32 |     yes | 13:57:12
...
---------------------------------------------------------------------------------
```

---

## Best Practices

- *Never* hardcode system-wide concurrency; tune by expected handler characteristics (IO-bound, CPU-bound, latency, etc.).
- Use queueing for bursty but acceptable-wait commands (e.g. chat, ping).
- For expensive or critical ops, set `queue: false` (fail fast).
- Monitor report output for unexpected pressure, overflows, and circuit breaker trip events!

---

## Integration & Thread Safety

- Works in both async and sync/legacy code; uses `SemaphoreSlim` per OpCode.
- All methods thread/goroutine safe; entry counts, queue lengths, and disposal managed via atomic operations.
- Compatible with service shutdown and server process recycling.

---

## Example: Handler Attribute

```csharp
[PacketConcurrencyLimit(3, queue: true, queueMax: 20)]
public async Task HandleUpload(UploadPacket pkt, IConnection conn) { ... }
```

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [PoolingOptions.md](../Configurations/PoolingOptions.md)
- [PacketDispatchChannel.md](../Routing/PacketDispatchChannel.md)
- [PacketConcurrencyLimitAttribute](../../../src/Nalix.Common/Networking/Packets/Attributes/PacketConcurrencyLimitAttribute.cs)
