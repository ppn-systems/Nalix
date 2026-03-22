# ♻️ Nalix AI Skill — Poolable Objects & Lifecycle

This skill covers the `IPoolable` interface and the lifecycle management of objects that are recycled to achieve zero-allocation performance.

---

## 🏗️ The `IPoolable` Interface

Objects that are frequently created and destroyed (like Packets and Contexts) must implement `IPoolable`.

### Interface:
- **`ResetForPool()`**: The most important method. It MUST clear all fields, nullify object references, and return the object to a "clean" state before it goes back into the pool.

---

## 📜 Pooling Lifecycle

1.  **Rent:** Use `ObjectPoolManager.Instance.Get<T>()` to get an instance.
2.  **Initialize:** Call an `Initialize(...)` method to set the data for the current operation.
3.  **Use:** Perform the required work with the object.
4.  **Dispose:** Call `.Dispose()` (which is usually implemented to call `Return()` to the pool).

---

## ⚡ Implementation Rules

### 1. The "No-New" Rule
Inside a handler or middleware, you should **NEVER** use the `new` keyword for any object that could be pooled.

### 2. Deep Cleanup
If your poolable object contains other poolable objects (e.g., a Packet containing a List of sub-objects), `ResetForPool()` must return those sub-objects to their respective pools as well.

---

## 🛤️ Common Poolable Types

- **`PacketContext<T>`**: Recycled for every packet dispatch.
- **`IBufferLease`**: Recycled for every network read.
- **`Control`, `Handshake`**: System packets that are constantly moving.

---

## 🛠️ Debugging Pool Leaks

Nalix provides diagnostic metrics to track pool health:
- **`TotalRented` vs. `TotalReturned`**: If these numbers diverge over time, you have a pool leak.
- **`PeakUsage`**: Helps tune the initial pool size to avoid runtime growth.

---

## 🛡️ Common Pitfalls

- **Use-After-Return:** Accessing an object after calling `Dispose()` or returning it to the pool will lead to unpredictable data corruption (as another thread may now be using it).
- **Missing Reset:** Failing to nullify a reference in `ResetForPool()` is a memory leak (the GC cannot collect the referenced object).
- **Double Return:** Returning the same object to the pool twice will cause it to be issued twice simultaneously, leading to race conditions.
