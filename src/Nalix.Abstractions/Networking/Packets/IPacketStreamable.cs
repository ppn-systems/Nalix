// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Defines a packet that is part of a multi-part stream or live feed.
/// Packets implementing this interface can be consumed via SDK streaming extensions.
/// </summary>
public interface IPacketStreamable
{
    /// <summary>
    /// Gets or sets a value indicating whether this packet is the final chunk in the stream.
    /// When true, the SDK will automatically complete the IAsyncEnumerable channel.
    /// </summary>
    bool IsEndOfStream { get; set; }
}
