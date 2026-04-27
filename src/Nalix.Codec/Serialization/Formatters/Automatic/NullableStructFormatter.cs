// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.Serialization.Formatters.Automatic;

/// <summary>
/// Provides serialization and deserialization for nullable value types (<see cref="System.Nullable{T}"/>) using a marker byte to indicate nullability.
/// </summary>
/// <typeparam name="T">The underlying value type to serialize or deserialize, which must be a struct.</typeparam>
/// <remarks>
/// This formatter uses a single byte marker (0 for null, 1 for non-null) followed by the serialized value if non-null.
/// It delegates the actual serialization and deserialization of the value type to an instance of <see cref="StructFormatter{T}"/>.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableStructFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T?> where T : struct
{
    private static readonly IFormatter<T> s_valueFormatter = FormatterProvider.Get<T>();

    private static string DebuggerDisplay => $"NullableStructFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a nullable value of type <typeparamref name="T"/> to the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to serialize the value to.</param>
    /// <param name="value">The nullable value to serialize.</param>
    /// <remarks>
    /// Writes a single byte marker (0 if the value is null, 1 if non-null) followed by the serialized value if non-null.
    /// The serialization of the underlying value is handled by an instance of <see cref="StructFormatter{T}"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T? value)
    {
        if (!value.HasValue)
        {
            writer.Write((byte)0);
            return;
        }

        writer.Write((byte)1);
        s_valueFormatter.Serialize(ref writer, value.Value);
    }

    /// <summary>
    /// Deserializes a nullable value of type <typeparamref name="T"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    /// <param name="reader">The reader to deserialize the value from.</param>
    /// <returns>
    /// A <see cref="System.Nullable{T}"/> containing the deserialized value, or <c>null</c> if the marker indicates a null value.
    /// </returns>
    /// <remarks>
    /// Reads a single byte marker to determine nullability (0 for null, 1 for non-null). If non-null, the underlying value
    /// is deserialized using an instance of <see cref="StructFormatter{T}"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T? Deserialize(ref DataReader reader)
    {
        byte marker = reader.ReadByte();
        return marker == 0 ? null : s_valueFormatter.Deserialize(ref reader);
    }
}
