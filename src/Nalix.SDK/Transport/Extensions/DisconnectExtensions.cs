// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Protocols;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for gracefully disconnecting a <see cref="TcpSession"/>.
/// </summary>
public static class DisconnectExtensions
{
    /// <summary>
    /// Sends a graceful DISCONNECT control frame to the server, enabling it to clean up resources,
    /// and then closes the local connection.
    /// </summary>
    /// <param name="session">The connected transport session.</param>
    /// <param name="reason">The protocol reason code for the disconnect.</param>
    /// <param name="closeLocalConnection">If true, immediately closes the local socket after the network send is queued.</param>
    /// <param name="ct">A cancellation token for the send operation.</param>
    /// <returns>A task representing the async disconnect sequence.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
    public static async ValueTask DisconnectGracefullyAsync(
        this TcpSession session,
        ProtocolReason reason = ProtocolReason.NONE,
        bool closeLocalConnection = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsConnected)
        {
            try
            {
                // Send the DISCONNECT frame to alert the server to clean up instantly
                await session.SendControlAsync(
                    opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
                    type: ControlType.DISCONNECT,
                    configure: ctrl => ctrl.Reason = reason,
                    ct: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Best-effort graceful shutdown: cancellation during send is acceptable.
            }
            catch (ObjectDisposedException)
            {
                // Best-effort graceful shutdown: connection resources may already be disposed.
            }
            catch (InvalidOperationException)
            {
                // Best-effort graceful shutdown: session may no longer be in a sendable state.
            }
            catch (Nalix.Common.Exceptions.NetworkException)
            {
                // Best-effort graceful shutdown: transport may already be disconnected.
            }
        }

        // Drop the local session
        if (closeLocalConnection)
        {
            await session.DisconnectAsync().ConfigureAwait(false);
        }
    }
}
