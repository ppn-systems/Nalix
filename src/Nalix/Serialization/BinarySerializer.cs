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
    private static readonly System.Reflection.BindingFlags _binding;

    #endregion Fields

    #region Constructor

    static BinarySerializer()
    {
        _serializers = [];
        _binding = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

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
            _serializers.Add(CreateFieldSerializer(Field.FieldType, Field));
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

    private static FieldSerializer CreateFieldSerializer(
        System.Type type,
        System.Reflection.FieldInfo field)
    {
        if (type == typeof(byte))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte value = (byte)field.GetValue(obj);
                    span.WriteByte(in value, offset);
                    offset += 1;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte value = span.ToByte(offset);
                    field.SetValue(obj, value);
                    offset += 1;
                },
                1);
        }
        else if (type == typeof(sbyte))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    sbyte value = (sbyte)field.GetValue(obj);
                    span.WriteSByte(in value, offset);
                    offset += 1;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    sbyte value = span.ToSByte(offset);
                    field.SetValue(obj, value);
                    offset += 1;
                },
                1);
        }
        else if (type == typeof(bool))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    bool value = (bool)field.GetValue(obj);
                    span.WriteBool(in value, offset);
                    offset += 1;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    bool value = span.ToBool(offset);
                    field.SetValue(obj, value);
                    offset += 1;
                },
                1);
        }
        else if (type == typeof(short))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    short value = (short)field.GetValue(obj);
                    span.WriteInt16(in value, offset);
                    offset += 2;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    short value = span.ToInt16(offset);
                    field.SetValue(obj, value);
                    offset += 2;
                },
                2);
        }
        else if (type == typeof(ushort))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    ushort value = (ushort)field.GetValue(obj);
                    span.WriteUInt16(in value, offset);
                    offset += 2;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    ushort value = span.ToUInt16(offset);
                    field.SetValue(obj, value);
                    offset += 2;
                },
                2);
        }
        else if (type == typeof(int))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    int value = (int)field.GetValue(obj);
                    span.WriteInt32(in value, offset);
                    offset += 4;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    int value = span.ToInt32(offset);
                    field.SetValue(obj, value);
                    offset += 4;
                },
                4);
        }
        else if (type == typeof(uint))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    uint value = (uint)field.GetValue(obj);
                    span.WriteUInt32(in value, offset);
                    offset += 4;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    uint value = span.ToUInt32(offset);
                    field.SetValue(obj, value);
                    offset += 4;
                },
                4);
        }
        else if (type == typeof(float))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    float value = (float)field.GetValue(obj);
                    span.WriteSingle(in value, offset);
                    offset += 4;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    float value = span.ToSingle(offset);
                    field.SetValue(obj, value);
                    offset += 4;
                },
                4);
        }
        else if (type == typeof(double))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    double value = (double)field.GetValue(obj);
                    span.WriteDouble(in value, offset);
                    offset += 8;
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    double value = span.ToDouble(offset);
                    field.SetValue(obj, value);
                    offset += 8;
                },
                8);
        }
        else if (type.IsEnum)
        {
            var underlyingType = System.Enum.GetUnderlyingType(type);
            if (underlyingType == typeof(int))
            {
                return new FieldSerializer(
                    (in T obj, System.Span<byte> span, ref int offset) =>
                    {
                        int value = (int)field.GetValue(obj)!;
                        span.WriteInt32(in value, offset);
                        offset += 4;
                    },
                    (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                    {
                        int value = span.ToInt32(offset);
                        field.SetValue(obj, System.Enum.ToObject(type, value));
                        offset += 4;
                    },
                    4);
            }
            else if (underlyingType == typeof(short))
            {
                return new FieldSerializer(
                    (in T obj, System.Span<byte> span, ref int offset) =>
                    {
                        short value = (short)field.GetValue(obj)!;
                        span.WriteInt16(in value, offset);
                        offset += 2;
                    },
                    (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                    {
                        short value = span.ToInt16(offset);
                        field.SetValue(obj, System.Enum.ToObject(type, value));
                        offset += 2;
                    },
                    2);
            }
            else if (underlyingType == typeof(byte))
            {
                return new FieldSerializer(
                    (in T obj, System.Span<byte> span, ref int offset) =>
                    {
                        byte value = (byte)field.GetValue(obj)!;
                        span.WriteByte(in value, offset);
                        offset += 1;
                    },
                    (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                    {
                        byte value = span.ToByte(offset);
                        field.SetValue(obj, System.Enum.ToObject(type, value));
                        offset += 1;
                    },
                    1);
            }
        }
        else if (type == typeof(string))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    string value = (string)field.GetValue(obj);
                    offset += span.WriteString(value, offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    string value = span.ReadString(ref offset);
                    field.SetValue(obj, value);
                },
                0);
        }
        else if (type.IsArray && type.GetElementType() == typeof(byte))
        {
            return new FieldSerializer(
                (in T obj, System.Span<byte> span, ref int offset) =>
                {
                    byte[] value = (byte[])field.GetValue(obj);
                    offset += span.WriteBytes(value, offset);
                },
                (ref T obj, System.ReadOnlySpan<byte> span, ref int offset) =>
                {
                    byte[] value = span.ReadBytes(ref offset);
                    field.SetValue(obj, value);
                },
                4);
        }

        throw new System.NotSupportedException($"Type {type.Name} is not supported for serialization.");
    }

    #endregion Private Method
}
