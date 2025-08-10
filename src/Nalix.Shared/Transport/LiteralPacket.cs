using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Security.Cryptography;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Extensions;
using Nalix.Shared.LZ4.Extensions;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Transport;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class LiteralPacket : IPacket, IPacketTransformer<LiteralPacket>
{
    /// <summary>Gets the total serialized length in bytes, including header and content.</summary>
    [SerializeIgnore]
    public System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + System.Text.Encoding.UTF8.GetByteCount(Content ?? System.String.Empty));

    /// <summary>Gets the magic number used to identify the packet format.</summary>
    [SerializeOrder(0)]
    public static System.UInt32 MagicNumber { get; set; }

    /// <summary>Gets the operation code (OpCode) of this packet.</summary>
    [SerializeOrder(4)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>Gets the flags associated with this packet.</summary>
    [SerializeOrder(6)]
    public PacketFlags Flags { get; set; }

    /// <summary>Gets the packet priority.</summary>
    [SerializeOrder(7)]
    public PacketPriority Priority { get; set; }

    /// <summary>Gets the transport protocol (e.g., TCP/UDP) this packet targets.</summary>
    [SerializeOrder(8)]
    public TransportProtocol Transport { get; set; }

    /// <summary>Gets or sets the UTF-8 string content of the packet.</summary>
    [SerializeOrder(9)]
    [SerializeDynamicSize(1024)]
    public System.String Content { get; set; }

    static LiteralPacket() => MagicNumber = (System.UInt32)PacketMagicNumbers.LiteralPacket;

    /// <summary>Initializes a new <see cref="LiteralPacket"/> with empty content.</summary>
    public LiteralPacket()
    {
        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
    }

    /// <summary>Initializes the packet with the specified string content.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    public void Initialize(System.String content) => Initialize(content, TransportProtocol.Null);

    /// <summary>Initializes the packet with content and transport protocol.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(System.String content, TransportProtocol transport = TransportProtocol.Tcp)
    {
        this.Transport = transport;
        this.Content = content ?? System.String.Empty;
    }

    /// <summary>Serializes this packet to a newly allocated byte array.</summary>
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>Serializes this packet into the provided destination buffer.</summary>
    /// <param name="buffer">The destination buffer. Must be large enough.</param>
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a <see cref="LiteralPacket"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="LiteralPacket"/> instance.</returns>
    public static LiteralPacket Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        LiteralPacket packet = ObjectPoolManager.Instance.Get<LiteralPacket>();
        System.Int32 bytesRead = LiteSerializer.Deserialize<LiteralPacket>(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <summary>
    /// Encrypts the packet content.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Encryption is handled by the pipeline.", error: true)]
    public static LiteralPacket Encrypt(LiteralPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Decrypts the packet content.
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static LiteralPacket Decrypt(LiteralPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Compresses the packet content (UTF-8 → LZ4 → Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static LiteralPacket Compress(LiteralPacket packet)
    {
        if (packet?.Content == null)
        {
            throw new System.ArgumentNullException(nameof(packet));
        }

        packet.Content = packet.Content.CompressToBase64();
        _ = packet.Flags.AddFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>
    /// Decompresses the packet content (Base64 → LZ4 → UTF-8).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static LiteralPacket Decompress(LiteralPacket packet)
    {
        if (packet?.Content == null)
        {
            throw new System.ArgumentNullException(nameof(packet));
        }

        packet.Content = packet.Content.DecompressFromBase64();
        _ = packet.Flags.RemoveFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>Resets this instance to its default state for pooling reuse.</summary>
    public void ResetForPool()
    {
        this.Flags = PacketFlags.None;
        this.Content = System.String.Empty;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"LiteralPacket(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Transport={Transport}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
