# BufferPoolManager Documentation

## Overview

The `BufferPoolManager` class (namespace: `Nalix.Shared.Memory.Pooling`) manages pools of reusable byte arrays (buffers) of various sizes for high-performance .NET applications. It provides optimized buffer allocation, deallocation, auto-tuning, trimming, and reporting, reducing memory fragmentation and GC pressure. This manager is ideal for scenarios requiring frequent and fast memory buffer reuse (e.g., serialization, network I/O, file operations).

---

## Functional Summary

- **Buffer Pooling:** Maintains pools of byte arrays of different sizes for rent/return operation.
- **Dynamic Sizing:** Automatically shrinks or grows buffer pools based on real-time usage and configurable policies.
- **Configurable:** Reads settings (sizes, allocations, limits) from configuration or via constructor.
- **Fallbacks:** Optionally falls back to `ArrayPool<byte>.Shared` for out-of-range sizes.
- **Secure Memory:** Supports secure clearing of buffers to prevent sensitive data leakage.
- **Reporting:** Provides rich reporting and statistics for monitoring and debugging.
- **Thread Safety:** Designed for high concurrency and robust operation in multi-threaded environments.

---

## Detailed Code Explanation

### Fields and Constants

- **_allocationPatternCache:** Caches parsed buffer allocation patterns for reuse.
- **_poolManager:** Manages the actual buffer pools (`BufferPoolCollection`).
- **_trimTimer, _trimCycleCount:** Used for periodic background trimming.
- **Various configuration fields:** Read from `BufferConfig`, control pool sizing, trimming, analytics, etc.
- **_suitablePoolSizeCache:** Caches the nearest pool size for requested buffer sizes to speed up repeated requests.

### Properties

- `MaxBufferSize`, `MinBufferSize`: The largest and smallest buffer sizes managed.
- `RecurringName`: Unique name for scheduled recurring trimming tasks.

### Constructors

- **Default:** Loads configuration and initializes pools.
- **Custom:** Accepts a `BufferConfig` object directly.

### Public API

#### Buffer Operations

- **`Rent(int size)`**  
  Rents a buffer of at least the requested size. If size matches a standard pool, rents directly. Otherwise, finds or caches the closest larger pool, or (optionally) falls back to `ArrayPool`.

- **`Return(byte[] buffer)`**  
  Returns a buffer to its correct pool (or the fallback pool if applicable). Optionally clears memory for security.

#### Reporting

- **`GetAllocationForSize(int size)`**  
  Returns the allocation ratio for a given size, used for tuning and pool resizing.

- **`GenerateReport()`**  
  Returns a detailed human-readable report of all buffer pools, their usage, capacity, and statistics.

#### IDisposable

- **`Dispose()`**  
  Cleans up resources, cancels trimming, clears caches, and logs disposal.

### Internal Logic

- **Buffer Allocation:** Pools are pre-allocated according to configured ratios and total buffer count.
- **Trimming:** Regularly trims over-provisioned pools based on current memory usage, free ratios, and budget.
- **Auto-Tuning:** Increases or shrinks pool sizes dynamically according to usage and miss rate.
- **Parsing:** Robust parsing and validation of buffer size/allocation strings from configuration.

---

## Usage

```csharp
// Create manager (uses default config)
var bufferPool = new BufferPoolManager();

// Rent a 4096-byte buffer
byte[] buffer = bufferPool.Rent(4096);

// Use buffer...

// Return buffer after use
bufferPool.Return(buffer);

// Generate a diagnostic report
string report = bufferPool.GenerateReport();
Console.WriteLine(report);

// Dispose when done (e.g., at application shutdown)
bufferPool.Dispose();
```

---

## Example

```csharp
// Custom configuration example
BufferConfig config = new BufferConfig
{
    TotalBuffers = 10000,
    BufferAllocations = "256,0.3;1024,0.5;4096,0.2",
    EnableMemoryTrimming = true,
    SecureClear = true
};

using var bufferPool = new BufferPoolManager(config);

// Rent a buffer for file I/O
byte[] buf = bufferPool.Rent(1024);

// ... use the buffer ...

// Return when done
bufferPool.Return(buf);
```

---

## Notes & Security

- **Type safety:** Only byte arrays (`byte[]`) are pooled.
- **Memory safety:** If `SecureClear` is enabled, buffers are zeroed before returning to the pool, which is critical for sensitive workloads (e.g., cryptography).
- **Fallback behavior:** If `FallbackToArrayPool` is enabled, the manager will not throw on unknown buffer sizes but fallback to the shared array pool, ensuring robustness.
- **Thread safety:** All APIs are safe for concurrent use.
- **Performance:** Reduces GC allocations and memory fragmentation, especially under high load.
- **Configuration:** Manage pool sizes and ratios carefully to avoid memory over-provisioning or under-utilization.
- **Reporting:** Use `GenerateReport` to monitor and optimize pool usage in production.

---

## SOLID & DDD Principles

- **Single Responsibility:** This class is responsible only for buffer pooling and management.
- **Open/Closed:** New buffer strategies can be added without modifying core logic.
- **Liskov Substitution:** Can be replaced by other pool managers if they respect the pooling contract.
- **Interface Segregation:** Implements only relevant interfaces (`IDisposable`, `IReportable`).
- **Dependency Inversion:** Uses configuration and logging abstractions for extensibility and testability.

**Domain-Driven Design:**  
Buffer pooling is a low-level infrastructure concern, separated from domain logic. All domain code should acquire/release buffers via this manager to ensure consistency and maintainability.

---

## Additional Remarks

- **Best Practices:** Always return rented buffers to avoid memory leaks.
- **Diagnostics:** Use analytics and reporting features to detect configuration or usage problems.
- **Visual Studio/VS Code:** Full IntelliSense support for all APIs.

---
