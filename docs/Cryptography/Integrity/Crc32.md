# Crc32 Class Documentation

The `Crc32` class provides a high-performance implementation of the CRC-32 (Cyclic Redundancy Check) checksum calculation using the reversed polynomial `0xEDB88320`, which is equivalent to \( x^{32} + x^{26} + x^{23} + x^{22} + x^{16} + x^{12} + x^{11} + x^{10} + x^8 + x^7 + x^5 + x^4 + x^2 + x + 1 \). This class is part of the `Notio.Cryptography.Integrity` namespace.

## Namespace

```csharp
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
```

## Class Definition

### Summary

The `Crc32` class offers efficient methods for computing the CRC-32 checksum for various data types and sizes. It utilizes a pre-computed lookup table for fast computation and supports hardware-accelerated implementations when available.

```csharp
namespace Notio.Cryptography.Integrity
{
    /// <summary>
    /// High-performance implementation of CRC32 checksum calculation using the
    /// reversed polynomial 0xEDB88320 (which is equivalent to
    /// x^32 + x^26 + x^23 + x^22 + x^16 + x^12 + x^11 + x^10 + x^8 + x^7 + x^5 + x^4 + x^2 + x + 1).
    /// </summary>
    public static class Crc32
    {
        // Class implementation...
    }
}
```

## Methods

### HashToUInt32

```csharp
public static uint HashToUInt32(byte[] bytes, int start, int length);
```

- **Description**: Computes the CRC32 checksum for the specified range in the byte array.
- **Parameters**:
  - `bytes`: The input byte array.
  - `start`: The starting index to begin CRC computation.
  - `length`: The number of bytes to process.
- **Returns**: The 32-bit CRC value.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the input array is null.
  - `ArgumentOutOfRangeException`: Thrown if parameters are out of valid range.

### HashToUInt32-1

```csharp
public static uint HashToUInt32(ReadOnlySpan<byte> bytes);
```

- **Description**: Computes the CRC32 checksum for the specified span of bytes with hardware acceleration when available.
- **Parameters**:
  - `bytes`: The input span of bytes.
- **Returns**: The 32-bit CRC value.

### HashToUInt32-2

```csharp
public static uint HashToUInt32(params byte[] bytes);
```

- **Description**: Computes the CRC32 checksum for the provided bytes.
- **Parameters**:
  - `bytes`: The input byte array.
- **Returns**: The 32-bit CRC value.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the bytes array is null.

### Verify

```csharp
public static bool Verify(ReadOnlySpan<byte> data, uint expectedCrc);
```

- **Description**: Verifies if the data matches the expected CRC32 checksum.
- **Parameters**:
  - `data`: The data to verify.
  - `expectedCrc`: The expected CRC32 value.
- **Returns**: True if the CRC matches, otherwise false.

### HashToUInt32-3

```csharp
public static uint HashToUInt32<T>(ReadOnlySpan<T> data) where T : unmanaged;
```

- **Description**: Computes CRC32 for any unmanaged type data.
- **Parameters**:
  - `data`: The data to compute CRC32 for.
- **Returns**: The 32-bit CRC value.

### HashToUInt32Scalar

```csharp
private static uint HashToUInt32Scalar(ReadOnlySpan<byte> bytes);
```

- **Description**: Scalar implementation of CRC32 calculation.
- **Parameters**:
  - `bytes`: The input span of bytes.
- **Returns**: The 32-bit CRC value.

### ProcessOctet

```csharp
private static uint ProcessOctet(uint crc, ReadOnlySpan<byte> octet);
```

- **Description**: Process 8 bytes at once for better performance.
- **Parameters**:
  - `crc`: The initial CRC value.
  - `octet`: The 8-byte chunk to process.
- **Returns**: The updated CRC value.

### HashToUInt32Simd

```csharp
private static unsafe uint HashToUInt32Simd(ReadOnlySpan<byte> bytes);
```

- **Description**: SIMD-accelerated implementation of CRC32 calculation using Vector<`byte`>.
- **Parameters**:
  - `bytes`: The input span of bytes.
- **Returns**: The 32-bit CRC value.

### HashToUInt32Sse42

```csharp
private static unsafe uint HashToUInt32Sse42(ReadOnlySpan<byte> bytes);
```

- **Description**: SSE4.2 hardware-accelerated implementation of CRC32 calculation.
- **Parameters**:
  - `bytes`: The input span of bytes.
- **Returns**: The 32-bit CRC value.

## Example Usage

Here's a basic example of how to use the `Crc32` class:

```csharp
using Notio.Cryptography.Integrity;
using System;

public class Example
{
    public void Crc32Example()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        
        // Compute CRC-32 checksum
        uint crc = Crc32.HashToUInt32(data);
        Console.WriteLine("CRC-32 checksum: " + crc.ToString("X8"));
    }
}
```

## Remarks

The `Crc32` class is designed to provide a high-performance and efficient implementation of the CRC-32 algorithm. It ensures accurate computation of CRC-32 checksums using a pre-computed lookup table and optimized methods for different data types and sizes. Hardware acceleration is utilized when available for even faster computations.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
