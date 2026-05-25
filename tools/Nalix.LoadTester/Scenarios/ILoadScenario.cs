// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Transport;

namespace Nalix.LoadTester.Scenarios;

internal interface ILoadScenario
{
    String Name { get; }

    ValueTask<Double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken);
}
