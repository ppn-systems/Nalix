# ConcurrencyGate — Per-opcode concurrency limiter

`ConcurrencyGate` limits how many handlers for a given opcode may run at the same time. Each opcode gets its own `Entry`, optional queue, and independent semaphore, so hot commands can be throttled without globally stalling the dispatcher.

## Mapped source

- `src/Nalix.Network/Throttling/ConcurrencyGate.cs`

## Current behavior

- Uses a `ConcurrentDictionary<ushort, Entry>` keyed by opcode.
- Each `Entry` owns:
  - `SemaphoreSlim` for concurrency slots
  - queue counters when `PacketConcurrencyLimitAttribute.Queue` is enabled
  - last-used timestamp for idle cleanup
  - reference counting so disposal does not race active users
- Starts a recurring cleanup task in the constructor.
- Opens a global circuit breaker when rejection pressure stays too high.

## Attribute contract

The gate is normally driven by `PacketConcurrencyLimitAttribute`.

```csharp
[PacketConcurrencyLimit(4, queue: true, queueMax: 32)]
public async Task HandleUpload(MyPacket packet, IConnection connection) { }
```

The implementation validates:

- `Max > 0`
- `QueueMax >= 0`

## Entry APIs

- `TryEnter(opcode, attr, out lease)` attempts an immediate non-blocking acquire.
- `EnterAsync(opcode, attr, ct)` optionally waits if queueing is enabled.
- `Lease.Dispose()` releases the semaphore slot and decrements the entry usage count.

If queueing is disabled, `EnterAsync` behaves like a fail-fast attempt and throws `ConcurrencyConflictException` when no slot is available.

## Queue behavior

- `Queue = false`: no waiting, immediate rejection when capacity is full.
- `Queue = true`: waits on the per-opcode semaphore.
- `QueueMax = 0`: no waiting slots; effectively immediate rejection even if queueing is enabled.
- `QueueMax < 0` is rejected by validation.
- `QueueMax == int.MaxValue` means effectively unbounded queue count tracking.

`EnterAsync` also applies an internal timeout of 20 seconds. If the wait exceeds that budget, it throws `TimeoutException`.

## Circuit breaker

The gate keeps global acquire/reject counters. The circuit breaker opens when:

- total samples are at least `1000`
- rejection rate is above `95%`

When open:

- `TryEnter` rejects immediately
- the open state stays for `60` seconds
- after the reset time passes, the gate closes and resets acquire/reject counters

Stats exposed by `GetStatistics()` include trip count and whether the breaker is currently open.

## Cleanup

- Cleanup runs every minute.
- An opcode entry is eligible only when it is idle and unused for at least 10 minutes.
- Removal happens before disposal, so new users cannot reacquire the same stale object.
- Disposal waits briefly for active users and logs if forced disposal still finds remaining users.

## Diagnostics

`GenerateReport()` includes:

- total acquired / rejected / queued / cleaned counts
- rejection rate
- circuit breaker status and trip count
- tracked opcode count
- per-opcode capacity, in-use count, available slots, queue depth, queue max, queueing mode, and `LastUsed`

## See also

- [PacketDispatchChannel](../Routing/PacketDispatchChannel.md)
- [PacketAttributes](../Routing/PacketAttributes.md)
