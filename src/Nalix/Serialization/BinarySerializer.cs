using Nalix.Common.Serialization;
using Nalix.Extensions.IO;
using Nalix.Serialization.Internal;

namespace Nalix.Serialization;

/// <summary>
/// Provides high-performance binary serialization and deserialization for type <typeparamref name="T"/> using <see cref="System.Span{T}"/> and reflection.
/// Fields must be marked with <see cref="FieldOrderAttribute"/> and can be private or public.
/// Supports primitive types, string, and byte arrays.
/// </summary>
/// <typeparam name="T">The type to serialize/deserialize. Must have a parameterless constructor.</typeparam>
public static class BinarySerializer<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] T> where T : new()
{
    #region Fields

    private delegate int OffsetTracker(in T obj);

    private delegate void FieldWriter(in T obj, System.Span<byte> span, ref int offset);

    private delegate void FieldReader(ref T obj, System.ReadOnlySpan<byte> span, ref int offset);

    private readonly struct FieldSerializer(
        FieldWriter writer,
        FieldReader reader,
        OffsetTracker offset)
    {
        public readonly FieldWriter Writer = writer;
        public readonly FieldReader Reader = reader;
        public readonly OffsetTracker Offset = offset;
    }

    private static readonly System.Collections.Generic.Dictionary<
        System.Reflection.FieldInfo, System.Func<T, object>> _getters = [];

    private static readonly System.Collections.Generic.Dictionary<
          System.Reflection.FieldInfo, System.Action<T, object>> _setters = [];

    private static readonly System.Collections.Generic.Dictionary<
          System.Reflection.FieldInfo, System.Action<object, object>> _valueTypeSetters = [];

    private static readonly System.Collections.Generic.List<FieldSerializer> _serializers = [];

    private static readonly System.Reflection.BindingFlags _binding =
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic;

