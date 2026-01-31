// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Formatters.Primitives;

/// <summary>
/// <para>Provides serialization and deserialization functionality for <see cref="string"/> arrays.</para>
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
internal sealed class StringArrayFormatter : IFormatter<string[]>
{
    private static string DebuggerDisplay => "StringFormatter<SYSTEM.String[]>";

    private static readonly IFormatter<ushort> s_uInt16Formatter = FormatterProvider.Get<ushort>();
    private static readonly IFormatter<string> s_stringFormatterInstance = FormatterProvider.Get<string>();

    /// <summary>
    /// Serializes a string array into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string array to serialize.</param>
    /// <exception cref="SerializationFailureException">Thrown when <paramref name="value"/> exceeds the maximum encodable element count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the writer cannot expand or a required nested formatter is unavailable.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, string[] value)
    {
        if (value is null)
        {
            // 65535 biểu diễn null array
            s_uInt16Formatter.Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        if (value.Length == 0)
        {
            s_uInt16Formatter.Serialize(ref writer, 0);
            return;
        }

        // Optional: nếu muốn giới hạn số phần tử, có thể thêm check ở đây
        // ví dụ: if (value.Length > SerializerBounds.MaxCollection) throw ...

        if (value.Length > ushort.MaxValue - 1)
        {
            // Chừa 65535 cho null
            throw new SerializationFailureException("The string array exceeds the maximum encodable length.");
        }

        s_uInt16Formatter.Serialize(ref writer, (ushort)value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            writer.Expand(sizeof(int));
            // Reuse StringFormatter logic (null, empty, UTF8, bounds, ...)
            s_stringFormatterInstance.Serialize(ref writer, value[i]);
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
    public string[] Deserialize(ref DataReader reader)
    {
        ushort length = s_uInt16Formatter.Deserialize(ref reader);

        if (length == 0)
        {
            return Array.Empty<string>();
        }

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        string[] result = new string[length];

        for (int i = 0; i < length; i++)
        {
            // Ensure non-null assignment; if null, assign string.Empty to avoid CS8601
            result[i] = s_stringFormatterInstance.Deserialize(ref reader);
        }

        return result;
    }
}
