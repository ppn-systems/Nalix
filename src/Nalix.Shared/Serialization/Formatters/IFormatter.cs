// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters;

/// <summary>
/// Defines methods to serialize and deserialize a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type to serialize and deserialize.</typeparam>
public interface IFormatter<[
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    /// <summary>
    /// Serializes the specified value into the provided serialization writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The value to serialize.</param>
    void Serialize(ref DataWriter writer, T value);

    /// <summary>
    /// Deserializes a value from the provided serialization reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized value of type <typeparamref name="T"/>.</returns>
    T Deserialize(ref DataReader reader);
}