    private static readonly System.Collections.Generic.Dictionary<
        System.Type,
        System.Func<System.Reflection.FieldInfo, FieldSerializer>> _typeSerializers = new()
        {
            [typeof(byte)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte value = (byte)(_getters[field](obj) ?? default(byte));
                    span.WriteByte(in value, offset);
                    offset += sizeof(byte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte value = span.ToByte(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(byte);
                },
                (in T obj) => sizeof(byte)
            ),
            [typeof(sbyte)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    sbyte value = (sbyte)(_getters[field](obj) ?? default(sbyte));
                    span.WriteSByte(in value, offset);
                    offset += sizeof(sbyte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    sbyte value = span.ToSByte(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(sbyte);
                },
                (in T obj) => sizeof(sbyte)
            ),
            [typeof(bool)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    bool value = (bool)(_getters[field](obj) ?? false);
                    span.WriteBool(in value, offset);
                    offset += sizeof(bool);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    bool value = span.ToBool(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(bool);
                },
                (in T obj) => sizeof(bool)
            ),
            [typeof(char)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    char value = (char)(_getters[field](obj) ?? default(char));
                    span.WriteChar(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    char value = span.ToChar(ref offset);
                    SetFieldValue(ref obj, field, value);
                },
                (in T obj) => sizeof(char)
            ),
            [typeof(short)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    short value = (short)(_getters[field](obj) ?? default(short));
                    span.WriteInt16(in value, offset);
                    offset += sizeof(short);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    short value = span.ToInt16(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(short);
                },
                (in T obj) => sizeof(short)
            ),
            [typeof(ushort)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ushort value = (ushort)(_getters[field](obj) ?? default(ushort));
                    span.WriteUInt16(in value, offset);
                    offset += sizeof(ushort);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ushort value = span.ToUInt16(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(ushort);
                },
                (in T obj) => sizeof(ushort)
            ),
            [typeof(int)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    int value = (int)(_getters[field](obj) ?? default(int));
                    span.WriteInt32(in value, offset);
                    offset += sizeof(int);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    int value = span.ToInt32(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(int);
                },
                (in T obj) => sizeof(int)
            ),
            [typeof(uint)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    uint value = (uint)(_getters[field](obj) ?? default(uint));
                    span.WriteUInt32(in value, offset);
                    offset += sizeof(uint);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    uint value = span.ToUInt32(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(uint);
                },
                (in T obj) => sizeof(uint)
            ),
            [typeof(float)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    float value = (float)(_getters[field](obj) ?? default(float));
                    span.WriteSingle(in value, offset);
                    offset += sizeof(float);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    float value = span.ToSingle(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(float);
                },
                (in T obj) => sizeof(float)
            ),
            [typeof(double)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    double value = (double)(_getters[field](obj) ?? default(double));
                    span.WriteDouble(in value, offset);
                    offset += sizeof(double);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    double value = span.ToDouble(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(double);
                },
                (in T obj) => sizeof(double)
            ),
            [typeof(long)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    long value = (long)(_getters[field](obj) ?? default(long));
                    span.WriteInt64(in value, offset);
                    offset += sizeof(long);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    long value = span.ToInt64(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(long);
                },
                (in T obj) => sizeof(long)
            ),
            [typeof(ulong)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ulong value = (ulong)(_getters[field](obj) ?? default(ulong));
                    span.WriteUInt64(in value, offset);
                    offset += sizeof(ulong);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ulong value = span.ToUInt64(offset);
                    SetFieldValue(ref obj, field, value);
                    offset += sizeof(ulong);
                },
                (in T obj) => sizeof(ulong)
            ),
            [typeof(string)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    string value = _getters[field](obj) as string;
                    span.WriteString(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    string value = span.ToString(ref offset);
                    SetFieldValue(ref obj, field, value);
                },
                (in T obj) =>
                {
                    if (_getters[field](obj) is not string value)
                        return sizeof(int); // length prefix only, e.g., -1 for null
                    return sizeof(int) + SerializationOptions.Encoding.GetByteCount(value);
                }
            ),
            [typeof(byte[])] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte[] value = _getters[field](obj) as byte[];
                    span.WriteBytes(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte[] value = span.ToBytes(ref offset);
                    SetFieldValue(ref obj, field, value);
                },
                (in T obj) =>
                {
                    if (_getters[field](obj) is not byte[] value)
                        return sizeof(int);
                    return sizeof(int) + value.Length;
                }
            ),
        };

    /// <summary>
    /// Static constructor that scans all fields of type
    /// <typeparamref name="T"/> with the <see cref="FieldOrderAttribute"/>,
    /// orders them by the attribute's <c>Order</c> property, and builds serializers for supported types.
    /// </summary>
    static BinarySerializer()
    {
        System.Collections.Generic.List<
            (System.Reflection.FieldInfo Field, FieldOrderAttribute Attribute)> fields = [];

        System.Reflection.FieldInfo[] allFields = typeof(T).GetFields(_binding);

        foreach (System.Reflection.FieldInfo field in allFields)
        {
            FieldOrderAttribute attribute = (FieldOrderAttribute)System.Attribute
                .GetCustomAttribute(field, typeof(FieldOrderAttribute));
            if (attribute is not null)
            {
                fields.Add((field, attribute));

                // Kiểm tra null safety cho getter/setter
                var getter = ExpressionField.CreateFieldGetter<T>(field);
                var setter = ExpressionField.CreateFieldSetter<T>(field);

                if (getter is not null)
                {
                    _getters[field] = getter;

                    if (typeof(T).IsValueType)
                    {
                        // Đối với value types, luôn sử dụng special setter
                        _valueTypeSetters[field] = ExpressionField.CreateValueTypeFieldSetterByRef<T>(field);
                    }
                    else
                    {
                        if (setter is not null)
                        {
                            _setters[field] = setter;
                        }
                        else
                        {
                            throw new System.InvalidOperationException(
                                $"Cannot create setter for field {field.Name}");
                        }
                    }
                }
                else
                {
                    throw new System.InvalidOperationException(
                        $"Cannot create getter for field {field.Name}");
                }
            }
        }

        // Kiểm tra nếu không có fields nào được tìm thấy
        if (fields.Count == 0)
        {
            throw new System.InvalidOperationException(
                $"Type {typeof(T).Name} has no fields marked with FieldOrderAttribute");
        }

        fields.Sort((a, b) => a.Attribute.Order.CompareTo(b.Attribute.Order));

        foreach (var (Field, Attribute) in fields)
        {
            if (_typeSerializers.TryGetValue(Field.FieldType, out var creator))
            {
                _serializers.Add(creator(Field));
            }
            else
            {
                throw new System.NotSupportedException(
                    $"Field type {Field.FieldType.Name} is not supported for serialization");
            }
        }
    }

