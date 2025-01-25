
namespace Notio.Cryptography.Mode.a
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading;

    /// <summary>
    /// Lớp cung cấp chức năng mã hóa và giải mã AES-256 với chế độ CtrMode (Counter)
    /// </summary>
    internal static unsafe class AesCtrCipher
    {
        private static readonly ThreadLocal<System.Security.Cryptography.Aes> AesInstanceCache =
            new(() => System.Security.Cryptography.Aes.Create());

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

                    counter.CopyTo(iv);

                    var aes = GetCachedAes(key);
                    using ICryptoTransform encryptor = aes.CreateEncryptor();

                    byte* outputPtr = (byte*)resultPtr + Aes256.BlockSize;
                    fixed (void* plaintextPtr = plaintext)
                    {
                        Unsafe.CopyBlock(outputPtr, plaintextPtr, (uint)plaintext.Length);
                    }

                    ProcessDataBlocksCtr(new Span<byte>(outputPtr, plaintext.Length), counter, encryptor);

                    return new Aes256.MemoryBuffer(resultOwner, plaintext.Length + Aes256.BlockSize);
                }
            }
            catch
            {
                resultOwner.Dispose();
                throw;
            }
            finally
            {
                Aes256.Pool.Return(iv.ToArray());
                Aes256.Pool.Return(counter.ToArray());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe Aes256.MemoryBuffer Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> encryptedData)
        {
            Aes256.ValidateKey(key.ToArray());

            if (encryptedData.Length < Aes256.BlockSize)
                throw new ArgumentException("Encrypted data is too short.");

            Span<byte> iv = Aes256.Pool.Rent(Aes256.BlockSize);
            Span<byte> counter = Aes256.Pool.Rent(Aes256.BlockSize);

            try
            {
                // Trích xuất IV
                encryptedData[..Aes256.BlockSize].CopyTo(iv);
                iv.CopyTo(counter);

                int dataLength = encryptedData.Length - Aes256.BlockSize;
                IMemoryOwner<byte> resultOwner = MemoryPool<byte>.Shared.Rent(dataLength);

                fixed (void* resultPtr = resultOwner.Memory.Span)
                {
                    var aes = GetCachedAes(key);
                    using ICryptoTransform encryptor = aes.CreateEncryptor();

                    ReadOnlySpan<byte> encryptedPayload = encryptedData[Aes256.BlockSize..];
                    Span<byte> resultSpan = resultOwner.Memory.Span[..dataLength];

                    encryptedPayload.CopyTo(resultSpan);

                    ProcessDataBlocksCtr(resultSpan, counter, encryptor);
                }

                return new Aes256.MemoryBuffer(resultOwner, dataLength);
            }
            catch
            {
                throw;
            }
            finally
            {
                Aes256.Pool.Return(iv.ToArray());
                Aes256.Pool.Return(counter.ToArray());
            }
        }


        private static void ProcessDataBlocksCtr(Span<byte> data, Span<byte> counter, ICryptoTransform transform)
        {
            if (counter.Length != Aes256.BlockSize)
                throw new ArgumentException("Counter length must match AES block size.");

            // Sử dụng stackalloc để cấp phát bộ nhớ tĩnh cho counter và encryptedCounter
            Span<byte> counterBuffer = stackalloc byte[Aes256.BlockSize];
            Span<byte> encryptedCounter = stackalloc byte[Aes256.BlockSize];

            // Sao chép counter đầu vào vào counterBuffer
            counter.CopyTo(counterBuffer);

            for (int i = 0; i < data.Length; i += Aes256.BlockSize)
            {
                // Mã hóa counter để tạo encryptedCounter
                transform.TransformBlock(counterBuffer.ToArray(), 0, Aes256.BlockSize, encryptedCounter.ToArray(), 0);

                int blockSize = Math.Min(Aes256.BlockSize, data.Length - i);
                for (int j = 0; j < blockSize; j++)
                {
                    // XOR dữ liệu với encryptedCounter
                    data[i + j] ^= encryptedCounter[j];
                }

                // Tăng giá trị counter (Big Endian)
                for (int k = counterBuffer.Length - 1; k >= 0; k--)
                {
                    if (++counterBuffer[k] != 0) break;
                }
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
    }
}
