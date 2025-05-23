namespace Nalix.Serialization.Internal.Types;

/// <summary>
/// Represents the kind of type.
/// </summary>
internal enum TypeKind : byte
{
    /// <summary>
    /// No specific type assigned.
    /// </summary>
    None,

    /// <summary>
    /// Represents an unmanaged single-dimensional array.
    /// </summary>
    UnmanagedSZArray,

    /// <summary>
    /// Represents a fixed-size serializable type.
    /// </summary>
    FixedSizeSerializable
}
