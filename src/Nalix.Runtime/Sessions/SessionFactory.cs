// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Configuration;
using Nalix.Environment.Time;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Sessions;

/// <summary>
/// Default implementation of <see cref="ISessionFactory"/> that captures transport sequence numbers,
/// security keys, and connection attributes.
/// </summary>
public sealed class SessionFactory : ISessionFactory
{
    private readonly SessionStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionFactory"/> class.
    /// </summary>
    public SessionFactory() => _options = ConfigurationManager.Instance.Get<SessionStoreOptions>();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public SessionEntry CreateSession(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        long now = Clock.UnixMillisecondsNow();

        // Rent a new object map for the session snapshot attributes
        ObjectMap<AttributeKey, object> attributes = ObjectMap<AttributeKey, object>.Rent();

        // Copy existing connection attributes
        foreach (KeyValuePair<AttributeKey, object> item in connection.Attributes)
        {
            attributes[item.Key] = item.Value;
        }

        // Capture current transport sequence numbers to allow seamless resume
        attributes[ConnectionAttributes.TcpSendSequence] = connection.TCP.SendSequence;
        attributes[ConnectionAttributes.TcpReceiveSequence] = connection.TCP.ReceiveSequence;

        if (connection.IsUdpCreated)
        {
            attributes[ConnectionAttributes.UdpSendSequence] = connection.UDP!.SendSequence;
            attributes[ConnectionAttributes.UdpReceiveSequence] = connection.UDP!.ReceiveSequence;
        }

        SessionSnapshot snapshot = new()
        {
            SessionToken = connection.ID,
            CreatedAtUnixMilliseconds = now,
            ExpiresAtUnixMilliseconds = now + (long)_options.SessionTtl.TotalMilliseconds,
            Secret = connection.Secret,
            Algorithm = connection.Algorithm,
            Level = connection.Level,
            Attributes = attributes
        };

        return new SessionEntry(snapshot, connection.ID);
    }
}
