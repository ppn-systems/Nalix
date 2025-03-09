using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Attribute used to mark packet controllers responsible for handling packet commands.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PacketControllerAttribute : Attribute
{
}
