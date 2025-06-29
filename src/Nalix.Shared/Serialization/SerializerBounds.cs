namespace Nalix.Shared.Serialization;

/// <summary>
/// Constants used in serialization for representing special values.
/// </summary>
public static class SerializerBounds
{
    /// <summary>
    /// Represents a null value (System.UInt16.MaxValue = 65535).
    /// </summary>
    public const System.UInt16 Null = System.UInt16.MaxValue;

    /// <summary>
    /// Maximum allowed array size (System.UInt16.MaxValue - 1 = 65534).
    /// </summary>
    public const System.UInt16 MaxArray = System.UInt16.MaxValue - 1;

    /// <summary>
    /// Maximum allowed string length (System.UInt16.MaxValue - 2 = 65533).
    /// </summary>
    public const System.UInt16 MaxString = System.UInt16.MaxValue - 2;
}
