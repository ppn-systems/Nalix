// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Serialization;

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
