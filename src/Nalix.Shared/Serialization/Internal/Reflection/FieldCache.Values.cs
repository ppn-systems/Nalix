// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Generic Value Operations - Zero Boxing

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static TField GetValue<TField>(T obj, System.Int32 fieldIndex)
    {
        var metadata = _metadata[fieldIndex];

        return metadata.FieldType != typeof(TField)
            ? throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'")
            : (TField)metadata.FieldInfo.GetValue(obj)!;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void SetValue<TField>(T obj, System.Int32 fieldIndex, TField value)
    {
        var metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'");
        }

        metadata.FieldInfo.SetValue(obj, value);
    }

    #endregion Generic Value Operations - Zero Boxing
}