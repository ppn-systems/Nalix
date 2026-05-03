// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization.Internal.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Codec.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Codec.Benchmarks")]
#endif

namespace Nalix.Codec.Serialization.Internal.Emit;

/// <summary>
/// Builds a per-type IL serializer for value types and caches the generated
/// delegates for reuse.
/// </summary>
internal static class StructILCodec<T> where T : struct
{
    public static readonly SerializeDelegate Serialize;
    public static readonly DeserializeDelegate Deserialize;

    public delegate void SerializeDelegate(ref DataWriter writer, in T value);
    public delegate T DeserializeDelegate(ref DataReader reader);

    // Reflection and method resolution are done once per closed struct type.
    private static readonly FieldSchema[] s_fields;
    private static readonly MethodInfo?[] s_directReadMethods;
    private static readonly MethodInfo?[] s_directWriteMethods;

    static StructILCodec()
    {
        s_fields = FieldCache<T>.GetFields();

        s_directWriteMethods = new MethodInfo?[s_fields.Length];
        s_directReadMethods = new MethodInfo?[s_fields.Length];

        for (int i = 0; i < s_fields.Length; i++)
        {
            Type fieldType = s_fields[i].FieldType;

            s_directReadMethods[i] = FieldILCodec.TryResolveReadMethod(fieldType);
            s_directWriteMethods[i] = FieldILCodec.TryResolveWriteMethod(fieldType);
        }

        Serialize = GenerateSerialize();
        Deserialize = GenerateDeserialize();
    }

    #region Direct Method Caching (called only once at startup)

    #endregion

    private static SerializeDelegate GenerateSerialize()
    {
        // For structs the generated serializer still writes fields sequentially,
        // but the owner value is passed by value because serialization does not
        // need to mutate the original instance.
        DynamicMethod dm = new($"StructSerialize_{typeof(T).Name}",
            typeof(void), [typeof(DataWriter).MakeByRefType(), typeof(T)],
            typeof(StructILCodec<T>).Module, skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        for (int i = 0; i < s_fields.Length; i++)
        {
            FieldILCodec.EmitWriteField(il, s_fields[i], s_directWriteMethods[i]);
        }

        il.Emit(OpCodes.Ret);
        return (SerializeDelegate)dm.CreateDelegate(typeof(SerializeDelegate));
    }

    private static DeserializeDelegate GenerateDeserialize()
    {
        // Build the struct in a local, initialize it once, then populate each field
        // directly through the emitted IL before returning the completed value.
        DynamicMethod dm = new($"StructDeserialize_{typeof(T).Name}",
            typeof(T), [typeof(DataReader).MakeByRefType()],
            typeof(StructILCodec<T>).Module, skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();
        LocalBuilder obj = il.DeclareLocal(typeof(T));

        il.Emit(OpCodes.Ldloca_S, obj);
        il.Emit(OpCodes.Initobj, typeof(T));

        for (int i = 0; i < s_fields.Length; i++)
        {
            FieldILCodec.EmitReadFieldValue(il, s_fields[i], s_directReadMethods[i], obj);
        }

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (DeserializeDelegate)dm.CreateDelegate(typeof(DeserializeDelegate));
    }
}
