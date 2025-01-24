using Notio.Cryptography;
using System;
using System.Drawing;
using System.Text;

namespace Notio.Testing;

public static class Aes256Testing
{
    public static void Main()
    {
        var tests = new Action[]
        {
            TestCbcEncryptionDecryption,
            TestGcmEncryptionDecryption,
            TestCtrEncryptionDecryption,
            //TestDifferentInputSizes
        };

        foreach (var test in tests)
        {
            try
            {
                test.Invoke();
                Console.WriteLine($"{test.Method.Name}: Passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{test.Method.Name}: Failed - {ex.Message}");
            }
        }
    }

    private static void TestCbcEncryptionDecryption()
    {
        var key = Aes256.GenerateKey();
        var originalText = GenerateRandomBytes(5000);

        var encrypted = Aes256.CbcMode.Encrypt(originalText, key);
        var decrypted = Aes256.CbcMode.Decrypt(encrypted, key);

        if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            throw new Exception("CBC Decryption failed");
    }

    private static void TestGcmEncryptionDecryption()
    {
        var key = Aes256.GenerateKey();
        var originalText = GenerateRandomBytes(5000);

        var encrypted = Aes256.GcmMode.Encrypt(originalText, key);
        var decrypted = Aes256.GcmMode.Decrypt(encrypted, key);

        if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            throw new Exception("GCM Decryption failed");
    }

    private static void TestCtrEncryptionDecryption()
    {
        try
        {
            var key = Aes256.GenerateKey();
            var originalText = GenerateRandomBytes(50);
            Console.WriteLine("Original text length: " + originalText.Length);

            var encrypted = Aes256.CtrMode.Encrypt(originalText, key);
            Console.WriteLine("Encrypted text length: " + encrypted.Length);

            var decrypted = Aes256.CtrMode.Decrypt(encrypted, key);
            Console.WriteLine("Decrypted text length: " + decrypted.Length);

            // In chi tiết để so sánh
            Console.WriteLine("Original text (hex):");
            Console.WriteLine(BitConverter.ToString(originalText));

            Console.WriteLine("Decrypted text (hex):");
            Console.WriteLine(BitConverter.ToString(decrypted.ToArray()));

            if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            {
                throw new Exception("CTR Decryption failed: Mismatch between original and decrypted data.");
            }

            Console.WriteLine("CTR Encryption/Decryption test passed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TestCtrEncryptionDecryption: Failed - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void TestDifferentInputSizes()
    {
        var key = Aes256.GenerateKey();
        var testSizes = new[] { 10240, 10240 * 2 };

        foreach (var size in testSizes)
        {
            byte[] originalText = GenerateRandomBytes(size);

            ReadOnlyMemory<byte> cbcEncrypted = Aes256.CbcMode.Encrypt(originalText, key);
            ReadOnlyMemory<byte> cbcDecrypted = Aes256.CbcMode.Decrypt(cbcEncrypted, key);

            ReadOnlyMemory<byte> gcmEncrypted = Aes256.GcmMode.Encrypt(originalText, key);
            ReadOnlyMemory<byte> gcmDecrypted = Aes256.GcmMode.Decrypt(gcmEncrypted, key);

            ReadOnlyMemory<byte> ctrEncrypted = Aes256.CtrMode.Encrypt(originalText, key);
            ReadOnlyMemory<byte> ctrDecrypted = Aes256.CtrMode.Decrypt(ctrEncrypted, key);
            if (!originalText.AsSpan().SequenceEqual(cbcDecrypted.Span) ||
                !originalText.AsSpan().SequenceEqual(gcmDecrypted.Span) ||
                !originalText.AsSpan().SequenceEqual(ctrDecrypted.Span))
            {
                throw new Exception($"Decryption failed for input size {size}");
            }
        }
    }

    private static byte[] GenerateRandomBytes(int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}