using System;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Common.Environment;

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
    }
}
