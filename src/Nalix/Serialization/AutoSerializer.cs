using Nalix.Common.Attributes;
using Nalix.Extensions.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Nalix.Serialization;

internal static class AutoSerializer<T> where T : new()
{
    private static readonly List<Action<T, Span<byte>, int>> _writers;
    private static readonly List<Action<T, ReadOnlySpan<byte>, int>> _readers;
    private static readonly int _fixedSize;

    static AutoSerializer()
    {
        _writers = [];
        _readers = [];
        _fixedSize = 0;

        var fields = typeof(T)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(f => new
            {
                Field = f,
                Attribute = f.GetCustomAttribute<SerializableFieldAttribute>()
            })
            .Where(f => f.Attribute != null)
            .OrderBy(f => f.Attribute!.Order)
            .ToList();

        foreach (var entry in fields)
        {
            var field = entry.Field;
            var fieldType = field.FieldType;

            if (fieldType == typeof(int))
            {
                // WRITE INT
                _writers.Add((obj, span, offset) =>
                {
                    var val = (int)field.GetValue(obj)!;
                    span.WriteInt32(in val, offset);
                    offset += 4;
                });

                // READ INT
                _readers.Add((obj, span, offset) =>
                {
                    int val = MemoryMarshal.Read<int>(span.Slice(offset, 4));
                    field.SetValue(obj, val);
                    offset += 4;
                });

                _fixedSize += 4;
            }
            else if (fieldType == typeof(short))
            {
                _writers.Add((obj, span, offset) =>
                {
                    var val = (short)field.GetValue(obj)!;
                    span.WriteInt16(in val, offset);
                    offset += 2;
                });

                _readers.Add((obj, span, offset) =>
                {
                    short val = MemoryMarshal.Read<short>(span.Slice(offset, 2));
                    field.SetValue(obj, val);
                    offset += 2;
                });

                _fixedSize += 2;
            }
            else if (fieldType == typeof(byte))
            {
                _writers.Add((obj, span, offset) =>
                {
                    var val = (byte)field.GetValue(obj)!;
                    span.WriteByte(in val, offset);
                    offset += 1;
                });

                _readers.Add((obj, span, offset) =>
                {
                    byte val = span[offset];
                    field.SetValue(obj, val);
                    offset += 1;
                });

                _fixedSize += 1;
            }

            // Add more types here: float, string, bool, enum, etc.
        }
    }

    public static int GetSize() => _fixedSize;

    public static void Serialize(T obj, Span<byte> span)
    {
        int offset = 0;
        foreach (var write in _writers)
            write(obj, span, offset);
    }

    public static T Deserialize(ReadOnlySpan<byte> span)
    {
        var obj = new T();
        int offset = 0;
        foreach (var read in _readers)
            read(obj, span, offset);
        return obj;
    }
}
