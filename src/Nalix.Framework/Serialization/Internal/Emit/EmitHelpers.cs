using System;
using System.Reflection;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal.Types;

internal static partial class EmitHelpers
{
    /// <summary>
    /// Returns the direct Write method from <see cref="DataWriterExtensions"/> if available.
    /// Prioritizes fast-path primitive and unmanaged writes.
    /// </summary>
    public static MethodInfo? TryGetDirectWriteMethod(Type fieldType)
    {
        Type ext = typeof(DataWriterExtensions);

        // === Exact primitive matches ===
        if (fieldType == typeof(byte))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(byte)]);
        }

        if (fieldType == typeof(bool))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(bool)]);
        }

        if (fieldType == typeof(ushort))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ushort)]);
        }

        if (fieldType == typeof(uint))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(uint)]);
        }

        if (fieldType == typeof(ulong))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ulong)]);
        }

        if (fieldType == typeof(int))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(int)]);
        }

        if (fieldType == typeof(long))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(long)]);
        }

        if (fieldType.IsEnum)
        {
            return TryGetDirectWriteMethod(Enum.GetUnderlyingType(fieldType));
        }

        // Note: short is missing in DataWriterExtensions → we fall through to WriteUnmanaged
        // You can add Write(short) later if you want.

        // === Arrays & Spans ===
        if (fieldType == typeof(byte[]))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(byte[])]);
        }

        if (fieldType == typeof(ReadOnlySpan<byte>))
        {
            return ext.GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>)]);
        }

        // === Generic Unmanaged (best fallback for all other primitives like short, float, double, char, enums, etc.) ===
        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            MethodInfo? method = ext.GetMethod("WriteUnmanaged", BindingFlags.Public | BindingFlags.Static);
            return method?.MakeGenericMethod(fieldType);
        }

        return null;
    }

    /// <summary>
    /// Returns the direct Read method from <see cref="DataReaderExtensions"/> if available.
    /// </summary>
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

        if (fieldType == typeof(uint))
        {
            return ext.GetMethod("ReadUInt32", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(ulong))
        {
            return ext.GetMethod("ReadUInt64", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(int))
        {
            return ext.GetMethod("ReadInt32", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType == typeof(long))
        {
            return ext.GetMethod("ReadInt64", [typeof(DataReader).MakeByRefType()]);
        }

        if (fieldType.IsEnum)
        {
            return TryGetDirectReadMethod(Enum.GetUnderlyingType(fieldType));
        }

        // Byte array support
        if (fieldType == typeof(byte[]))
        {
            return ext.GetMethod("ReadBytes", [typeof(DataReader).MakeByRefType(), typeof(int)]); // Note: needs length
        }

        // Generic unmanaged fallback
        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            MethodInfo? method = ext.GetMethod("ReadUnmanaged", BindingFlags.Public | BindingFlags.Static);
            return method?.MakeGenericMethod(fieldType);
        }

        return null;
    }
}
