// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Tunneling.Protocols;

/// <summary>
/// Opcodes for the tunneling protocol.
/// Starting from 0x00FB (251) downwards.
/// </summary>
public enum TunnelOpCode : ushort
{
    TunnelProvide    = 0x00FB,
    TunnelConnect    = 0x00F9,
    TunnelReady      = 0x00F7
}
