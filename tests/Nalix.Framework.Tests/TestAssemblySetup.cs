using System;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Environment.IO;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerClass, MaxParallelThreads = 4)]

namespace Nalix.Framework.Tests;

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

[CollectionDefinition("Sequential Pooling Tests", DisableParallelization = true)]
public sealed class SequentialPoolingTestCollection;

[CollectionDefinition("ObjectPoolDiagnostics", DisableParallelization = true)]
public sealed class ObjectPoolDiagnosticsCollection;

[CollectionDefinition("ReturnValidation", DisableParallelization = true)]
public sealed class ReturnValidationCollection;

[CollectionDefinition("TypePoolPhase1", DisableParallelization = true)]
public sealed class TypePoolPhase1Collection;

[CollectionDefinition("ClockTests", DisableParallelization = true)]
public sealed class ClockTestCollection;














