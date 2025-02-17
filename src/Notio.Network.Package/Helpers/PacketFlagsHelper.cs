using Notio.Network.Package.Enums;
using System.Collections.Generic;

namespace Notio.Network.Package.Helpers;

/// <summary>
/// Provides helper methods for working with PacketFlags and byte flags.
/// </summary>
public class PacketFlagsHelper
{
    /// <summary>
    /// Converts the PacketFlags to a human-readable string.
    /// </summary>
    public static string ToReadableString(PacketFlags flags)
    {
        var flagStrings = new List<string>();
        
        if (flags.HasFlag(PacketFlags.IsCompressed)) flagStrings.Add("IsCompressed");
        if (flags.HasFlag(PacketFlags.IsFragmented)) flagStrings.Add("IsFragmented");
        if (flags.HasFlag(PacketFlags.AckRequired)) flagStrings.Add("AckRequired");
        if (flags.HasFlag(PacketFlags.IsEncrypted)) flagStrings.Add("IsEncrypted");

        return flagStrings.Count > 0 ? string.Join(", ", flagStrings) : "None";
    }

    /// <summary>
    /// Converts the PacketFlags to a human-readable string.
    /// </summary>
    public static string ToReadableString(byte flags)
        => ToReadableString((PacketFlags)flags);
}
