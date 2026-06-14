// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Default implementation of <see cref="ISessionPersistencePolicy"/> that checks the handshake status 
/// and minimum attribute count requirements.
/// </summary>
public sealed class DefaultSessionPersistencePolicy : ISessionPersistencePolicy
{
    private readonly SessionStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSessionPersistencePolicy"/> class.
    /// </summary>
    public DefaultSessionPersistencePolicy() => _options = ConfigurationManager.Instance.Get<SessionStoreOptions>();

    /// <inheritdoc/>
    public bool ShouldPersist(IConnection connection)
    {
        System.ArgumentNullException.ThrowIfNull(connection);

        // Policy 1: Only persist if the handshake was established
        if (!connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? established) || established is not true)
        {
            return false;
        }

        // Policy 2: Only persist if there is meaningful metadata beyond internal flags.
        if (connection.Attributes.Count <= _options.MinAttributesForPersistence)
        {
            return false;
        }

        return true;
    }
}
