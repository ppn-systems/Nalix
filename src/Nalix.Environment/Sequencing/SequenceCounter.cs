// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Security;

namespace Nalix.Environment.Sequencing;

/// <summary>
/// Provides a thread-safe, monotonically increasing sequence number generator
/// for use with packet encryption per session or connection.
/// </summary>
/// <remarks>
/// <para>
/// This class is designed to be used separately for each communication direction
/// (send and receive) to prevent nonce/counter reuse attacks.
/// </para>
/// <list type="bullet">
/// <item>Starts at 1 by default (after first <see cref="Next"/> call)</item>
/// <item>Never repeats values for the lifetime of the instance</item>
/// <item>Fully thread-safe using <see cref="Interlocked"/> and <see cref="Volatile"/></item>
/// <item>Critical for security when using stream ciphers (ChaCha20, Salsa20, etc.)</item>
/// </list>
/// </remarks>
public struct SequenceCounter : ISequenceCounter
{
    private uint _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceCounter"/> class.
    /// </summary>
    /// <param name="initialValue">
    /// The initial value of the counter. 
    /// The first call to <see cref="Next"/> will return <paramref name="initialValue"/> + 1.
    /// Default is 0.
    /// </param>
    public SequenceCounter(uint initialValue = 0) => _value = initialValue;

    /// <summary>
    /// Returns the next sequence number and increments the counter atomically.
    /// </summary>
    /// <returns>The next monotonic sequence number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Next() => Interlocked.Increment(ref _value);

    /// <summary>
    /// Returns the current sequence number without incrementing it.
    /// </summary>
    /// <returns>The current value of the counter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly uint Current() => Volatile.Read(ref Unsafe.AsRef(in _value));

    /// <summary>
    /// Resets the counter to a new value.
    /// </summary>
    /// <param name="newValue">The new value to set.</param>
    /// <remarks>
    /// <b>Warning:</b> Should only be used when performing a full key rotation.
    /// Resetting without changing the key may open replay attack vectors.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(uint newValue = 0) => Volatile.Write(ref _value, newValue);

    /// <summary>
    /// Validates whether a received sequence number is valid (helps prevent replay attacks).
    /// </summary>
    /// <param name="receivedSeq">The sequence number received from the remote party.</param>
    /// <param name="window">
    /// Allowed reordering window. 
    /// Use a small value (e.g. 32–128) if your protocol allows out-of-order packets.
    /// Default is 0 (strictly monotonic).
    /// </param>
    /// <returns><c>true</c> if the sequence number is valid; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsValid(uint? receivedSeq, uint window = 0)
    {
        if (receivedSeq == null)
        {
            return true;
        }

        uint current = this.Current();

        if (receivedSeq == 0)
        {
            return current == 0;
        }

        if (current == 0)
        {
            return true;
        }

        return receivedSeq > current || (window > 0 && receivedSeq + window > current);
    }

    /// <summary>
    /// Updates the internal counter to the received sequence number if it is higher.
    /// Should be called after successfully decrypting and validating a packet.
    /// </summary>
    /// <param name="receivedSeq">The sequence number from a successfully decrypted packet.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateTo(uint receivedSeq)
    {
        uint current = this.Current();
        if (receivedSeq > current)
        {
            Volatile.Write(ref _value, receivedSeq);
        }
    }

    /// <summary>
    /// Resumes the counter from a previously saved value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResumeFrom(uint lastKnownSeq, uint safetyGap = 1000)
    {
        if (lastKnownSeq == 0)
        {
            return;
        }

        uint newValue = lastKnownSeq + safetyGap;
        uint current = this.Current();

        if (newValue > current)
        {
            Volatile.Write(ref _value, newValue);
        }
    }
}
