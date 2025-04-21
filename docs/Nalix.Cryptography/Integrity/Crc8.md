# Crc8 Class Documentation

The `Crc8` class provides a high-performance implementation of the CRC-8 (Cyclic Redundancy Check) algorithm using the polynomial \( x^8 + x^7 + x^6 + x^4 + x^2 + 1 \). This class is part of the `Notio.Cryptography.Integrity` namespace.

## Namespace

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
```

## Class Definition

### Summary

The `Crc8` class offers efficient methods for computing the CRC-8 checksum for various data types and sizes. It utilizes a pre-computed lookup table for fast computation.

```csharp
namespace Notio.Cryptography.Integrity
{
    /// <summary>
    /// A high-performance CRC-8 implementation using polynomial x^8 + x^7 + x^6 + x^4 + x^2 + 1
    /// </summary>
    public static class Crc8
    {
        // Class implementation...
    }
}
```

## Methods

### HashToByte-1

```csharp
public static byte HashToByte(params byte[] bytes);
```

- **Description**: Computes the CRC-8 checksum of the specified bytes.
- **Parameters**:
  - `bytes`: The buffer to compute the CRC upon.
- **Returns**: The computed CRC-8 checksum.
- **Exceptions**:
  - `ArgumentException`: Thrown if the bytes array is null or empty.

### HashToByte-2

```csharp
public static byte HashToByte(ReadOnlySpan<byte> bytes);
```

- **Description**: Computes the CRC-8 checksum of the specified bytes.
- **Parameters**:
  - `bytes`: The buffer to compute the CRC upon.
- **Returns**: The computed CRC-8 checksum.
- **Exceptions**:
  - `ArgumentException`: Thrown if the bytes span is empty.

### HashToByte-3

```csharp
public static byte HashToByte(byte[] bytes, int start, int length);
```

- **Description**: Computes the CRC-8 checksum of the specified byte range.
- **Parameters**:
  - `bytes`: The buffer to compute the CRC upon.
  - `start`: The start index upon which to compute the CRC.
  - `length`: The length of the buffer upon which to compute the CRC.
- **Returns**: The computed CRC-8 checksum.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the bytes array is null.
  - `ArgumentOutOfRangeException`: Thrown if the start or length is negative, or if the specified range is out of bounds.

### HashToByte-4

```csharp
public static unsafe byte HashToByte<T>(Span<T> data) where T : unmanaged;
```

- **Description**: Computes the CRC-8 checksum of the specified memory.
- **Parameters**:
  - `data`: The memory to compute the CRC upon.
- **Returns**: The computed CRC-8 checksum.
- **Exceptions**:
  - `ArgumentException`: Thrown if the data span is empty.

### ProcessOctet

```csharp
private static byte ProcessOctet(byte crc, ReadOnlySpan<byte> octet);
```

- **Description**: Processes 8 bytes at a time for better performance on larger inputs.
- **Parameters**:
  - `crc`: The initial CRC value.
  - `octet`: The 8-byte chunk to process.
- **Returns**: The updated CRC value.

## Example Usage

Here's a basic example of how to use the `Crc8` class:

```csharp
using Notio.Cryptography.Integrity;
using System;

public class Example
{
    public void Crc8Example()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        
        // Compute CRC-8 checksum
        byte crc = Crc8.HashToByte(data);
        Console.WriteLine("CRC-8 checksum: " + crc.ToString("X2"));
    }
}
```

## Remarks

The `Crc8` class is designed to provide a high-performance and efficient implementation of the CRC-8 algorithm. It ensures accurate computation of CRC-8 checksums using a pre-computed lookup table and optimized methods for different data types and sizes.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
