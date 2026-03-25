// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of reference types.
/// </summary>
/// <typeparam name="T">The reference type of the array elements.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReferenceArrayFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T[]> where T : class
{
    private static string DebuggerDisplay => $"ReferenceArrayFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes an array of reference type objects into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array of reference type objects to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            writer.Expand(sizeof(ushort));
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        writer.Expand(sizeof(ushort));
        FormatterProvider.Get<ushort>()
                         .Serialize(ref writer, (ushort)value.Length);

        if (value.Length == 0)
        {
            return;
        }

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        for (ushort i = 0; i < value.Length; i++)
        {
            formatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes an array of reference type objects from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of reference type objects, or null if the serialized data represents a null array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T[] Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>()
                                                .Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        T[] array = new T[length];
        for (ushort i = 0; i < length; i++)
        {
            array[i] = formatter.Deserialize(ref reader);
        }

        return array;
    }
}
