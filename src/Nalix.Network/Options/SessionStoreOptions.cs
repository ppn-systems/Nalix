// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Store options for resumable sessions, controlling how long inactive sessions are retained before expiration.
/// </summary>
[IniComment("Session store configuration — controls retention of resumable sessions and their expiration")]
public class SessionStoreOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the time-to-live for resumable sessions.
    /// </summary>
    [IniComment("Duration after which an inactive session expires (default 30m)")]
    public System.TimeSpan SessionTtl { get; init; } = System.TimeSpan.FromMinutes(30);
}
