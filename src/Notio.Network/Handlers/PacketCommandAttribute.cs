using Notio.Common.Models;
using System;

namespace Notio.Network.Handlers;

/// <summary>
/// Attribute to define a packet command and its required authority level.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PacketCommandAttribute(ushort command, Authoritys authoritys = Authoritys.User) : Attribute
{
    /// <summary>
    /// The unique command identifier for the packet.
    /// </summary>
    public ushort CommandId { get; } = command;

    /// <summary>
    /// The minimum authority level required to execute this command.
    /// </summary>
    public Authoritys RequiredAuthority { get; } = authoritys;
}
