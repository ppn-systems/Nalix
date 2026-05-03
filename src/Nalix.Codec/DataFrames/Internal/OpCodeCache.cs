// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;

namespace Nalix.Codec.DataFrames.Internal;

internal class OpCodeCache
{
    public static readonly ushort Handshake = (ushort)ProtocolOpCode.HANDSHAKE;
    public static readonly ushort SystemControl = (ushort)ProtocolOpCode.SYSTEM_CONTROL;
    public static readonly ushort SessionSignal = (ushort)ProtocolOpCode.SESSION_SIGNAL;
}
