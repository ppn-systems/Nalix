# Crc16 Class Documentation

The `Crc16` class provides a high-performance implementation of the CRC-16 (Cyclic Redundancy Check) checksum calculation using the CRC-16/MODBUS polynomial. This class is part of the `Notio.Cryptography.Integrity` namespace.

## Namespace

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
```

## Class Definition

### Summary

The `Crc16` class offers efficient methods for computing the CRC-16 checksum for various data types and sizes. It utilizes a pre-computed lookup table for fast computation.

```csharp
namespace Notio.Cryptography.Integrity
{
    /// <summary>
    /// High-performance implementation of CRC16 checksum calculation.
    /// </summary>
    public static class Crc16
    {
        // Class implementation...
    }
}
```

## Methods

### HashToUnit16

```csharp
public static ushort HashToUnit16(params byte[] bytes);
```

- **Description**: Calculates the CRC16 for the entire byte array provided.
- **Parameters**:
  - `bytes`: The input byte array.
- **Returns**: The CRC16 value as a ushort.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the bytes array is null.

### HashToUnit16-1

```csharp
public static ushort HashToUnit16(byte[] bytes, int start, int length);
```

- **Description**: Calculates the CRC16 for a chunk of data in a byte array.
- **Parameters**:
  - `bytes`: The input byte array.
  - `start`: The index to start processing.
  - `length`: The number of bytes to process.
- **Returns**: The CRC16 value as a ushort.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the bytes array is null.
  - `ArgumentOutOfRangeException`: Thrown if the start or length is negative, or if the specified range is out of bounds.

### HashToUnit16-2

```csharp
public static ushort HashToUnit16(ReadOnlySpan<byte> bytes);
```

- **Description**: Computes the CRC16 for a span of bytes with optimized processing.
- **Parameters**:
  - `bytes`: Span of input bytes.
- **Returns**: The CRC16 value as a ushort.
- **Exceptions**:
  - `ArgumentException`: Thrown if the bytes span is empty.

### HashToUnit16-3

```csharp
public static ushort HashToUnit16<T>(ReadOnlySpan<T> data) where T : unmanaged;
```

- **Description**: Computes the CRC16 for any unmanaged generic data type.
- **Parameters**:
  - `data`: The data to compute the CRC16 for.
- **Returns**: The CRC16 value as a ushort.
- **Exceptions**:
  - `ArgumentException`: Thrown if the data span is empty.

### Verify

```csharp
public static bool Verify(ReadOnlySpan<byte> data, ushort expectedCrc);
```

- **Description**: Verifies if the provided data matches the expected CRC16 value.
- **Parameters**:
  - `data`: The data to verify.
  - `expectedCrc`: The expected CRC16 value.
- **Returns**: True if the CRC matches, otherwise false.

### ProcessOctet

```csharp
private static ushort ProcessOctet(ushort crc, ReadOnlySpan<byte> octet);
```

- **Description**: Processes 8 bytes at once for improved performance on larger inputs.
- **Parameters**:
  - `crc`: The initial CRC value.
  - `octet`: The 8-byte chunk to process.
- **Returns**: The updated CRC value.

## Example Usage

Here's a basic example of how to use the `Crc16` class:

```csharp
using Notio.Cryptography.Integrity;
using System;

public class Example
{
    public void Crc16Example()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        
        // Compute CRC-16 checksum
        ushort crc = Crc16.HashToUnit16(data);
        Console.WriteLine("CRC-16 checksum: " + crc.ToString("X4"));
    }
}
```

## Remarks

The `Crc16` class is designed to provide a high-performance and efficient implementation of the CRC-16 algorithm. It ensures accurate computation of CRC-16 checksums using a pre-computed lookup table and optimized methods for different data types and sizes.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
