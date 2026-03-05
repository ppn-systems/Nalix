using Nalix.Common.Enums;
using Nalix.Shared.Extensions;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

internal static class Program
{
    private static readonly System.Threading.ManualResetEvent QuitEvent = new(false);

    public static async Task Main(String[] args)
    //InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
    //InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();

    //Console.WriteLine("Starting Custom TCP Listener...");
    //const UInt16 port = 8080;

    //// Tạo đối tượng Protocol và Listener
    //var protocol = new EchoProtocol();
    //var listener = new CustomTcpListener(port, protocol);

    //// Bắt đầu lắng nghe
    //CancellationTokenSource cts = new();
    //try
    //{
    //    Console.CancelKeyPress += (s, e) =>
    //    {
    //        e.Cancel = true;
    //        cts.Cancel(); // Dừng server khi nhấn Ctrl+C
    //    };

    //    listener.Activate(cts.Token);

    //    QuitEvent.WaitOne();
    //}
    //catch (Exception ex)
    //{
    //    Console.WriteLine($"Error: {ex.Message}");
    //}
    //finally
    //{
    //    listener.Dispose(); // Dọn dẹp tài nguyên
    //    Console.WriteLine("Listener stopped.");
    //}
    {
        // Create a random 256-bit key for AES-GCM (32 bytes)
        Byte[] key = new Byte[32];
        RandomNumberGenerator.Fill(key);

        String plaintext = "Xin chào, đây là test encryption/decryption!";

        Console.WriteLine("Original: " + plaintext);

        // Encrypt to Base64
        String cipherBase64 = plaintext.EncryptToBase64(key, CipherSuiteType.SALSA20);
        Console.WriteLine("Cipher (Base64): " + cipherBase64);

        // Decrypt back
        String decrypted = cipherBase64.DecryptFromBase64(key);
        Console.WriteLine("Decrypted: " + decrypted);

        // Intentional failure: wrong key
        Byte[] wrongKey = new Byte[32];
        RandomNumberGenerator.Fill(wrongKey);
        try
        {
            String bad = cipherBase64.DecryptFromBase64(wrongKey);
            Console.WriteLine("Decrypted with wrong key (should not happen): " + bad);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Expected failure with wrong key: " + ex.Message);
        }
    }
}