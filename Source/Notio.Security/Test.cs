//using Notio.Security.Exceptions;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Threading.Tasks;

//namespace Notio.Security
//{
//    internal class Test
//    {
//        /// <summary>
//        /// Giải mã dữ liệu đã mã hóa bất đồng bộ sử dụng AES-256 CTR mode
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
//        public static async ValueTask<byte[]> DecryptAsync(byte[] key, byte[] ciphertext)
//        {
//            Aes256.ValidateKey(key);
//            Aes256.ValidateInput(ciphertext, nameof(ciphertext));

//            if (ciphertext.Length <= Aes256.BlockSize)
//                throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));

//            byte[] encryptedCounter = null;
//            byte[] counter = null;
//            byte[] buffer = null;

//            try
//            {
//                encryptedCounter = Aes256.Pool.Rent(Aes256.BlockSize);
//                counter = Aes256.Pool.Rent(Aes256.BlockSize);
//                buffer = Aes256.Pool.Rent(Aes256.BufferSize);

//                Buffer.BlockCopy(ciphertext, 0, counter, 0, Aes256.BlockSize);

//                using var aes = CreateAesCTR(key);
//                using var ms = new MemoryStream(ciphertext, Aes256.BlockSize, ciphertext.Length - Aes256.BlockSize);
//                using var decryptor = aes.CreateDecryptor();
//                using var resultStream = new MemoryStream(ciphertext.Length - Aes256.BlockSize);

//                int bytesRead;
//                while ((bytesRead = await ms.ReadAsync(buffer.AsMemory(0, Aes256.BufferSize))) > 0)
//                {
//                    for (int i = 0; i < bytesRead; i += Aes256.BlockSize)
//                    {
//                        int currentBlockSize = Math.Min(Aes256.BlockSize, bytesRead - i);
//                        decryptor.TransformBlock(counter, 0, Aes256.BlockSize, encryptedCounter, 0);
//                        Aes256.XorBlock(buffer.AsSpan(i, currentBlockSize), encryptedCounter.AsSpan(0, currentBlockSize));
//                        Aes256.IncrementCounter(counter);
//                    }

//                    await resultStream.WriteAsync(buffer.AsMemory(0, bytesRead));
//                }

//                return resultStream.ToArray();
//            }
//            catch (Exception ex) when (ex is not CryptoOperationException)
//            {
//                throw new CryptoOperationException("Decryption failed", ex);
//            }
//            finally
//            {
//                if (encryptedCounter != null) Aes256.Pool.Return(encryptedCounter);
//                if (counter != null) Aes256.Pool.Return(counter);
//                if (buffer != null) Aes256.Pool.Return(buffer);
//            }
//        }
//    }
//}
