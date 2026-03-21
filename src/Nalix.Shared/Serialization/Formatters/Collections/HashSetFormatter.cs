// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization logic for
/// <see cref="System.Collections.Generic.HashSet{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the set. Must be non-nullable.</typeparam>
/// <remarks>
/// <para>
/// Wire format:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>[4 bytes]</c> Count (<see cref="System.Int32"/>, little-endian)
/// — <c>-1</c> indicates <c>null</c>, <c>0</c> indicates empty set.
/// </description>
/// </item>
/// <item>
/// <description>
/// For each element:
/// <list type="bullet">
/// <item><description>Element serialized using <see cref="IFormatter{T}"/>.</description></item>
/// </list>
/// </description>
/// </item>
/// </list>
/// <para>
/// Iteration order of <see cref="System.Collections.Generic.HashSet{T}"/> is not guaranteed.
/// Deserialization restores set membership but not insertion order.
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class HashSetFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    : IFormatter<System.Collections.Generic.HashSet<T>?>
    where T : notnull
{
    private static System.String DebuggerDisplay => $"HashSetFormatter<{typeof(T).Name}>";

    /// <summary>
    /// Formatter used to serialize and deserialize set elements.
    /// </summary>
    private readonly IFormatter<T> _elementFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashSetFormatter{T}"/> class.
    /// </summary>
    /// <exception cref="System.NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is a class other than <see cref="System.String"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Element type restrictions:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Allowed: primitive types, <see cref="System.String"/>, enums, unmanaged structs.</description></item>
    /// <item><description>Not allowed: reference types (except <see cref="System.String"/>).</description></item>
    /// </list>
    /// <para>
    /// This restriction ensures deterministic equality and stable hashing during deserialization.
    /// </para>
    /// </remarks>
    public HashSetFormatter()
    {
        System.Type elementType = typeof(T);

        if (elementType.IsClass && elementType != typeof(System.String))
        {
            throw new System.NotSupportedException(
                $"HashSetFormatter: T='{elementType.Name}' is a class — only supports primitive, string, enum, or unmanaged struct as element.");
        }

        _elementFormatter = FormatterProvider.Get<T>();
    }

    // ------------------------------------------------------------------ //
    //  Serialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Serializes a hash set into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to which data will be written.</param>
    /// <param name="value">The hash set to serialize. Can be <c>null</c>.</param>
    /// <remarks>
    /// <para>
    /// Serialization behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>null</c> → writes <c>-1</c> as count.</description></item>
    /// <item><description>Empty set → writes <c>0</c>.</description></item>
    /// <item><description>Otherwise writes count followed by each unique element.</description></item>
    /// </list>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.HashSet<T>? value)
    {
        if (value is null)
        {
            writer.Expand(sizeof(System.Int32));
            FormatterProvider.Get<System.Int32>()
                             .Serialize(ref writer, -1);
            return;
        }

        System.Int32 count = value.Count;
        writer.Expand(sizeof(System.Int32));
        FormatterProvider.Get<System.Int32>()
                         .Serialize(ref writer, count);

        if (count is 0)
        {
            return;
        }

        foreach (T element in value)
        {
            _elementFormatter.Serialize(ref writer, element);
        }
    }

    // ------------------------------------------------------------------ //
    //  Deserialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Deserializes a hash set from the specified <see cref="DataReader"/>.
    /// </summary>
    /// <param name="reader">The reader containing serialized data.</param>
    /// <returns>
    /// A reconstructed hash set instance, or <c>null</c> if the input represents null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Deserialization behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>-1</c> → returns <c>null</c>.</description></item>
    /// <item><description><c>0</c> → returns an empty hash set.</description></item>
    /// <item><description>
    /// Otherwise reads elements and adds them to the set.
    /// Duplicate elements (if any) are silently ignored by <see cref="System.Collections.Generic.HashSet{T}.Add"/>.
    /// </description></item>
    /// </list>
    /// <para>
    /// The set is initialized with the exact capacity to avoid internal resizing.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.HashSet<T>? Deserialize(ref DataReader reader)
    {
        System.Int32 count = FormatterProvider.Get<System.Int32>()
                                              .Deserialize(ref reader);

        if (count == -1)
        {
            return null;
        }

        System.Collections.Generic.HashSet<T> set = new(count);

        for (System.Int32 i = 0; i < count; i++)
        {
            set.Add(_elementFormatter.Deserialize(ref reader));
        }

        return set;
    }
}