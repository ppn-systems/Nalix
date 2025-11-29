// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization for enum types without boxing,
/// directly serializing the underlying type.
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
    private static readonly SerializeDelegate _serialize;
    private static readonly DeserializeDelegate _deserialize;

    private static readonly System.TypeCode UnderlyingTypeCode;
    private static System.String DebuggerDisplay => $"EnumFormatter<{typeof(T).FullName}>";

    static EnumFormatter()
    {
        if (!typeof(T).IsEnum)
        {
            throw new System.InvalidOperationException($"TYPE {typeof(T)} is not an enum.");
        }

        UnderlyingTypeCode = System.Type.GetTypeCode(System.Enum
                                        .GetUnderlyingType(typeof(T)));

        (_serialize, _deserialize) = CreateEnumFormatterDelegates();
    }

    /// <summary>
    /// Serializes an enum value into the provided writer using its underlying type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The enum value to serialize.</param>
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
        => _serialize(ref writer, value);

    /// <summary>
    /// Deserializes an enum value from the provided reader using its underlying type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized enum value.</returns>
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader)
        => _deserialize(ref reader);

    #region Delegates for Enum Formatter

    private delegate T DeserializeDelegate(ref DataReader reader);
    private delegate void SerializeDelegate(ref DataWriter writer, T value);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0350:Use implicitly typed lambda", Justification = "<Pending>")]
    private static (SerializeDelegate serialize, DeserializeDelegate deserialize) CreateEnumFormatterDelegates()
    {
        return UnderlyingTypeCode switch
        {
            System.TypeCode.Byte => (
                (ref DataWriter writer, T value) =>
                {
                    System.Byte b = System.Runtime.CompilerServices.Unsafe.As<T, System.Byte>(ref value);
                    FormatterProvider.Get<System.Byte>().Serialize(ref writer, b);
                },
                (ref DataReader reader) =>
                {
                    System.Byte b = FormatterProvider.Get<System.Byte>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.Byte, T>(ref b);
                }
            ),
            System.TypeCode.SByte => (
                (ref DataWriter writer, T value) =>
                {
                    System.SByte b = System.Runtime.CompilerServices.Unsafe.As<T, System.SByte>(ref value);
                    FormatterProvider.Get<System.SByte>().Serialize(ref writer, b);
                },
                (ref DataReader reader) =>
                {
                    System.SByte b = FormatterProvider.Get<System.SByte>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.SByte, T>(ref b);
                }
            ),
            System.TypeCode.Int16 => (
                (ref DataWriter writer, T value) =>
                {
                    System.Int16 v = System.Runtime.CompilerServices.Unsafe.As<T, System.Int16>(ref value);
                    FormatterProvider.Get<System.Int16>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.Int16 v = FormatterProvider.Get<System.Int16>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.Int16, T>(ref v);
                }
            ),
            System.TypeCode.UInt16 => (
                (ref DataWriter writer, T value) =>
                {
                    System.UInt16 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt16>(ref value);
                    FormatterProvider.Get<System.UInt16>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.UInt16 v = FormatterProvider.Get<System.UInt16>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, T>(ref v);
                }
            ),
            System.TypeCode.Int32 => (
                (ref DataWriter writer, T value) =>
                {
                    System.Int32 v = System.Runtime.CompilerServices.Unsafe.As<T, System.Int32>(ref value);
                    FormatterProvider.Get<System.Int32>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.Int32 v = FormatterProvider.Get<System.Int32>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.Int32, T>(ref v);
                }
            ),
            System.TypeCode.UInt32 => (
                (ref DataWriter writer, T value) =>
                {
                    System.UInt32 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt32>(ref value);
                    FormatterProvider.Get<System.UInt32>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.UInt32 v = FormatterProvider.Get<System.UInt32>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, T>(ref v);
                }
            ),
            System.TypeCode.Int64 => (
                (ref DataWriter writer, T value) =>
                {
                    System.Int64 v = System.Runtime.CompilerServices.Unsafe.As<T, System.Int64>(ref value);
                    FormatterProvider.Get<System.Int64>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.Int64 v = FormatterProvider.Get<System.Int64>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.Int64, T>(ref v);
                }
            ),
            System.TypeCode.UInt64 => (
                (ref DataWriter writer, T value) =>
                {
                    System.UInt64 v = System.Runtime.CompilerServices.Unsafe.As<T, System.UInt64>(ref value);
                    FormatterProvider.Get<System.UInt64>().Serialize(ref writer, v);
                },
                (ref DataReader reader) =>
                {
                    System.UInt64 v = FormatterProvider.Get<System.UInt64>().Deserialize(ref reader);
                    return System.Runtime.CompilerServices.Unsafe.As<System.UInt64, T>(ref v);
                }
            ),
            _ => throw new System.NotSupportedException($"Enum underlying type '{UnderlyingTypeCode}' is not supported."),
        };
    }

    #endregion Delegates for Enum Formatter
}
