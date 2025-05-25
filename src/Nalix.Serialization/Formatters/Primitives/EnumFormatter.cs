using Nalix.Serialization.Buffers;

namespace Nalix.Serialization.Formatters.Primitives;

/// <summary>
/// Provides serialization and deserialization for enum types without boxing,
/// directly serializing the underlying type.
/// </summary>
/// <typeparam name="T">The enum type to be serialized and deserialized.</typeparam>
public sealed class EnumFormatter<T> : IFormatter<T>
{
    private static readonly System.TypeCode UnderlyingTypeCode;

    static EnumFormatter()
    {
        UnderlyingTypeCode = System.Type.GetTypeCode(System.Enum.GetUnderlyingType(typeof(T)));
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
                    .Get<byte>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, byte>(ref value));

                break;

            case System.TypeCode.SByte:
                FormatterProvider
                    .Get<sbyte>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, sbyte>(ref value));

                break;

            case System.TypeCode.Int16:
                FormatterProvider
                    .Get<short>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, short>(ref value));

                break;

            case System.TypeCode.UInt16:
                FormatterProvider
                    .Get<ushort>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, ushort>(ref value));

                break;

            case System.TypeCode.Int32:
                FormatterProvider
                    .Get<int>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, int>(ref value));

                break;

            case System.TypeCode.UInt32:
                FormatterProvider
                    .Get<uint>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, uint>(ref value));

                break;

            case System.TypeCode.Int64:
                FormatterProvider
                    .Get<long>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, long>(ref value));

                break;

            case System.TypeCode.UInt64:
                FormatterProvider
                    .Get<ulong>()
                    .Serialize(ref writer, System.Runtime.CompilerServices.Unsafe
                    .As<T, ulong>(ref value));

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
                byte byteValue = FormatterProvider
                    .Get<byte>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<byte, T>(ref byteValue);

            case System.TypeCode.SByte:
                sbyte sbyteValue = FormatterProvider
                    .Get<sbyte>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<sbyte, T>(ref sbyteValue);

            case System.TypeCode.Int16:
                short shortValue = FormatterProvider
                    .Get<short>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<short, T>(ref shortValue);

            case System.TypeCode.UInt16:
                ushort ushortValue = FormatterProvider
                    .Get<ushort>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<ushort, T>(ref ushortValue);

            case System.TypeCode.Int32:
                int intValue = FormatterProvider
                    .Get<int>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<int, T>(ref intValue);

            case System.TypeCode.UInt32:
                uint uintValue = FormatterProvider
                    .Get<uint>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<uint, T>(ref uintValue);

            case System.TypeCode.Int64:
                long longValue = FormatterProvider
                    .Get<long>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<long, T>(ref longValue);

            case System.TypeCode.UInt64:
                ulong ulongValue = FormatterProvider
                    .Get<ulong>()
                    .Deserialize(ref reader);

                return System.Runtime.CompilerServices.Unsafe.As<ulong, T>(ref ulongValue);

            default:
                throw new System.NotSupportedException("The underlying enum type is not supported.");
        }
    }
}
