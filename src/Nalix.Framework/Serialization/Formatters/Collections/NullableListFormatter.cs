// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization.Internal;
using System.Runtime.InteropServices;

namespace Nalix.Framework.Serialization.Formatters.Collections;

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
    private static readonly IFormatter<T> s_elementFormatter = FormatterProvider.Get<T>();
    private static string DebuggerDisplay => $"NullableValueListFormatter<{typeof(T).FullName}?>";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T?> value)
    {
        if (value == null)
        {
            BufferPrimitives.WriteUInt16(ref writer, SerializerBounds.Null);
            return;
        }

        ushort count = (ushort)value.Count;
        BufferPrimitives.WriteUInt16(ref writer, count);

        if (count == 0)
        {
            return;
        }

        System.ReadOnlySpan<T?> span = CollectionsMarshal.AsSpan(value);

        for (int i = 0; i < span.Length; i++)
        {
            T? item = span[i];
            if (!item.HasValue)
            {
                BufferPrimitives.WriteByte(ref writer, 0);
                continue;
            }

            BufferPrimitives.WriteByte(ref writer, 1);
            s_elementFormatter.Serialize(ref writer, item.Value);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T?> Deserialize(ref DataReader reader)
    {
        ushort length = BufferPrimitives.ReadUInt16(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        System.Collections.Generic.List<T?> list = new(length);
        CollectionsMarshal.SetCount(list, length);
        System.Span<T?> span = CollectionsMarshal.AsSpan(list);

        for (int i = 0; i < span.Length; i++)
        {
            span[i] = BufferPrimitives.ReadByte(ref reader) == 0
                ? null
                : s_elementFormatter.Deserialize(ref reader);
        }

        return list;
    }
}
