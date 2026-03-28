// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Network.Examples.Protocols;

/// <summary>
/// Thin TCP listener wrapper used by the example app.
/// </summary>
/// <remarks>
/// Creates a listener that delegates connection handling to the supplied protocol.
/// </remarks>
public sealed class ExampleTcpListener(IProtocol protocol) : TcpListenerBase(protocol)
{
}
