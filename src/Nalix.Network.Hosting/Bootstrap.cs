// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Framework.Configuration;
using Nalix.Framework.Options;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

namespace Nalix.Network.Hosting;

/// <summary>
/// Provides bootstrapping and global initialization for Nalix server-side hosting.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Automatically configures server-side defaults when the Hosting assembly is loaded.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Architectural requirement to auto-configure server defaults")]
    [ModuleInitializer]
    internal static void AutoInitialize() => Initialize();

    /// <summary>
    /// Initializes the Nalix Hosting environment with server-optimized settings.
    /// This is called automatically by the module initializer, but can be called manually if needed.
    /// </summary>
    public static void Initialize()
    {
        // Server typically uses server.ini to avoid conflicts with client.ini in dual-deployment scenarios
        ConfigurationManager.Instance.SetConfigFilePath("server.ini");

        // 1. Enable packet pooling (explicitly) for maximum server throughput
        ConfigurationManager.Instance.Get<PacketOptions>().EnablePooling = true;

        // 2. Initialize all core server options to provide full templates in server.ini

        // Framework-level options
        _ = ConfigurationManager.Instance.Get<BufferOptions>();
        _ = ConfigurationManager.Instance.Get<CompressionOptions>();
        _ = ConfigurationManager.Instance.Get<FragmentOptions>();
        _ = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        _ = ConfigurationManager.Instance.Get<SecurityOptions>();
        _ = ConfigurationManager.Instance.Get<SnowflakeOptions>();
        _ = ConfigurationManager.Instance.Get<TaskManagerOptions>();

        // Network-level options
        _ = ConfigurationManager.Instance.Get<TimingWheelOptions>();
        _ = ConfigurationManager.Instance.Get<SessionStoreOptions>();
        _ = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        _ = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
        _ = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
        _ = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();
        _ = ConfigurationManager.Instance.Get<Network.Options.PoolingOptions>();

        // Runtime-level options
        _ = ConfigurationManager.Instance.Get<DispatchOptions>();
        _ = ConfigurationManager.Instance.Get<Runtime.Options.PoolingOptions>();

        // Security and concurrency options
        //_ = ConfigurationManager.Instance.Get<ConcurrencyOptions>();
        //_ = ConfigurationManager.Instance.Get<TokenBucketOptions>();

        // Persist all server-side defaults to server.ini
        ConfigurationManager.Instance.Flush();
    }
}