    #endregion Fields

    #region API

    /// <summary>
    /// Calculates the total number of bytes required to serialize an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The object to measure.</param>
    /// <returns>Total byte size required for serialization.</returns>
    public static int GetSize(in T obj)
    {
        int size = 0;
        foreach (FieldSerializer serializer in _serializers)
            size += serializer.Offset(in obj);
        return size;
    }

    /// <summary>
    /// Serializes the object into the provided <see cref="System.Span{Byte}"/>.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="span">The destination span to write bytes into.</param>
    /// <exception cref="System.ArgumentException">Thrown when span is too small for serialization.</exception>
    public static void Serialize(in T obj, System.Span<byte> span)
    {
        int requiredSize = GetSize(in obj);
        if (span.Length < requiredSize)
        {
            throw new System.ArgumentException(
                $"Span size ({span.Length}) is insufficient. Required: {requiredSize} bytes");
        }

        int offset = 0;
        foreach (FieldSerializer serializer in _serializers)
        {
            serializer.Writer(in obj, span, ref offset);
        }
    }

    /// <summary>
    /// Deserializes an object of type <typeparamref name="T"/> from the provided <see cref="System.ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="span">The source span to read bytes from.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="System.ArgumentException">Thrown when span is too small for deserialization.</exception>
    public static T Deserialize(System.ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
        {
            throw new System.ArgumentException("Cannot deserialize from empty span");
        }

        T obj = new();
        int offset = 0;

        try
        {
            foreach (FieldSerializer serializer in _serializers)
            {
                if (offset >= span.Length)
                {
                    throw new System.ArgumentException($"Span ended unexpectedly at offset {offset}");
                }
                serializer.Reader(ref obj, span, ref offset);
            }
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException($"Deserialization failed at offset {offset}", ex);
        }

        return obj;
    }

    /// <summary>
    /// Serializes the object into the provided <see cref="System.Memory{Byte}"/>.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="memory">The destination memory buffer to write bytes into.</param>
    public static void Serialize(in T obj, System.Memory<byte> memory) => Serialize(obj, memory.Span);

    /// <summary>
    /// Deserializes an object of type <typeparamref name="T"/> from the provided <see cref="System.ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <param name="memory">The source memory buffer to read bytes from.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    public static T Deserialize(System.ReadOnlyMemory<byte> memory) => Deserialize(memory.Span);

    /// <summary>
    /// Serializes the object to a new byte array.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A byte array containing the serialized data.</returns>
    public static byte[] SerializeToArray(in T obj)
    {
        int size = GetSize(in obj);
        byte[] buffer = new byte[size];
        Serialize(in obj, System.MemoryExtensions.AsSpan(buffer));
        return buffer;
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing serialized data.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    public static T DeserializeFromArray(byte[] data)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        return Deserialize(System.MemoryExtensions.AsSpan(data));
    }

    #endregion API

    #region Private Method

    /// <summary>
    /// Helper method để set field value, handle cả value types và reference types
    /// </summary>
    /// <param name="obj">Object instance</param>
    /// <param name="field">Field info</param>
    /// <param name="value">Value to set</param>
    private static void SetFieldValue(ref T obj, System.Reflection.FieldInfo field, object value)
    {
        if (typeof(T).IsValueType)
        {
            object boxed = obj;
            _valueTypeSetters[field](boxed, value);
            obj = (T)boxed;
        }
        else
        {
            _setters[field](obj, value);
        }
    }

    #endregion Private Method
}
