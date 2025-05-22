using Nalix.Common.Attributes;
using Nalix.Extensions.IO;
using Nalix.Serialization.Internal;

namespace Nalix.Serialization;

/// <summary>
/// Provides high-performance binary serialization and deserialization for type <typeparamref name="T"/> using <see cref="System.Span{T}"/> and reflection.
/// Fields must be marked with <see cref="SerializableFieldAttribute"/> and can be private or public.
/// Supports primitive types, string, and byte arrays.
/// </summary>
/// <typeparam name="T">The type to serialize/deserialize. Must have a parameterless constructor.</typeparam>

public static class BinarySerializer<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] T> where T : new()
{
    #region Fields

    private delegate int SizeCalculator(in T obj);

    private delegate void FieldWriter(in T obj, System.Span<byte> span, ref int offset);

    private delegate void FieldReader(ref T obj, System.ReadOnlySpan<byte> span, ref int offset);

    private readonly struct FieldSerializer(
        FieldWriter writer,
        FieldReader reader,
        SizeCalculator getSize)
    {
        public readonly FieldWriter Writer = writer;
        public readonly FieldReader Reader = reader;
        public readonly SizeCalculator GetSize = getSize;
    }

    private static readonly System.Collections.Generic.Dictionary<
        System.Reflection.FieldInfo, System.Func<T, object>> _getters = new();

