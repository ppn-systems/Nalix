using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Package.Extensions;

public static class HashCodeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddBytes(this ref HashCode hash, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i += sizeof(int))
        {
            int bytesToRead = Math.Min(data.Length - i, sizeof(int));
            int value = MemoryMarshal.Read<int>(data.Slice(i, bytesToRead));
            hash.Add(value);
        }
    }
}