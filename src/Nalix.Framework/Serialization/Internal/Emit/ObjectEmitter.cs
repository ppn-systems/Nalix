using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization;
using Nalix.Framework.Serialization.Formatters.Cache;
using Nalix.Framework.Serialization.Internal.Reflection;

/// <summary>
/// Fully unrolled, zero-overhead IL serializer for reference types (classes).
/// </summary>
internal static class ObjectEmitter<T> where T : class, new()
{
    public static readonly SerializeDelegate Serialize;
    public static readonly DeserializeDelegate Deserialize;

    public delegate void SerializeDelegate(ref DataWriter writer, T value);
    public delegate T DeserializeDelegate(ref DataReader reader);

    private static readonly FieldSchema[] s_fields;
    private static readonly MethodInfo?[] s_directWriteMethods;
    private static readonly MethodInfo?[] s_directReadMethods;

    static ObjectEmitter()
    {
        s_fields = FieldCache<T>.GetFields();

        if (s_fields == null || s_fields.Length == 0)
        {
            throw new InvalidOperationException("No serializable fields found.");
        }

        s_directWriteMethods = new MethodInfo?[s_fields.Length];
        s_directReadMethods = new MethodInfo?[s_fields.Length];

        for (int i = 0; i < s_fields.Length; i++)
        {
            Type ft = s_fields[i].FieldType;
            s_directReadMethods[i] = EmitHelpers.TryGetDirectReadMethod(ft);
            s_directWriteMethods[i] = EmitHelpers.TryGetDirectWriteMethod(ft);
        }

        Serialize = GenerateSerialize();
        Deserialize = GenerateDeserialize();
    }

    private static SerializeDelegate GenerateSerialize()
    {
        DynamicMethod dm = new(
            $"ObjectSerialize_{typeof(T).Name}",
            typeof(void),
            [typeof(DataWriter).MakeByRefType(), typeof(T)],
            typeof(ObjectEmitter<T>).Module,
            skipVisibility: true);

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
        DynamicMethod dm = new(
            $"ObjectDeserialize_{typeof(T).Name}",
            typeof(T),
            [typeof(DataReader).MakeByRefType()],
            typeof(ObjectEmitter<T>).Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();
        LocalBuilder obj = il.DeclareLocal(typeof(T));

        // T obj = new T();
        il.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Stloc, obj);

        for (int i = 0; i < s_fields.Length; i++)
        {
            EmitDeserializeField(il, s_fields[i], s_directReadMethods[i], obj, isStruct: false);

            Debug.WriteLine(">>> EmitFormatterDeserialize cho field: " + s_fields[i].FieldType);
        }

        il.Emit(OpCodes.Ldloc, obj);
        il.Emit(OpCodes.Ret);

        return (DeserializeDelegate)dm.CreateDelegate(typeof(DeserializeDelegate));
    }

    // Emit methods (same as before)
    private static void EmitSerializeField(ILGenerator il, FieldSchema field, MethodInfo? directWrite)
    {
        if (directWrite != null)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, field.FieldInfo);
            il.Emit(OpCodes.Call, directWrite);

            return;
        }

        EmitFormatterSerialize(il, field);
    }

    private static void EmitDeserializeField(ILGenerator il, FieldSchema field, MethodInfo? directRead, LocalBuilder objLocal, bool isStruct)
    {
        FieldInfo fi = field.FieldInfo;


        if (directRead != null)
        {
            il.Emit(isStruct ? OpCodes.Ldloca_S : OpCodes.Ldloc, objLocal);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, directRead);
            il.Emit(OpCodes.Stfld, fi);

            return;
        }

        EmitFormatterDeserialize(il, fi, objLocal, isStruct);
    }

    private static void EmitFormatterSerialize(ILGenerator il, FieldSchema field)
    {
        Type fType = field.FieldType;
        FieldInfo fi = field.FieldInfo;

        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instance = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? method = typeof(IFormatter<>).MakeGenericType(fType)
                        .GetMethod("Serialize", [typeof(DataWriter).MakeByRefType(), fType]);

        il.Emit(OpCodes.Ldsfld, instance!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, fi);
        il.Emit(OpCodes.Callvirt, method!);
    }

    private static void EmitFormatterDeserialize(ILGenerator il, FieldInfo field, LocalBuilder objLocal, bool isStruct)
    {
        Type fType = field.FieldType;
        Type cache = typeof(FormatterCache<>).MakeGenericType(fType);
        FieldInfo? instance = cache.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? method = typeof(IFormatter<>).MakeGenericType(fType)
                        .GetMethod("Deserialize", [typeof(DataReader).MakeByRefType()]);

        il.Emit(isStruct ? OpCodes.Ldloca_S : OpCodes.Ldloc, objLocal);
        il.Emit(OpCodes.Ldsfld, instance!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, method!);
        il.Emit(OpCodes.Stfld, field);
    }
}
