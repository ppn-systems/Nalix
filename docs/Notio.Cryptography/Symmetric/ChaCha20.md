# ChaCha20 Class Documentation

The `ChaCha20` class provides encryption and decryption functionality using the ChaCha20 stream cipher. This class is part of the `Notio.Cryptography.Symmetric` namespace.

## Namespace

```csharp
using Notio.Cryptography.Utilities;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `ChaCha20` class offers methods for encrypting and decrypting data using the ChaCha20 algorithm. It supports both synchronous and asynchronous operations and can utilize SIMD hardware acceleration when available.

```csharp
namespace Notio.Cryptography.Symmetric
{
    /// <summary>
    /// Class for ChaCha20 encryption / decryption
    /// </summary>
    public sealed class ChaCha20 : IDisposable
    {
        // Class implementation...
    }
}
```

## Methods

### Constructor

```csharp
public ChaCha20(byte[] key, byte[] nonce, uint counter);
```

- **Description**: Initializes a new instance of the `ChaCha20` class.
- **Parameters**:
  - `key`: A 32-byte (256-bit) key.
  - `nonce`: A 12-byte (96-bit) nonce.
  - `counter`: A 4-byte (32-bit) block counter.

```csharp
public ChaCha20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter);
```

- **Description**: Initializes a new instance of the `ChaCha20` class.
- **Parameters**:
  - `key`: A 32-byte (256-bit) key.
  - `nonce`: A 12-byte (96-bit) nonce.
  - `counter`: A 4-byte (32-bit) block counter.

### EncryptBytes

```csharp
public void EncryptBytes(byte[] output, byte[] input, int numBytes, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts an arbitrary-length byte array, writing the resulting byte array to the preallocated output buffer.
- **Parameters**:
  - `output`: Output byte array, must have enough bytes.
  - `input`: Input byte array.
  - `numBytes`: Number of bytes to encrypt.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

```csharp
public void EncryptBytes(byte[] output, byte[] input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts an arbitrary-length byte array, writing the resulting byte array to the preallocated output buffer.
- **Parameters**:
  - `output`: Output byte array, must have enough bytes.
  - `input`: Input byte array.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

```csharp
public byte[] EncryptBytes(byte[] input, int numBytes, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts an arbitrary-length byte array, writing the resulting byte array that is allocated by the method.
- **Parameters**:
  - `input`: Input byte array.
  - `numBytes`: Number of bytes to encrypt.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: Byte array that contains encrypted bytes.

```csharp
public byte[] EncryptBytes(byte[] input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts an arbitrary-length byte array, writing the resulting byte array that is allocated by the method.
- **Parameters**:
  - `input`: Input byte array.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: Byte array that contains encrypted bytes.

```csharp
public byte[] EncryptString(string input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts a string as a UTF8 byte array, returning a byte array that is allocated by the method.
- **Parameters**:
  - `input`: Input string.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: Byte array that contains encrypted bytes.

### EncryptStream

```csharp
public void EncryptStream(Stream output, Stream input, int howManyBytesToProcessAtTime = 1024, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Encrypts an arbitrary-length byte stream, writing the resulting bytes to another stream.
- **Parameters**:
  - `output`: Output stream.
  - `input`: Input stream.
  - `howManyBytesToProcessAtTime`: How many bytes to read and write at a time (default is 1024).
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

### EncryptStreamAsync

```csharp
public async Task EncryptStreamAsync(Stream output, Stream input, int howManyBytesToProcessAtTime = 1024, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Asynchronously encrypts an arbitrary-length byte stream, writing the resulting bytes to another stream.
- **Parameters**:
  - `output`: Output stream.
  - `input`: Input stream.
  - `howManyBytesToProcessAtTime`: How many bytes to read and write at a time (default is 1024).
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

### DecryptBytes

```csharp
public void DecryptBytes(byte[] output, byte[] input, int numBytes, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts an arbitrary-length byte array, writing the resulting byte array to the output buffer.
- **Parameters**:
  - `output`: Output byte array.
  - `input`: Input byte array.
  - `numBytes`: Number of bytes to decrypt.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

```csharp
public void DecryptBytes(byte[] output, byte[] input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts an arbitrary-length byte array, writing the resulting byte array to the output buffer.
- **Parameters**:
  - `output`: Output byte array.
  - `input`: Input byte array.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

```csharp
public byte[] DecryptBytes(byte[] input, int numBytes, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts an arbitrary-length byte array, writing the resulting byte array that is allocated by the method.
- **Parameters**:
  - `input`: Input byte array.
  - `numBytes`: Number of bytes to decrypt.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: Byte array that contains decrypted bytes.

```csharp
public byte[] DecryptBytes(byte[] input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts an arbitrary-length byte array, writing the resulting byte array that is allocated by the method.
- **Parameters**:
  - `input`: Input byte array.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: Byte array that contains decrypted bytes.

### DecryptUtf8ByteArray

```csharp
public string DecryptUtf8ByteArray(byte[] input, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts a UTF8 byte array to a string.
- **Parameters**:
  - `input`: Byte array.
  - `simdMode`: Chosen SIMD mode (default is auto-detect).
- **Returns**: String that contains decrypted bytes.

### DecryptStream

```csharp
public void DecryptStream(Stream output, Stream input, int howManyBytesToProcessAtTime = 1024, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Decrypts an arbitrary-length byte stream, writing the resulting bytes to another stream.
- **Parameters**:
  - `output`: Output stream.
  - `input`: Input stream.
  - `howManyBytesToProcessAtTime`: How many bytes to read and write at a time (default is 1024).
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

### DecryptStreamAsync

```csharp
public async Task DecryptStreamAsync(Stream output, Stream input, int howManyBytesToProcessAtTime = 1024, SimdMode simdMode = SimdMode.AutoDetect);
```

- **Description**: Asynchronously decrypts an arbitrary-length byte stream, writing the resulting bytes to another stream.
- **Parameters**:
  - `output`: Output stream.
  - `input`: Input stream.
  - `howManyBytesToProcessAtTime`: How many bytes to read and write at a time (default is 1024).
  - `simdMode`: Chosen SIMD mode (default is auto-detect).

### Dispose

```csharp
public void Dispose();
```

- **Description**: Clears and disposes of the internal state. Also requests the GC not to call the finalizer.

### Dispose(bool disposing)

```csharp
private void Dispose(bool disposing);
```

- **Description**: Disposes of the internal state. This method should only be invoked from `Dispose()` or the finalizer.
- **Parameters**:
  - `disposing`: Should be true if called by `Dispose()`; false if called by the finalizer.

## Example Usage

Here's a basic example of how to use the `ChaCha20` class:

```csharp
using Notio.Cryptography.Symmetric;
using System;
using System.IO;

public class Example
{
    public void ChaCha20Example()
    {
        byte[] key = new byte[32]; // Replace with your 32-byte key
        byte[] nonce = new byte[12]; // Replace with your 12-byte nonce
        uint counter = 1; // Initial counter value

        // Create ChaCha20 instance
        ChaCha20 chacha20 = new ChaCha20(key, nonce, counter);

        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] ciphertext = new byte[plaintext.Length];

        // Encrypt the plaintext
        chacha20.EncryptBytes(ciphertext, plaintext);

        Console.WriteLine("Ciphertext: " + BitConverter.ToString(ciphertext).Replace("-", "").ToLower());

        byte[] decrypted = new byte[plaintext.Length];

        // Decrypt the ciphertext
        chacha20.DecryptBytes(decrypted, ciphertext);

        Console.WriteLine("Decrypted: " + System.Text.Encoding.UTF8.GetString(decrypted));
    }
}
```

## Remarks

The `ChaCha20` class is designed to provide a secure and efficient implementation of the ChaCha20 stream cipher. It ensures accurate encryption and decryption using a given key, nonce, and counter. The class supports both synchronous and asynchronous operations and can utilize SIMD hardware acceleration when available.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
