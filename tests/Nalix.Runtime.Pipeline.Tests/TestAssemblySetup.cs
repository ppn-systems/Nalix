using System;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Runtime.Pipeline.Throttling;
using Nalix.Framework.Environment;

namespace Nalix.Runtime.Pipeline.Tests;

internal static class TestAssemblySetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "NalixPipelineTests", Guid.NewGuid().ToString("N"));
        Directories.SetBasePathOverride(testDir);
    }
}
