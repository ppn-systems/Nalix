using Nalix.Serialization.Buffers;

namespace Nalix.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization for enum types without boxing,
/// directly serializing the underlying type.
/// </summary>
/// <typeparam name="T">The enum type to be serialized and deserialized.</typeparam>
public sealed class EnumFormatter<T> : IFormatter<T> where T : struct, System.Enum
{
    private static readonly System.TypeCode UnderlyingTypeCode;

    static EnumFormatter()
    {
        UnderlyingTypeCode = System.Type
            .GetTypeCode(System.Enum
            .GetUnderlyingType(typeof(T)));
    }

    /// <summary>
    /// Serializes an enum value into the provided writer using its underlying type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The enum value to serialize.</param>
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    public void Serialize(ref DataWriter writer, T value)
    {
        switch (UnderlyingTypeCode)
        {
            case System.TypeCode.Byte:
                FormatterProvider
                    .Get<System.Byte>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.Byte>(ref value));

                break;

            case System.TypeCode.SByte:
                FormatterProvider
                    .Get<System.SByte>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.SByte>(ref value));

                break;

            case System.TypeCode.Int16:
                FormatterProvider
                    .Get<System.Int16>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.Int16>(ref value));

                break;

            case System.TypeCode.UInt16:
                FormatterProvider
                    .Get<System.UInt16>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.UInt16>(ref value));

                break;

            case System.TypeCode.Int32:
                FormatterProvider
                    .Get<System.Int32>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.Int32>(ref value));

                break;

            case System.TypeCode.UInt32:
                FormatterProvider
                    .Get<System.UInt32>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.UInt32>(ref value));

                break;

            case System.TypeCode.Int64:
                FormatterProvider
                    .Get<System.Int64>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.Int64>(ref value));

                break;

            case System.TypeCode.UInt64:
                FormatterProvider
                    .Get<System.UInt64>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, System.UInt64>(ref value));

                break;

            default:
                throw new System.NotSupportedException("The underlying type of the enum is not supported.");
        }
    }

    /// <summary>
    /// Deserializes an enum value from the provided reader using its underlying type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized enum value.</returns>
    /// <exception cref="System.NotSupportedException">
    /// Thrown if the underlying type of the enum is not supported.
    /// </exception>
    public T Deserialize(ref DataReader reader)
    {
        switch (UnderlyingTypeCode)
        {
            case System.TypeCode.Byte:
                System.Byte byteValue = FormatterProvider
                    .Get<System.Byte>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.Byte, T>(ref byteValue);

            case System.TypeCode.SByte:
                System.SByte sbyteValue = FormatterProvider
                    .Get<System.SByte>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.SByte, T>(ref sbyteValue);

            case System.TypeCode.Int16:
                System.Int16 shortValue = FormatterProvider
                    .Get<System.Int16>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.Int16, T>(ref shortValue);

            case System.TypeCode.UInt16:
                System.UInt16 ushortValue = FormatterProvider
                    .Get<System.UInt16>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, T>(ref ushortValue);

            case System.TypeCode.Int32:
                System.Int32 intValue = FormatterProvider
                    .Get<System.Int32>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.Int32, T>(ref intValue);

            case System.TypeCode.UInt32:
                System.UInt32 uintValue = FormatterProvider
                    .Get<System.UInt32>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, T>(ref uintValue);

            case System.TypeCode.Int64:
                System.Int64 longValue = FormatterProvider
                    .Get<System.Int64>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.Int64, T>(ref longValue);

            case System.TypeCode.UInt64:
                System.UInt64 ulongValue = FormatterProvider
                    .Get<System.UInt64>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<System.UInt64, T>(ref ulongValue);

            default:
                throw new System.NotSupportedException("The underlying enum type is not supported.");
        }
    }
}
