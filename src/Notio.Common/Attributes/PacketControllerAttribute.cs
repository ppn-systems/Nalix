using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Marks a class as a packet controller responsible for handling packet commands.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PacketControllerAttribute(string name = null) : Attribute
{
    /// <summary>
    /// The name of the packet controller, used for logging and debugging.
    /// </summary>
    public string Name { get; } = name ?? "Unknown";
}
