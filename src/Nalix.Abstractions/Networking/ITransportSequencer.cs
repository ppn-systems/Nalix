// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Security;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides sequence number management for secure packet encryption/decryption.
/// </summary>
/// <remarks>
/// Any connection that wants strong anti-replay protection and proper nonce management
/// should implement this interface in addition to <see cref="IConnection"/>.
/// </remarks>
public interface ITransportSequencer
{
    /// <summary>
    /// Gets the sequence counter for outgoing packets.
    /// </summary>
    ISequenceCounter SendSequence { get; }

    /// <summary>
    /// Gets the sequence counter for incoming packets.
    /// </summary>
    ISequenceCounter ReceiveSequence { get; }
}
