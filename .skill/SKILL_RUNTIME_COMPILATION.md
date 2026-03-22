# ⚙️ Nalix AI Skill — Runtime Compilation & Dispatch Internals

This skill covers the deep internals of the `Nalix.Runtime` compilation engine, which dynamically optimizes packet handling.

---

## 🏗️ The Compilation Engine

At startup, Nalix scans all registered controllers and generates optimized dispatch logic for each opcode.

### `PacketHandlerCompiler`
- **Purpose:** Converts reflected `MethodInfo` into a high-performance execution delegate.
- **Mechanism:** Uses `Expression` trees or IL generation to create a "trampoline" that calls your handler method with zero boxing.

---

## 📜 Handler Signatures & Binding

The compiler is responsible for binding different method signatures to the same unified dispatch contract.

### Supported Patterns:
1.  **Fast Path:** `(TPacket, IConnection)`
2.  **Full Context:** `(PacketContext<T>)`
3.  **Async/Sync:** Supports `void`, `Task`, and `ValueTask` return types.

### Generic Handlers:
- **Rule:** While the compiler can handle some generic cases, it is highly recommended to use concrete types to avoid ambiguity (NALIX058).

---

## ⚡ Dispatch Optimization

### `PacketHandlerDescriptor`
Stores the metadata about a handler, including its opcode, encryption requirements, and permission levels.

### The Jump Table:
- During `Build()`, Nalix creates an O(1) lookup table (typically a specialized dictionary or array) mapping `Opcode -> CompiledHandler`.
- This eliminates the need for expensive reflection during packet processing.

---

## 🛡️ Best Practices for Handlers

- **Avoid Closures:** The compiler works best with `static` methods. Captured variables in instance methods add indirection and potential allocations.
- **Return Task/ValueTask:** Always use asynchronous return types for handlers that involve I/O.
- **Minimal Parameters:** Only request the parameters you need (`TPacket`, `IConnection`) to reduce stack frame size.

---

## 🛡️ Common Pitfalls

- **Ambiguous Overloads:** Having two methods with the same Opcode in the same controller will cause a compilation failure (NALIX001).
- **Missing Opcodes:** Handlers without `[PacketOpcode]` will be ignored during the scan (NALIX002).
- **Incompatible Signatures:** Methods with unsupported parameter types (e.g., custom objects not in the DI container) will fail to compile (NALIX048).
