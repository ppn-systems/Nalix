// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Accessors;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal abstract class FieldAccessor<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    #region Fields

    // Cached MethodInfo to avoid repeated reflection lookups
    private static readonly System.Reflection.MethodInfo s_createTypedGeneric;

    // Cache of compiled factories per field runtime type
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Int32, FieldAccessor<T>>> s_factories;

    #endregion Fields

    static FieldAccessor()
    {
        s_factories = new();
        s_createTypedGeneric = typeof(FieldAccessor<T>).GetMethod(
            nameof(CreateTyped), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new System.MissingMethodException(typeof(FieldAccessor<T>).FullName, nameof(CreateTyped));
    }

    [System.Diagnostics.DebuggerStepThrough]
    public abstract void Serialize(ref DataWriter writer, T obj);

    [System.Diagnostics.DebuggerStepThrough]
    public abstract void Deserialize(ref DataReader reader, T obj);

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldAccessor<T> Create(FieldSchema schema, System.Int32 index)
    {
        System.ArgumentNullException.ThrowIfNull(schema.FieldInfo, "schema.FieldInfo");

        // Normalize and validate target field type
        System.Type fieldType = schema.FieldType ?? throw new System.ArgumentException("schema.FieldType is null", nameof(schema));
        fieldType = System.Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (fieldType.IsByRef || fieldType.IsPointer)
        {
            throw new System.NotSupportedException($"ByRef/Pointer types are not supported: {fieldType}");
        }

        if (fieldType.ContainsGenericParameters)
        {
            throw new System.NotSupportedException($"Open generic field type is not supported: {fieldType}");
        }

        try
        {
            // Get or create compiled factory for this fieldType
            System.Func<System.Int32, FieldAccessor<T>> factory = s_factories.GetOrAdd(fieldType, static ft =>
            {
                // Build closed generic method: CreateTyped<TField>(int)
                System.Reflection.MethodInfo mi = s_createTypedGeneric.MakeGenericMethod(ft);

                // Compile to delegate to avoid MethodInfo.Invoke overhead
                // Signature: Func<int, FieldAccessor<T>>
                return mi.CreateDelegate<System.Func<System.Int32, FieldAccessor<T>>>();
            });

            return factory(index);
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException($"Failed to create accessor for field '{schema.Name}' of type '{fieldType}'.", ex);
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static FieldAccessorImpl<T, TField> CreateTyped<TField>(System.Int32 index) => new(index);
}