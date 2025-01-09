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
    internal const int MinParallelSize = 1024 * 64; // 64KB threshold cho xử lý song song
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null or empty");
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes for AES-256", nameof(key));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateInput(ReadOnlySpan<byte> data, string paramName)
    {
        if (data.IsEmpty)
            throw new ArgumentNullException(paramName, "Input data cannot be null or empty");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] GenerateSecureIV()
    {
        byte[] iv = new byte[BlockSize];
        try
        {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }
        catch
        {
            for (int i = 0; i < iv.Length; i++)
                iv[i] = (byte)(DateTime.UtcNow.Ticks >> (i % 8) * 8);

            return iv;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GenerateSecureIV(Span<byte> iv)
    {
        if (iv.Length != BlockSize)
            throw new ArgumentException($"IV must be {BlockSize} bytes", nameof(iv));

        try
        {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
        }
        catch
        {
            for (int i = 0; i < iv.Length; i++)
                iv[i] = (byte)(DateTime.UtcNow.Ticks >> (i % 8) * 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            int vectorSize = Vector<byte>.Count;

            for (int i = 0; i <= data.Length - vectorSize; i += vectorSize)
            {
                var dataSlice = data.Slice(i, vectorSize);
                var counterSlice = counter.Slice(i, vectorSize);

                var dataVec = new Vector<byte>(dataSlice);
                var counterVec = new Vector<byte>(counterSlice);
                (dataVec ^ counterVec).CopyTo(dataSlice);
            }
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
            {
                Unsafe.Add(ref dataRef, i) ^= Unsafe.Add(ref counterRef, i);
            }
        }
    }
}