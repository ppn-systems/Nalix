// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;

namespace Nalix.Common.Networking.Sessions;

/// <summary>
/// Represents resumable transport state captured after a successful handshake.
/// </summary>
public sealed class SessionSnapshot
{
    /// <summary>
    /// Gets or sets the snapshot token.
    /// </summary>
    public UInt56 SessionToken { get; init; }

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
    public byte[] Secret { get; init; } = [];

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
        if (this.Secret is { Length: > 0 })
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(this.Secret);
        }

        this.Attributes?.Return();
        this.Attributes = null;
    }
}
