// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.Memory;
using Nalix.Codec.Serialization.Internal.Emit;
using Nalix.Codec.Serialization.Internal.Types;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Codec.Serialization.Formatters.Automatic;

/// <summary>
/// Optimized field-based serializer eliminating boxing for maximum performance.
/// Implements SOLID principles with Domain-Driven Design patterns.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StructFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T> where T : struct
{
    #region Core Fields

    private static string DebuggerDisplay => $"StructFormatter<{typeof(T).FullName}>";

    #endregion Core Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectFormatter{T}"/>.
    /// </summary>
    /// <exception cref="SerializationFailureException">
    /// Thrown if initialization of property accessors fails.
    /// </exception>
    public StructFormatter() => TypeMetadata.RecursiveWarmupFields(typeof(T));

    #endregion Constructors

    #region Serialization

    /// <summary>
    /// Serializes an object into the provided binary writer.
    /// </summary>
    /// <param name="writer">The binary writer used for serialization.</param>
    /// <param name="value">The object to serialize.</param>
    /// <exception cref="SerializationFailureException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value) => StructILCodec<T>.Serialize(ref writer, value);

    /// <summary>
    /// Deserializes an object from the provided binary reader.
    /// </summary>
    /// <param name="reader">The binary reader containing serialized data.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if deserialization encounters an error.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader) => StructILCodec<T>.Deserialize(ref reader);

    #endregion Serialization
}
