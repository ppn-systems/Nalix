// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.ProtocolFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.LoadTester.Scenarios;

internal sealed class DdosControlScenario : ILoadScenario
{
    private readonly RequestOptions _requestOptions;
    private int _sequence;

    public DdosControlScenario(int timeoutMs) => _requestOptions = RequestOptions.Default.WithTimeout(timeoutMs);

    public string Name => "ddos-control";

    public async ValueTask<double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        ushort sequence = unchecked((ushort)Interlocked.Increment(ref _sequence));
        Stopwatch stopwatch = Stopwatch.StartNew();

        Control packet = new();
        packet.Initialize(
            opCode: 0x0111,
            type: ControlType.FAIL,
            sequenceId: sequence,
            flags: Abstractions.Networking.Packets.PacketFlags.SYSTEM | Abstractions.Networking.Packets.PacketFlags.RELIABLE,
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
