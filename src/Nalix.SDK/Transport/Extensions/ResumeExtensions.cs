// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Security.Hashing;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides reconnect and session resume helpers for <see cref="TcpSession"/>.
/// </summary>
public static class ResumeExtensions
{
    /// <summary>
    /// Attempts to resume the existing session state on an already connected TCP session.
    /// </summary>
    /// <param name="session">The connected TCP session to resume.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns><see langword="true"/> when the server accepted the resume request; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> ResumeSessionAsync(this TcpSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsConnected)
        {
            throw new InvalidOperationException("Session must be connected to perform resume.");
        }

        if (!HasResumeState(session.Options))
        {
            return false;
        }

        SessionResume request = new();
        request.Initialize(SessionResumeStage.REQUEST, session.Options.SessionToken);

        // SEC-16: Compute proof-of-possession using HMAC-SHA256(Secret, SessionToken).
        // This proves to the server that we own the session secret.
        Span<byte> proofBytes = stackalloc byte[32];
        Span<byte> tokenBytes = stackalloc byte[8];
        _ = session.Options.SessionToken.TryWriteBytes(tokenBytes);

        // SEC-16: Use fast HMAC instead of slow PBKDF2 for session resumption.
        HmacKeccak256.Compute(session.Options.Secret, tokenBytes, proofBytes);
        request.Proof = new Fixed256(proofBytes);

        try
        {
            SessionResume response = await PacketAwaiter.AwaitAsync<SessionResume>(
                session,
                predicate: packet => packet.Stage == SessionResumeStage.RESPONSE && packet.SessionToken == request.SessionToken,
                timeoutMs: session.Options.ResumeTimeoutMillis,
                sendAsync: token => session.SendAsync(request, encrypt: false, token),
                ct).ConfigureAwait(false);

            if (response.Reason != ProtocolReason.NONE)
            {
                session.Options.SessionToken = response.SessionToken;
                return false;
            }

            session.Options.SessionToken = response.SessionToken;
            session.Options.EncryptionEnabled = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (NetworkException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Connects the session, then tries resume first and falls back to a full handshake when allowed.
    /// </summary>
    /// <param name="session">The TCP session to connect and resume.</param>
    /// <param name="host">The optional host override.</param>
    /// <param name="port">The optional port override.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns><see langword="true"/> when resume succeeded; <see langword="false"/> when a fresh handshake was used.</returns>
    public static async Task<bool> ConnectWithResumeAsync(
        this TcpSession session,
        string? host = null,
        ushort? port = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ConnectAsync(host, port, ct).ConfigureAwait(false);

        if (session.Options.ResumeEnabled && HasResumeState(session.Options))
        {
            bool resumed = await session.ResumeSessionAsync(ct).ConfigureAwait(false);
            if (resumed)
            {
                return true;
            }

            if (!session.Options.ResumeFallbackToHandshake)
            {
                throw new NetworkException("Session resume failed and handshake fallback is disabled.");
            }

            if (session.IsConnected)
            {
                await session.DisconnectAsync().ConfigureAwait(false);
            }

            await session.ConnectAsync(host, port, ct).ConfigureAwait(false);
        }

        // Use explicit encrypt=false for the handshake send without mutating the shared
        // EncryptionEnabled flag — this avoids a race condition where concurrent SendAsync
        // calls could see a temporarily-disabled encryption state (SEC-06).
        bool previousEncryption = session.Options.EncryptionEnabled;

        try
        {
            await session.HandshakeAsync(ct).ConfigureAwait(false);
            return false;
        }
        finally
        {
            // Ensure encryption is restored/enabled after handshake completes.
            // HandshakeAsync itself may enable encryption; only restore if it didn't.
            if (!session.Options.EncryptionEnabled)
            {
                session.Options.EncryptionEnabled = previousEncryption && session.Options.Secret.Length > 0;
            }
        }
    }

    /// <summary>
    /// Checks whether the session has enough state to attempt resume.
    /// </summary>
    /// <param name="options">The transport options to inspect.</param>
    /// <returns><see langword="true"/> when the session has a token and secret.</returns>
    private static bool HasResumeState(TransportOptions options) => !options.SessionToken.IsEmpty && options.Secret is { Length: > 0 };
}
