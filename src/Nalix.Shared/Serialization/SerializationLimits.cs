namespace Nalix.Shared.Serialization;

/// <summary>
/// Constants used in serialization for representing special values.
/// </summary>
public static class SerializationLimits
{
    /// <summary>
    /// Represents a null value (System.UInt16.MaxValue = 65535).
    /// </summary>
    public const ushort Null = ushort.MaxValue;

    /// <summary>
    /// Maximum allowed array size (System.UInt16.MaxValue - 1 = 65534).
    /// </summary>
    public const ushort MaxArray = ushort.MaxValue - 1;

    /// <summary>
    /// Maximum allowed string length (System.UInt16.MaxValue - 2 = 65533).
    /// </summary>
    public const ushort MaxString = ushort.MaxValue - 2;
}
