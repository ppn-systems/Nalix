# Xtea Class Documentation

The `Xtea` class provides static methods for encrypting and decrypting data using the XTEA (Extended Tiny Encryption Algorithm) algorithm. XTEA is a block cipher designed to be simple and efficient, making it suitable for environments with limited resources. This class is part of the `Notio.Cryptography.Symmetric` namespace.

## Namespace

```csharp
using System;
using System.Runtime.InteropServices;
```

## Class Definition

### Summary

The `Xtea` class offers high-performance encryption and decryption methods using the XTEA algorithm, which operates on 64-bit blocks and uses a 128-bit key.

```csharp
namespace Notio.Cryptography.Symmetric
{
    /// <summary>
    /// Provides static methods for encrypting and decrypting data using the XTEA algorithm.
    /// </summary>
    public static class Xtea
    {
        // Class implementation...
    }
}
```

## Methods

### Encrypt

```csharp
public static void Encrypt(Memory<byte> data, ReadOnlyMemory<uint> key, Memory<byte> output);
```

- **Description**: Encrypts the specified data using the XTEA algorithm.
- **Parameters**:
  - `data`: The data to encrypt.
  - `key`: The encryption key (must be exactly 4 elements).
  - `output`: The buffer to store the encrypted data (must be large enough to hold the result).
- **Exceptions**:
  - `ArgumentException`: Thrown when the data is empty, the key is not exactly 4 elements, or the output buffer is too small.

### Decrypt

```csharp
public static void Decrypt(Memory<byte> data, ReadOnlyMemory<uint> key, Memory<byte> output);
```

- **Description**: Decrypts the specified data using the XTEA algorithm.
- **Parameters**:
  - `data`: The data to decrypt.
  - `key`: The decryption key (must be exactly 4 elements).
  - `output`: The buffer to store the decrypted data (must be large enough to hold the result).
- **Exceptions**:
  - `ArgumentException`: Thrown when the key length is not exactly 4 elements, the data length is not a multiple of 8, or the output buffer is too small.

## Example Usage

Here's a basic example of how to use the `Xtea` class:

```csharp
using Notio.Cryptography.Symmetric;
using System;

public class Example
{
    public void EncryptDecryptExample()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        uint[] key = { 0x01234567, 0x89ABCDEF, 0xFEDCBA98, 0x76543210 };
        byte[] encryptedData = new byte[(data.Length + 7) & ~7];
        byte[] decryptedData = new byte[encryptedData.Length];

        // Encrypt
        Xtea.Encrypt(data, key, encryptedData);
        Console.WriteLine("Encrypted: " + BitConverter.ToString(encryptedData).Replace("-", "").ToLower());

        // Decrypt
        Xtea.Decrypt(encryptedData, key, decryptedData);
        Console.WriteLine("Decrypted: " + System.Text.Encoding.UTF8.GetString(decryptedData).TrimEnd('\0'));
    }
}
```

## Remarks

The `Xtea` class is designed to offer a straightforward implementation of the XTEA algorithm for encryption and decryption. It ensures that the encryption key is exactly 128 bits (4 elements), and the input data length for decryption is a multiple of 64 bits (8 bytes).

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
