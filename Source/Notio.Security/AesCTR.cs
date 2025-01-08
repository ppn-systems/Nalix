using Notio.Security.Exceptions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Notio.Security;

/// <summary>
/// Lớp cung cấp chức năng mã hóa và giải mã AES-256 với chế độ CTR (Counter)
/// </summary>
public static unsafe class AesCTR
{
    // Cache cho AES instances
    private static readonly ThreadLocal<Aes> AesInstanceCache = new(() => Aes.Create());

    /// <summary>
    /// Mã hóa dữ liệu sử dụng AES-256 CTR mode, trả về kết quả trong CryptoBuffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe CryptoBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        Aes256.ValidateKey(key.ToArray());

        int totalLength = plaintext.Length + Aes256.BlockSize;
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(totalLength);

        try
        {
            fixed (void* resultPtr = resultOwner.Memory.Span)
            fixed (void* keyPtr = key)
            {
                using var rentedBuffers = new RentedBuffers();

                // Tối ưu việc tạo và copy IV
                Span<byte> iv = rentedBuffers.GetBuffer(Aes256.BlockSize);
                Aes256.GenerateSecureIV(iv);

                Unsafe.CopyBlock(resultPtr, Unsafe.AsPointer(ref MemoryMarshal.GetReference(iv)), (uint)Aes256.BlockSize);

                // Setup counters
                Span<byte> counter = rentedBuffers.GetBuffer(Aes256.BlockSize);
                Span<byte> encryptedCounter = rentedBuffers.GetBuffer(Aes256.BlockSize);
                Unsafe.CopyBlock(Unsafe.AsPointer(ref MemoryMarshal.GetReference(counter)),
                               Unsafe.AsPointer(ref MemoryMarshal.GetReference(iv)),
                               (uint)Aes256.BlockSize);

                // Sử dụng cached AES instance
                var aes = GetCachedAes(key);
                using var encryptor = aes.CreateEncryptor();

                // Copy và xử lý plaintext
                var outputPtr = (byte*)resultPtr + Aes256.BlockSize;
                fixed (void* plaintextPtr = plaintext)
                {
                    Unsafe.CopyBlock(outputPtr, plaintextPtr, (uint)plaintext.Length);
                }

                Aes256.ProcessBlocksOptimized(
                    new Span<byte>(outputPtr, plaintext.Length),
                    counter,
                    encryptedCounter,
                    encryptor);

                return new CryptoBuffer(resultOwner, totalLength);
            }
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Giải mã dữ liệu sử dụng AES-256 CTR mode, trả về kết quả trong CryptoBuffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe CryptoBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length <= Aes256.BlockSize)
            throw new ArgumentException("Ciphertext quá ngắn", nameof(ciphertext));

        Aes256.ValidateKey(key.ToArray());

        int resultLength = ciphertext.Length - Aes256.BlockSize;
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(resultLength);

        try
        {
            fixed (void* resultPtr = resultOwner.Memory.Span)
            fixed (void* keyPtr = key)
            fixed (void* ciphertextPtr = ciphertext)
            {
                using var rentedBuffers = new RentedBuffers();

                // Setup counters với pointer operations
                Span<byte> counter = rentedBuffers.GetBuffer(Aes256.BlockSize);
                Span<byte> encryptedCounter = rentedBuffers.GetBuffer(Aes256.BlockSize);

                Unsafe.CopyBlock(Unsafe.AsPointer(ref MemoryMarshal.GetReference(counter)),
                               ciphertextPtr,
                               (uint)Aes256.BlockSize);

                // Sử dụng cached AES instance
                var aes = GetCachedAes(key);
                using var decryptor = aes.CreateDecryptor();

                // Copy và xử lý ciphertext
                Unsafe.CopyBlock(resultPtr,
                               (byte*)ciphertextPtr + Aes256.BlockSize,
                               (uint)resultLength);

                Aes256.ProcessBlocksOptimized(
                    new Span<byte>(resultPtr, resultLength),
                    counter,
                    encryptedCounter,
                    decryptor);

                return new CryptoBuffer(resultOwner, resultLength);
            }
        }
        catch
        {
            resultOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Helper class để quản lý nhiều buffers tạm thời với tối ưu hóa thêm
    /// </summary>
    private sealed class RentedBuffers : IDisposable
    {
        private readonly List<(byte[] Buffer, int Length)> _buffers = [];
        private readonly Lock _lock = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetBuffer(int length)
        {
            lock (_lock)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
                _buffers.Add((buffer, length));
                return buffer.AsSpan(0, length);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var (buffer, _) in _buffers)
                {
                    ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                }
                _buffers.Clear();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Aes GetCachedAes(ReadOnlySpan<byte> key)
    {
        Aes aes = AesInstanceCache.Value;
        key.CopyTo(aes.Key);
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        return aes;
    }
}