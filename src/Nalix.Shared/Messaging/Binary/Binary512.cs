// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;

namespace Nalix.Shared.Messaging.Binary;

/// <inheritdoc/>
[MagicNumber(MagicNumbers.Binary512)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Binary512 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Binary512 : Binary128
{
    /// <inheritdoc/>
    public new const System.Int32 DynamicSize = 512;

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Binary512()
    {
        Data = [];
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)MagicNumbers.Binary512;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"Binary512(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
