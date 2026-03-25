// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Serializes/deserializes a List of nullable value-type elements (List&lt;T?&gt; where T : struct).
/// Writes a 1-byte null flag per element: 0 = null, 1 = present.
/// </summary>
/// <typeparam name="T"></typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableValueListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    : IFormatter<System.Collections.Generic.List<T?>> where T : struct
{
    private static string DebuggerDisplay => $"NullableValueListFormatter<{typeof(T).FullName}?>";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T?> value)
    {
        // List null?
        if (value == null)
        {
            writer.Expand(sizeof(ushort));
            FormatterProvider.Get<ushort>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        // Write length
        writer.Expand(sizeof(ushort));
        ushort count = (ushort)value.Count;
        FormatterProvider.Get<ushort>().Serialize(ref writer, count);

        if (count == 0)
        {
            return;
        }

        // Per-element: write null flag then payload (if any)
        IFormatter<byte> byteFormatter = FormatterProvider.Get<byte>();
        IFormatter<T> elemFormatter = FormatterProvider.Get<T>();

        for (int i = 0; i < count; i++)
        {
            T? item = value[i];
            if (!item.HasValue)
            {
                byteFormatter.Serialize(ref writer, 0); // null
                continue;
            }

            byteFormatter.Serialize(ref writer, 1);     // has value
            elemFormatter.Serialize(ref writer, item.Value);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T?> Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>().Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        IFormatter<byte> byteFormatter = FormatterProvider.Get<byte>();
        IFormatter<T> elemFormatter = FormatterProvider.Get<T>();

        System.Collections.Generic.List<T?> list = new(length);
        for (int i = 0; i < length; i++)
        {
            byte flag = byteFormatter.Deserialize(ref reader);
            if (flag == 0)
            {
                list.Add(null);
            }
            else
            {
                T value = elemFormatter.Deserialize(ref reader);
                list.Add(value);
            }
        }

        return list;
    }
}

/// <summary>
/// Serializes/deserializes a List of nullable reference-type elements (List&lt;T?&gt; where T : class).
/// Writes a 1-byte null flag per element: 0 = null, 1 = present.
/// </summary>
/// <typeparam name="T"></typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableRefListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    : IFormatter<System.Collections.Generic.List<T?>>
    where T : class
{
    private static string DebuggerDisplay => $"NullableRefListFormatter<{typeof(T).FullName}?>";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T?> value)
    {
        if (value == null)
        {
            FormatterProvider.Get<ushort>().Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        ushort count = (ushort)value.Count;
        FormatterProvider.Get<ushort>().Serialize(ref writer, count);

        if (count == 0)
        {
            return;
        }

        IFormatter<byte> byteFormatter = FormatterProvider.Get<byte>();
        IFormatter<T> elemFormatter = FormatterProvider.Get<T>();

        for (int i = 0; i < count; i++)
        {
            T? item = value[i];
            if (item is null)
            {
                byteFormatter.Serialize(ref writer, 0); // null
                continue;
            }

            byteFormatter.Serialize(ref writer, 1);     // has value
            elemFormatter.Serialize(ref writer, item);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T?> Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>().Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        IFormatter<byte> byteFormatter = FormatterProvider.Get<byte>();
        IFormatter<T> elemFormatter = FormatterProvider.Get<T>();

        System.Collections.Generic.List<T?> list = new(length);
        for (int i = 0; i < length; i++)
        {
            byte flag = byteFormatter.Deserialize(ref reader);
            if (flag == 0)
            {
                list.Add(null);
            }
            else
            {
                T value = elemFormatter.Deserialize(ref reader);
                list.Add(value);
            }
        }

        return list;
    }
}
