using Notio.Security.Exceptions;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Notio.Security;

public static class Aes256
{
    internal const int BufferSize = 81920; // 80KB buffer for better performance
    internal const int BlockSize = 16;  // AES block size in bytes
    internal const int KeySize = 32;    // AES-256 key size in bytes
    internal static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Tạo một khóa AES 256-bit mới
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = Aes256.KeySize * 8; // Convert bytes to bits
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate encryption key", ex);
        }
    }

    internal static void ValidateKey(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null");
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    internal static void ValidateInput(byte[] data, string paramName)
    {
        if (data == null)
            throw new ArgumentNullException(paramName, "Input data cannot be null");
        if (data.Length == 0)
            throw new ArgumentException("Input data cannot be empty", paramName);
    }

    internal static byte[] GenerateSecureIV()
    {
        var iv = new byte[BlockSize];
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate secure IV", ex);
        }
    }

    internal static void GenerateSecureIV(Span<byte> iv)
    {
        if (iv.Length != BlockSize)
            throw new ArgumentException($"IV must be {BlockSize} bytes", nameof(iv));

        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
        }
        catch (Exception ex)
        {
            throw new CryptoOperationException("Failed to generate secure IV", ex);
        }
    }

    internal static void IncrementCounter(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void XorBlock(Span<byte> data, ReadOnlySpan<byte> counter)
    {
        ref byte dataRef = ref MemoryMarshal.GetReference(data);
        ref byte counterRef = ref MemoryMarshal.GetReference(counter);

        if (Vector.IsHardwareAccelerated && data.Length >= Vector<byte>.Count)
        {
            int i = 0;
            int vectorSize = Vector<byte>.Count;

            for (; i <= data.Length - vectorSize; i += vectorSize)
            {
                var dataVec = new Vector<byte>(data[i..]);
                var counterVec = new Vector<byte>(counter[i..]);
                (dataVec ^ counterVec).CopyTo(data[i..]);
            }

            // Xử lý các byte còn lại
            for (; i < data.Length; i++)
            {
                Unsafe.Add(ref dataRef, i) ^= Unsafe.Add(ref counterRef, i);
            }
        }
        else
        {
            // Fallback cho các hệ thống không hỗ trợ SIMD
            for (int i = 0; i < data.Length; i++)
            {
                Unsafe.Add(ref dataRef, i) ^= Unsafe.Add(ref counterRef, i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ProcessBlocksOptimized(Span<byte> data, Span<byte> counter, Span<byte> encryptedCounter, ICryptoTransform transform)
    {
        for (int i = 0; i < data.Length; i += Aes256.BlockSize)
        {
            int currentBlockSize = Math.Min(Aes256.BlockSize, data.Length - i);

            transform.TransformBlock(
                counter.ToArray(), 0, Aes256.BlockSize,
                encryptedCounter.ToArray(), 0);

            // XOR operation với SIMD khi có thể
            var blockSpan = data.Slice(i, currentBlockSize);
            Aes256.XorBlock(blockSpan, encryptedCounter[..currentBlockSize]);

            Aes256.IncrementCounter(counter);
        }
    }
}