// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for changing a <see cref="TcpSession"/>'s cipher algorithm dynamically.
/// </summary>
public static class CipherExtensions
{
    private static int s_cipherUpdateSeq;

    /// <summary>
    /// Changes the active cipher suite of the connection by synchronizing with the server.
    /// Both sides will switch immediately after the request is transmitted.
    /// </summary>
    /// <param name="session">The connected transport session.</param>
    /// <param name="cipherSuite">The new cipher suite to use.</param>
    /// <param name="timeoutMs">The timeout for acknowledging the switch in milliseconds.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
    /// <exception cref="NetworkException">Thrown if the session is not connected.</exception>
    public static async ValueTask UpdateCipherAsync(
        this TransportSession session,
        CipherSuiteType cipherSuite,
        int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        ushort seq = unchecked((ushort)Interlocked.Increment(ref s_cipherUpdateSeq));
        CipherSuiteType previousCipher = session.Options.Algorithm;

        // HACK: Payload Overloading.
        // We reuse the existing 'Control' packet to avoid creating a dedicated cipher packet.
        // Since CIPHER_UPDATE does not use the 'Reason' field, we safely cast our 1-byte 
        // CipherSuiteType into the 2-bytes ProtocolReason to carry it over the wire.
        Control req = session.NewControl((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.CIPHER_UPDATE)
            .WithSeq(seq)
            .WithReason((ProtocolReason)cipherSuite)
            .Build();

        // Use PacketAwaiter directly to tightly orchestrate the switch: 
        // 1. Subscribe Event
        // 2. Send (Encrypted with Old Cipher)
        // 3. Switch Local Session Algorithm (New Cipher)
        // 4. Await Server's ACK (Which will be encrypted with New Cipher)
        try
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: p => p.Type == ControlType.CIPHER_UPDATE_ACK && p.SequenceId == seq,
                timeoutMs: timeoutMs,
                sendAsync: async token =>
                {
                    await session.SendAsync(req, token).ConfigureAwait(false);
                    session.Options.Algorithm = cipherSuite;
                },
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            RestoreCipher(session, previousCipher);
            throw;
        }
    }

    /// <summary>
    /// Roll back the active cipher suite when a cipher update fails mid-flight.
    /// </summary>
    /// <param name="session">The client session whose cipher should be restored.</param>
    /// <param name="previousCipher">The cipher suite active before the update attempt.</param>
    /// <remarks>
    /// This keeps the client from drifting out of sync with the server when the ACK never arrives.
    /// </remarks>
    private static void RestoreCipher(TransportSession session, CipherSuiteType previousCipher)
    {
        if (session.Options.Algorithm != previousCipher)
        {
            session.Options.Algorithm = previousCipher;
        }
    }
}
