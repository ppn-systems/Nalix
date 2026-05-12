// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Nalix.Abstractions.Security;

/// <summary>
/// Defines a contract for a thread-safe monotonically increasing sequence counter.
/// </summary>
/// <remarks>
/// Used to ensure unique nonces and counters for encryption, preventing replay attacks
/// and counter reuse in stream ciphers (ChaCha20, Salsa20, etc.).
/// </remarks>
public interface ISequenceCounter
{
    /// <summary>
    /// Returns the next sequence number and increments the counter atomically.
    /// </summary>
    uint Next();

    /// <summary>
    /// Returns the current sequence number without incrementing it.
    /// </summary>
    uint Current();

    /// <summary>
    /// Resets the counter to a specified value.
    /// </summary>
    /// <remarks>Should only be used when the encryption key is rotated.</remarks>
    void Reset(uint newValue = 0);

    /// <summary>
    /// Checks if a received sequence number is valid (anti-replay protection).
    /// </summary>
    /// <param name="receivedSeq">Sequence number from the incoming packet.</param>
    /// <param name="window">Allowed reordering window (0 = strict monotonic).</param>
    bool IsValid(uint? receivedSeq, uint window = 0);

    /// <summary>
    /// Updates the counter to the received sequence if it is higher.
    /// Should be called after successful decryption.
    /// </summary>
    void UpdateTo(uint receivedSeq);

    /// <summary>
    /// Resumes the counter from a previously saved value (used when client reconnects).
    /// </summary>
    /// <param name="lastKnownSeq">Last sequence number used before disconnect.</param>
    /// <param name="safetyGap">Extra gap to prevent collision after resume (default 1000).</param>
    void ResumeFrom(uint lastKnownSeq, uint safetyGap = 1000);
}
