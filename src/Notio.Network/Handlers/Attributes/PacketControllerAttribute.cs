using System;

namespace Notio.Network.Handlers.Attributes;

/// <summary>
/// Attribute used to mark packet controllers responsible for handling packet commands.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PacketControllerAttribute : Attribute
{
}
