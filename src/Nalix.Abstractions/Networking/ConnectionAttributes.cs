// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides a centralized set of well-known connection attribute keys used throughout the Nalix framework.
/// </summary>
public static class ConnectionAttributes
{
    /// <summary>
    /// Key for the connection hub that owns this connection.
    /// </summary>
    public static readonly AttributeKey OwnerHub = AttributeKey.FromName("nalix.owner_hub");

    /// <summary>
    /// Key for the shared sequence numbers state stored during the connection lifecycle.
    /// </summary>
    public static readonly AttributeKey SequenceState = AttributeKey.FromName("nalix.connection.sequence_state");

    /// <summary>
    /// Key for the runtime specific state stored during the connection lifecycle.
    /// </summary>
    public static readonly AttributeKey RuntimeState = AttributeKey.FromName("nalix.connection.runtime_state");
}
