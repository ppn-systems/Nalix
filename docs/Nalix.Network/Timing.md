# TimingWheel & TimeSynchronizer Documentation

## Overview

This document describes two key timing-related infrastructure components in the networking layer:

- **TimeSynchronizer**: Emits periodic time synchronization ticks (typically every ~16ms, i.e. ~60Hz), useful for time-based logic, distributed coordination, or periodic background tasks.
- **TimingWheel**: Implements an efficient "hashed wheel timer" to monitor and disconnect idle connections, minimizing allocations and maximizing scalability.

Both are critical for building high-performance, maintainable .NET servers that require robust connection management and time-driven workflows.

---

## 1. TimeSynchronizer

### What Is It?

A lightweight singleton service that periodically emits "time synchronized" events at a configurable cadence (default: every 16ms for 60 FPS logic).

### Key Features

- **Configurable Tick Rate**: Change tick period for different timing needs.
- **Enable/Disable**: Explicit control to start/stop ticks for resource efficiency.
- **Fire-and-Forget Option**: Optionally dispatches tick events on the ThreadPool to avoid blocking.
- **Thread-Safe**: All state handled with atomic operations and locks.
- **Events**: Exposes `TimeSynchronized` event for subscribers (handlers should be fast).

### Usage Example

```csharp
var synchronizer = new TimeSynchronizer();
synchronizer.TimeSynchronized += ts => Console.WriteLine($"Tick at {ts} ms");

synchronizer.IsTimeSyncEnabled = true; // Start ticking

// Optionally change period (e.g. to 100ms)
synchronizer.Period = TimeSpan.FromMilliseconds(100);

// Stop when done
synchronizer.IsTimeSyncEnabled = false;
synchronizer.Dispose();
```

---

## 2. TimingWheel

### What Is It?

A high-performance "hashed wheel timer" for efficiently tracking idle connections and closing them after a configurable timeout. Inspired by Netty and Akka's timing wheel.

### Key Features

- **Idle Connection Management**: Monitors registered connections and disconnects them if they've been idle for longer than the configured threshold.
- **Low Allocation**: Uses a fixed number of queues (buckets) and object pooling for tasks.
- **Scalable**: O(1) operations for register/unregister; O(k) per-tick processing where k is the number of connections in the current bucket.
- **Thread-Safe**: Registration, unregistration, and event handling are safe for concurrent use.
- **Configurable**: Tick duration, wheel size, and idle timeout are all configurable via `TimingWheelOptions`.

### How It Works

- Each connection is assigned a timeout task and placed in a "bucket" (queue) based on its expected timeout.
- On each tick, only the current bucket is checked. Connections that still have time left are re-scheduled; idle ones are closed.
- Uses a bitmask for fast modulo when the wheel size is a power of two.
- Automatically unregisters closed connections.

### Usage Example

```csharp
var timer = new TimingWheel();
timer.Activate();

timer.Register(connection); // Start monitoring a connection

// ... after some time
timer.Unregister(connection);

timer.Deactivate();
timer.Dispose();
```

---

## Best Practices

- **TimeSynchronizer**: Keep event handlers lightweight if not using `FireAndForget`, or enable it for long-running tasks.
- **TimingWheel**: Register every new connection and always unregister (or ensure OnCloseEvent is hooked) to avoid memory leaks.
- **Configuration**: Adjust timing parameters for your application's needs (e.g., more aggressive timeouts for sensitive systems).
- **Thread Safety**: All APIs are safe for concurrent calls from multiple threads.
- **Resource Management**: Always call `Dispose()` when finished to release resources.

---

## SOLID & DDD Principles

- **Single Responsibility**: Each class handles only timing or connection lifecycle.
- **Open/Closed**: Extend with custom logic (e.g., custom events, hooks) via subclassing or event handlers.
- **Liskov Substitution**: Can be replaced with mocks/fakes for testing.
- **Interface Segregation**: Minimal, focused interfaces (`IActivatable`, `IDisposable`).
- **Dependency Inversion**: Uses dependency injection for logging and object pooling.

**Domain-Driven Design**:  
Timing infrastructure is separated from domain logic—use these components to provide time-based services to your domain/application layer.

---

## Additional Remarks

- **Integration**: Works seamlessly with dependency injection and logging.
- **Performance**: Optimized for large numbers of connections and high-frequency timing.
- **VS/VS Code Friendly**: Full IntelliSense and strong typing.
- **Diagnostics**: Both components provide logging and diagnostic hooks for operational visibility.

---
