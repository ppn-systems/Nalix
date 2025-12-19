// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization logic for
/// <see cref="System.Memory{T}"/> and <see cref="System.ReadOnlyMemory{T}"/>.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
/// <remarks>
/// <para>
/// Wire format:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>[4 bytes]</c> Length (<see cref="System.Int32"/>, little-endian)
/// — <c>-1</c> indicates default (empty), <c>0</c> indicates zero-length.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>[Length * sizeof(T) bytes]</c> Raw element data, little-endian per element.
/// </description>
/// </item>
/// </list>
/// <para>
/// Because <c>Memory&lt;T&gt;</c> is a value type, default and empty are treated identically.
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class MemoryFormatter<T> : IFormatter<System.Memory<T>>
    where T : unmanaged
{
    private static System.String DebuggerDisplay => $"MemoryFormatter<{typeof(T).Name}>";

    // ------------------------------------------------------------------ //
    //  Serialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Serializes a <see cref="System.Memory{T}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to which data will be written.</param>
    /// <param name="value">The memory segment to serialize.</param>
    /// <remarks>
    /// Writes length prefix followed by raw element bytes using
    /// <see cref="System.Runtime.InteropServices.MemoryMarshal.AsBytes{T}(System.ReadOnlySpan{T})"/>
    /// for zero-copy reinterpretation.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Memory<T> value)
    {
        System.Int32 length = value.Length;
        writer.Expand(sizeof(System.Int32));
        FormatterProvider.Get<System.Int32>()
                         .Serialize(ref writer, length);

        if (length is 0)
        {
            return;
        }

        // Zero-copy: reinterpret Memory<T> thành raw bytes
        System.ReadOnlySpan<System.Byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.Span);

        writer.Expand(bytes.Length);
        bytes.CopyTo(writer.FreeBuffer);
        writer.Advance(bytes.Length);
    }

    // ------------------------------------------------------------------ //
    //  Deserialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Deserializes a <see cref="System.Memory{T}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    /// <param name="reader">The reader containing serialized data.</param>
    /// <returns>
    /// A <see cref="System.Memory{T}"/> wrapping a newly allocated array,
    /// or <see cref="System.Memory{T}.Empty"/> if length is 0 or -1.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Memory<T> Deserialize(ref DataReader reader)
    {
        System.Int32 length = FormatterProvider.Get<System.Int32>()
                                               .Deserialize(ref reader);

        if (length <= 0)
        {
            return System.Memory<T>.Empty;
        }

        T[] array = System.GC.AllocateUninitializedArray<T>(length);
        System.Int32 byteCount = length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        ref System.Byte src = ref reader.GetSpanReference(byteCount);
        ref T first = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
        ref System.Byte dst = ref System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref first);

        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref dst, ref src, (System.UInt32)byteCount);
        reader.Advance(byteCount);

        return System.MemoryExtensions.AsMemory(array);
    }
}

// --------------------------------------------------------------------------

/// <summary>
/// Provides serialization and deserialization logic for
/// <see cref="System.ReadOnlyMemory{T}"/>.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
/// <remarks>
/// Delegates to <see cref="MemoryFormatter{T}"/> internally —
/// wire format is identical to <see cref="System.Memory{T}"/>.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReadOnlyMemoryFormatter<T> : IFormatter<System.ReadOnlyMemory<T>>
    where T : unmanaged
{
    private static System.String DebuggerDisplay => $"ReadOnlyMemoryFormatter<{typeof(T).Name}>";

    private static readonly MemoryFormatter<T> _inner = new();

    /// <summary>
    /// Serializes a <see cref="System.ReadOnlyMemory{T}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.ReadOnlyMemory<T> value) => _inner.Serialize(ref writer, System.Runtime.InteropServices.MemoryMarshal.AsMemory(value));

    /// <summary>
    /// Deserializes a <see cref="System.ReadOnlyMemory{T}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ReadOnlyMemory<T> Deserialize(ref DataReader reader) => _inner.Deserialize(ref reader);
}
