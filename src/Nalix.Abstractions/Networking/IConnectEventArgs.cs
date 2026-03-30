// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable CA1711

using System;
using Nalix.Abstractions.Networking;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Represents connection events and provides event data.
/// </summary>
public interface IConnectEventArgs : IDisposable
{
    /// <summary>
    /// The connection related to the event.
    /// </summary>
    /// <remarks>
    /// This property provides access to the connection that triggered the event, allowing event handlers to interact with it.
    /// </remarks>
    IConnection Connection { get; }

    /// <summary>
    /// The buffer lease associated with the connection event, if applicable. This may be null if the event does not involve a buffer.
    /// </summary>
    IBufferLease? Lease { get; }

    /// <summary>
    /// Endpoint information related to the connection event, if applicable. This may be null if the event does not involve an endpoint.
    /// </summary>
    INetworkEndpoint? NetworkEndpoint { get; }
}
