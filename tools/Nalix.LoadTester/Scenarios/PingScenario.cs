// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.LoadTester.Scenarios;

internal sealed class PingScenario : ILoadScenario
{
    private readonly int _timeoutMs;

    public PingScenario(int timeoutMs) => _timeoutMs = timeoutMs;

    public string Name => "ping";

    public async ValueTask<double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        return await session.PingAsync(timeoutMs: _timeoutMs, ct: cancellationToken).ConfigureAwait(false);
    }
}
