// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Store options for resumable sessions, controlling how long inactive sessions are retained before expiration.
/// </summary>
[IniComment("Session store configuration — controls retention of resumable sessions and their expiration")]
public sealed class SessionStoreOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the time-to-live for resumable sessions.
    /// </summary>
    [IniComment("Duration after which an inactive session expires (default 30m)")]
    [Required(ErrorMessage = "SessionTtl is required.")]
    public TimeSpan SessionTtl { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets a value indicating whether sessions should be automatically saved when a connection is unregistered.
    /// </summary>
    [IniComment("Enable automatic session saving when a connection is unregistered from the hub")]
    public bool AutoSaveOnUnregister { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of attributes in the ObjectMap required to persist a session.
    /// This helps prevent DDoS by not saving "empty" sessions from handshake-only connections.
    /// </summary>
    [IniComment("Minimum number of attributes required to persist a session (excluding internal flags, default 4)")]
    [Range(0, int.MaxValue, ErrorMessage = "MinAttributesForPersistence cannot be negative.")]
    public int MinAttributesForPersistence { get; set; } = 4;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {

        if (this.SessionTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(this.SessionTtl), "SessionTtl must be a positive time duration.");
        }

        if (this.MinAttributesForPersistence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.MinAttributesForPersistence), "MinAttributesForPersistence cannot be negative.");
        }
    }
}
