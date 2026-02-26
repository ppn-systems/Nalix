# Concurrency Gate

`ConcurrencyGate` limits how many handlers for a given opcode may execute at the same time.

## Source mapping

- `src/Nalix.Network.Pipeline/Throttling/ConcurrencyGate.cs`

## What it does

- keeps a separate entry per opcode
- uses per-opcode semaphores
- optionally queues waiting work
- cleans up idle opcode entries
- trips a breaker when rejection pressure stays too high

## Basic usage

Most commonly it is driven by metadata:

```csharp
[PacketConcurrencyLimit(4, queue: true, queueMax: 32)]
public async Task HandleUpload(PacketContext<IPacket> request) { }
```

Imperative usage is also possible:

```csharp
using var lease = await gate.EnterAsync(
    opCode,
    new PacketConcurrencyLimitAttribute(4, queue: true, queueMax: 32),
    ct);
```

## Queue behavior

- `queue: false` means fail fast
- `queue: true` means wait for a slot
- `queueMax` limits how much waiting work can accumulate
- `TryEnter(...)` is the non-throwing fast path
- `EnterAsync(...)` throws `ConcurrencyConflictException` for immediate or queue-limit rejections and `TimeoutException` when waiting exceeds the gate timeout

## Diagnostics

`GenerateReport()` includes:

- acquired, rejected, queued, and cleaned totals
- breaker status and trip count
- tracked opcode count
- per-opcode capacity and queue depth

## Related APIs

- [Packet Attributes](../routing/packet-attributes.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
