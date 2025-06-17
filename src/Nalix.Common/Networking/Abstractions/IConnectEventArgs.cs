// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Abstractions;

/// <summary>
/// Represents connection events and provides event data.
/// </summary>
public interface IConnectEventArgs
{
    /// <summary>
    /// The connection related to the event.
    /// </summary>
    /// <remarks>
    /// This property provides access to the connection that triggered the event, allowing event handlers to interact with it.
    /// </remarks>
    IConnection Connection { get; }
}
