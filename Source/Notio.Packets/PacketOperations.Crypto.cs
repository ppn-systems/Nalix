using Notio.Packets.Enums;
using Notio.Packets.Helpers;
using Notio.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Notio.Packets;

public static partial class PacketOperations
{
    /// <summary>
    /// Mã hóa Payload trong Packet và cập nhật trực tiếp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet EncryptPayload(this in Packet packet, byte[] key)
        => new(
            packet.Type,
            packet.Flags.AddFlag(PacketFlags.IsEncrypted),
            packet.Command,
            AesCTR.Encrypt(key, packet.Payload.Span)
        );
    
}
