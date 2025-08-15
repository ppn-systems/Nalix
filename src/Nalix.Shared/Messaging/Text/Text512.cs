// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;

namespace Nalix.Shared.Messaging.Text;

/// <inheritdoc/>
[MagicNumber(MagicNumbers.Binary512)]
public sealed class Text512 : Text256
{
    /// <inheritdoc/>
    public new const System.Int32 DynamicSize = 512;

    /// <summary>Initializes a new <see cref="Text512"/> with empty content.</summary>
    public Text512()
    {
        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)MagicNumbers.Text512;
    }
}
