using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Cryptography.Mode;

/// <summary>
/// Lớp cung cấp chức năng mã hóa và giải mã AES-256 với chế độ CtrMode (Counter)
/// </summary>
internal static unsafe class AesCtrCipher
{
    private static readonly int ProcessorCount = Environment.ProcessorCount;

    private static readonly ThreadLocal<System.Security.Cryptography.Aes> AesInstanceCache =
        new(() => System.Security.Cryptography.Aes.Create());

    /// <summary>
    /// Mã hóa dữ liệu với hỗ trợ xử lý song song
    /// </summary>
    public static Aes256.MemoryBuffer EncryptParallel(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length >= Aes256.MinParallelSize)
        {
            return EncryptParallelImplementation(key, plaintext);
        }
        return Encrypt(key, plaintext);
    }

    /// <summary>
    /// Mã hóa dữ liệu sử dụng AES-256 CtrMode mode, trả về kết quả trong MemoryBuffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe Aes256.MemoryBuffer Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        Aes256.ValidateKey(key.ToArray());

        int totalLength = plaintext.Length + Aes256.BlockSize;
        Span<byte> iv = Aes256.Pool.Rent(Aes256.BlockSize);
        Span<byte> counter = Aes256.Pool.Rent(Aes256.BlockSize);
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(totalLength);

        try
        {
            fixed (void* resultPtr = resultOwner.Memory.Span)
            fixed (void* keyPtr = key)
            {
                Aes256.GenerateSecureIV(iv);
                Unsafe.CopyBlock(resultPtr, Unsafe.AsPointer(ref MemoryMarshal.GetReference(iv)), Aes256.BlockSize);

                Unsafe.CopyBlock(
                    Unsafe.AsPointer(ref MemoryMarshal.GetReference(counter)),
                    Unsafe.AsPointer(ref MemoryMarshal.GetReference(iv)),
                    Aes256.BlockSize
                );

                var aes = GetCachedAes(key);
                using var encryptor = aes.CreateEncryptor();

                var outputPtr = (byte*)resultPtr + Aes256.BlockSize;
                fixed (void* plaintextPtr = plaintext)
                {
                    Unsafe.CopyBlock(outputPtr, plaintextPtr, (uint)plaintext.Length);
                }

                ProcessDataBlocksSimd(new Span<byte>(outputPtr, plaintext.Length), counter, encryptor);

                return new Aes256.MemoryBuffer(resultOwner, totalLength);
            }
        }
        catch
        {
            Aes256.Pool.Return(iv.ToArray());
            Aes256.Pool.Return(counter.ToArray());
            resultOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Giải mã dữ liệu sử dụng AES-256 CtrMode mode, trả về kết quả trong MemoryBuffer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe Aes256.MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length <= Aes256.BlockSize)
            throw new ArgumentException("Ciphertext quá ngắn", nameof(ciphertext));

        Aes256.ValidateKey(key.ToArray());

        int resultLength = ciphertext.Length - Aes256.BlockSize;
        Span<byte> counter = Aes256.Pool.Rent(Aes256.BlockSize);
        IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(resultLength);

        try
        {
            fixed (void* resultPtr = resultOwner.Memory.Span)
            fixed (void* keyPtr = key)
            fixed (void* ciphertextPtr = ciphertext)
            {
                Unsafe.CopyBlock(
                    Unsafe.AsPointer(ref MemoryMarshal.GetReference(counter)),
                    ciphertextPtr,
                    Aes256.BlockSize
                );

                var aes = GetCachedAes(key);
                using var decryptor = aes.CreateDecryptor();

                Unsafe.CopyBlock(
                    resultPtr,
                    (byte*)ciphertextPtr + Aes256.BlockSize,
                    (uint)resultLength
                );

                ProcessDataBlocksSimd(new Span<byte>(resultPtr, resultLength), counter, decryptor);

                return new Aes256.MemoryBuffer(resultOwner, resultLength);
            }
        }
        catch
        {
            Aes256.Pool.Return(counter.ToArray());
            resultOwner.Dispose();
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.Security.Cryptography.Aes GetCachedAes(ReadOnlySpan<byte> key)
    {
        var aes = AesInstanceCache.Value;
        key.CopyTo(aes.Key);
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        return aes;
    }

    private static Aes256.MemoryBuffer EncryptParallelImplementation(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        var keyArray = key.ToArray(); // Convert key to array outside the loop
        var tasks = new List<Task<Aes256.MemoryBuffer>>();
        int blockSize = plaintext.Length / ProcessorCount;

        for (int i = 0; i < ProcessorCount; i++)
        {
            int start = i * blockSize;
            int length = i == ProcessorCount - 1 ? plaintext.Length - start : blockSize;

            var slice = plaintext.Slice(start, length).ToArray();
            tasks.Add(Task.Run(() => Encrypt(keyArray, slice)));
        }

        Task.WaitAll([.. tasks]);
        return CombineResults(tasks.Select(t => t.Result).ToArray());
    }

    private static Aes256.MemoryBuffer CombineResults(Aes256.MemoryBuffer[] results)
    {
        int totalLength = results.Sum(r => r.Length);
        IMemoryOwner<byte> combinedOwner = MemoryPool<byte>.Shared.Rent(totalLength);

        int offset = 0;
        foreach (var result in results)
        {
            result.Memory.Span.CopyTo(combinedOwner.Memory.Span[offset..]);
            offset += result.Length;
            result.Dispose();
        }

        return new Aes256.MemoryBuffer(combinedOwner, totalLength);
    }

    private static unsafe void ProcessDataBlocksSse2(Span<byte> data, Span<byte> counter, ICryptoTransform transform)
    {
        byte[] counterBuffer = Aes256.Pool.Rent(Aes256.BlockSize);
        byte[] encryptedBuffer = Aes256.Pool.Rent(16); // SSE2 works with 128-bit (16 bytes)

        try
        {
            fixed (byte* dataPtr = data)
            fixed (byte* encryptedPtr = encryptedBuffer)
            {
                counter.CopyTo(counterBuffer);
                int offset = 0;

                while (offset + 16 <= data.Length)
                {
                    // Process 16 bytes at a time using SSE2
                    transform.TransformBlock(counterBuffer, 0, Aes256.BlockSize, encryptedBuffer, 0);

                    Vector128<byte> dataVector = Sse2.LoadVector128(dataPtr + offset);
                    Vector128<byte> counterVector = Sse2.LoadVector128(encryptedPtr);
                    Vector128<byte> result = Sse2.Xor(dataVector, counterVector);
                    Sse2.Store(dataPtr + offset, result);

                    Aes256.IncrementCounter(counterBuffer.AsSpan());
                    offset += 16;
                }

                if (offset < data.Length)
                    ProcessDataBlocksScalar(data[offset..], counter, transform);
            }
        }
        finally
        {
            Aes256.Pool.Return(counterBuffer);
            Aes256.Pool.Return(encryptedBuffer);
        }
    }

    private static void ProcessDataBlocksScalar(Span<byte> data, Span<byte> counter, ICryptoTransform transform)
    {
        byte[] counterBuffer = Aes256.Pool.Rent(Aes256.BlockSize);
        byte[] encryptedBuffer = Aes256.Pool.Rent(Aes256.BlockSize);

        try
        {
            counter.CopyTo(counterBuffer);
            for (int i = 0; i < data.Length; i += Aes256.BlockSize)
            {
                int currentBlockSize = Math.Min(Aes256.BlockSize, data.Length - i);
                transform.TransformBlock(counterBuffer, 0, Aes256.BlockSize, encryptedBuffer, 0);

                Span<byte> blockSpan = data.Slice(i, currentBlockSize);
                Aes256.XorBlock(blockSpan, encryptedBuffer.AsSpan(0, currentBlockSize));

                Aes256.IncrementCounter(counterBuffer.AsSpan());
            }
        }
        finally
        {
            Aes256.Pool.Return(counterBuffer);
            Aes256.Pool.Return(encryptedBuffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessDataBlocksSimd(Span<byte> data, Span<byte> counter, ICryptoTransform transform)
    {
        if (Avx2.IsSupported && data.Length >= 32)
        {
            ProcessDataBlocksAvx2(data, counter, transform);
        }
        else if (Sse2.IsSupported && data.Length >= 16)
        {
            ProcessDataBlocksSse2(data, counter, transform);
        }
        else
        {
            ProcessDataBlocksScalar(data, counter, transform);
        }
    }

    private static unsafe void ProcessDataBlocksAvx2(Span<byte> data, Span<byte> counter, ICryptoTransform transform)
    {
        byte[] counterBuffer = Aes256.Pool.Rent(Aes256.BlockSize);
        byte[] encryptedBuffer = Aes256.Pool.Rent(32); // AVX2 works with 256-bit (32 bytes)

        try
        {
            fixed (byte* dataPtr = data)
            fixed (byte* encryptedPtr = encryptedBuffer)
            {
                counter.CopyTo(counterBuffer);
                int offset = 0;

                while (offset + 32 <= data.Length)
                {
                    // Process 32 bytes at a time using AVX2
                    transform.TransformBlock(counterBuffer, 0, Aes256.BlockSize, encryptedBuffer, 0);

                    Vector256<byte> dataVector = Avx.LoadVector256(dataPtr + offset);
                    Vector256<byte> counterVector = Avx.LoadVector256(encryptedPtr);
                    Vector256<byte> result = Avx2.Xor(dataVector, counterVector);
                    Avx.Store(dataPtr + offset, result);

                    Aes256.IncrementCounter(counterBuffer.AsSpan());
                    offset += 32;
                }

                if (offset < data.Length)
                    ProcessDataBlocksScalar(data[offset..], counter, transform);
            }
        }
        finally
        {
            Aes256.Pool.Return(counterBuffer);
            Aes256.Pool.Return(encryptedBuffer);
        }
    }
}