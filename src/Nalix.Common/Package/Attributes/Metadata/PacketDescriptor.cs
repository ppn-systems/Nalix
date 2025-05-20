namespace Nalix.Common.Package.Attributes.Metadata;

/// <summary>
/// Represents a set of attributes that define metadata and behavior for a packet.
/// </summary>
/// <param name="Opcode">The unique identifier attribute for the packet type.</param>
/// <param name="Timeout">The optional timeout attribute specifying how long to wait for a response.</param>
/// <param name="RateLimit">The optional rate limit attribute that limits how frequently the packet can be sent.</param>
/// <param name="Permission">The optional permission attribute indicating required privileges to handle the packet.</param>
/// <param name="Encryption">The optional encryption attribute that specifies whether the packet should be encrypted.</param>
public record PacketDescriptor(
    PacketOpcodeAttribute Opcode,
    PacketTimeoutAttribute Timeout,
    PacketRateLimitAttribute RateLimit,
    PacketPermissionAttribute Permission,
    PacketEncryptionAttribute Encryption
);
