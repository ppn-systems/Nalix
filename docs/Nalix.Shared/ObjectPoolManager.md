# ObjectPoolManager Documentation

## Overview

The `ObjectPoolManager` class (namespace: `Nalix.Shared.Memory.Pooling`) provides thread-safe management and access to collections of object pools for types implementing the `IPoolable` interface. It enables efficient object reuse, reduces memory allocations, and offers rich APIs for pool configuration, reporting, and statistics. This design helps optimize memory usage and improve performance in high-throughput .NET applications.

---

## Functional Overview

- **Pool Management:** Maintains a thread-safe dictionary of type-specific object pools.
- **Object Reuse:** Allows getting and returning objects to minimize allocation overhead.
- **Type Safety:** Uses generics and constraints (`IPoolable, new()`) for strong type safety.
- **Pool Configuration:** Supports setting max capacity and preallocating pool objects.
- **Statistics & Reporting:** Tracks usage stats, exposes detailed reports, and supports reset.
- **Maintenance:** Provides APIs to clear, trim, and schedule background maintenance on all pools.
- **Integration:** Designed for dependency injection and logging.

---

## Detailed Code Explanation

### Fields

- `_poolDict`: ConcurrentDictionary mapping `Type` to `ObjectPool` for thread-safe pool storage.
- `_defaultMaxPoolSize`: Default maximum size for new pools (configurable).
- Statistics fields (`_totalGetOperations`, `_totalReturnOperations`, `_startTime`): Track usage and uptime.

### Properties

- `DefaultMaxPoolSize`: Gets/sets the default max pool size (with a minimum of 1,024).
- `PoolCount`: Number of currently managed pools.
- `TotalGetOperations`, `TotalReturnOperations`: Thread-safe counters of pool usage.
- `Uptime`: Time since manager creation or last reset.

### Constructor

- `ObjectPoolManager()`: Initializes a new instance with empty pool dictionary and stats.

### APIs

#### Core Pool Operations

- `Get<T>()`: Retrieves an object of type `T` from the corresponding pool. Creates the pool if needed.
- `Return<T>(T obj)`: Returns an object to its pool for reuse.
- `GetTypedPool<T>()`: Returns a typed pool adapter for batch or advanced operations.
- `Prealloc<T>(int count)`: Preallocates a specified number of objects in the pool.

#### Pool Configuration & Info

- `SetMaxCapacity<T>(int maxCapacity)`: Sets max pool size for a specific type, creating pool if absent.
- `GetTypeInfo<T>()`: Gets statistics for a specific type's pool.

#### Pool Maintenance

- `ClearPool<T>()`: Removes all objects of a specific type from the pool.
- `ClearAllPools()`: Removes all objects from all pools.
- `TrimAllPools(int percentage)`: Trims all pools, keeping only a specified percentage of max capacity.

#### Statistics & Reporting

- `ResetStatistics()`: Resets all usage statistics and pool stats.
- `GenerateReport()`: Returns a string with a formatted report of pool states and statistics.
- `GetDetailedStatistics()`: Returns a dictionary with detailed stats for all pools.

#### Background Maintenance

- `ScheduleRegularTrimming(TimeSpan interval, int percentage, CancellationToken)`: Schedules background trimming of all pools at a regular interval.

#### Internal

- `GetOrCreatePool<T>()`: Ensures a pool for type `T` exists, creating it if necessary.

---

## Usage

```csharp
// Create the manager
var poolManager = new ObjectPoolManager();

// Get an object from the pool
MyPooledObject obj = poolManager.Get<MyPooledObject>();

// Return object to the pool after use
poolManager.Return(obj);

// Preallocate objects to avoid runtime allocations
poolManager.Prealloc<MyPooledObject>(100);

// Set pool size for a specific type
poolManager.SetMaxCapacity<MyPooledObject>(500);

// Generate a human-readable report
string report = poolManager.GenerateReport();
Console.WriteLine(report);

// Schedule background trimming
var cts = new CancellationTokenSource();
poolManager.ScheduleRegularTrimming(TimeSpan.FromMinutes(5), 50, cts.Token);
```

**Note:** `MyPooledObject` must implement `IPoolable` and have a parameterless constructor.

---

## Example

```csharp
public class Bullet : IPoolable
{
    public int Damage { get; set; }
    public void Reset()
    {
        Damage = 0;
    }
}

// Usage in a game loop
var pools = new ObjectPoolManager();

// Get a bullet from pool
Bullet bullet = pools.Get<Bullet>();
bullet.Damage = 10;

// Use bullet...

// Return bullet to the pool when done
pools.Return(bullet);

// Preallocate bullets at game start for performance
pools.Prealloc<Bullet>(200);
```

---

## Notes & Security

- **Type Safety:** Only types implementing `IPoolable` and having a parameterless constructor are supported.
- **Thread Safety:** All pool operations are thread-safe.
- **Performance:** Reduces GC pressure by reusing objects. Preallocation can avoid runtime spikes.
- **Logging:** Integrates with an injected logger for diagnostics and error reporting.
- **Background Maintenance:** Use `ScheduleRegularTrimming` to control memory usage in long-running applications.
- **Security:** Do not pool objects that hold sensitive data unless they are securely cleared (`Reset()`).
- **Resource Management:** Always return objects to the pool after use to prevent leaks.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method and the class itself focuses on pool management.
- **Open/Closed:** Supports extension via typed adapters and generic design.
- **Liskov Substitution:** Any `IPoolable` type can be pooled and reused as designed.
- **Interface Segregation:** Only exposes relevant APIs; pool logic is decoupled from business logic.
- **Dependency Inversion:** Uses abstractions (`IPoolable`, `ILogger`) to decouple implementation.

**Domain-Driven Design:**  
Pooling logic is isolated from domain objects. Domain entities should implement `IPoolable` and provide safe reset logic, keeping business logic and infrastructure separated.

---

## Additional Remarks

- **Visual Studio/VS Code Friendly:** Intuitive API, auto-complete support, and clear error messages.
- **Maintenance:** Regularly monitor pool usage and adjust `DefaultMaxPoolSize` as needed.
- **Customization:** Extendable by overriding `IPoolable.Reset()` for custom cleanup.
- **Best Practices:** Always check pool statistics and adjust trimming/preallocation for your workload.

---
