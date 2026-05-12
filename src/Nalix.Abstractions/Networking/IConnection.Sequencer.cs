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
public interface IConnectionSequencer
{
    /// <summary>
    /// Gets the sequence counter for outgoing packets.
    /// </summary>
    ISequenceCounter SendSequence { get; }

    /// <summary>
    /// Gets the sequence counter for incoming packets.
    /// </summary>
    ISequenceCounter ReceiveSequence { get; }

    /// <summary>
    /// Resumes both send and receive sequence counters after reconnection.
    /// </summary>
    /// <param name="lastSendSeq">Last sent sequence before disconnect.</param>
    /// <param name="lastReceiveSeq">Last received sequence before disconnect.</param>
    void ResumeSequences(uint lastSendSeq = 0, uint lastReceiveSeq = 0);
}
