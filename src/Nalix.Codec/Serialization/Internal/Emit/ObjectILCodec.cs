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
/// Builds a per-type IL serializer for reference types and caches the generated
/// delegates for reuse.
/// </summary>
internal static class ObjectILCodec<T> where T : class, new()
{
    public delegate void SerializeDelegate(ref DataWriter writer, in T value);
    public delegate void FillDelegate(ref DataReader reader, T value);

    public delegate T DeserializeDelegate(ref DataReader reader);

    public static readonly SerializeDelegate Serialize;
    public static readonly DeserializeDelegate Deserialize;
    public static readonly FillDelegate Fill;

    private static readonly FieldSchema[] s_fields;
    private static readonly MethodInfo?[] s_directWriteMethods;
    private static readonly MethodInfo?[] s_directReadMethods;

    static ObjectILCodec()
    {
        // Reflection is paid once per closed type, then the generated delegates are reused
        // on the hot path without further metadata discovery.
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
            s_directReadMethods[i] = FieldILCodec.TryResolveReadMethod(ft);
            s_directWriteMethods[i] = FieldILCodec.TryResolveWriteMethod(ft);
        }

        Fill = GenerateFill();
        Serialize = GenerateSerialize();
        Deserialize = GenerateDeserialize();
    }

    private static SerializeDelegate GenerateSerialize()
    {
        // Emit a linear serializer: each discovered field is written in the cached order
        // and the generated method contains no loops at runtime.
        DynamicMethod dm = new(
            $"ObjectSerialize_{typeof(T).Name}",
            typeof(void),
            [typeof(DataWriter).MakeByRefType(), typeof(T).MakeByRefType()],
            typeof(ObjectILCodec<T>).Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        for (int i = 0; i < s_fields.Length; i++)
        {
            FieldILCodec.EmitWriteField(il, s_fields[i], s_directWriteMethods[i], derefArg1: true);
        }

        il.Emit(OpCodes.Ret);
        return (SerializeDelegate)dm.CreateDelegate(typeof(SerializeDelegate));
    }

    private static DeserializeDelegate GenerateDeserialize()
    {
        // Emit a matching constructor + field-population path so the object is
        // fully reconstructed before it is returned to the caller.
        DynamicMethod dm = new(
            $"ObjectDeserialize_{typeof(T).Name}",
            typeof(T),
            [typeof(DataReader).MakeByRefType()],
            typeof(ObjectILCodec<T>).Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();
        LocalBuilder obj = il.DeclareLocal(typeof(T));

        il.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Stloc, obj);

        for (int i = 0; i < s_fields.Length; i++)
        {
            FieldILCodec.EmitReadFieldRef(il, s_fields[i], s_directReadMethods[i], obj);
        }

        il.Emit(OpCodes.Ldloc, obj);
        il.Emit(OpCodes.Ret);

        return (DeserializeDelegate)dm.CreateDelegate(typeof(DeserializeDelegate));
    }

    private static FillDelegate GenerateFill()
    {
        // Emit a high-performance in-place population path. This method takes
        // an existing instance (arg 1) and writes field data directly into it,
        // skipping the allocation cost of 'newobj'.
        DynamicMethod dm = new(
            $"ObjectFill_{typeof(T).Name}",
            typeof(void),
            [typeof(DataReader).MakeByRefType(), typeof(T)],
            typeof(ObjectILCodec<T>).Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        // We copy arg 1 (the instance) to a local to mirror the Deserialize logic exactly.
        LocalBuilder obj = il.DeclareLocal(typeof(T));
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stloc, obj);

        for (int i = 0; i < s_fields.Length; i++)
        {
            FieldILCodec.EmitReadFieldRef(il, s_fields[i], s_directReadMethods[i], obj);
        }

        il.Emit(OpCodes.Ret);

        return (FillDelegate)dm.CreateDelegate(typeof(FillDelegate));
    }
}
