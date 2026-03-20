// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;

namespace Nalix.Common.Networking;

/// <summary>
/// Represents connection events and provides event data.
/// </summary>
public interface IConnectEventArgs : System.IDisposable
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
    IBufferLease Lease { get; }

    /// <summary>
    /// Endpoint information related to the connection event, if applicable. This may be null if the event does not involve an endpoint.
    /// </summary>
    INetworkEndpoint NetworkEndpoint { get; }
}
