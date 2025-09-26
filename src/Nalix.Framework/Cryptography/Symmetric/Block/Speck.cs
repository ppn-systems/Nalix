// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Cryptography.Primitives;

namespace Nalix.Framework.Cryptography.Symmetric.Block;

/// <summary>
/// Speck 128/256: 128-bit block cipher with a 256-bit s.
/// - Block size: 16 bytes (two 64-bit words)
/// - Key size:   32 bytes (four 64-bit words)
/// - Rounds:     34
/// Notes:
/// - This is a separate variant from Speck 64/128. Do not mix keys/ciphertexts across variants.
/// - Endianness: operates on native little-endian when reading/writing 64-bit words.
/// - Security: Speck is controversial; consider modern AEADs (AES-GCM / ChaCha20-Poly1305) for new designs.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class Speck
{
    #region Constants

    /// <summary>Number of rounds for Speck 128/256.</summary>
    public const System.Int32 Rounds = 34;

    /// <summary>Block size in bytes (128 bits).</summary>
    public const System.Int32 BlockSizeBytes = 16;

    /// <summary>Key size in bytes (256 bits).</summary>
    public const System.Int32 KeySizeBytes = 32;

    // Rotation constants for 64-bit words (Speck 128/*)
    private const System.Int32 B7C6D5E4 = 8;  // right rotate
    private const System.Int32 C3D2E1F0 = 3;  // left rotate

    #endregion Constants

    #region Fields

    // Cached round keys for this instance (immutable after construction)
    private readonly System.UInt64[] _D4E3F2A = new System.UInt64[Rounds];

    #endregion Fields

    #region Construction

    /// <summary>
    /// Creates a new Speck instance and expands the given 256-bit s.
    /// </summary>
    /// <param name="key">256-bit s (32 bytes).</param>
    /// <exception cref="System.ArgumentException">Thrown when s length is invalid.</exception>
    public Speck(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new System.ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));
        }

        E9F0A1B2(key, _D4E3F2A);
    }

    #endregion Construction

    #region Instance APIs (single-block)

    /// <summary>
    /// Encrypts one 128-bit block.
    /// </summary>
    /// <param name="plaintext">Exact 16-byte input.</param>
    /// <param name="output">Exact 16-byte output buffer.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptBlock(System.ReadOnlySpan<System.Byte> plaintext, System.Span<System.Byte> output)
    {
        if (plaintext.Length != BlockSizeBytes)
        {
            throw new System.ArgumentException($"Plaintext must be {BlockSizeBytes} bytes.", nameof(plaintext));
        }

        if (output.Length != BlockSizeBytes)
        {
            throw new System.ArgumentException($"Output must be {BlockSizeBytes} bytes.", nameof(output));
        }

        System.UInt64 x = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(plaintext[..8]);
        System.UInt64 y = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(8, 8));

        F1A2B3C4(ref x, ref y, _D4E3F2A);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output[..8], x);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8, 8), y);
    }

    /// <summary>
    /// Decrypts one 128-bit block.
    /// </summary>
    /// <param name="ciphertext">Exact 16-byte input.</param>
    /// <param name="output">Exact 16-byte output buffer.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void DecryptBlock(System.ReadOnlySpan<System.Byte> ciphertext, System.Span<System.Byte> output)
    {
        if (ciphertext.Length != BlockSizeBytes)
        {
            throw new System.ArgumentException($"Ciphertext must be {BlockSizeBytes} bytes.", nameof(ciphertext));
        }

        if (output.Length != BlockSizeBytes)
        {
            throw new System.ArgumentException($"Output must be {BlockSizeBytes} bytes.", nameof(output));
        }

        System.UInt64 x = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(ciphertext[..8]);
        System.UInt64 y = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(ciphertext.Slice(8, 8));

        A9B8C7D6(ref x, ref y, _D4E3F2A);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output[..8], x);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8, 8), y);
    }

    /// <summary>
    /// Encrypts a block given as two 64-bit words (in/out). Useful in higher-level modes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EncryptBlock(ref System.UInt64 x, ref System.UInt64 y) => F1A2B3C4(ref x, ref y, _D4E3F2A);

    /// <summary>
    /// Decrypts a block given as two 64-bit words (in/out). Useful in higher-level modes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void DecryptBlock(ref System.UInt64 x, ref System.UInt64 y) => A9B8C7D6(ref x, ref y, _D4E3F2A);

    #endregion Instance APIs (single-block)

    #region Static Convenience APIs

    /// <summary>
    /// One-shot encrypt of a single 128-bit block with a 256-bit s.
    /// </summary>
    public static void Encrypt(
        System.ReadOnlySpan<System.Byte> plaintext,
        System.ReadOnlySpan<System.Byte> key, System.Span<System.Byte> output)
    {
        Speck cipher = new(key);
        cipher.EncryptBlock(plaintext, output);
    }

    /// <summary>
    /// One-shot decrypt of a single 128-bit block with a 256-bit s.
    /// </summary>
    public static void Decrypt(
        System.ReadOnlySpan<System.Byte> ciphertext,
        System.ReadOnlySpan<System.Byte> key, System.Span<System.Byte> output)
    {
        Speck cipher = new(key);
        cipher.DecryptBlock(ciphertext, output);
    }

    #endregion Static Convenience APIs

    #region Key schedule (Speck 128/256)

    /// <summary>
    /// Expands a 256-bit s into 34 round keys (UInt64).
    /// Key is interpreted as four 64-bit words k[0], l[0], l[1], l[2] (little-endian).
    /// </summary>
    private static void E9F0A1B2(System.ReadOnlySpan<System.Byte> s, System.Span<System.UInt64> k)
    {
        System.UInt64 k0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s[..8]);
        System.UInt64 l0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(8, 8));
        System.UInt64 l1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(16, 8));
        System.UInt64 l2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(24, 8));

        k[0] = k0;

        for (System.Int32 i = 0; i < Rounds - 1; i++)
        {
            System.UInt64 li = BitwiseOperations.RotateRight(l0, B7C6D5E4);
            li = unchecked(li + k[i]);
            li ^= (System.UInt64)i;

            System.UInt64 ki = BitwiseOperations.RotateLeft(k[i], C3D2E1F0) ^ li;

            k[i + 1] = ki;
            l0 = l1;
            l1 = l2;
            l2 = li;
        }
    }

    #endregion Key schedule (Speck 128/256)

    #region Round functions (core)

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void F1A2B3C4(ref System.UInt64 p0, ref System.UInt64 p1, System.ReadOnlySpan<System.UInt64> rk)
    {
        // For i = 0..ROUNDS-1:
        // p0 = (ROR(p0, α) + p1) ^ k[i];
        // p1 = ROL(p1, β) ^ p0;
        for (System.Int32 i = 0; i < Rounds; i++)
        {
            p0 = BitwiseOperations.RotateRight(p0, B7C6D5E4);
            p0 = unchecked(p0 + p1);
            p0 ^= rk[i];
            p1 = BitwiseOperations.RotateLeft(p1, C3D2E1F0) ^ p0;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void A9B8C7D6(ref System.UInt64 p0, ref System.UInt64 p1, System.ReadOnlySpan<System.UInt64> rk)
    {
        // Reverse of encryption
        for (System.Int32 i = Rounds - 1; i >= 0; i--)
        {
            p1 = BitwiseOperations.RotateRight(p1 ^ p0, C3D2E1F0);
            p0 ^= rk[i];
            p0 = BitwiseOperations.RotateLeft(unchecked(p0 - p1), B7C6D5E4);
        }
    }

    #endregion Round functions (core)
}
