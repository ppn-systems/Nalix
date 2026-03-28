// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization logic for
/// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <remarks>
/// <para>
/// Wire format:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>[4 bytes]</c> Count (<see cref="int"/>, little-endian)
/// — <c>-1</c> indicates <c>null</c>, <c>0</c> indicates empty dictionary.
/// </description>
/// </item>
/// <item>
/// <description>
/// For each entry:
/// <list type="bullet">
/// <item><description>Key serialized using <see cref="IFormatter{TKey}"/>.</description></item>
/// <item><description>Value serialized using <see cref="IFormatter{TValue}"/>.</description></item>
/// </list>
/// </description>
/// </item>
/// </list>
/// <para>
/// This formatter relies on <c>FormatterProvider</c> to resolve formatters
/// for both key and value types, supporting primitives, enums, structs,
/// classes, and nullable types automatically.
/// </para>
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class DictionaryFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] TKey,
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] TValue> : IFormatter<System.Collections.Generic.Dictionary<TKey, TValue>?> where TKey : notnull
{
    /// <summary>
    /// Gets the debugger display string for this formatter.
    /// </summary>
    private static string DebuggerDisplay =>
        $"DictionaryFormatter<{typeof(TKey).Name}, {typeof(TValue).Name}>";

    /// <summary>
    /// Formatter used to serialize and deserialize dictionary keys.
    /// </summary>
    private readonly IFormatter<TKey> _keyFormatter = FormatterProvider.Get<TKey>();

    /// <summary>
    /// Formatter used to serialize and deserialize dictionary values.
    /// </summary>
    private readonly IFormatter<TValue> _valueFormatter = FormatterProvider.Get<TValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryFormatter{TKey, TValue}"/> class.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="TKey"/> is not a supported key type.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Key type restrictions:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Allowed: primitive types, <see cref="string"/>, enums, unmanaged structs.</description></item>
    /// <item><description>Not allowed: reference types (except <see cref="string"/>).</description></item>
    /// </list>
    /// <para>
    /// This restriction ensures deterministic equality and stable hashing behavior
    /// during serialization and deserialization.
    /// </para>
    /// </remarks>
    public DictionaryFormatter()
    {
        // ── Validate TKey ──────────────────────────────────────────────
        // Chỉ cho phép: primitive, string, enum, unmanaged struct
        Type keyType = typeof(TKey);
        if (keyType.IsClass && keyType != typeof(string))
        {
            throw new NotSupportedException(
                $"DictionaryFormatter: TKey='{keyType.Name}' is a class — only supports primitive, string, enum, or unmanaged struct as key.");
        }

        _keyFormatter = FormatterProvider.Get<TKey>();
        _valueFormatter = FormatterProvider.Get<TValue>();
    }

    // ------------------------------------------------------------------ //
    //  Serialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Serializes a dictionary into the specified <see cref="DataWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to which data will be written.</param>
    /// <param name="value">The dictionary to serialize. Can be <c>null</c>.</param>
    /// <exception cref="InvalidOperationException">Thrown when the target writer cannot expand or the key/value formatter cannot be resolved.</exception>
    /// <remarks>
    /// <para>
    /// Serialization behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>null</c> → writes <c>-1</c> as count.</description></item>
    /// <item><description>Empty dictionary → writes <c>0</c>.</description></item>
    /// <item><description>
    /// Otherwise writes count followed by serialized key-value pairs.
    /// </description></item>
    /// </list>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.Dictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.Expand(sizeof(int));
            FormatterProvider.Get<int>()
                             .Serialize(ref writer, -1);
            return;
        }

        int count = value.Count;
        writer.Expand(sizeof(int));
        FormatterProvider.Get<int>()
                         .Serialize(ref writer, count);

        if (count is 0)
        {
            return;
        }

        foreach (System.Collections.Generic.KeyValuePair<TKey, TValue> kvp in value)
        {
            _keyFormatter.Serialize(ref writer, kvp.Key);
            _valueFormatter.Serialize(ref writer, kvp.Value);
        }
    }

    // ------------------------------------------------------------------ //
    //  Deserialize
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Deserializes a dictionary from the specified <see cref="DataReader"/>.
    /// </summary>
    /// <param name="reader">The reader containing serialized data.</param>
    /// <returns>
    /// A reconstructed dictionary instance, or <c>null</c> if the input represents null.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the key or value formatter cannot be resolved.</exception>
    /// <exception cref="Common.Exceptions.SerializationException">Thrown when the reader does not contain enough bytes for the declared entries.</exception>
    /// <remarks>
    /// <para>
    /// Deserialization behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>-1</c> → returns <c>null</c>.</description></item>
    /// <item><description><c>0</c> → returns an empty dictionary.</description></item>
    /// <item><description>
    /// Otherwise reads key-value pairs sequentially.
    /// </description></item>
    /// </list>
    /// <para>
    /// The dictionary is initialized with the exact capacity to avoid resizing overhead.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<TKey, TValue>? Deserialize(ref DataReader reader)
    {
        int count = FormatterProvider.Get<int>()
                                              .Deserialize(ref reader);

        if (count == -1)
        {
            return null;
        }

        if (count < -1)
        {
            throw new Common.Exceptions.SerializationException("Dictionary count out of range.");
        }

        System.Collections.Generic.Dictionary<TKey, TValue> dict = new(count);

        for (int i = 0; i < count; i++)
        {
            TKey key = _keyFormatter.Deserialize(ref reader);
            dict[key] = _valueFormatter.Deserialize(ref reader);
        }

        return dict;
    }
}
