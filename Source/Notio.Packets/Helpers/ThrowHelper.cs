using System;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Helpers;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPayloadTooLarge() =>
        throw new ArgumentException("Payload size exceeds maximum allowed");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidPacketSize() =>
        throw new ArgumentException("Data too short for packet header");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidLength() =>
        throw new ArgumentException("Invalid packet length");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentNullException(string paramName) =>
        throw new ArgumentNullException(paramName);

    public static void ThrowArgumentException(string message) =>
        throw new ArgumentException(message);
}