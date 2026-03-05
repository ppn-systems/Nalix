// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Primitives;

namespace Nalix.Common.Networking.Sessions;

/// <summary>
/// Wraps a <see cref="SessionSnapshot"/> with runtime connection information for session management.
/// </summary>
public sealed class SessionEntry
{
    /// <summary>
    /// Gets or sets the session snapshot.
    /// </summary>
    public SessionSnapshot Snapshot { get; set; }

    /// <summary>
    /// Gets or sets the current identifier of the connection associated with this session.
    /// </summary>
    public UInt56 ConnectionId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEntry"/> class.
    /// </summary>
    /// <param name="snapshot">The session snapshot.</param>
    /// <param name="connectionId">The current connection identifier.</param>
    public SessionEntry(SessionSnapshot snapshot, UInt56 connectionId)
    {
        this.Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        this.ConnectionId = connectionId;
    }

    /// <summary>
    /// Returns the session resources to the object pool.
    /// </summary>
    public void Return() => this.Snapshot.Return();
}
