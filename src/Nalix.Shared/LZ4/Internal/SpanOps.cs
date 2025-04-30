namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Helper methods for working with Spans.
/// </summary>
internal static unsafe class SpanOps
{
    /// <summary>
    /// Ensures the requested slice is within the span bounds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void CheckSliceBounds(int length, int start, int count)
    {
        // Use unsigned arithmetic for efficient check: (uint)start + (uint)count > (uint)length
        if ((uint)start > (uint)length || (uint)count > (uint)(length - start)) ThrowOutOfRange();
    }

    /// <summary>
    /// Writes a variable-length integer (little-endian). Used for lengths greater than 15.
    /// Writes bytes until the value is less than 255.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int WriteVarInt(byte* dest, int value)
    {
        int bytesWritten = 0;
        uint uValue = (uint)value;

        while (uValue >= 255)
        {
            *dest++ = 255;
            uValue -= 255;
            bytesWritten++;
        }

        *dest = (byte)uValue;
        return bytesWritten + 1;
    }

    /// <summary>
    /// Reads a variable-length integer (little-endian).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ReadVarInt(ref byte* src, byte* srcEnd, out int value)
    {
        value = 0;
        int bytesRead = 0;
        byte currentByte;

        while (src < srcEnd)
        {
            currentByte = *src;
            src++;
            value += currentByte;
            bytesRead++;

            if (currentByte < 255) return bytesRead;
        }

        value = -1; // Error: reached end without termination
        return bytesRead;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // Keep exception throwing logic separate
    private static void ThrowOutOfRange() => throw new System.ArgumentOutOfRangeException("start or count");
}
