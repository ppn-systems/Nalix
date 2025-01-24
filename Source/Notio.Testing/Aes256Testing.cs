using System;
using System.Text;
using Notio.Cryptography;

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
            TestDifferentInputSizes
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
        var originalText = Encoding.UTF8.GetBytes("Hello, World!");

        var encrypted = Aes256.CbcMode.Encrypt(originalText, key);
        var decrypted = Aes256.CbcMode.Decrypt(encrypted, key);

        if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            throw new Exception("CBC Decryption failed");
    }

    private static void TestGcmEncryptionDecryption()
    {
        var key = Aes256.GenerateKey();
        var originalText = Encoding.UTF8.GetBytes("Secure Message");

        var encrypted = Aes256.GcmMode.Encrypt(originalText, key);
        var decrypted = Aes256.GcmMode.Decrypt(encrypted, key);

        if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            throw new Exception("GCM Decryption failed");
    }

    private static void TestCtrEncryptionDecryption()
    {
        var key = Aes256.GenerateKey();
        var originalText = Encoding.UTF8.GetBytes("CTR Mode Test");

        var encrypted = Aes256.CtrMode.Encrypt(originalText, key);
        var decrypted = Aes256.CtrMode.Decrypt(encrypted, key);

        if (!originalText.AsSpan().SequenceEqual(decrypted.Span))
            throw new Exception("CTR Decryption failed");
    }

    private static void TestDifferentInputSizes()
    {
        var key = Aes256.GenerateKey();
        var testSizes = new[] { 1, 16, 100, 1024, 10240 };

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