    private static readonly System.Collections.Generic.Dictionary<
          System.Reflection.FieldInfo, System.Action<T, object>> _setters = new();

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
                    byte value = (byte)_getters[field](obj);
                    span.WriteByte(in value, offset);
                    offset += sizeof(byte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte value = span.ToByte(offset);
                    _setters[field](obj, value);
                    offset += sizeof(byte);
                },
                (in T obj) => sizeof(byte)
            ),
            [typeof(sbyte)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    sbyte value = (sbyte)_getters[field](obj);
                    span.WriteSByte(in value, offset);
                    offset += sizeof(sbyte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    sbyte value = span.ToSByte(offset);
                    _setters[field](obj, value);
                    offset += sizeof(sbyte);
                },
                (in T obj) => sizeof(sbyte)
            ),
            [typeof(bool)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    bool value = (bool)_getters[field](obj);
                    span.WriteBool(in value, offset);
                    offset += sizeof(bool);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    bool value = span.ToBool(offset);
                    _setters[field](obj, value);
                    offset += sizeof(bool);
                },
                (in T obj) => sizeof(bool)
            ),
            [typeof(char)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    char value = (char)_getters[field](obj);
                    span.WriteChar(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    char value = span.ToChar(ref offset);
                    _setters[field](obj, value);
                },
                (in T obj) => sizeof(char)
            ),
            [typeof(short)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    short value = (short)_getters[field](obj);
                    span.WriteInt16(in value, offset);
                    offset += sizeof(short);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    short value = span.ToInt16(offset);
                    _setters[field](obj, value);
                    offset += sizeof(short);
                },
                (in T obj) => sizeof(short)
            ),
            [typeof(ushort)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ushort value = (ushort)_getters[field](obj);
                    span.WriteUInt16(in value, offset);
                    offset += sizeof(ushort);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ushort value = span.ToUInt16(offset);
                    _setters[field](obj, value);
                    offset += sizeof(ushort);
                },
                (in T obj) => sizeof(ushort)
            ),
            [typeof(int)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    int value = (int)_getters[field](obj);
                    span.WriteInt32(in value, offset);
                    offset += sizeof(int);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    int value = span.ToInt32(offset);
                    _setters[field](obj, value);
                    offset += sizeof(int);
                },
                (in T obj) => sizeof(int)
            ),
            [typeof(uint)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    uint value = (uint)_getters[field](obj);
                    span.WriteUInt32(in value, offset);
                    offset += sizeof(uint);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    uint value = span.ToUInt32(offset);
                    _setters[field](obj, value);
                    offset += sizeof(uint);
                },
                (in T obj) => sizeof(uint)
            ),
            [typeof(float)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    float value = (float)_getters[field](obj);
                    span.WriteSingle(in value, offset);
                    offset += sizeof(double);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    float value = span.ToSingle(offset);
                    _setters[field](obj, value);
                    offset += sizeof(float);
                },
                (in T obj) => sizeof(float)
            ),
            [typeof(double)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    double value = (double)_getters[field](obj);
                    span.WriteDouble(in value, offset);
                    offset += sizeof(double);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    double value = span.ToDouble(offset);
                    _setters[field](obj, value);
                    offset += sizeof(double);
                },
                (in T obj) => sizeof(double)
            ),
            [typeof(long)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    long value = (long)_getters[field](obj);
                    span.WriteInt64(in value, offset);
                    offset += sizeof(long);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    long value = span.ToInt64(offset);
                    _setters[field](obj, value);
                    offset += sizeof(long);
                },
                (in T obj) => sizeof(long)
            ),
            [typeof(ulong)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ulong value = (ulong)_getters[field](obj);
                    span.WriteUInt64(in value, offset);
                    offset += sizeof(ulong);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ulong value = span.ToUInt64(offset);
                    _setters[field](obj, value);
                    offset += sizeof(ulong);
                },
                (in T obj) => sizeof(ulong)
            ),
            [typeof(string)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    string value = (string)_getters[field](obj);
                    span.WriteString(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    string value = span.ToString(ref offset);
                    _setters[field](obj, value);
                },
                (in T obj) =>
                {
                    string value = (string)_getters[field](obj);
                    if (value == null)
                        return sizeof(int); // length prefix only, e.g., -1 for null
                    return sizeof(int) + SerializationOptions.Encoding.GetByteCount(value);
                }
            ),
            [typeof(byte[])] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte[] value = (byte[])_getters[field](obj);
                    span.WriteBytes(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte[] value = span.ToBytes(ref offset);
                    _setters[field](obj, value);
                },
                (in T obj) =>
                {
                    byte[] value = (byte[])_getters[field](obj);
                    if (value == null)
                        return sizeof(int);
                    return sizeof(int) + value.Length;
                }
            ),
            // Add more primitive types as needed...
        };

    /// <summary>
    /// Static constructor that scans all fields of type
    /// <typeparamref name="T"/> with the <see cref="SerializableFieldAttribute"/>,
    /// orders them by the attribute's <c>Order</c> property, and builds serializers for supported types.
    /// </summary>
    static BinarySerializer()
    {
        System.Collections.Generic.List<
            (System.Reflection.FieldInfo Field, SerializableFieldAttribute Attribute)> fields = [];

        System.Reflection.FieldInfo[] allFields = typeof(T).GetFields(_binding);

        foreach (System.Reflection.FieldInfo field in allFields)
        {
            SerializableFieldAttribute attribute = (SerializableFieldAttribute)System.Attribute
                .GetCustomAttribute(field, typeof(SerializableFieldAttribute));
            if (attribute != null)
            {
                fields.Add((field, attribute));
                _getters[field] = ExpressionField.CreateFieldGetter<T>(field);
                _setters[field] = ExpressionField.CreateFieldSetter<T>(field);
            }
        }

        fields.Sort((a, b) => a.Attribute.Order.CompareTo(b.Attribute.Order));

        foreach (var (Field, Attribute) in fields)
        {
            if (_typeSerializers.TryGetValue(Field.FieldType, out var creator))
            {
                _serializers.Add(creator(Field));
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
            size += serializer.GetSize(in obj);
        return size;
    }

    /// <summary>
    /// Serializes the object into the provided <see cref="System.Span{Byte}"/>.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="span">The destination span to write bytes into.</param>
    public static void Serialize(in T obj, System.Span<byte> span)
    {
        int offset = 0;
        foreach (FieldSerializer serializer in _serializers)
            serializer.Writer(in obj, span, ref offset);
    }

    /// <summary>
    /// Deserializes an object of type <typeparamref name="T"/> from the provided <see cref="System.ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="span">The source span to read bytes from.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    public static T Deserialize(System.ReadOnlySpan<byte> span)
    {
        T obj = new();
        int offset = 0;
        foreach (FieldSerializer serializer in _serializers)
            serializer.Reader(ref obj, span, ref offset);
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

    #endregion API

    #region Private Method

    /// <summary>
    /// Gets the value of a field, using <see cref="System.Reflection.FieldInfo.GetValueDirect"/> for structs
    /// and <see cref="System.Reflection.FieldInfo.GetValue"/> for classes.
    /// </summary>
    /// <typeparam name="TValue">The field type.</typeparam>
    /// <param name="obj">The object instance to read from.</param>
    /// <param name="field">The reflection metadata for the field.</param>
    /// <returns>The field value.</returns>
    private static TValue GetField<TValue>(in T obj, System.Reflection.FieldInfo field)
    {
        if (typeof(T).IsValueType)
        {
            // For value types, use TypedReference (requires unsafe)
            object boxed = (object)obj;
            var tr = __makeref(boxed);
            return (TValue)field.GetValueDirect(tr);
        }
        else
        {
            return (TValue)field.GetValue(obj);
        }
    }

    /// <summary>
    /// Sets the value of a field, using <see cref="System.Reflection.FieldInfo.SetValueDirect"/> for structs
    /// and <see cref="System.Reflection.FieldInfo.SetValue(object?, object?)"/> for classes.
    /// </summary>
    /// <typeparam name="TValue">The field type.</typeparam>
    /// <param name="obj">The object instance to modify.</param>
    /// <param name="field">The reflection metadata for the field.</param>
    /// <param name="value">The new value to assign.</param>
    private static void SetField<TValue>(ref T obj, System.Reflection.FieldInfo field, TValue value)
    {
        if (typeof(T).IsValueType)
        {
            // For value types, use TypedReference (requires unsafe)
            object boxed = obj!;
            var tr = __makeref(boxed);
            field.SetValueDirect(tr, value);
            obj = (T)boxed!;
        }
        else
        {
            field.SetValue(obj, value);
        }
    }

    #endregion Private Method
}
