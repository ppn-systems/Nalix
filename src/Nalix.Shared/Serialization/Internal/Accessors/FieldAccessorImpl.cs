// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Accessors;

/// <summary>
/// Strongly-typed field accessor implementation eliminates boxing
/// và leverage FieldCache cho optimal performance.
/// </summary>
/// <typeparam name="T">Object type chứa field.</typeparam>
/// <typeparam name="TField">Field type.</typeparam>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class FieldAccessorImpl<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TField>(System.Int32 index) : FieldAccessor<T>
{
    #region Fields

    private readonly System.Int32 _index = index;
    private readonly IFormatter<TField> _formatter = FormatterProvider.Get<TField>();

    #endregion Fields

    #region Serialization Implementation

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref DataWriter writer, T obj)
    {
        System.ArgumentNullException.ThrowIfNull(obj);

        TField value = FieldCache<T>.GetValue<TField>(obj, _index);
        _formatter.Serialize(ref writer, value);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, T obj)
    {
        System.ArgumentNullException.ThrowIfNull(obj);

        TField value = _formatter.Deserialize(ref reader);
        FieldCache<T>.SetValue(obj, _index, value);
    }

    #endregion Serialization Implementation
}