// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking;

/// <summary>
/// Provides a centralized set of well-known connection attribute keys used throughout the Nalix framework.
/// </summary>
public static class ConnectionAttributes
{
    /// <summary>
    /// Key for the boolean attribute indicating whether a handshake has been successfully established.
    /// </summary>
    public const string HandshakeEstablished = "nalix.handshake.established";

    /// <summary>
    /// Key for the handshake context state stored during the negotiation process.
    /// </summary>
    public const string HandshakeState = "nalix.handshake.state";

    /// <summary>
    /// Key for the session token associated with the connection, if any.
    /// </summary>
    public const string SessionToken = "nalix.session.token";
}
