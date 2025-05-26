using Nalix.Common.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class FieldCache<[
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] T>
{
    #region Static Fields

    private static readonly FieldSchema[] _metadata;
    private static readonly Dictionary<string, int> _fieldIndex;
    private static readonly SerializeLayout _layout;

    #endregion Static Fields

    #region Static Constructor

    static FieldCache()
    {
        _layout = GetSerializeLayout();
        _metadata = DiscoverFields();
        _fieldIndex = BuildFieldIndex();
        ValidateExplicitLayout();
    }

    #endregion Static Constructor

    #region Layout Detection

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        var packableAttr = typeof(T).GetCustomAttribute<SerializePackableAttribute>();
        return packableAttr?.SerializeLayout ?? SerializeLayout.Sequential;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout() => _layout;

    #endregion Layout Detection

    #region Query Interface - Public API

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

    #endregion Query Interface - Public API
}
