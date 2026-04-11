// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configures the default in-memory session snapshot manager.
/// </summary>
[IniComment("Resume-session storage and token rotation policy")]
public sealed class SessionManagerOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets how long a snapshot remains valid, in milliseconds.
    /// </summary>
    [IniComment("Snapshot lifetime in milliseconds")]
    [Range(1000, int.MaxValue, ErrorMessage = "SnapshotTtlMillis must be at least 1000.")]
    public int SnapshotTtlMillis { get; set; } = 300_000;

    /// <summary>
    /// Gets or sets whether a successful resume rotates the token.
    /// </summary>
    [IniComment("Rotate the session token after a successful resume")]
    public bool RotateTokenOnResume { get; set; } = true;

    /// <summary>
    /// Validates the option values.
    /// </summary>
    public void Validate()
    {
        if (this.SnapshotTtlMillis < 1000)
        {
            throw new ValidationException("SnapshotTtlMillis must be at least 1000.");
        }
    }
}
