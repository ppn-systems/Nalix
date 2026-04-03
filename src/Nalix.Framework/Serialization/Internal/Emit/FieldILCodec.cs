using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization;
using Nalix.Framework.Serialization.Formatters.Cache;
using Nalix.Framework.Serialization.Internal.Reflection;
using Nalix.Framework.Serialization.Internal.Types;

internal static class FieldILCodec
{
    #region Nested Types

    private readonly record struct FormatterEmitMethods(
        FieldInfo InstanceField,
        MethodInfo SerializeMethod,
        MethodInfo DeserializeMethod);

    #endregion Nested Types

    #region Static Fields

    private static readonly ConcurrentDictionary<Type, MethodInfo> s_genericReadMethods = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_genericWriteMethods = new();
    private static readonly ConcurrentDictionary<Type, FormatterEmitMethods> s_formatterEmitMethods = new();

    private static readonly MethodInfo s_writeByteMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(byte)])!;
    private static readonly MethodInfo s_writeBoolMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(bool)])!;
    private static readonly MethodInfo s_writeUInt16Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ushort)])!;
    private static readonly MethodInfo s_writeUInt32Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(uint)])!;
    private static readonly MethodInfo s_writeUInt64Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ulong)])!;
    private static readonly MethodInfo s_writeInt32Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(int)])!;
    private static readonly MethodInfo s_writeInt64Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(long)])!;
    private static readonly MethodInfo s_writeSByteMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(sbyte)])!;
    private static readonly MethodInfo s_writeInt16Method = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(short)])!;
    private static readonly MethodInfo s_writeCharMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(char)])!;
    private static readonly MethodInfo s_writeSingleMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(float)])!;
    private static readonly MethodInfo s_writeDoubleMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(double)])!;
    private static readonly MethodInfo s_writeReadOnlySpanByteMethod = typeof(DataWriterExtensions).GetMethod("Write", [typeof(DataWriter).MakeByRefType(), typeof(ReadOnlySpan<byte>)])!;
    private static readonly MethodInfo s_writeUnmanagedMethod = typeof(DataWriterExtensions).GetMethod("WriteUnmanaged", BindingFlags.Public | BindingFlags.Static)!;

    // Note: DataReaderExtensions doesn't have Read(short), float, double, char, etc. -> we fall back to ReadUnmanaged for those.
    private static readonly MethodInfo s_readByteMethod = typeof(DataReaderExtensions).GetMethod("ReadByte", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readBooleanMethod = typeof(DataReaderExtensions).GetMethod("ReadBoolean", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readUInt16Method = typeof(DataReaderExtensions).GetMethod("ReadUInt16", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readUInt32Method = typeof(DataReaderExtensions).GetMethod("ReadUInt32", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readUInt64Method = typeof(DataReaderExtensions).GetMethod("ReadUInt64", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readInt32Method = typeof(DataReaderExtensions).GetMethod("ReadInt32", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readInt64Method = typeof(DataReaderExtensions).GetMethod("ReadInt64", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readSByteMethod = typeof(DataReaderExtensions).GetMethod("ReadSByte", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readInt16Method = typeof(DataReaderExtensions).GetMethod("ReadInt16", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readCharMethod = typeof(DataReaderExtensions).GetMethod("ReadChar", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readSingleMethod = typeof(DataReaderExtensions).GetMethod("ReadSingle", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readDoubleMethod = typeof(DataReaderExtensions).GetMethod("ReadDouble", [typeof(DataReader).MakeByRefType()])!;
    private static readonly MethodInfo s_readUnmanagedMethod = typeof(DataReaderExtensions).GetMethod("ReadUnmanaged", BindingFlags.Public | BindingFlags.Static)!;

    #endregion Static Fields


    #region Public API

    public static void EmitWriteField(ILGenerator il, FieldSchema field, MethodInfo? directWrite)
    {
        FieldInfo fi = field.FieldInfo;

        if (directWrite != null)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, fi);
            il.Emit(OpCodes.Call, directWrite);
            return;
        }

        EMIT_WRITE_WITH_FORMATTER(il, field);
    }

    public static void EmitReadFieldRef(ILGenerator il, FieldSchema field, MethodInfo? directRead, LocalBuilder objLocal)
    {
        FieldInfo fi = field.FieldInfo;

        if (directRead != null)
        {
            il.Emit(OpCodes.Ldloc, objLocal);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, directRead);
            il.Emit(OpCodes.Stfld, fi);
            return;
        }

        EMIT_READ_REF_WITH_FORMATTER(il, fi, objLocal);
    }

    public static void EmitReadFieldValue(ILGenerator il, FieldSchema field, MethodInfo? directRead, LocalBuilder objLocal)
    {
        FieldInfo fi = field.FieldInfo;

        if (directRead != null)
        {
            il.Emit(OpCodes.Ldloca_S, objLocal);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, directRead);
            il.Emit(OpCodes.Stfld, fi);
            return;
        }

        EMIT_READ_VALUE_WITH_FORMATTER(il, fi, objLocal);
    }

    private static FormatterEmitMethods ResolveFormatterMethods(Type fieldType)
        => s_formatterEmitMethods.GetOrAdd(fieldType, static ft =>
        {
            Type cacheType = typeof(FormatterCache<>).MakeGenericType(ft);
            Type formatterType = typeof(IFormatter<>).MakeGenericType(ft);

            return new FormatterEmitMethods(
                cacheType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!,
                formatterType.GetMethod("Serialize", [typeof(DataWriter).MakeByRefType(), ft])!,
                formatterType.GetMethod("Deserialize", [typeof(DataReader).MakeByRefType()])!);
        });

    /// <summary>
    /// Returns the direct Write method from <see cref="DataWriterExtensions"/> if available.
    /// Prioritizes fast-path primitive and unmanaged writes.
    /// </summary>
    public static MethodInfo? TryResolveWriteMethod(Type fieldType)
    {
        // === Exact primitive matches ===
        if (fieldType == typeof(byte))
        {
            return s_writeByteMethod;
        }

        if (fieldType == typeof(bool))
        {
            return s_writeBoolMethod;
        }

        if (fieldType == typeof(sbyte))
        {
            return s_writeSByteMethod;
        }

        if (fieldType == typeof(short))
        {
            return s_writeInt16Method;
        }

        if (fieldType == typeof(ushort))
        {
            return s_writeUInt16Method;
        }

        if (fieldType == typeof(int))
        {
            return s_writeInt32Method;
        }

        if (fieldType == typeof(uint))
        {
            return s_writeUInt32Method;
        }

        if (fieldType == typeof(long))
        {
            return s_writeInt64Method;
        }

        if (fieldType == typeof(ulong))
        {
            return s_writeUInt64Method;
        }

        if (fieldType == typeof(float))
        {
            return s_writeSingleMethod;
        }

        if (fieldType == typeof(double))
        {
            return s_writeDoubleMethod;
        }

        if (fieldType == typeof(char))
        {
            return s_writeCharMethod;
        }

        if (fieldType.IsEnum)
        {
            return TryResolveWriteMethod(Enum.GetUnderlyingType(fieldType));
        }

        // Note: short is missing in DataWriterExtensions -> we fall through to WriteUnmanaged
        // You can add Write(short) later if you want.

        // Arrays need their formatter to preserve framing metadata such as length prefixes.
        if (fieldType == typeof(ReadOnlySpan<byte>))
        {
            return s_writeReadOnlySpanByteMethod;
        }

        if (Nullable.GetUnderlyingType(fieldType) is not null)
        {
            return null;
        }

        // === Generic Unmanaged (best fallback for all other primitives like short, float, double, char, enums, etc.) ===
        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            return s_genericWriteMethods.GetOrAdd(
                fieldType, static t => s_writeUnmanagedMethod.MakeGenericMethod(t));
        }

        return null;
    }

    /// <summary>
    /// Returns the direct Read method from <see cref="DataReaderExtensions"/> if available.
    /// </summary>
    public static MethodInfo? TryResolveReadMethod(Type fieldType)
    {
        if (fieldType == typeof(byte))
        {
            return s_readByteMethod;
        }

        if (fieldType == typeof(bool))
        {
            return s_readBooleanMethod;
        }

        if (fieldType == typeof(sbyte))
        {
            return s_readSByteMethod;
        }

        if (fieldType == typeof(short))
        {
            return s_readInt16Method;
        }

        if (fieldType == typeof(ushort))
        {
            return s_readUInt16Method;
        }

        if (fieldType == typeof(int))
        {
            return s_readInt32Method;
        }

        if (fieldType == typeof(uint))
        {
            return s_readUInt32Method;
        }

        if (fieldType == typeof(long))
        {
            return s_readInt64Method;
        }

        if (fieldType == typeof(ulong))
        {
            return s_readUInt64Method;
        }

        if (fieldType == typeof(float))
        {
            return s_readSingleMethod;
        }

        if (fieldType == typeof(double))
        {
            return s_readDoubleMethod;
        }

        if (fieldType == typeof(char))
        {
            return s_readCharMethod;
        }

        if (fieldType.IsEnum)
        {
            return TryResolveReadMethod(Enum.GetUnderlyingType(fieldType));
        }

        if (Nullable.GetUnderlyingType(fieldType) is not null)
        {
            return null;
        }

        // Generic unmanaged fallback
        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            return s_genericReadMethods.GetOrAdd(
                fieldType, static t => s_readUnmanagedMethod.MakeGenericMethod(t));
        }

        return null;
    }

    #endregion Public API

    #region Private Methods

    private static void EMIT_WRITE_WITH_FORMATTER(ILGenerator il, FieldSchema field)
    {
        FieldInfo fi = field.FieldInfo;
        FormatterEmitMethods emitMethods = ResolveFormatterMethods(field.FieldType);

        il.Emit(OpCodes.Ldsfld, emitMethods.InstanceField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, fi);
        il.Emit(OpCodes.Callvirt, emitMethods.SerializeMethod);
    }

    private static void EMIT_READ_REF_WITH_FORMATTER(ILGenerator il, FieldInfo field, LocalBuilder objLocal)
    {
        FormatterEmitMethods emitMethods = ResolveFormatterMethods(field.FieldType);

        il.Emit(OpCodes.Ldloc, objLocal);
        il.Emit(OpCodes.Ldsfld, emitMethods.InstanceField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, emitMethods.DeserializeMethod);
        il.Emit(OpCodes.Stfld, field);
    }

    private static void EMIT_READ_VALUE_WITH_FORMATTER(ILGenerator il, FieldInfo field, LocalBuilder objLocal)
    {
        FormatterEmitMethods emitMethods = ResolveFormatterMethods(field.FieldType);

        il.Emit(OpCodes.Ldloca_S, objLocal);
        il.Emit(OpCodes.Ldsfld, emitMethods.InstanceField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, emitMethods.DeserializeMethod);
        il.Emit(OpCodes.Stfld, field);
    }

    #endregion Private Methods
}
