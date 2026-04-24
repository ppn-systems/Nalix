# 💉 Nalix AI Skill — Advanced Dependency Injection (InstanceManager)

This skill covers the high-performance dependency injection and instance management system used internally by Nalix to minimize overhead and maximize thread safety.

---

## 🏗️ The `InstanceManager` Engine

Nalix does not rely on heavy DI containers for its hot path. Instead, it uses `InstanceManager`, a high-speed cache for singleton instances.

### Key Features:
- **`GenericSlot<T>`**: Uses static generic fields to store instances, allowing O(1) retrieval without dictionary lookups.
- **Thread L1 Cache**: Uses `[ThreadStatic]` variables for the most recently accessed instance to further reduce contention.
- **Activator Caching**: Caches constructor delegates (`Func<object?[], object>`) to avoid repeated reflection when creating new instances.

---

## 📜 Lifecycle & Security

### 1. Registration (`Register<T>`)
- **Atomic Swap:** Uses `Interlocked` operations to safely replace instances in a multi-threaded environment.
- **Automatic Disposal:** If a new instance replaces an old one, the manager automatically disposes of the old one if it implements `IDisposable`.
- **Interface Mapping:** Automatically registers the instance for all its implemented interfaces (except common ones like `IDisposable`).

### 2. Lockdown Mechanism
To prevent "Service Hijacking" in production, the manager can be locked.
- **API:** `instanceManager.Lockdown()`.
- **Effect:** Once locked, any attempt to `Register` or `GetOrCreate` a new instance will throw an `InvalidOperationException`.

---

## ⚡ Performance Optimization

- **RuntimeTypeHandle:** Uses `RuntimeTypeHandle` instead of `Type` objects as keys in dictionaries to reduce hashing and equality overhead.
- **Lazy Initialization:** Leverages `Lazy<T>` and atomic patterns to ensure instances are only created when first requested.
- **LOH Avoidance:** All internal caches are designed to avoid growing into the Large Object Heap (LOH).

---

## 🛠️ Usage Patterns

### Get or Create (Pattern)
```csharp
var service = InstanceManager.Instance.GetOrCreateInstance<MyService>();
```

### Manual Registration
```csharp
var config = LoadConfig();
InstanceManager.Instance.Register(config);
```

---

## 🛡️ Common Pitfalls

- **Circular Dependencies:** `InstanceManager` does not automatically detect circular dependencies during `GetOrCreate`. Ensure your object graph is acyclic.
- **Interface Clobbering:** Registering two different objects that implement the same interface will cause the second one to overwrite the first in the interface cache.
- **Double Disposal:** If you manually dispose an object that is also registered in the `InstanceManager`, ensure it handles multiple `Dispose()` calls gracefully.
