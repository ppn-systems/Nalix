// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// <para>Provides serialization and deserialization functionality for <see cref="System.String"/> arrays.</para>
/// <para>
/// Encoding format:
/// - UInt16: number of elements
///     - 0          => empty array
///     - 65535      => null array (SerializerBounds.Null)
///     - otherwise  => element count, followed by that many serialized strings
/// </para>
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StringArrayFormatter : IFormatter<System.String[]>
{
    private static System.String DebuggerDisplay => "StringFormatter<SYSTEM.String[]>";

    private static readonly IFormatter<System.UInt16> UInt16Formatter = FormatterProvider.Get<System.UInt16>();
    private static readonly IFormatter<System.String> StringFormatterInstance = FormatterProvider.Get<System.String>();

    /// <summary>
    /// Serializes a string array into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string array to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.String[] value)
    {
        if (value is null)
        {
            // 65535 biểu diễn null array
            UInt16Formatter.Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        if (value.Length == 0)
        {
            UInt16Formatter.Serialize(ref writer, 0);
            return;
        }

        // Optional: nếu muốn giới hạn số phần tử, có thể thêm check ở đây
        // ví dụ: if (value.Length > SerializerBounds.MaxCollection) throw ...

        if (value.Length > System.UInt16.MaxValue - 1)
        {
            // Chừa 65535 cho null
            throw new SerializationException("The string array exceeds the maximum encodable length.");
        }

        UInt16Formatter.Serialize(ref writer, (System.UInt16)value.Length);

        for (System.Int32 i = 0; i < value.Length; i++)
        {
            // Reuse StringFormatter logic (null, empty, UTF8, bounds, ...)
            StringFormatterInstance.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes a string array from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized string array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public System.String[] Deserialize(ref DataReader reader)
    {
        System.UInt16 length = UInt16Formatter.Deserialize(ref reader);

        if (length == 0)
        {
            return System.Array.Empty<System.String>();
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        System.String[] result = new System.String[length];

        for (System.Int32 i = 0; i < length; i++)
        {
            // Ensure non-null assignment; if null, assign string.Empty to avoid CS8601
            result[i] = StringFormatterInstance.Deserialize(ref reader);
        }

        return result;
    }
}