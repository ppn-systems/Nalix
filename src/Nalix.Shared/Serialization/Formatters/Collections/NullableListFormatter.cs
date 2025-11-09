// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Serializes/deserializes a List of nullable value-type elements (List&lt;T?&gt; where T : struct).
/// Writes a 1-byte null flag per element: 0 = null, 1 = present.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NullableValueListFormatter<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    : IFormatter<System.Collections.Generic.List<T?>>
    where T : struct
{
    private static System.String DebuggerDisplay => $"NullableValueListFormatter<{typeof(T).FullName}?>";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T?> value)
    {
        // List null?
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>().Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        // Write length
        System.UInt16 count = (System.UInt16)value.Count;
        FormatterProvider.Get<System.UInt16>().Serialize(ref writer, count);

        if (count == 0)
        {
            return;
        }

        // Per-element: write null flag then payload (if any)
        var byteFormatter = FormatterProvider.Get<System.Byte>();
        var elemFormatter = FormatterProvider.Get<T>();

        for (System.Int32 i = 0; i < count; i++)
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
        System.UInt16 length = FormatterProvider.Get<System.UInt16>().Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        var byteFormatter = FormatterProvider.Get<System.Byte>();
        var elemFormatter = FormatterProvider.Get<T>();

        System.Collections.Generic.List<T?> list = new(length);
        for (System.Int32 i = 0; i < length; i++)
        {
            System.Byte flag = byteFormatter.Deserialize(ref reader);
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
    private static System.String DebuggerDisplay => $"NullableRefListFormatter<{typeof(T).FullName}?>";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T?> value)
    {
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>().Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        System.UInt16 count = (System.UInt16)value.Count;
        FormatterProvider.Get<System.UInt16>().Serialize(ref writer, count);

        if (count == 0)
        {
            return;
        }

        var byteFormatter = FormatterProvider.Get<System.Byte>();
        var elemFormatter = FormatterProvider.Get<T>();

        for (System.Int32 i = 0; i < count; i++)
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
        System.UInt16 length = FormatterProvider.Get<System.UInt16>().Deserialize(ref reader);

        if (length == SerializerBounds.Null)
        {
            return null!;
        }

        if (length == 0)
        {
            return [];
        }

        var byteFormatter = FormatterProvider.Get<System.Byte>();
        var elemFormatter = FormatterProvider.Get<T>();

        System.Collections.Generic.List<T?> list = new(length);
        for (System.Int32 i = 0; i < length; i++)
        {
            System.Byte flag = byteFormatter.Deserialize(ref reader);
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
