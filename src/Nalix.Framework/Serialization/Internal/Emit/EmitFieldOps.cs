using System;
using System.Reflection;
using System.Reflection.Emit;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization;
using Nalix.Framework.Serialization.Formatters.Cache;
using Nalix.Framework.Serialization.Internal.Reflection;
using Nalix.Framework.Serialization.Internal.Types;

internal static class EmitFieldOps
{
    public static void EmitSerializeField(ILGenerator il, FieldSchema field, MethodInfo? directWrite)
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

        EmitFormatterSerialize(il, field);
    }

    public static void EmitDeserializeObjectField(ILGenerator il, FieldSchema field, MethodInfo? directRead, LocalBuilder objLocal)
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

        EmitFormatterDeserializeObject(il, fi, objLocal);
    }

    public static void EmitDeserializeStructField(ILGenerator il, FieldSchema field, MethodInfo? directRead, LocalBuilder objLocal)
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

        EmitFormatterDeserializeStruct(il, fi, objLocal);
    }

    private static void EmitFormatterSerialize(ILGenerator il, FieldSchema field)
    {
        Type fType = field.FieldType;
        FieldInfo fi = field.FieldInfo;

        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instanceField = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? serializeMethod = typeof(IFormatter<>).MakeGenericType(fType)
            .GetMethod("Serialize", [typeof(DataWriter).MakeByRefType(), fType]);

        il.Emit(OpCodes.Ldsfld, instanceField!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, fi);
        il.Emit(OpCodes.Callvirt, serializeMethod!);
    }

    private static void EmitFormatterDeserializeObject(ILGenerator il, FieldInfo field, LocalBuilder objLocal)
    {
        Type fType = field.FieldType;
        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instanceField = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? deserializeMethod = typeof(IFormatter<>).MakeGenericType(fType)
            .GetMethod("Deserialize", [typeof(DataReader).MakeByRefType()]);

        il.Emit(OpCodes.Ldloc, objLocal);
        il.Emit(OpCodes.Ldsfld, instanceField!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, deserializeMethod!);
        il.Emit(OpCodes.Stfld, field);
    }

    private static void EmitFormatterDeserializeStruct(ILGenerator il, FieldInfo field, LocalBuilder objLocal)
    {
        Type fType = field.FieldType;
        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instanceField = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? deserializeMethod = typeof(IFormatter<>).MakeGenericType(fType)
            .GetMethod("Deserialize", [typeof(DataReader).MakeByRefType()]);

        il.Emit(OpCodes.Ldloca_S, objLocal);
        il.Emit(OpCodes.Ldsfld, instanceField!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, deserializeMethod!);
        il.Emit(OpCodes.Stfld, field);
    }

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

        // Arrays need their formatter to preserve framing metadata such as length prefixes.
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

        // Generic unmanaged fallback
        if (TypeMetadata.IsUnmanaged(fieldType))
        {
            MethodInfo? method = ext.GetMethod("ReadUnmanaged", BindingFlags.Public | BindingFlags.Static);
            return method?.MakeGenericMethod(fieldType);
        }

        return null;
    }
}
