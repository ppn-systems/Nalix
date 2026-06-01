// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.LoadTester.Contracts;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.LoadTester.Scenarios;

internal sealed class PayloadEchoScenario : ILoadScenario
{
    private readonly RequestOptions _requestOptions;
    private readonly byte[]? _payload;
    private int _sequence;

    public PayloadEchoScenario(int timeoutMs, int payloadSize)
    {
        _requestOptions = RequestOptions.Default.WithTimeout(timeoutMs);
        _payload = payloadSize > 0 ? CreatePayload(payloadSize) : null;
    }

    public string Name => "payload";

    public async ValueTask<double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        ushort sequence = unchecked((ushort)Interlocked.Increment(ref _sequence));
        Stopwatch stopwatch = Stopwatch.StartNew();
        BenchmarkPacket packet = BenchmarkPacket.Create();
        packet.SequenceId = sequence;
        packet.Payload = _payload;

        try
        {
            using BenchmarkPacket response = await session.RequestAsync<BenchmarkPacket>(
                packet,
                options: _requestOptions,
                predicate: p => p.SequenceId == sequence,
                ct: cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            packet.Dispose();
        }
    }

    private static byte[] CreatePayload(int payloadSize)
    {
        byte[] payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);
        return payload;
    }
}
