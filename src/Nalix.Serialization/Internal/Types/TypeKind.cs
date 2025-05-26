namespace Nalix.Serialization.Internal.Types;

/// <summary>
/// Represents the kind of type.
/// </summary>
internal enum TypeKind : byte
{
    /// <summary>
    /// No specific type assigned.
    /// </summary>
    None = 0,

    /// <summary>
    /// Represents an unmanaged single-dimensional array.
    /// </summary>
    UnmanagedSZArray = 1,

    /// <summary>
    /// Represents a fixed-size serializable type.
    /// </summary>
    FixedSizeSerializable = 2,

    /// <summary>
    /// Represents a composite serializable type, which may include nested types or complex structures.
    /// </summary>
    CompositeSerializable = 3
}
