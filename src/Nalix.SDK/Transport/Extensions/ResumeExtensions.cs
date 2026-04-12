// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

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
    public static async Task<bool> TryResumeAsync(this TcpSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsConnected)
        {
            throw new InvalidOperationException("Session must be connected to perform resume.");
        }

        if (!CanResume(session.Options))
        {
            return false;
        }

        SessionResume request = new();
        request.Initialize(SessionResumeStage.REQUEST, session.Options.SessionToken);

        try
        {
            SessionResume response = await PacketAwaiter.AwaitAsync<SessionResume>(
                session,
                predicate: static packet => packet.Stage == SessionResumeStage.RESPONSE,
                timeoutMs: session.Options.ResumeTimeoutMillis,
                sendAsync: token => session.SendAsync(request, encrypt: false, token),
                ct).ConfigureAwait(false);

            if (response.Reason != ProtocolReason.NONE)
            {
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
    public static async Task<bool> ConnectAndResumeOrHandshakeAsync(
        this TcpSession session,
        string? host = null,
        ushort? port = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ConnectAsync(host, port, ct).ConfigureAwait(false);

        if (session.Options.ResumeEnabled && CanResume(session.Options))
        {
            bool resumed = await session.TryResumeAsync(ct).ConfigureAwait(false);
            if (resumed)
            {
                return true;
            }

            if (!session.Options.ResumeFallbackToHandshake)
            {
                throw new NetworkException("Session resume failed and handshake fallback is disabled.");
            }

            if (!session.IsConnected)
            {
                await session.ConnectAsync(host, port, ct).ConfigureAwait(false);
            }
        }

        bool previousEncryption = session.Options.EncryptionEnabled;
        session.Options.EncryptionEnabled = false;

        try
        {
            await session.HandshakeAsync(ct).ConfigureAwait(false);
            return false;
        }
        finally
        {
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
    private static bool CanResume(TransportOptions options) => !options.SessionToken.IsEmpty && options.Secret is { Length: > 0 };
}
