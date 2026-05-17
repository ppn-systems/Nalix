using System;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Environment.IO;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, MaxParallelThreads = 1)]

namespace Nalix.Network.Tests;

internal static class TestAssemblySetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Redirect all application directories to a temporary location to ensure test isolation
        // and avoid collisions with system-wide configuration or data directories.
        string testDir = Path.Combine(Path.GetTempPath(), "NalixTests", Guid.NewGuid().ToString("N"));
        Directories.SetBasePathOverride(testDir);

        // Write default.ini with high connection limits for tests to prevent false IP bans
        string configDir = Path.Combine(testDir, "data", "config");
        _ = Directory.CreateDirectory(configDir);
        string defaultIniPath = Path.Combine(configDir, "default.ini");
        File.WriteAllText(defaultIniPath, @"[ConnectionLimitOptions]
MaxConnectionsPerIpAddress = 10000
MaxConnectionsPerWindow = 10000000
");
    }
}














