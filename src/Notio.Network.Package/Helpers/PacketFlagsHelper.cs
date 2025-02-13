using Notio.Network.Package.Enums;
using System.Collections.Generic;

namespace Notio.Network.Package.Helpers;

public class PacketFlagsHelper
{
    /// <summary>
    /// Converts the PacketFlags to a human-readable string.
    /// </summary>
    public static string ToReadableString(PacketFlags flags)
    {
        var flagStrings = new List<string>();

        if (flags.HasFlag(PacketFlags.AckRequired)) flagStrings.Add("AckRequired");
        if (flags.HasFlag(PacketFlags.IsAcknowledged)) flagStrings.Add("IsAcknowledged");
        if (flags.HasFlag(PacketFlags.IsCompressed)) flagStrings.Add("IsCompressed");
        if (flags.HasFlag(PacketFlags.IsEncrypted)) flagStrings.Add("IsEncrypted");
        if (flags.HasFlag(PacketFlags.IsReliable)) flagStrings.Add("IsReliable");
        if (flags.HasFlag(PacketFlags.IsFragmented)) flagStrings.Add("IsFragmented");
        if (flags.HasFlag(PacketFlags.IsStream)) flagStrings.Add("IsStream");
        if (flags.HasFlag(PacketFlags.IsSigned)) flagStrings.Add("IsSigned");

        return flagStrings.Count > 0 ? string.Join(", ", flagStrings) : "None";
    }

    public static string ToReadableString(byte flags)
        => ToReadableString((PacketFlags)flags);
}
