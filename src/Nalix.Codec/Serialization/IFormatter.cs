// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Serialization;

/// <summary>
/// Defines methods to serialize and deserialize a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type to serialize and deserialize.</typeparam>
public interface IFormatter<[
    DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
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

/// <summary>
/// Extends <see cref="IFormatter{T}"/> to support in-place rehydration of an existing instance.
/// </summary>
/// <typeparam name="T">The type to deserialize.</typeparam>
public interface IFillableFormatter<[
    DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T>
{
    /// <summary>
    /// Populates an existing instance of <typeparamref name="T"/> using data from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing serialized data.</param>
    /// <param name="value">The existing instance to populate.</param>
    void Fill(ref DataReader reader, T value);
}
