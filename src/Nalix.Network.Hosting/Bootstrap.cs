// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Framework.Configuration;
using Nalix.Framework.Environment;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Hosting.Options;
using Nalix.Network.Options;
using Nalix.Runtime.Options;

namespace Nalix.Network.Hosting;

/// <summary>
/// Provides bootstrapping and global initialization for Nalix server-side hosting.
/// </summary>
public static class Bootstrap
{
    private static readonly Lock s_lock = new();
    private static readonly string s_serverGC = System.Runtime.GCSettings.IsServerGC ? "Server GC" : "Workstation GC";

    static Bootstrap()
    {
        Console.CancelKeyPress += OnProcessExit;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <summary>
    /// Automatically configures server-side defaults when the Hosting assembly is loaded.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Architectural requirement to auto-configure server defaults")]
    [ModuleInitializer]
    internal static void AutoInitialize() => Initialize();

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        lock (s_lock)
        {
            Console.CancelKeyPress -= OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;


            // Gracefully stop all background workers to allow in-flight tasks to complete.
            _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                        .CancelAllWorkers();

            InstanceManager.Instance.Dispose();

            // Flush any pending configuration changes to disk on shutdown.
            // This ensures that if the server was running with defaults
            ConfigurationManager.Instance.Flush();
        }
    }

    /// <summary>
    /// Initializes the Nalix Hosting environment with server-optimized settings.
    /// This is called automatically by the module initializer, but can be called manually if needed.
    /// </summary>
    public static void Initialize()
    {
        // Server typically uses server.ini to avoid conflicts with client.ini in dual-deployment scenarios
        ConfigurationManager.Instance.SetConfigFilePath(Path.Combine(Directories.ConfigurationDirectory, "server.ini"));

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

        // 3. Hosting specific options
        HostingOptions hostingOptions = ConfigurationManager.Instance.Get<HostingOptions>();

        // Persist all server-side defaults to server.ini
        ConfigurationManager.Instance.Flush();

        // 4. Show high-end startup diagnostics if running in interactive mode
        if (Environment.UserInteractive && !hostingOptions.DisableStartupBanner)
        {
            PRINT_STARTUP_BANNER(hostingOptions);
        }
    }

    private static void PRINT_STARTUP_BANNER(HostingOptions options)
    {
        if (!Console.IsOutputRedirected && !options.DisableConsoleClear)
        {
            Console.Clear();
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Resource.Banner1);
        Console.WriteLine(Resource.Banner2);
        Console.WriteLine(Resource.Banner3);
        Console.WriteLine(Resource.Banner4);
        Console.WriteLine(Resource.Banner5);
        Console.ResetColor();

        Console.WriteLine($"[INIT] Version   : {typeof(Bootstrap).Assembly.GetName().Version}");
        Console.WriteLine($"[INIT] OS        : {Environment.OSVersion}");
        Console.WriteLine($"[INIT] Runtime   : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"[INIT] GC Mode   : {s_serverGC}");
        Console.WriteLine($"[INIT] Processors: {Environment.ProcessorCount}");
        Console.WriteLine(Resource.Separator);
    }
}
