# Buffer Pooling Configuration

Buffer pooling is the cornerstone of Nalix's zero-allocation architecture. By reusing pinned byte arrays from the Pinned Object Heap (POH), Nalix eliminates Gen 0 GC churn and memory fragmentation during high-frequency network operations.

---

## 1. How it Works

Nalix uses a **Slab Pooling** strategy. Instead of allocating arbitrary buffers, it maintains "buckets" of pre-allocated, pinned arrays of fixed sizes.

- **BufferPoolManager**: The orchestrator that manages all buckets.
- **BufferLease**: The lightweight "shell" that you rent to access a buffer.
- **Pinned Object Heap (POH)**: Arrays are allocated using `GC.AllocateArray(pinned: true)`, ensuring they never move and don't require pinning/unpinning overhead during I/O.

---

## 2. Configuration via Hosting Builder

To use buffer pooling, you must register a `BufferPoolManager` with the application builder.

### Basic Setup

```csharp
using Nalix.Hosting;
using Nalix.Framework.Memory.Buffers;

var app = NetworkApplication.CreateBuilder()
    // 1. Initialize and configure the pool manager
    .ConfigureBufferPoolManager(new BufferPoolManager(logger) 
    {
        // Optional: Tuning parameters
        TotalBuffers = 10000,
        InitialCapacity = 1024
    })
    .Build();
```

### Advanced Tuning (Manual Allocation Profiles)

You can precisely control how many buffers of each size are pre-allocated using the `BufferAllocations` format: `size,ratio; size,ratio`.

```csharp
builder.ConfigureBufferPoolManager(new BufferPoolManager(logger)
{
    // Define a custom profile:
    // 256 bytes (15%), 1KB (15%), 4KB (30%), 16KB (30%), 32KB (10%)
    BufferAllocations = "256,0.15; 1024,0.15; 4096,0.30; 16384,0.30; 32768,0.10"
});
```

---

## 3. Configuration via .ini Files

In production, it is often better to tune these values without recompiling. Nalix automatically binds `buffer.ini` settings if available.

### `buffer.ini`
```ini
[BufferOptions]
TotalBuffers = 50000
InitialCapacity = 2048
BufferAllocations = 512,0.20; 2048,0.40; 8192,0.40
```

---

## 4. Using the Pool in Handlers

Once configured, the entire framework (Protocols, Serializers, Handlers) uses this pool automatically. You can also use it manually for custom logic:

```csharp
[PacketOpcode(0x1001)]
public async ValueTask HandleLargeData(IPacketContext<MyPacket> context)
{
    // Rent a buffer from the configured pool
    using var lease = BufferLease.Rent(4096);
    
    // Perform zero-allocation operations...
    int written = DoWork(lease.SpanFull);
    lease.CommitLength(written);
    
    await context.Connection.TCP.SendAsync(lease.Memory);
}
```

---

## 5. Monitoring Pool Health

To ensure your pool is sized correctly, monitor the **Miss Rate**. A high miss rate means the pool is too small or the requested sizes don't match your `BufferAllocations`.

```csharp
// Get a diagnostic report
string report = manager.GenerateReport();
Console.WriteLine(report);
```

### Key Metrics to Watch:
| Metric | Description |
| :--- | :--- |
| **Hit Rate** | % of requests satisfied by the pool. Aim for > 99%. |
| **Miss Rate** | % of requests that forced a new POH allocation. |
| **Usage Ratio** | How much of the total pool is currently in use. |

---

## Related Information

- [Buffer Management API](../../api/framework/memory/buffer-management.md)
- [Zero-Allocation Hot Path](../low-level/zero-allocation-hot-path.md)
- [Server Boilerplate](../getting-started/server-boilerplate.md)
