using Notio.Packets.Enums;

namespace Notio.Packets.Helpers;

public static class PacketFlagsHelper
{
    public static bool HasFlag(this PacketFlags flags, PacketFlags flag) => flags.HasFlag(flag);
    public static bool HasFlag(this byte flags, PacketFlags flag) => ((PacketFlags)flags).HasFlag(flag);

    public static PacketFlags AddFlag(this PacketFlags flags, PacketFlags flag) => flags | flag;
    public static byte AddFlag(this byte flags, PacketFlags flag) => (byte)(flags | (byte)flag);

    public static PacketFlags RemoveFlag(this PacketFlags flags, PacketFlags flag) => flags & ~flag;
    public static byte RemoveFlag(this byte flags, PacketFlags flag) => (byte)(flags & ~(byte)flag);

    public static bool IsNone(this PacketFlags flags) => flags == PacketFlags.None;
    public static bool IsNone(this byte flags) => flags == (byte)PacketFlags.None;

    public static string ToReadableString(this PacketFlags flags) => flags == PacketFlags.None ? "None" : flags.ToString();
    public static string ToReadableString(this byte flags) => ((PacketFlags)flags).ToReadableString();

    public static bool Matches(this PacketFlags flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => flags.HasFlag(requiredFlags) && !flags.HasFlag(excludedFlags);

    public static bool Matches(this byte flags, PacketFlags requiredFlags, PacketFlags excludedFlags = PacketFlags.None)
        => flags.HasFlag(requiredFlags) && !flags.HasFlag(excludedFlags);
}