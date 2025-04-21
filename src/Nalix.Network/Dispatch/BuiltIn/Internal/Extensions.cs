using Nalix.Common.Connection;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Dispatch.BuiltIn.Internal;

/// <summary>
/// Provides extension methods for packet operations.
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// Sends a string packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    internal static bool SendString(this IConnection connection, PacketCode code, string message)
      => connection.SendAsync(PacketWriter.String(code, message)).Result;

    /// <summary>
    /// Sends a string packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    internal static bool SendCode(this IConnection connection, PacketCode code)
      => connection.SendAsync(PacketWriter.String(code)).Result;
}
