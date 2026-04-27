// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Formatters.Primitives;

/// <summary>
/// Serializes value tuples element-by-element using the registered formatter for
/// each generic slot.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ValueTupleFormatter<T1, T2> : IFormatter<(T1, T2)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}>";

    private readonly IFormatter<T1> _f1 = FormatterProvider.Get<T1>();
    private readonly IFormatter<T2> _f2 = FormatterProvider.Get<T2>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <remarks>
    /// The tuple is written in declaration order so the receiver can reconstruct it
    /// without any extra shape metadata.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, (T1, T2) value)
    {
        _f1.Serialize(ref writer, value.Item1);
        _f2.Serialize(ref writer, value.Item2);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2) Deserialize(ref DataReader reader) => (_f1.Deserialize(ref reader), _f2.Deserialize(ref reader));
}

// =========================================================================

/// <summary>
/// Serializes value tuples element-by-element using the registered formatter for
/// each generic slot.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ValueTupleFormatter<T1, T2, T3> : IFormatter<(T1, T2, T3)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}>";

    private readonly IFormatter<T1> _f1 = FormatterProvider.Get<T1>();
    private readonly IFormatter<T2> _f2 = FormatterProvider.Get<T2>();
    private readonly IFormatter<T3> _f3 = FormatterProvider.Get<T3>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2, T3}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <remarks>
    /// The tuple is written in declaration order so the receiver can reconstruct it
    /// without any extra shape metadata.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, (T1, T2, T3) value)
    {
        _f1.Serialize(ref writer, value.Item1);
        _f2.Serialize(ref writer, value.Item2);
        _f3.Serialize(ref writer, value.Item3);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2, T3}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3) Deserialize(ref DataReader reader)
        => (_f1.Deserialize(ref reader), _f2.Deserialize(ref reader), _f3.Deserialize(ref reader));
}

// =========================================================================

/// <summary>
/// Serializes value tuples element-by-element using the registered formatter for
/// each generic slot.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ValueTupleFormatter<T1, T2, T3, T4> : IFormatter<(T1, T2, T3, T4)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, " +
        $"{typeof(T3).Name}, {typeof(T4).Name}>";

    private readonly IFormatter<T1> _f1 = FormatterProvider.Get<T1>();
    private readonly IFormatter<T2> _f2 = FormatterProvider.Get<T2>();
    private readonly IFormatter<T3> _f3 = FormatterProvider.Get<T3>();
    private readonly IFormatter<T4> _f4 = FormatterProvider.Get<T4>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2, T3, T4}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <remarks>
    /// The tuple is written in declaration order so the receiver can reconstruct it
    /// without any extra shape metadata.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, (T1, T2, T3, T4) value)
    {
        _f1.Serialize(ref writer, value.Item1);
        _f2.Serialize(ref writer, value.Item2);
        _f3.Serialize(ref writer, value.Item3);
        _f4.Serialize(ref writer, value.Item4);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2, T3, T4}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3, T4) Deserialize(ref DataReader reader)
        => (_f1.Deserialize(ref reader), _f2.Deserialize(ref reader), _f3.Deserialize(ref reader), _f4.Deserialize(ref reader));
}

// =========================================================================

/// <summary>
/// Serializes tuples with five or more elements, including the nested <c>TRest</c>
/// layout used by the BCL for tuples larger than arity seven.
/// </summary>
/// <remarks>
/// <para>
/// .NET encodes tuples with 8+ elements as
/// <c>ValueTuple&lt;T1,T2,T3,T4,T5,T6,T7,TRest&gt;</c>
/// where <c>TRest</c> is itself a <c>ValueTuple</c>.
/// This formatter handles that case transparently by resolving
/// <c>TRest</c> through <see cref="FormatterProvider"/> recursively.
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ValueTupleFormatter<T1, T2, T3, T4, T5>
    : IFormatter<(T1, T2, T3, T4, T5)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, " +
        $"{typeof(T4).Name}, {typeof(T5).Name}>";

    private readonly IFormatter<T1> _f1 = FormatterProvider.Get<T1>();
    private readonly IFormatter<T2> _f2 = FormatterProvider.Get<T2>();
    private readonly IFormatter<T3> _f3 = FormatterProvider.Get<T3>();
    private readonly IFormatter<T4> _f4 = FormatterProvider.Get<T4>();
    private readonly IFormatter<T5> _f5 = FormatterProvider.Get<T5>();

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, (T1, T2, T3, T4, T5) value)
    {
        _f1.Serialize(ref writer, value.Item1);
        _f2.Serialize(ref writer, value.Item2);
        _f3.Serialize(ref writer, value.Item3);
        _f4.Serialize(ref writer, value.Item4);
        _f5.Serialize(ref writer, value.Item5);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3, T4, T5) Deserialize(ref DataReader reader)
        => (_f1.Deserialize(ref reader), _f2.Deserialize(ref reader), _f3.Deserialize(ref reader), _f4.Deserialize(ref reader), _f5.Deserialize(ref reader));
}
