using System;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<FieldSchema> GetFields() => _metadata.AsSpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetFieldCount() => _metadata.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(int index) => _metadata[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(string fieldName)
    {
        if (_fieldIndex.TryGetValue(fieldName, out var index))
        {
            return _metadata[index];
        }

        ThrowFieldNotFound(fieldName);
        return default; // Never reached
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasField(string fieldName) => _fieldIndex.ContainsKey(fieldName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Type GetFieldType(string fieldName) => GetField(fieldName).FieldType;
}
