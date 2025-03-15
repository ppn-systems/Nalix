using Notio.Common.Package;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides extension methods for working with PacketFlags and byte flags.
/// </summary>
public static class PacketFlagsExtensions
{
    /// <summary>
    /// Determines whether the flags contain the specified flag.
    /// </summary>
    public static bool HasFlag(this PacketFlags flags, PacketFlags flag)
        => (flags & flag) == flag;

    /// <summary>
    /// Determines whether the byte flags contain the specified flag.
    /// </summary>
    public static bool HasFlag(this byte flags, PacketFlags flag)
        => (flags & (byte)flag) == (byte)flag;

    /// <summary>
    /// Adds the specified flag to the flags.
    /// </summary>
    public static PacketFlags AddFlag(this PacketFlags flags, PacketFlags flag)
        => flags | flag;

    /// <summary>
    /// Adds the specified flag to the byte flags.
    /// </summary>
    public static byte AddFlag(this byte flags, PacketFlags flag)
        => (byte)(flags | (byte)flag);

    /// <summary>
    /// Removes the specified flag from the flags.
    /// </summary>
    public static PacketFlags RemoveFlag(this PacketFlags flags, PacketFlags flag)
        => flags & ~flag;

    /// <summary>
    /// Removes the specified flag from the byte flags.
    /// </summary>
    public static byte RemoveFlag(this byte flags, PacketFlags flag)
        => (byte)(flags & ~(byte)flag);

    /// <summary>
    /// Determines whether the flags are set to None.
    /// </summary>
    public static bool IsNone(this PacketFlags flags)
        => flags == PacketFlags.None;

    /// <summary>
    /// Determines whether the byte flags are set to None.
    /// </summary>
    public static bool IsNone(this byte flags)
        => flags == (byte)PacketFlags.None;

    /// <summary>
    /// Determines whether the flags match the required flags and do not contain the excluded flags.
    /// </summary>
    public static bool Matches(this PacketFlags flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => (flags & requiredFlags) == requiredFlags && (flags & excludedFlags) == PacketFlags.None;

    /// <summary>
    /// Determines whether the byte flags match the required flags and do not contain the excluded flags.
    /// </summary>
    public static bool Matches(this byte flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => (flags & (byte)requiredFlags) == (byte)requiredFlags && (flags & (byte)excludedFlags) == 0;
}
