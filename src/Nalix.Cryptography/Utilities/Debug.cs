using System;
using System.Diagnostics;

namespace Nalix.Cryptography.Utilities;

internal static class DebugHelper
{
    public static string ToHexString(byte[] data) => Convert.ToHexString(data);

    public static string ToHexString(uint value) => $"0x{value:X8}";

    public static void LogBlock(string prefix, uint x, uint y)
        => Debug.WriteLine($"{prefix}: x={ToHexString(x)}, y={ToHexString(y)}");

    public static void LogSubkeys(string prefix, uint[] subkeys)
    {
        Debug.WriteLine($"{prefix}:");
        for (int i = 0; i < subkeys.Length; i++)
        {
            Debug.WriteLine($"  [{i}]: {ToHexString(subkeys[i])}");
        }
    }
}
