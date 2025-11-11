// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of nullable value types.
/// </summary>
/// <typeparam name="T">The value type of the array elements, which must be a structure.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableArrayFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T?[]> where T : struct
{
    private static System.String DebuggerDisplay => $"NullableArrayFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes an array of nullable value type elements into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array of nullable value type elements to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T?[] value)
    {
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)value.Length);

        if (value.Length == 0)
        {
            return;
        }

        IFormatter<T?> formatter = FormatterProvider.Get<T?>();
        for (System.UInt16 i = 0; i < value.Length; i++)
        {
            formatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes an array of nullable value type elements from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of nullable value type elements, or null if the serialized data represents a null array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T?[] Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider.Get<System.UInt16>()
                                                .Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        IFormatter<T?> formatter = FormatterProvider.Get<T?>();
        T?[] array = new T?[length];
        for (System.UInt16 i = 0; i < length; i++)
        {
            array[i] = formatter.Deserialize(ref reader);
        }

        return array;
    }
}
