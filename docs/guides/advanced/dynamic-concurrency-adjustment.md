# Dynamic Concurrency Adjustment

Standard Nalix concurrency limits are applied via static `[PacketConcurrencyLimit]` attributes. However, in high-load or multi-tenant environments, you may need to adjust these limits dynamically based on system health (CPU/Memory) or client-specific metrics.

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Advanced
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Concurrency Gate API](../../api/runtime/middleware/concurrency-gate.md)

---

## 1. System Architecture Clarification

It is important to distinguish between the two concurrency systems in Nalix:

1.  **TaskManager Concurrency**: Manages the global background worker thread pool and recurring tasks. It uses its own internal gates to prevent thread starvation.
2.  **Runtime Concurrency (ConcurrencyGate)**: Manages per-opcode handler execution limits. This is what `ConcurrencyMiddleware` uses to protect your business logic.

While these systems are independent, a **Custom Transport Policy** acts as a bridge, allowing the runtime to adjust its opcode-level limits based on the overall health of the system (monitored by `TaskManager` or other providers).

---

## 2. The "Advance & Retreat" Pattern

Adjusting a `SemaphoreSlim` (the engine behind the Concurrency Gate) at runtime requires care to avoid deadlocks. Nalix uses a pattern called **Advance & Retreat**:

- **Advance (Scaling Up)**: Call `Release(n)` to instantly add slots to the semaphore.
- **Retreat (Scaling Down)**: Use a non-blocking `Wait(0)` loop to "capture" free slots. If slots are currently in use, we "retreat" partially and try again later, ensuring we never block the caller.

### Core Logic Snippet
```csharp
// Scaling Up
if (delta > 0) {
    _semaphore.Release(delta);
} 
// Scaling Down
else if (delta < 0) {
    for (int i = 0; i < -delta; i++) {
        if (!_semaphore.Wait(0)) break; // Stop if no free slots to capture
    }
}
```

---

## 3. Designing a Custom Dynamic Policy

Here is a complete example of a middleware that lowers the concurrency limit of a specific opcode when the system CPU usage exceeds a threshold.

```csharp
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Tasks;
using Nalix.Runtime.Throttling;

public class CpuAdaptiveConcurrencyMiddleware : IPacketMiddleware<IPacket>
{
    private readonly TaskManager _taskManager;
    private readonly ConcurrencyGate _gate;
    private int _currentLimit = 100;

    public CpuAdaptiveConcurrencyMiddleware(TaskManager taskManager, ConcurrencyGate gate)
    {
        _taskManager = taskManager;
        _gate = gate;
    }

    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        // 1. Obtain system health metrics (CPU, ThreadPool load, etc.)
        // TaskManager properties provide a direct source for these metrics.
        int running = _taskManager.PeakRunningWorkerCount; 
        
        // 2. Determine if we are under heavy load
        bool isUnderPressure = running > 100; // Example metric

        // 3. Calculate dynamic limit
        int targetLimit = isUnderPressure ? 5 : 50;

        // 4. Apply adjustment (Advance/Retreat) to the specific opcode entry
        // This effectively "wraps" the ConcurrencyGate with dynamic intelligence.
        // ... adjustment logic ...

        await next(context.CancellationToken);
    }
}
```

---

## 4. Integration with Concurrency Gates

Because Nalix's `ConcurrencyGate` caches entries by opcode, the most effective way to implement dynamic limits is to:

1. **Calculate the limit** in your custom middleware.
2. **Modify the Attribute**: Replace the `PacketConcurrencyLimitAttribute` in the `context.Attributes` collection.
3. **Trigger Re-creation**: If the limit needs to change significantly, your policy can "evict" the old gate entry (if you have an extension point) or use the middleware to manually handle the `SemaphoreSlim`.

### Example: Per-Client Dynamic Budget

You can also use this pattern to implement **Fairness Policies**, where "VIP" clients get a larger concurrency share than "Guest" clients, even for the same opcode.

```csharp
public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
{
    bool isVip = context.Connection.RemoteIdentity.IsAuthenticated;
    
    // Dynamically assign a limit based on identity
    context.Attributes.ConcurrencyLimit = new PacketConcurrencyLimitAttribute(
        max: isVip ? 50 : 5, 
        queue: true, 
        queueMax: 10
    );

    await next(context.CancellationToken);
}
```

---

## Best Practices

- **Hysteresis**: Don't adjust limits on every single packet. Use a "streak" or a "cool-down" period (e.g., only adjust once every 5 seconds) to avoid oscillation.
- **Graceful Retreat**: When scaling down, never dispose the semaphore while requests are active. Use the `Wait(0)` pattern to gradually lower the ceiling.
- **Observability**: Always log when a dynamic policy triggers an adjustment so you can correlate it with performance spikes.

---

## Related Information

- [Concurrency Gate API](../../api/runtime/middleware/concurrency-gate.md)
- [TaskManager Monitoring](../../api/framework/runtime/task-manager.md)
- [Custom Middleware Guide](../extensibility/custom-middleware.md)
