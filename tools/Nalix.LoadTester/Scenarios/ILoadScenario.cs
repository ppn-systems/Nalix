// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Transport;

namespace Nalix.LoadTester.Scenarios;

internal interface ILoadScenario
{
    string Name { get; }

    ValueTask<double> ExecuteAsync(TcpSession session, CancellationToken cancellationToken);
}
