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
    private readonly Byte[]? _payload;
    private Int32 _sequence;

    public PayloadEchoScenario(Int32 timeoutMs, Int32 payloadSize)
    {
        _requestOptions = RequestOptions.Default.WithTimeout(timeoutMs);
        _payload = payloadSize > 0 ? CreatePayload(payloadSize) : null;
    }

    public String Name => "payload";

    public async ValueTask<Double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        UInt16 sequence = unchecked((UInt16)Interlocked.Increment(ref _sequence));
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

    private static Byte[] CreatePayload(Int32 payloadSize)
    {
        Byte[] payload = new Byte[payloadSize];
        Random.Shared.NextBytes(payload);
        return payload;
    }
}
