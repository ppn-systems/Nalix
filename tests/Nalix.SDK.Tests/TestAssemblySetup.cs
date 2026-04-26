using System;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Framework.Environment;

using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Nalix.SDK.Tests;

internal static class TestAssemblySetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "NalixSdkTests", Guid.NewGuid().ToString("N"));
        Directories.SetBasePathOverride(testDir);
    }
}
