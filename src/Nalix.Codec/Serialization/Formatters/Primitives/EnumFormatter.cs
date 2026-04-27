// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;
using Nalix.Common.Exceptions;

namespace Nalix.Codec.Serialization.Formatters.Primitives;

/// <summary>
/// Serializes enum values through their underlying integral representation without boxing.
/// The formatter resolves the enum's backing type once and then reuses specialized
/// read/write delegates for the hot path.
/// </summary>
/// <typeparam name="T">The enum type to be serialized and deserialized.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class EnumFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IFormatter<T>
{
    private static readonly SerializeDelegate s_serialize;
    private static readonly DeserializeDelegate s_deserialize;
    private static readonly TypeCode s_underlyingTypeCode;

    private static string DebuggerDisplay => $"EnumFormatter<{typeof(T).FullName}>";

    static EnumFormatter()
    {
        // Resolve the enum backing type once so the generated delegates can be cached.
        s_underlyingTypeCode = Type.GetTypeCode(Enum
                                   .GetUnderlyingType(typeof(T)));

        (s_serialize, s_deserialize) = CreateEnumFormatterDelegates();
    }

    /// <summary>
    /// Serializes an enum value into the provided writer using its underlying type.
    /// </summary>
    /// <remarks>
    /// The generated delegate avoids boxing by reinterpreting the enum bits as the
    /// matching primitive type before calling the primitive formatter.
    /// </remarks>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The enum value to serialize.</param>
    /// <exception cref="InvalidOperationException">Thrown when the formatter was created for a non-enum type.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value) => s_serialize(ref writer, value);

    /// <summary>
    /// Deserializes an enum value from the provided reader using its underlying type.
    /// </summary>
    /// <remarks>
    /// The primitive value is read first and then reinterpreted back into <typeparamref name="T"/>.
    /// </remarks>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized enum value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the formatter was created for a non-enum type.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader) => s_deserialize(ref reader);

    #region Delegates for Enum Formatter

    private delegate T DeserializeDelegate(ref DataReader reader);

    private delegate void SerializeDelegate(ref DataWriter writer, T value);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0072:Add missing cases", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0350:Use implicitly typed lambda", Justification = "<Pending>")]
    private static (SerializeDelegate serialize, DeserializeDelegate deserialize) CreateEnumFormatterDelegates()
    {
        // Dispatch once by backing type, then close over the matching primitive formatter
        // so the runtime path stays as small as possible.
        return s_underlyingTypeCode switch
        {
            TypeCode.Byte => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(byte));
                    byte b = System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref value);
                    FormatterProvider.Get<byte>().Serialize(ref writer, b);
                },
                (ref DataReader reader) =>
                {
                    byte b = FormatterProvider.Get<byte>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<byte, T>(ref b);
                }
            ),
            TypeCode.SByte => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(sbyte));
                    sbyte b = System.Runtime.CompilerServices.Unsafe.As<T, sbyte>(ref value);
                    FormatterProvider.Get<sbyte>().Serialize(ref writer, b);
                },
                (ref DataReader reader) =>
                {
                    sbyte b = FormatterProvider.Get<sbyte>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<sbyte, T>(ref b);
                }
            ),
            TypeCode.Int16 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(short));
                    short v = System.Runtime.CompilerServices.Unsafe.As<T, short>(ref value);
                    FormatterProvider.Get<short>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    short v = FormatterProvider.Get<short>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<short, T>(ref v);
                }
            ),
            TypeCode.UInt16 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(ushort));
                    ushort v = System.Runtime.CompilerServices.Unsafe.As<T, ushort>(ref value);
                    FormatterProvider.Get<ushort>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    ushort v = FormatterProvider.Get<ushort>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<ushort, T>(ref v);
                }
            ),
            TypeCode.Int32 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(int));
                    int v = System.Runtime.CompilerServices.Unsafe.As<T, int>(ref value);
                    FormatterProvider.Get<int>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    int v = FormatterProvider.Get<int>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<int, T>(ref v);
                }
            ),
            TypeCode.UInt32 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(uint));
                    uint v = System.Runtime.CompilerServices.Unsafe.As<T, uint>(ref value);
                    FormatterProvider.Get<uint>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    uint v = FormatterProvider.Get<uint>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<uint, T>(ref v);
                }
            ),
            TypeCode.Int64 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(long));
                    long v = System.Runtime.CompilerServices.Unsafe.As<T, long>(ref value);
                    FormatterProvider.Get<long>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    long v = FormatterProvider.Get<long>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<long, T>(ref v);
                }
            ),
            TypeCode.UInt64 => (
                (ref DataWriter writer, T value) =>
                {
                    writer.Expand(sizeof(ulong));
                    ulong v = System.Runtime.CompilerServices.Unsafe.As<T, ulong>(ref value);
                    FormatterProvider.Get<ulong>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    ulong v = FormatterProvider.Get<ulong>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<ulong, T>(ref v);
                }
            ),
            // Only the primitive enum backing types are supported by the formatter pipeline.
            _ => throw new SerializationFailureException($"Enum underlying type '{s_underlyingTypeCode}' is not supported."),
        };
    }

    #endregion Delegates for Enum Formatter
}
