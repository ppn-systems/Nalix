// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Store options for resumable sessions, controlling how long inactive sessions are retained before expiration.
/// </summary>
[IniComment("Session store configuration — controls retention of resumable sessions and their expiration")]
public sealed partial class SessionStoreOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the time-to-live for resumable sessions.
    /// </summary>
    [IniComment("Duration after which an inactive session expires (default 5m)")]
    [Required(ErrorMessage = "SessionTtl is required.")]
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the minimum number of attributes in the ObjectMap required to persist a session.
    /// This helps prevent DDoS by not saving "empty" sessions from handshake-only connections.
    /// </summary>
    [IniComment("Minimum number of attributes required to persist a session (excluding internal flags, default 10)")]
    [Range(0, int.MaxValue, ErrorMessage = "MinAttributesForPersistence cannot be negative.")]
    public int MinAttributesForPersistence { get; set; } = 10;

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
