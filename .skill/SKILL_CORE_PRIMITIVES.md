# 🧩 Nalix AI Skill — Core Primitives & Identity

This skill covers the fundamental data types and identity systems used across the Nalix ecosystem to ensure maximum performance and memory efficiency.

---

## 🔢 High-Performance Numeric Types

### `UInt56`
Nalix uses a custom 56-bit unsigned integer for connection and session identifiers.
- **Rationale:** Optimized for storage in 7 bytes, providing a massive range while saving memory in large-scale connection hubs.
- **Operations:** Supports bitwise operations, fast byte-array conversion, and is highly optimized for hash-map keys.

### `Bytes32`
A fixed-size 32-byte value type (struct) used for cryptographic keys and hashes.
- **Zero-Allocation:** Replaces `byte[]` to avoid heap allocations.
- **Safety:** Implements `IEquatable` and is designed for use in `ReadOnlySpan<byte>` contexts.

---

## ❄️ Snowflake IDs

Nalix uses the **Snowflake** algorithm for generating unique, time-ordered identifiers (`ISnowflake`).

### Structure:
- **Timestamp:** High bits for temporal ordering.
- **Machine/Worker ID:** Mid bits for distributed uniqueness.
- **Sequence:** Low bits for collision prevention within the same millisecond.

### Advantages:
- **O(1) Generation:** No centralized database lookup required.
- **Sortable:** IDs naturally sort by creation time.
- **Persistence:** Easily serializable as a single `long` or `UInt56`.

---

## ⚡ Performance Optimization

- **`ref struct` Usage:** Many primitives are designed to be used as `ref struct` where possible to ensure they never leave the stack.
- **SIMD Equality:** Comparison of types like `Bytes32` is accelerated using SIMD instructions where available.
- **Memory Layout:** Primitives use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to minimize padding and memory footprint.

---

## 🛡️ Common Pitfalls

- **Endians:** Always use `LittleEndian` variants when converting primitives to/from byte arrays for network transmission.
- **Collision Risk:** Ensure each server instance has a unique `WorkerID` when generating Snowflakes to prevent ID collisions.
- **Casting:** Avoid unnecessary casting between `long`, `ulong`, and `UInt56`. Use the built-in conversion methods.
