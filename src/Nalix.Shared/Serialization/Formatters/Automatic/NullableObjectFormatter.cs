// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Automatic;

/// <summary>
/// Provides serialization and deserialization for nullable reference types using a marker byte to indicate nullability.
/// </summary>
/// <typeparam name="T">The reference type to serialize or deserialize, which must be a class with a parameterless constructor.</typeparam>
/// <remarks>
/// This formatter writes a single byte marker (0 for null, 1 for non-null) followed by the serialized object if non-null.
/// Serialization and deserialization of the underlying object are delegated to an instance of <see cref="IFormatter{T}"/> obtained via <see cref="FormatterProvider"/>.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableObjectFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T?> where T : class, new()
{
    private static System.String DebuggerDisplay => $"NullableObjectFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Serializes a nullable reference type value of type <typeparamref name="T"/> to the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to serialize the value to.</param>
    /// <param name="value">The nullable reference type value to serialize.</param>
    /// <remarks>
    /// Writes a single byte marker (0 if the value is null, 1 if non-null) followed by the serialized object if non-null.
    /// The serialization of the underlying object is handled by an <see cref="IFormatter{T}"/> obtained via <see cref="FormatterProvider.Get{TFormatter}"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T? value)
    {
        if (value is null)
        {
            FormatterProvider.Get<System.Byte>()
                             .Serialize(ref writer, 0);

            return;
        }

        FormatterProvider.Get<System.Byte>()
                         .Serialize(ref writer, 1);

        FormatterProvider.GetComplex<T>()
                         .Serialize(ref writer, value);
    }

    /// <summary>
    /// Deserializes a nullable reference type value of type <typeparamref name="T"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    /// <param name="reader">The reader to deserialize the value from.</param>
    /// <returns>
    /// A value of type <typeparamref name="T"/> if the marker indicates a non-null value; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Reads a single byte marker to determine nullability (0 for null, 1 for non-null). If non-null, the underlying object
    /// is deserialized using an <see cref="IFormatter{T}"/> obtained via <see cref="FormatterProvider.Get{TFormatter}"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T? Deserialize(ref DataReader reader)
    {
        System.Byte marker = FormatterProvider.Get<System.Byte>()
                                              .Deserialize(ref reader);
        return marker == 0 ? null
            : FormatterProvider.GetComplex<T>()
                               .Deserialize(ref reader);
    }
}