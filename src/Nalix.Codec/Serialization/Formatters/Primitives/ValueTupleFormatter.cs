// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Environment.Memory;

namespace Nalix.Codec.Serialization.Formatters.Primitives;

/// <summary>
/// Serializes value tuples element-by-element using the registered formatter for
/// each generic slot.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ValueTupleFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T1,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T2> : IFormatter<(T1, T2)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}>";

    private IFormatter<T1>? _f1;
    private IFormatter<T2>? _f2;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T1> F1() => _f1 ??= FormatterProvider.Get<T1>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T2> F2() => _f2 ??= FormatterProvider.Get<T2>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in (T1, T2) value)
    {
        F1().Serialize(ref writer, value.Item1);
        F2().Serialize(ref writer, value.Item2);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2) Deserialize(ref DataReader reader) => (F1().Deserialize(ref reader), F2().Deserialize(ref reader));
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
internal sealed class ValueTupleFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T1,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T2,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T3> : IFormatter<(T1, T2, T3)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}>";

    private IFormatter<T1>? _f1;
    private IFormatter<T2>? _f2;
    private IFormatter<T3>? _f3;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T1> F1() => _f1 ??= FormatterProvider.Get<T1>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T2> F2() => _f2 ??= FormatterProvider.Get<T2>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T3> F3() => _f3 ??= FormatterProvider.Get<T3>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2, T3}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in (T1, T2, T3) value)
    {
        F1().Serialize(ref writer, value.Item1);
        F2().Serialize(ref writer, value.Item2);
        F3().Serialize(ref writer, value.Item3);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2, T3}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3) Deserialize(ref DataReader reader)
        => (F1().Deserialize(ref reader), F2().Deserialize(ref reader), F3().Deserialize(ref reader));
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
internal sealed class ValueTupleFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T1,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T2,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T3,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T4> : IFormatter<(T1, T2, T3, T4)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}>";

    private IFormatter<T1>? _f1;
    private IFormatter<T2>? _f2;
    private IFormatter<T3>? _f3;
    private IFormatter<T4>? _f4;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T1> F1() => _f1 ??= FormatterProvider.Get<T1>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T2> F2() => _f2 ??= FormatterProvider.Get<T2>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T3> F3() => _f3 ??= FormatterProvider.Get<T3>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T4> F4() => _f4 ??= FormatterProvider.Get<T4>();

    /// <summary>
    /// Serializes a <see cref="System.ValueTuple{T1, T2, T3, T4}"/> into the specified <see cref="DataWriter"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in (T1, T2, T3, T4) value)
    {
        F1().Serialize(ref writer, value.Item1);
        F2().Serialize(ref writer, value.Item2);
        F3().Serialize(ref writer, value.Item3);
        F4().Serialize(ref writer, value.Item4);
    }

    /// <summary>
    /// Deserializes a <see cref="System.ValueTuple{T1, T2, T3, T4}"/> from the specified <see cref="DataReader"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3, T4) Deserialize(ref DataReader reader)
        => (F1().Deserialize(ref reader), F2().Deserialize(ref reader), F3().Deserialize(ref reader), F4().Deserialize(ref reader));
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
internal sealed class ValueTupleFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T1,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T2,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T3,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T4,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T5>
    : IFormatter<(T1, T2, T3, T4, T5)>
{
    private static string DebuggerDisplay =>
        $"ValueTupleFormatter<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}>";

    private IFormatter<T1>? _f1;
    private IFormatter<T2>? _f2;
    private IFormatter<T3>? _f3;
    private IFormatter<T4>? _f4;
    private IFormatter<T5>? _f5;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T1> F1() => _f1 ??= FormatterProvider.Get<T1>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T2> F2() => _f2 ??= FormatterProvider.Get<T2>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T3> F3() => _f3 ??= FormatterProvider.Get<T3>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T4> F4() => _f4 ??= FormatterProvider.Get<T4>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IFormatter<T5> F5() => _f5 ??= FormatterProvider.Get<T5>();

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in (T1, T2, T3, T4, T5) value)
    {
        F1().Serialize(ref writer, value.Item1);
        F2().Serialize(ref writer, value.Item2);
        F3().Serialize(ref writer, value.Item3);
        F4().Serialize(ref writer, value.Item4);
        F5().Serialize(ref writer, value.Item5);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (T1, T2, T3, T4, T5) Deserialize(ref DataReader reader)
        => (F1().Deserialize(ref reader), F2().Deserialize(ref reader), F3().Deserialize(ref reader), F4().Deserialize(ref reader), F5().Deserialize(ref reader));
}
