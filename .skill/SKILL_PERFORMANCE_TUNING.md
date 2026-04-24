# ⚡ Nalix AI Skill — Performance Tuning & Zero-Allocation

This skill defines the high-performance engineering standards for the Nalix framework. All code must adhere to these principles to maintain sub-microsecond latencies.

---

## 🚫 The "No-New" Rule

In the **Hot Path** (Packet Receive -> Pipeline -> Handler -> Send), the word `new` is forbidden for any object that lives longer than a single stack frame.

### Solutions:
- **`ObjectPoolManager`**: For renting instances of classes (e.g., `PacketContext`, `ChatMessagePacket`).
- **`BufferPoolManager`**: For renting byte arrays (Slabs).
- **`stackalloc`**: For small, temporary buffers (< 1KB).
- **`ref struct`**: For temporary state that must stay on the stack.

---

## 🛠️ Memory Management

### 1. The `Slab` System
Nalix uses a custom Slab-based memory manager (`SlabPoolManager`) to reduce GC pressure.

- **`IBufferLease`**: A handle to a rented buffer.
- **`Dispose()`**: You **MUST** dispose of every lease exactly once to return it to the pool (NALIX039).

### 2. Span-First Design
Always prefer `ReadOnlySpan<byte>` or `Span<byte>` over `byte[]`.

- **Aggressive Inlining:** Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small helpers in the serialization/dispatch path.
- **Avoid Boxing:** Use generic constraints and `IPacketContext<T>` to avoid boxing value types.

---

## 🛤️ Dispatch Optimization

- **O(1) Routing:** Packet dispatch uses a frozen dictionary or a jump table based on the Opcode.
- **Function Pointers:** Internal dispatch often uses `delegate* managed` (unsafe function pointers) to eliminate the overhead of standard C# delegates.
- **TimingWheel:** For timeouts and delayed tasks, use `TimingWheel` instead of `Task.Delay` to handle thousands of timers with O(1) complexity.

---

## 🧪 Benchmarking Standards

All performance-critical changes must be validated using **BenchmarkDotNet**.

- **Target:** Zero allocations (`Allocated = 0B`).
- **Context:** Test with `Server GC` enabled.
- **Comparison:** Always benchmark against the existing implementation and standard .NET alternatives (e.g., `System.Text.Json`).

---

## 🛡️ Common Pitfalls

- **Async/Await Overhead:** Use `ValueTask` for methods that often complete synchronously.
- **Closure Allocations:** Avoid lambda expressions that capture local variables in hot loops. Use static lambdas or pass state explicitly.
- **LOH Fragmentation:** Rent multiple small slabs instead of one giant array for large payloads.
