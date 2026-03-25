// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Framework.Memory.Buffers;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Accessors;

/// <summary>
/// Strongly-typed field accessor implementation that eliminates boxing
/// and leverages FieldCache for optimal performance.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TField"></typeparam>
/// <param name="index"></param>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class FieldAccessorImpl<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TField>(int index) : FieldAccessor<T>
{
    #region Fields

    private readonly int _index = index;
    private readonly IFormatter<TField> _formatter = FormatterProvider.Get<TField>();

    #endregion Fields

    #region Serialization Implementation

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref DataWriter writer, T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        TField value = FieldCache<T>.GetValue<TField>(obj, _index);
        _formatter.Serialize(ref writer, value);
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        TField value = _formatter.Deserialize(ref reader);
        FieldCache<T>.SetValue(obj, _index, value);
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, ref T obj)
    {
        TField value = _formatter.Deserialize(ref reader);
        FieldCache<T>.SetValue(ref obj, _index, value); // ← FieldCache cần có ref overload
    }

    #endregion Serialization Implementation
}
