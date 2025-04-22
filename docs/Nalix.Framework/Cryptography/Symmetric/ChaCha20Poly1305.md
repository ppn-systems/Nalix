# ChaCha20Poly1305 Class Documentation

The `ChaCha20Poly1305` class provides encryption and decryption utilities using the ChaCha20 stream cipher combined with Poly1305 for message authentication. ChaCha20-Poly1305 is an authenticated encryption algorithm providing both confidentiality and integrity. This class is part of the `Notio.Cryptography.Symmetric` namespace.

## Namespace

```csharp
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
```

## Class Definition

### Summary

The `ChaCha20Poly1305` class provides methods for encrypting and decrypting data using the ChaCha20-Poly1305 algorithm, which combines ChaCha20 for encryption and Poly1305 for message authentication.

```csharp
namespace Notio.Cryptography.Symmetric
{
    /// <summary>
    /// Provides encryption and decryption utilities using the ChaCha20 stream cipher combined with Poly1305 for message authentication.
    /// ChaCha20Poly1305 is an authenticated encryption algorithm providing both confidentiality and integrity.
    /// </summary>
    public static class ChaCha20Poly1305
    {
        // Class implementation...
    }
}
```

## Methods

### Encrypt

```csharp
public static void Encrypt(
    ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
    ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad,
    out byte[] ciphertext, out byte[] tag);
```

- **Description**: Encrypts the plaintext using ChaCha20-Poly1305.
- **Parameters**:
  - `key`: A 32-byte key.
  - `nonce`: A 12-byte nonce.
  - `plaintext`: The plaintext to encrypt.
  - `aad`: Additional authenticated data (AAD) – can be empty.
  - `ciphertext`: Output: the resulting ciphertext.
  - `tag`: Output: the authentication tag (16 bytes).
- **Exceptions**:
  - `ArgumentException`: Thrown if `key` is not 32 bytes or `nonce` is not 12 bytes.

### Decrypt

```csharp
public static bool Decrypt(
    ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext,
    ReadOnlySpan<byte> aad, ReadOnlySpan<byte> tag, out byte[] plaintext);
```

- **Description**: Decrypts the ciphertext using ChaCha20-Poly1305.
- **Parameters**:
  - `key`: A 32-byte key.
  - `nonce`: A 12-byte nonce.
  - `ciphertext`: The ciphertext to decrypt.
  - `aad`: Additional authenticated data (AAD) – must be the same as during encryption.
  - `tag`: The authentication tag (16 bytes) to verify.
  - `plaintext`: Output: the resulting plaintext if authentication succeeds.
- **Returns**: True if authentication passes and decryption is successful; otherwise, false.
- **Exceptions**:
  - `ArgumentException`: Thrown if `key` is not 32 bytes or `nonce` is not 12 bytes.

## Example Usage

Here's a basic example of how to use the `ChaCha20Poly1305` class:

```csharp
using Notio.Cryptography.Symmetric;
using System;

public class Example
{
    public void EncryptDecryptExample()
    {
        byte[] key = new byte[32]; // Replace with your key
        byte[] nonce = new byte[12]; // Replace with your nonce
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] aad = System.Text.Encoding.UTF8.GetBytes("Additional Data");

        ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);
        Console.WriteLine("Ciphertext: " + BitConverter.ToString(ciphertext).Replace("-", "").ToLower());
        Console.WriteLine("Tag: " + BitConverter.ToString(tag).Replace("-", "").ToLower());

        if (ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, out byte[] decryptedPlaintext))
        {
            Console.WriteLine("Decrypted plaintext: " + System.Text.Encoding.UTF8.GetString(decryptedPlaintext));
        }
        else
        {
            Console.WriteLine("Decryption failed!");
        }
    }
}
```

## Remarks

The `ChaCha20Poly1305` class is designed to provide a secure and efficient implementation of the ChaCha20-Poly1305 algorithm, which ensures both the confidentiality and integrity of the encrypted data. The class supports encrypting and decrypting data with additional authenticated data (AAD) for enhanced security.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
