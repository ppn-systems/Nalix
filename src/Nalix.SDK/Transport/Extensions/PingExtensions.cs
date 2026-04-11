// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Time;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for performing PING operations over a <see cref="TcpSession"/>.
/// </summary>
public static class PingExtensions
{
    private static int s_pingSequence;

    /// <summary>
    /// Sends a PING control packet to the server and awaits a PONG response.
    /// </summary>
    /// <param name="session">The connected transport session.</param>
    /// <param name="timeoutMs">The maximum time to wait for a PONG response, in milliseconds.</param>
    /// <param name="ct">A cancellation token that can be used to abort the ping process.</param>
    /// <returns>The measured round-trip time (RTT) in milliseconds.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
    /// <exception cref="NetworkException">Thrown if the session is not connected.</exception>
    /// <exception cref="TimeoutException">Thrown if no PONG response is received within the specified timeout.</exception>
    public static async Task<double> PingAsync(this TcpSession session, int timeoutMs = 5000, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        uint seq = unchecked((uint)Interlocked.Increment(ref s_pingSequence));

        // Use NewControl fluent builder which also handles Timestamp/MonoTicks setup
        Control ping = session.NewControl((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING).WithSeq(seq).Build();
        long startTicks = ping.MonoTicks;

        _ = await session.RequestAsync<Control>(
            ping,
            options: RequestOptions.Default.WithTimeout(timeoutMs),
            predicate: p => p.Type == ControlType.PONG && p.SequenceId == seq,
            ct: ct).ConfigureAwait(false);

        long endTicks = Clock.MonoTicksNow();

        return Clock.MonoTicksToMilliseconds(endTicks - startTicks);
    }
}
