// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Formatters.Cache;

namespace Nalix.Framework.Serialization.Internal.Emit;

/// <summary>
/// Optimized IL Emit serializer - SAFE + HIGH PERFORMANCE
/// </summary>
internal static class StructEmitter<T> where T : struct
{
    public static readonly SerializeDelegate Serialize;
    public static readonly DeserializeDelegate Deserialize;

    public delegate void SerializeDelegate(ref DataWriter writer, T value);
    public delegate T DeserializeDelegate(ref DataReader reader);

    // Cached per type
    private static readonly FieldInfo[] s_fields;
    private static readonly MethodInfo?[] s_directReadMethods;
    private static readonly MethodInfo?[] s_directWriteMethods;

    static StructEmitter()
    {
        s_fields = EmitHelpers.GetSerializableFields(typeof(T));

        s_directWriteMethods = new MethodInfo?[s_fields.Length];
        s_directReadMethods = new MethodInfo?[s_fields.Length];

        for (int i = 0; i < s_fields.Length; i++)
        {
            Type fieldType = s_fields[i].FieldType;

            s_directReadMethods[i] = EmitHelpers.TryGetDirectReadMethod(fieldType);
            s_directWriteMethods[i] = EmitHelpers.TryGetDirectWriteMethod(fieldType);
        }

        Serialize = GenerateSerialize();
        Deserialize = GenerateDeserialize();
    }

    #region Direct Method Caching (called only once at startup)

    #endregion

    private static SerializeDelegate GenerateSerialize()
    {
        DynamicMethod dm = new($"StructSerialize_{typeof(T).Name}",
            typeof(void), [typeof(DataWriter).MakeByRefType(), typeof(T)],
            typeof(StructEmitter<T>).Module, skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        for (int i = 0; i < s_fields.Length; i++)
        {
            EmitSerializeField(il, s_fields[i], s_directWriteMethods[i]);
        }

        il.Emit(OpCodes.Ret);
        return (SerializeDelegate)dm.CreateDelegate(typeof(SerializeDelegate));
    }

    private static DeserializeDelegate GenerateDeserialize()
    {
        DynamicMethod dm = new($"StructDeserialize_{typeof(T).Name}",
            typeof(T), [typeof(DataReader).MakeByRefType()],
            typeof(StructEmitter<T>).Module, skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();
        LocalBuilder obj = il.DeclareLocal(typeof(T));

        il.Emit(OpCodes.Ldloca_S, obj);
        il.Emit(OpCodes.Initobj, typeof(T));

        for (int i = 0; i < s_fields.Length; i++)
        {
            EmitDeserializeField(il, s_fields[i], s_directReadMethods[i], obj, isStruct: true);
        }

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (DeserializeDelegate)dm.CreateDelegate(typeof(DeserializeDelegate));
    }

    #region Emit Methods

    private static void EmitSerializeField(ILGenerator il, FieldInfo field, MethodInfo? directWrite)
    {
        if (directWrite != null)
        {
            // Fast path: direct extension call
            il.Emit(OpCodes.Ldarg_0);           // ref DataWriter
            il.Emit(OpCodes.Ldarg_1);           // value
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Call, directWrite);
            return;
        }

        // Formatter fallback
        EmitFormatterSerialize(il, field);
    }

    private static void EmitDeserializeField(ILGenerator il, FieldInfo field, MethodInfo? directRead, LocalBuilder objLocal, bool isStruct)
    {
        if (directRead != null)
        {
            if (isStruct)
            {
                il.Emit(OpCodes.Ldloca_S, objLocal);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, objLocal);
            }

            il.Emit(OpCodes.Ldarg_0);           // ref DataReader
            il.Emit(OpCodes.Call, directRead);
            il.Emit(OpCodes.Stfld, field);
            return;
        }

        EmitFormatterDeserialize(il, field, objLocal, isStruct);
    }

    private static void EmitFormatterSerialize(ILGenerator il, FieldInfo field)
    {
        Type fType = field.FieldType;
        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instanceField = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? serializeMethod = typeof(IFormatter<>).MakeGenericType(fType)
                                .GetMethod("Serialize", [typeof(DataWriter).MakeByRefType(), fType]);

        il.Emit(OpCodes.Ldsfld, instanceField!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Callvirt, serializeMethod!);
    }

    private static void EmitFormatterDeserialize(ILGenerator il, FieldInfo field, LocalBuilder objLocal, bool isStruct)
    {
        Type fType = field.FieldType;
        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instanceField = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? deserializeMethod = typeof(IFormatter<>).MakeGenericType(fType)
                                  .GetMethod("Deserialize", [typeof(DataReader).MakeByRefType()]);

        if (isStruct)
        {
            il.Emit(OpCodes.Ldloca_S, objLocal);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, objLocal);
        }

        il.Emit(OpCodes.Ldsfld, instanceField!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, deserializeMethod!);
        il.Emit(OpCodes.Stfld, field);
    }

    #endregion
}
