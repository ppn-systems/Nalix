// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace DDoS.Models;

/// <summary>
/// Định nghĩa các loại packet có thể gửi
/// </summary>
public enum PacketType
{
    /// <summary>
    /// Text packet 256 bytes
    /// </summary>
    Text256,

    /// <summary>
    /// Text packet 512 bytes  
    /// </summary>
    Text512,

    /// <summary>
    /// Text packet 1024 bytes
    /// </summary>
    Text1024,

    /// <summary>
    /// Control packet for system commands
    /// </summary>
    Control,

    /// <summary>
    /// Directive packet for instructions
    /// </summary>
    Directive,

    /// <summary>
    /// Handshake packet for connection setup
    /// </summary>
    Handshake
}