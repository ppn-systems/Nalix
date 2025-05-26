using Nalix.Common.Serialization;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Static Fields

    private static readonly FieldSchema[] _metadata;
    private static readonly SerializeLayout _layout;
    private static readonly Dictionary<string, int> _fieldIndex;

    #endregion Static Fields

    #region Static Constructor

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    static FieldCache()
    {
        _layout = GetSerializeLayout();
        _metadata = DiscoverFields<T>();
        _fieldIndex = BuildFieldIndex();
        ValidateExplicitLayout();
    }

    #endregion Static Constructor

    #region Layout Detection

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        SerializePackableAttribute packableAttr = typeof(T).GetCustomAttribute<SerializePackableAttribute>();
        return packableAttr?.SerializeLayout ?? SerializeLayout.Sequential;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout() => _layout;

    #endregion Layout Detection
}
