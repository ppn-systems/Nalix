// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.ProtocolFrames;
using Nalix.LoadTester.Contracts;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.LoadTester.Scenarios;

internal sealed class DdosControlScenario : ILoadScenario
{
    private readonly RequestOptions _requestOptions;
    private Int32 _sequence;

    public DdosControlScenario(Int32 timeoutMs)
    {
        _requestOptions = RequestOptions.Default.WithTimeout(timeoutMs);
    }

    public String Name => "ddos-control";

    public async ValueTask<Double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        UInt16 sequence = unchecked((UInt16)Interlocked.Increment(ref _sequence));
        Stopwatch stopwatch = Stopwatch.StartNew();

        Control packet = new Control();
        packet.Initialize(
            opCode: 0x0111,
            type: ControlType.FAIL,
            sequenceId: sequence,
            flags: Nalix.Abstractions.Networking.Packets.PacketFlags.SYSTEM | Nalix.Abstractions.Networking.Packets.PacketFlags.RELIABLE,
            reasonCode: ProtocolReason.NONE);

        try
        {
            // We just send without awaiting response to simulate DDOS
            await session.SendAsync(packet, ct: cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }
        catch
        {
            stopwatch.Stop();
            throw;
        }
    }
}
