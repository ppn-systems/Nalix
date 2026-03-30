using System;
using System.Reflection;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal.Types;

/// <summary>
/// Shared utilities for both Struct and Object serializers.
/// </summary>
internal static class EmitHelpers
{
    public static FieldInfo[] GetSerializableFields(Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Array.Sort(fields, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        return fields;
    }

    public static MethodInfo? TryGetDirectWriteMethod(Type fieldType)
    {
        Type ext = typeof(DataWriterExtensions);

        if (fieldType == typeof(byte))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(byte)]);
        }

        if (fieldType == typeof(bool))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(bool)]);
        }

        if (fieldType == typeof(short))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(short)]);
        }

        if (fieldType == typeof(ushort))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ushort)]);
        }

        if (fieldType == typeof(int))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(int)]);
        }

        if (fieldType == typeof(uint))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(uint)]);
        }

        if (fieldType == typeof(long))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(long)]);
        }

        if (fieldType == typeof(ulong))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ulong)]);
        }

        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            MethodInfo? method = ext.GetMethod("WriteUnmanaged", BindingFlags.Public | BindingFlags.Static);
            return method?.MakeGenericMethod(fieldType);
        }

        return null;
    }

    public static MethodInfo? TryGetDirectReadMethod(Type fieldType)
    {
        Type ext = typeof(DataReaderExtensions);

        if (fieldType == typeof(byte))
        {
            return ext.GetMethod("ReadByte", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(bool))
        {
            return ext.GetMethod("ReadBoolean", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(ushort))
        {
            return ext.GetMethod("ReadUInt16", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(int))
        {
            return ext.GetMethod("ReadInt32", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(uint))
        {
            return ext.GetMethod("ReadUInt32", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(long))
        {
            return ext.GetMethod("ReadInt64", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(ulong))
        {
            return ext.GetMethod("ReadUInt64", [typeof(DataReader).MakeByRefType()]);
        }

        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            MethodInfo? method = ext.GetMethod("ReadUnmanaged", BindingFlags.Public | BindingFlags.Static);
            return method?.MakeGenericMethod(fieldType);
        }

        return null;
    }
}
