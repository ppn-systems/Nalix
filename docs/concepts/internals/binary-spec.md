# Nalix Binary Serialization Specification

This document defines the wire format used by the Nalix framework for packet serialization. It is intended for developers implementing Nalix SDKs in languages other than C# (e.g., Rust, Go, TypeScript).

---

## 1. Core Principles

- **Byte Order**: Always **Little-Endian** for all numeric types.
- **Alignment**: No padding or alignment is added between fields. Data is tightly packed.
- **Sentinel Values**:
  - `Null` is represented by `-1` (0xFFFFFFFF) in length prefixes.
  - `Empty` is represented by `0` (0x00000000).
- **Limits**: Default maximum length for strings/arrays is **1,048,576 bytes (1 MB)**.

---

## 2. Fixed-Size Primitives

All fixed-size primitives are written bit-for-bit using their standard representation.

| Type | Bytes | Description |
| :--- | :--- | :--- |
| `bool` | 1 | `0x00` = false, `0x01` = true. |
| `byte` / `sbyte` | 1 | 8-bit unsigned/signed integer. |
| `short` / `ushort` | 2 | 16-bit signed/unsigned integer. |
| `int` / `uint` | 4 | 32-bit signed/unsigned integer. |
| `long` / `ulong` | 8 | 64-bit signed/unsigned integer. |
| `float` | 4 | 32-bit floating point (IEEE 754). |
| `double` | 8 | 64-bit floating point (IEEE 754). |
| `char` | 2 | UTF-16 code unit. |
| `Snowflake` | 8 | 64-bit unique identifier (Decomposed from ulong). |
| `Enum` | Varies | Underlying integral type (e.g., `int32`, `byte`). |

### Special 16-Byte Primitives

| Type | Bytes | Layout |
| :--- | :--- | :--- |
| `Guid` | 16 | .NET internal: `[Data1:int32][Data2:int16][Data3:int16][Data4:8 bytes]`. |
| `decimal` | 16 | Four 32-bit integers: `[lo][mid][hi][flags]`. |
| `DateTimeOffset` | 16 | `[DateTime:8 bytes][OffsetTicks:8 bytes]`. |

---

## 3. Specialized Primitives

### Value Tuples

`ValueTuple<T1, T2, ...>` and C# Tuples `(a, b)` are serialized as a sequence of their components without any headers or metadata.

- **Format**: `[Element 0][Element 1]...[Element N]`

### Memory<`T`> and ReadOnlyMemory<`T`>

Only unmanaged element types are supported for direct memory serialization.

- **Format**: `[Count:int32][RawBytes...]`
- **Optimization**: This is a direct "blit" of the memory buffer.

### Date and Time

Temporal types are serialized as 64-bit signed integers representing **Ticks** (1 tick = 100ns since 0001-01-01).

| Type | Bytes | Description |
| :--- | :--- | :--- |
| `DateTime` | 8 | Total ticks. |
| `TimeSpan` | 8 | Duration in ticks. |
| `DateOnly` | 4 | Days since epoch (Int32). |
| `TimeOnly` | 8 | Ticks since midnight (Int64). |

---

## 4. Strings and Blobs

Encoded as a length-prefixed UTF-8 sequence.

| Field | Type | Description |
| :--- | :--- | :--- |
| **Length** | `int32` | UTF-8 byte count. `-1` for null, `0` for empty. |
| **Bytes** | `byte[]` | Raw UTF-8 payload. |

---

## 5. Nullable Mechanics

Nalix uses a **1-byte presence flag** to handle optional data for types that don't have built-in sentinel values.

### Nullable Value Types (`T?`)

- **Format**: `[Presence:byte][Value:T]`
  - `0x00`: Null.
  - `0x01`: Present (followed by the data for `T`).

### Nullable Reference Types (Class Fields)

When a class member is a reference type (e.g., `public MyClass? Item { get; set; }`):

- **Format**: `[Presence:byte][ObjectData...]`
  - `0x00`: Null reference.
  - `0x01`: Instance present (followed by recursive serialization of the object).

---

## 6. Collections

### Standard Layout

Arrays, Lists, HashSets, Stacks, and Queues follow a uniform pattern:

- **Format**: `[Count:int32][Element 0][Element 1]...`
- **Ordering**:
  - `Stack<T>`: Top to Bottom (Enumeration order).
  - `Queue<T>`: FIFO order.

### Performance Optimization: Blitting

If the element type `T` is **Unmanaged** or an **Enum**, Nalix uses a "Fast-Path" blit:

- The entire array/list is written as a single memory block after the `Count`.

### Dictionaries

- **Format**: `[Count:int32][Key 0][Value 0][Key 1][Value 1]...`

---

## 7. Complex Type Layout

### Automatic Layout (`[SerializePackable]`)

Fields are sorted at compile-time/warmup to minimize memory gaps. A parser must reproduce this order:

1. **Headers**: Fields with `[SerializeHeader(Order)]`. Sorted by `Order` ascending.
2. **Body**: Remaining fields sorted by **Type Size (Descending)**.
   - *Primitives*: Fixed size (1, 2, 4, 8, 16).
   - *Reference Types*: Treated as 8 bytes (pointer-size).
   - *Enums*: Size of underlying type.
3. **Tie-breaker**: If sizes are equal, use **Declaration Order** (sequential discovery from derived to base).

### Explicit Layout (`SerializeLayout.Explicit`)

Fields are written strictly in ascending order of their `[SerializeOrder(Order)]` attribute value.

---

## 8. Implementation Guide for SDK Developers

| Language | Recommendation |
| :--- | :--- |
| **Rust** | Use `zerocopy` for primitives and `std::io::Cursor` for streams. |
| **Go** | Use `encoding/binary.LittleEndian`. |
| **C++** | Use `memcpy` for blittable collections after ensuring `Endianness` conversion. |
| **JS/TS** | Use `DataView` or `Buffer` for manual slicing. |
