# Core Infrastructure Benchmarks

Detailed performance metrics for the Nalix core runtime, including connection management, session storage, packet registration, and concurrency gates.

## Connection Hub

The `ConnectionHub` acts as the central registry for active socket connections.

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **RegisterAndUnregister** | *N/A* | *N/A* | *N/A* | *N/A* |
| **GetConnection** | **6.889 ns** | 0.5410 ns | 0.6230 ns | 0 B |

!!! note
    `RegisterAndUnregister` was skipped in this run due to execution issues. It is currently under review for optimization.

### Behind the design

- **Lock-Free Indexing**: The connection hub relies on high-performance concurrent collections and index arrays to handle concurrent registrations without global locks.
- **Fast Get Route**: Looking up a session by its long identifier takes less than 7 nanoseconds, ensuring that session retrieval is not a bottleneck in the inbound message loop.

---

## Connection Guard

The `ConnectionGuard` manages connection rate-limiting and connection-level IP blacklisting.

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **TryAccept_Allowed** | **205.38 ns** | 5.689 ns | 6.323 ns | 80 B |
| **TryAccept_Blacklisted** | **66.33 ns** | 1.430 ns | 1.647 ns | 0 B |

### Behind the design

- **IP-Based Blacklist Fast Path**: Blacklisted IPs are checked immediately using an optimized trie-like structure or hashset. Rejecting a connection takes under 70 ns with absolutely zero allocations.
- **Quota Validation**: Accepting a connection requires checking current concurrency limits and sliding-window request limits. This process consumes only 80 bytes of heap memory and executes in ~205 ns.

---

## Session Store

The `SessionStore` maintains high-performance local user sessions.

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **StoreAndConsume** | **113.8 ns** | 10.05 ns | 11.57 ns | 48 B |

### Behind the design

- **Thread-Safe Session Maps**: Uses lock-free lookup maps allowing simultaneous read operations. Adding and consuming session metadata is optimized for cache-friendly layouts.

---

## Packet Registry

The `PacketRegistry` maps payload identifiers to concrete handler contracts.

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **TryDeserialize** | **5.174 ns** | 0.1014 ns | 0.1085 ns | 0 B |

### Behind the design

- **Zero-Allocation Deserialization Mapping**: Mapping an incoming packet type identifier to its deserialization logic is fully pre-compiled and cached. A resolution path executes in ~5 ns with zero GC pressure.

---

## Concurrency Gate & Throttling

Nalix uses a token bucket limiter and concurrency gates to prevent server overload and protect the hot-path.

### Concurrency Gate

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **TryEnterAndDispose** | **121.5 ns** | 3.52 ns | 4.06 ns | 32 B |

### Token Bucket Limiter

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| **Evaluate** | **77.95 ns** | 1.311 ns | 1.510 ns | 0 B |

### Optimization Strategy

- **Atomic CAS (Compare-And-Swap) Loops**: Throttling decisions are made using lock-free interlocked structures to perform atomic operations in nanoseconds.
- **Zero-Allocation Evaluation**: The `TokenBucket` evaluation runs without object allocation (0 B) in just ~78 ns, allowing rate-limiting checks directly on high-frequency packets.
