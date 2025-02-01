using Notio.Lite;
using System;
using System.Linq;

namespace Notio.Network.Web.Net.Internal;

/// <summary>
/// Represents some System.NET custom extensions.
/// </summary>
internal static class NetExtensions
{
    internal static byte[] ToByteArray(this ushort value, Endianness order)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!order.IsHostOrder())
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    internal static byte[] ToByteArray(this ulong value, Endianness order)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!order.IsHostOrder())
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    internal static byte[] ToHostOrder(this byte[] source, Endianness sourceOrder)
    {
        return source.Length < 1 ? source
            : sourceOrder.IsHostOrder() ? source
            : source.Reverse().ToArray();
    }

    // true: !(true ^ true) or !(false ^ false)
    // false: !(true ^ false) or !(false ^ true)
    private static bool IsHostOrder(this Endianness order)
    {
        return !(BitConverter.IsLittleEndian ^ order == Endianness.Little);
    }
}