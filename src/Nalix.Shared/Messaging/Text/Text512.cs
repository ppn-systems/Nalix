// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;

namespace Nalix.Shared.Messaging.Text;

/// <inheritdoc/>
[MagicNumber(MagicNumbers.Text512)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Text512 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
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

    /// <inheritdoc/>
    public override System.String ToString()
        => $"Text512(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Transport={Transport}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
