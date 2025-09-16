# LiteSerializer Documentation

## Overview

The `LiteSerializer` class provides high-performance serialization and deserialization APIs for objects in .NET, supporting unmanaged types, arrays, and complex types. Its design aims for efficiency, zero-boxing, and type safety, leveraging reflection and advanced C# features. This class is part of the `Nalix.Shared.Serialization` namespace.

## Functional Summary

- **Serialization:** Converts objects of various types (value, reference, array, struct, class) to compact `byte[]` or `Span<byte>`.
- **Deserialization:** Reconstructs objects from binary data.
- **Buffer Support:** Works with arrays and spans for high performance.
- **Type Support:** Handles unmanaged, nullable, arrays, and user-defined types via formatters.
- **Error Handling:** Throws precise exceptions for unsupported types or invalid buffers.

---

## Code Explanation

### 1. Constants

- `NullArrayMarker`, `EmptyArrayMarker`: Special markers for serializing `null` and empty arrays, using magic numbers for fast detection during deserialization.

### 2. Serialization Methods

#### `Serialize<T>(in T value)`

- **Purpose:** Serializes an object of type `T` into a `byte[]`.
- **Details:**
  - **Unmanaged types:** Direct memory copy for speed.
  - **Arrays:** Handles `null`, empty, and unmanaged arrays specially.
  - **Fixed-size serializable objects:** Uses registered formatters.
  - **Fallback:** Throws exception for unsupported types.

#### `Serialize<T>(in T value, byte[] buffer)`

- **Purpose:** Serializes an object into a provided buffer.
- **Details:**
  - Checks buffer size.
  - Supports only value and fixed-size types for this overload.

#### `Serialize<T>(in T value, Span<byte> buffer)`

- **Purpose:** Serializes an object into a provided span.
- **Details:**
  - For fixed-size serializable only.
  - Throws for unsupported or reference types.

### 3. Deserialization

#### `Deserialize<T>(ReadOnlySpan<byte> buffer, ref T value)`

- **Purpose:** Reads and reconstructs an object from a buffer.
- **Details:**
  - Checks for null/empty buffer.
  - Handles unmanaged types, arrays (with magic markers), and complex types via formatters.

### 4. Private Helpers

- **`IsNullArrayMarker`, `IsEmptyArrayMarker`:** Checks for magic markers in the buffer to identify special array cases.

---

## Usage

```csharp
using Nalix.Shared.Serialization;

// Serialize to byte array
MyStruct data = new MyStruct { /* ... */ };
byte[] bytes = LiteSerializer.Serialize(in data);

// Serialize into existing buffer
byte[] buffer = new byte[LiteSerializer.GetSerializedSize(data)];
int written = LiteSerializer.Serialize(in data, buffer);

// Serialize into Span<byte>
Span<byte> span = stackalloc byte[expectedSize];
int written = LiteSerializer.Serialize(in data, span);

// Deserialize from buffer
MyStruct result = default;
LiteSerializer.Deserialize(bytes, ref result);
```

---

## Example

```csharp
// Define a fixed-size struct
[Serializable]
public struct Point
{
    public int X;
    public int Y;
}

// Serialization
Point p = new Point { X = 10, Y = 20 };
byte[] serialized = LiteSerializer.Serialize(in p);

// Deserialization
Point deserialized = default;
LiteSerializer.Deserialize(serialized, ref deserialized);

// Output
Console.WriteLine($"X={deserialized.X}, Y={deserialized.Y}"); // X=10, Y=20
```

---

## Notes & Security

- **Type Safety:** Only supported types (unmanaged, arrays, registered complex types) can be serialized/deserialized.
- **Exception Handling:** Throws `SerializationException` or `NotSupportedException` on errors (e.g., buffer too small, unsupported types).
- **Magic Numbers:** Special array markers are used for efficient detection of `null` and empty arrays.
- **Security:** Do not deserialize untrusted data. Tampered or malformed data can cause exceptions or undefined behavior.
- **Performance:** Uses aggressive inlining, memory pooling, and unsafe APIs for maximum speed.
- **Extensibility:** Register custom formatters for user-defined types via `FormatterProvider`.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each method has a clear, focused responsibility (serialize or deserialize).
- **Open/Closed:** New formatters can be registered without modifying the core serializer.
- **Liskov Substitution:** Works for all types as long as they meet serialization constraints.
- **Interface Segregation:** Uses strongly-typed interfaces for formatters.
- **Dependency Inversion:** Core logic depends on abstractions (`IFormatter<T>`), not on concrete implementations.

**Domain-Driven Design:**  
Serialization logic is separated from domain entities. Complex types should implement or register custom formatters to maintain clear domain boundaries.

---

## Additional Notes

- **Debugging:** Extensive debug output in DEBUG builds for traceability.
- **Memory Management:** Pools and reuses buffers for efficiency; releases unmanaged resources promptly.
- **Reflection:** Reflection is used for type discovery but is cached for performance.

---
