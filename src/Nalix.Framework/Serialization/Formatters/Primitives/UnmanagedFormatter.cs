// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Formatters.Primitives;

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
internal sealed partial class UnmanagedFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T> where T : unmanaged
{
    private static string DebuggerDisplay => $"UnmanagedFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Writes an unmanaged value to the buffer without alignment requirements.
    /// </summary>
    /// <param name="writer">The <see cref="DataWriter"/> to write to.</param>
    /// <param name="value">The unmanaged value to write.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        writer.Expand(size);

        if (size == sizeof(byte))
        {
            System.Span<byte> dst = writer.FreeBuffer[..sizeof(byte)];
            // Bit-preserving cast for byte/sbyte/bool
            dst[0] = System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref value);
            writer.Advance(sizeof(byte));
            return;
        }

        if (size == sizeof(ushort))
        {
            ushort v = System.Runtime.CompilerServices.Unsafe.As<T, ushort>(ref value);
            System.Span<byte> dst = writer.FreeBuffer[..sizeof(ushort)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst, v);
            writer.Advance(sizeof(ushort));
            return;
        }

        if (size == sizeof(uint))
        {
            // Works for int/uint/float via bit reinterpret
            uint v = System.Runtime.CompilerServices.Unsafe.As<T, uint>(ref value);
            System.Span<byte> dst = writer.FreeBuffer[..sizeof(uint)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dst, v);
            writer.Advance(sizeof(uint));
            return;
        }

        if (size == sizeof(ulong))
        {
            // Works for long/ulong/double via bit reinterpret
            ulong v = System.Runtime.CompilerServices.Unsafe.As<T, ulong>(ref value);
            System.Span<byte> dst = writer.FreeBuffer[..sizeof(ulong)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(dst, v);
            writer.Advance(sizeof(ulong));
            return;
        }

        if (size == sizeof(decimal))
        {
            if (typeof(T) == typeof(decimal))
            {
                decimal dec = System.Runtime.CompilerServices.Unsafe.As<T, decimal>(ref value);
                int[] parts = decimal.GetBits(dec); // length = 4

                System.Span<byte> dst = writer.FreeBuffer[..sizeof(decimal)];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst[..4], parts[0]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(4, 4), parts[1]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(8, 4), parts[2]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(12, 4), parts[3]);
                writer.Advance(sizeof(decimal));
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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Deserialize(ref DataReader reader)
    {
        int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        if (size == sizeof(byte))
        {
            ref byte start1 = ref reader.GetSpanReference(sizeof(byte));
            byte b = start1;
            reader.Advance(sizeof(byte));
            return System.Runtime.CompilerServices.Unsafe.As<byte, T>(ref b);
        }

        if (size == sizeof(ushort))
        {
            ref byte start2 = ref reader.GetSpanReference(sizeof(ushort));
            System.Span<byte> src2 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start2, sizeof(ushort));
            ushort v2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(src2);
            reader.Advance(sizeof(ushort));
            return System.Runtime.CompilerServices.Unsafe.As<ushort, T>(ref v2);
        }

        if (size == sizeof(uint))
        {
            ref byte start4 = ref reader.GetSpanReference(sizeof(uint));
            System.Span<byte> src4 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start4, sizeof(uint));
            uint v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(src4);
            reader.Advance(sizeof(uint));
            return System.Runtime.CompilerServices.Unsafe.As<uint, T>(ref v4);
        }

        if (size == sizeof(ulong))
        {
            ref byte start8 = ref reader.GetSpanReference(sizeof(ulong));
            System.Span<byte> src8 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start8, sizeof(ulong));
            ulong v8 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(src8);
            reader.Advance(sizeof(ulong));
            return System.Runtime.CompilerServices.Unsafe.As<ulong, T>(ref v8);
        }

        if (size == sizeof(decimal))
        {
            if (typeof(T) == typeof(decimal))
            {
                ref byte start16 = ref reader.GetSpanReference(sizeof(decimal));
                System.Span<byte> src16 = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref start16, sizeof(decimal));

                int lo = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16[..4]);
                int mid = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(4, 4));
                int hi = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(8, 4));
                int flags = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src16.Slice(12, 4));

                decimal dec = new([lo, mid, hi, flags]);
                reader.Advance(sizeof(decimal));
                return System.Runtime.CompilerServices.Unsafe.As<decimal, T>(ref dec);
            }

            throw new System.NotSupportedException(
                $"UnmanagedFormatter<{typeof(T).Name}>: 16-byte type not supported (only decimal).");
        }

        throw new System.NotSupportedException(
            $"UnmanagedFormatter<{typeof(T).Name}>: Unsupported size {size}.");
    }
}
