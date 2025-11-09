// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides formatting for unmanaged types.
/// </summary>
/// <remarks>
/// Unmanaged types include:
/// <list type="bullet">
/// <item>
/// <description>
/// sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool
/// </description>
/// </item>
/// <item><description>Any enum type</description></item>
/// <item><description>Any pointer type</description></item>
/// <item><description>Any user-defined struct type that contains fields of unmanaged types only</description></item>
/// </list>
/// Reference: <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types"/>.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed partial class UnmanagedFormatter<T> : IFormatter<T> where T : unmanaged
{
    private static System.String DebuggerDisplay => $"UnmanagedFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Writes an unmanaged value to the buffer without alignment requirements.
    /// </summary>
    /// <param name="writer">The <see cref="DataWriter"/> to write to.</param>
    /// <param name="value">The unmanaged value to write.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        System.Int32 size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        if (size == sizeof(System.Byte))
        {
            System.Span<System.Byte> dst = writer.FreeBuffer[..sizeof(System.Byte)];
            // Bit-preserving cast for byte/sbyte/bool
            dst[0] = System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref value);
            writer.Advance(sizeof(System.Byte));
            return;
        }

        if (size == sizeof(System.UInt16))
        {
            System.UInt16 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt16>(ref value);
            System.Span<System.Byte> dst = writer.FreeBuffer[..sizeof(System.UInt16)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst, v);
            writer.Advance(sizeof(System.UInt16));
            return;
        }

        if (size == sizeof(System.UInt32))
        {
            // Works for int/uint/float via bit reinterpret
            System.UInt32 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt32>(ref value);
            System.Span<System.Byte> dst = writer.FreeBuffer[..sizeof(System.UInt32)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dst, v);
            writer.Advance(sizeof(System.UInt32));
            return;
        }

        if (size == sizeof(System.UInt64))
        {
            // Works for long/ulong/double via bit reinterpret
            System.UInt64 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt64>(ref value);
            System.Span<System.Byte> dst = writer.FreeBuffer[..sizeof(System.UInt64)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(dst, v);
            writer.Advance(sizeof(System.UInt64));
            return;
        }

        if (size == sizeof(System.Decimal))
        {
            if (typeof(T) == typeof(System.Decimal))
            {
                System.Decimal dec = System.Runtime.CompilerServices.Unsafe.As<T, System.Decimal>(ref value);
                System.Int32[] parts = System.Decimal.GetBits(dec); // length = 4

                System.Span<System.Byte> dst = writer.FreeBuffer[..sizeof(System.Decimal)];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst[..4], parts[0]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(4, 4), parts[1]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(8, 4), parts[2]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(12, 4), parts[3]);
                writer.Advance(sizeof(System.Decimal));
                return;
            }

            throw new System.NotSupportedException(
                $"UnmanagedFormatter<{typeof(T).Name}>: 16-byte type not supported (only decimal).");
        }

        throw new System.NotSupportedException(
            $"UnmanagedFormatter<{typeof(T).Name}>: Unsupported size {size}.");
    }

    /// <summary>
    /// Reads an unmanaged value from the buffer without alignment requirements.
    /// </summary>
    /// <param name="reader">The <see cref="DataReader"/> to read from.</param>
    /// <returns>The unmanaged value read from the buffer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader)
    {
        System.Int32 size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        if (size == sizeof(System.Byte))
        {
            ref System.Byte start1 = ref reader.GetSpanReference(sizeof(System.Byte));
            System.Byte b = start1;
            reader.Advance(sizeof(System.Byte));
            return System.Runtime.CompilerServices.Unsafe.As<System.Byte, T>(ref b);
        }

        if (size == sizeof(System.UInt16))
        {
            ref System.Byte start2 = ref reader.GetSpanReference(sizeof(System.UInt16));
            System.Span<System.Byte> src2 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start2, sizeof(System.UInt16));
            System.UInt16 v2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(src2);
            reader.Advance(sizeof(System.UInt16));
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, T>(ref v2);
        }

        if (size == sizeof(System.UInt32))
        {
            ref System.Byte start4 = ref reader.GetSpanReference(sizeof(System.UInt32));
            System.Span<System.Byte> src4 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start4, sizeof(System.UInt32));
            System.UInt32 v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src4);
            reader.Advance(sizeof(System.UInt32));
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, T>(ref v4);
        }

        if (size == sizeof(System.UInt64))
        {
            ref System.Byte start8 = ref reader.GetSpanReference(sizeof(System.UInt64));
            System.Span<System.Byte> src8 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start8, sizeof(System.UInt64));
            System.UInt64 v8 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(src8);
            reader.Advance(sizeof(System.UInt64));
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt64, T>(ref v8);
        }

        if (size == sizeof(System.Decimal))
        {
            if (typeof(T) == typeof(System.Decimal))
            {
                ref System.Byte start16 = ref reader.GetSpanReference(sizeof(System.Decimal));
                System.Span<System.Byte> src16 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start16, sizeof(System.Decimal));

                System.Int32 lo = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16[..4]);
                System.Int32 mid = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(4, 4));
                System.Int32 hi = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(8, 4));
                System.Int32 flags = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(12, 4));

                System.Decimal dec = new([lo, mid, hi, flags]);
                reader.Advance(sizeof(System.Decimal));
                return System.Runtime.CompilerServices.Unsafe.As<System.Decimal, T>(ref dec);
            }

            throw new System.NotSupportedException(
                $"UnmanagedFormatter<{typeof(T).Name}>: 16-byte type not supported (only decimal).");
        }

        throw new System.NotSupportedException(
            $"UnmanagedFormatter<{typeof(T).Name}>: Unsupported size {size}.");
    }
}
