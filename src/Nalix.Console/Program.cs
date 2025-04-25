using Nalix.Cryptography.Hashing;
using System;
using System.Text;

namespace Nalix.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        string input = "abc";

        System.Console.WriteLine($"HASH: {BitConverter.ToString(
            System.Security.Cryptography.SHA1.HashData(Encoding.ASCII.GetBytes(input)))}");
        System.Console.WriteLine($"HASH: {BitConverter.ToString(SHA1.HashData(Encoding.ASCII.GetBytes(input)))}");
    }
}
