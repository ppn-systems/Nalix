// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Hosting.Options;

/// <summary>
/// Provides configuration options for Nalix hosting and bootstrapping.
/// </summary>
[IniComment("Hosting configuration — controls startup diagnostics, console behavior, and lifecycle settings")]
public sealed class HostingOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets a value indicating whether to disable clearing the console on startup.
    /// </summary>
    [IniComment("If true, the console will not be cleared during the startup sequence")]
    public bool DisableConsoleClear { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to disable the startup banner.
    /// </summary>
    [IniComment("If true, the Nalix startup banner and diagnostic info will not be printed")]
    public bool DisableStartupBanner { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public static void Validate()
    {
        // No validation needed for boolean flags
    }
}
