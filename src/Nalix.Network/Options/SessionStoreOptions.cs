// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

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
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);

        if (this.SessionTtl <= TimeSpan.Zero)
        {
            throw new ValidationException("SessionTtl must be a positive time duration.");
        }
    }
}
