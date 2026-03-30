// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// Represents resumable transport state captured after a successful handshake.
/// </summary>
public sealed class SessionSnapshot
{
    /// <summary>
    /// Gets or sets the snapshot token.
    /// </summary>
    public ulong SessionToken { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp in Unix milliseconds.
    /// </summary>
    public long CreatedAtUnixMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets the expiration timestamp in Unix milliseconds.
    /// </summary>
    public long ExpiresAtUnixMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets the symmetric secret to restore on resume.
    /// </summary>
    public Bytes32 Secret { get; set; }

    /// <summary>
    /// Gets or sets the negotiated cipher suite.
    /// </summary>
    public CipherSuiteType Algorithm { get; init; }

    /// <summary>
    /// Gets or sets the permission level restored on resume.
    /// </summary>
    public PermissionLevel Level { get; init; }

    /// <summary>
    /// Gets or sets the whitelisted connection attributes copied during resume.
    /// </summary>
    public IObjectMap<string, object>? Attributes { get; set; }

    /// <summary>
    /// Returns the session attributes to the object pool and zeroizes the secret.
    /// </summary>
    public void Return()
    {
        // Zeroize the secret to prevent key material from lingering in memory
        this.Secret = Bytes32.Zero;

        this.Attributes?.Return();
        this.Attributes = null;
    }
}

