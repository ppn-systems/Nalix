using Notio.Testing.Ciphers;
using System;

namespace Notio.Testing;

public class Program
{
    public static void Main()
    {
        X25519Testing.Main();
        Aes256Testing.Main();
        ChaCha20Poly1305Testing.Main();

        PacketTesting.Main();

        Console.ReadKey();
    }
}