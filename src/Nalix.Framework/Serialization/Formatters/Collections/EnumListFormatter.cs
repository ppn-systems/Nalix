// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Serialization;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Formatters.Primitives;

namespace Nalix.Framework.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for <see cref="System.Collections.Generic.List{T}"/>
/// where T is an enum type.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class EnumListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<System.Collections.Generic.List<T>>
    where T : struct, System.Enum
{
    private static readonly EnumFormatter<T> _enumFormatter = new();
    private static string DebuggerDisplay => $"EnumListFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a list of enum values into the provided writer using their underlying primitive type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of enum values to serialize.</param>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the underlying type of the enum is not supported by the <see cref="EnumFormatter{T}"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T> value)
    {
        if (value is null)
        {
            writer.Expand(sizeof(ushort));
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        writer.Expand(sizeof(ushort));
        ushort count = (ushort)value.Count;
        FormatterProvider.Get<ushort>().Serialize(ref writer, count);

        for (int i = 0; i < count; i++)
        {
            _enumFormatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes a list of enum values from the provided reader using their underlying primitive type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of enum values, or null if the serialized data represents a null list.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the list length is out of range or if the underlying type of the enum is not supported by the <see cref="EnumFormatter{T}"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        ushort count = FormatterProvider.Get<ushort>()
                                               .Deserialize(ref reader);

        if (count == 0)
        {
            return [];
        }

        if (count == SerializerBounds.Null)
        {
            return null!;
        }

        if (count > SerializerBounds.MaxArray)
        {
            throw new SerializationFailureException("Enum list length out of range.");
        }

        System.Collections.Generic.List<T> result = new(count);
        for (ushort i = 0; i < count; i++)
        {
            result.Add(_enumFormatter.Deserialize(ref reader));
        }

        return result;
    }
}
