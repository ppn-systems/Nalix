# Arc4 Class Documentation

The `Arc4` class implements the ARC4 (Alleged RC4) symmetric stream cipher. ARC4 is a stream cipher that operates on a key to generate a pseudo-random keystream, which is then XORed with the plaintext or ciphertext to encrypt/decrypt data. This class is part of the `Notio.Cryptography.Symmetric` namespace.

**WARNING**: ARC4 is considered cryptographically weak and should not be used for new applications. Consider using more secure alternatives like ChaCha20 or AES-GCM.

## Namespace

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
```

## Class Definition

### Summary

The `Arc4` class provides methods for initializing the cipher with a key, processing data for encryption or decryption, resetting the internal state, and disposing of resources.

```csharp
namespace Notio.Cryptography.Symmetric
{
    /// <summary>
    /// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
    /// WARNING: ARC4 is considered cryptographically weak and should not be used for new applications.
    /// Consider using more secure alternatives like ChaCha20 or AES-GCM.
    /// </summary>
    public sealed class Arc4 : IDisposable
    {
        // Class implementation...
    }
}
```

## Methods

### Constructor

```csharp
public Arc4(ReadOnlySpan<byte> key);
```

- **Description**: Initializes a new instance of the `Arc4` class with the given key.
- **Parameters**:
  - `key`: The encryption/decryption key (should be between 5 and 256 bytes).
- **Exceptions**:
  - `ArgumentNullException`: Thrown if the key is null.
  - `ArgumentException`: Thrown if the key is shorter than 5 bytes or longer than 256 bytes.

### Process

```csharp
public void Process(Span<byte> buffer);
```

- **Description**: Encrypts or decrypts the given data in-place using the ARC4 stream cipher.
- **Parameters**:
  - `buffer`: The data buffer to be encrypted or decrypted.
- **Exceptions**:
  - `ObjectDisposedException`: Thrown if this instance has been disposed.

### Reset

```csharp
public void Reset();
```

- **Description**: Resets the internal state of the cipher.
- **Exceptions**:
  - `ObjectDisposedException`: Thrown if this instance has been disposed.

### Dispose

```csharp
public void Dispose();
```

- **Description**: Disposes the resources used by this instance.

### Private Methods

```csharp
private void Initialize(ReadOnlySpan<byte> key);
```

- **Description**: Initializes the cipher with the provided key.
- **Parameters**:
  - `key`: The encryption/decryption key.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void ProcessBlocks(Span<uint> blocks);
```

- **Description**: Processes blocks of 4 bytes at a time for better performance.
- **Parameters**:
  - `blocks`: The blocks of data to be processed.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void ProcessBytes(Span<byte> buffer);
```

- **Description**: Processes individual bytes (used for the remainder).
- **Parameters**:
  - `buffer`: The data buffer to be processed.

## Example Usage

Here's a basic example of how to use the `Arc4` class:

```csharp
using Notio.Cryptography.Symmetric;
using System;

public class Example
{
    public void Arc4Example()
    {
        byte[] key = System.Text.Encoding.UTF8.GetBytes("SecretKey");
        Arc4 arc4 = new Arc4(key);

        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        Console.WriteLine("Original: " + System.Text.Encoding.UTF8.GetString(data));

        // Encrypt the data
        arc4.Process(data);
        Console.WriteLine("Encrypted: " + BitConverter.ToString(data).Replace("-", "").ToLower());

        // Decrypt the data (ARC4 is symmetric, so the same method is used)
        arc4 = new Arc4(key); // Reset the cipher state with the same key
        arc4.Process(data);
        Console.WriteLine("Decrypted: " + System.Text.Encoding.UTF8.GetString(data));
    }
}
```

## Remarks

The `Arc4` class is designed to provide a simple and efficient implementation of the ARC4 stream cipher. It ensures that data can be encrypted and decrypted using a specified key. The class supports in-place processing of data buffers for both encryption and decryption.

**WARNING**: ARC4 is considered cryptographically weak and should not be used for new applications. Consider using more secure alternatives like ChaCha20 or AES-GCM.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
