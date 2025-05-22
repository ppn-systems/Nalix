using Nalix.Common.Attributes;
using Nalix.Extensions.IO;

namespace Nalix.Serialization;

internal static class BinarySerializer<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] T> where T : new()
{
    #region Fields

    private delegate void FieldWriter(in T obj, System.Span<byte> span, ref int offset);

    private delegate void FieldReader(ref T obj, System.ReadOnlySpan<byte> span, ref int offset);

    private readonly struct FieldSerializer(
        FieldWriter writer,
        FieldReader reader,
        int fixedSize)
    {
        public readonly FieldWriter Writer = writer;
        public readonly FieldReader Reader = reader;
        public readonly int FixedSize = fixedSize;
    }

    private static readonly System.Collections.Generic.List<FieldSerializer> _serializers;

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
                    byte value = GetField<byte>(in obj, field);
                    span.WriteByte(in value, offset);
                    offset += sizeof(byte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte value = span.ToByte(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(byte);
                }, sizeof(byte)
            ),
            [typeof(sbyte)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    sbyte value = GetField<sbyte>(in obj, field);
                    span.WriteSByte(in value, offset);
                    offset += sizeof(sbyte);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    sbyte value = span.ToSByte(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(sbyte);
                }, sizeof(sbyte)
            ),
            [typeof(bool)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    bool value = GetField<bool>(in obj, field);
                    span.WriteBool(in value, offset);
                    offset += sizeof(bool);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    bool value = span.ToBool(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(bool);
                }, sizeof(bool)
            ),
            [typeof(char)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    char value = GetField<char>(in obj, field);
                    span.WriteChar(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    char value = span.ToChar(ref offset);
                    SetField(ref obj, field, value);
                }, sizeof(char)
            ),
            [typeof(short)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    short value = GetField<short>(in obj, field);
                    span.WriteInt16(in value, offset);
                    offset += sizeof(short);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    short value = span.ToInt16(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(short);
                }, sizeof(short)
            ),
            [typeof(ushort)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ushort value = GetField<ushort>(in obj, field);
                    span.WriteUInt16(in value, offset);
                    offset += sizeof(ushort);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ushort value = span.ToUInt16(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(ushort);
                }, sizeof(ushort)
            ),
            [typeof(int)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    int value = GetField<int>(in obj, field);
                    span.WriteInt32(in value, offset);
                    offset += sizeof(int);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    int value = span.ToInt32(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(int);
                }, sizeof(int)
            ),
            [typeof(uint)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    uint value = GetField<uint>(in obj, field);
                    span.WriteUInt32(in value, offset);
                    offset += sizeof(uint);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    uint value = span.ToUInt32(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(uint);
                }, sizeof(uint)
            ),
            [typeof(float)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    float value = GetField<float>(in obj, field);
                    span.WriteSingle(in value, offset);
                    offset += sizeof(double);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    float value = span.ToSingle(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(float);
                }, sizeof(float)
            ),
            [typeof(double)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    double value = GetField<double>(in obj, field);
                    span.WriteDouble(in value, offset);
                    offset += sizeof(double);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    double value = span.ToDouble(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(double);
                }, sizeof(double)
            ),
            [typeof(long)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    long value = GetField<long>(in obj, field);
                    span.WriteInt64(in value, offset);
                    offset += sizeof(long);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    long value = span.ToInt64(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(long);
                }, sizeof(long)
            ),
            [typeof(ulong)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ulong value = GetField<ulong>(in obj, field);
                    span.WriteUInt64(in value, offset);
                    offset += sizeof(ulong);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ulong value = span.ToUInt64(offset);
                    SetField(ref obj, field, value);
                    offset += sizeof(ulong);
                }, sizeof(ulong)
            ),
            [typeof(string)] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    string value = GetField<string>(in obj, field);
                    span.WriteString(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    string value = span.ToString(ref offset);
                    SetField(ref obj, field, value);
                }, sizeof(int)
            ),
            [typeof(byte[])] = field => new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte[] value = (byte[])field.GetValue(obj);
                    span.WriteBytes(value, ref offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte[] value = span.ToBytes(ref offset);
                    field.SetValue(obj, value);
                }, sizeof(int)
            ),
            // Add more primitive types as needed...
        };

    #endregion Fields

    #region Constructor

    static BinarySerializer()
    {
        _serializers = [];

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

    #endregion Constructor

    #region API

    public static void Serialize(in T obj, System.Span<byte> span)
    {
        int offset = 0;
        foreach (var serializer in _serializers)
            serializer.Writer(in obj, span, ref offset);
    }

    public static T Deserialize(System.ReadOnlySpan<byte> span)
    {
        T obj = new();
        int offset = 0;
        foreach (var serializer in _serializers)
            serializer.Reader(ref obj, span, ref offset);
        return obj;
    }

    #endregion API

    #region Private Method

    // 2. Use SetValueDirect and GetValueDirect for value types (structs)
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
