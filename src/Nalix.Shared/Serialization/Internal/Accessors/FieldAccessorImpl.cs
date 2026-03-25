// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Accessors;

/// <summary>
/// Strongly-typed field accessor implementation that eliminates boxing
/// and leverages FieldCache for optimal performance.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TField"></typeparam>
/// <param name="index"></param>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class FieldAccessorImpl<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TField>(int index) : FieldAccessor<T>
{
    #region Fields

    private readonly int _index = index;
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

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, ref T obj)
    {
        TField value = _formatter.Deserialize(ref reader);
        FieldCache<T>.SetValue(ref obj, _index, value); // ← FieldCache cần có ref overload
    }

    #endregion Serialization Implementation
}
