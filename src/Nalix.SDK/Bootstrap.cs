// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Environment.Configuration;
using Nalix.Environment.IO;
using Nalix.SDK.Options;

namespace Nalix.SDK;

/// <summary>
/// Provides bootstrapping and global initialization for the Nalix SDK.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Automatically configures client-side defaults when the SDK is loaded.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Architectural requirement to auto-configure client defaults")]
    [ModuleInitializer]
    internal static void AutoInitialize() => Initialize();

    /// <summary>
    /// Initializes the Nalix SDK with client-optimized settings.
    /// This is called automatically by the module initializer, but can be called manually if needed.
    /// </summary>
    public static void Initialize()
    {
        // Use a dedicated client configuration file to avoid conflicts with server-side default.ini
        ConfigurationManager.Instance.SetConfigFilePath(Path.Combine(Directories.ConfigurationDirectory, "client.ini"));

        // 2. Initialize TransportOptions to provide a default template in client.ini
        _ = ConfigurationManager.Instance.Get<TransportOptions>();

        // Persist all client-side defaults to client.ini
        ConfigurationManager.Instance.Flush();
    }
}
