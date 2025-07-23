using Nalix.Common.Packets.Attributes;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Represents a fully attributed packet descriptor used to define behavior and metadata
/// of network packets, such as operation code, timeout policy, rate limits, permission requirements,
/// and encryption strategy.
/// </summary>
/// <remarks>
/// This struct uses sequential layout and is optimized for performance in network dispatch systems.
/// All attributes are designed to be immutable for safe usage in high-throughput scenarios.
/// </remarks>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
public readonly struct PacketMetadata(
    PacketOpcodeAttribute opCode,
    PacketTimeoutAttribute? timeout,
    PacketPermissionAttribute? permission,
    PacketEncryptionAttribute? encryption)
{
    /// <summary>
    /// Gets the operation code attribute which uniquely identifies the type of packet.
    /// </summary>
    public readonly PacketOpcodeAttribute OpCode = opCode;

    /// <summary>
    /// Gets the optional timeout attribute which defines the time duration
    /// after which the packet operation is considered expired.
    /// </summary>
    public readonly PacketTimeoutAttribute? Timeout = timeout;

    /// <summary>
    /// Gets the optional permission attribute that specifies access control
    /// or authorization level required to handle this packet.
    /// </summary>
    public readonly PacketPermissionAttribute? Permission = permission;

    /// <summary>
    /// Gets the optional encryption attribute that defines the required
    /// encryption mechanism for this packet’s payload.
    /// </summary>
    public readonly PacketEncryptionAttribute? Encryption = encryption;
}